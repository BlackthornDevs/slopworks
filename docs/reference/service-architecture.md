# Service architecture reference

VContainer dependency injection for all subsystem wiring in Slopworks. This covers why DI over static singletons, the scope hierarchy, registration patterns, and the two-developer workflow.

---

## Why DI over static singletons

Static singletons (`ItemRegistry.Instance`) cause:
- Test isolation failures — tests share global state and affect each other
- Load order dependencies — singletons can initialize before their dependencies are ready
- Hidden coupling — any class can access any singleton without declaring it

VContainer provides:
- Explicit dependency declaration via constructor injection
- Scope-bounded lifetimes (per-game, per-scene, per-request)
- Testability — inject mocks in EditMode tests
- Two-developer safety — each developer registers in their own scope file

**Package:** VContainer 1.15+ via OpenUPM: `openupm add jp.hadashikick.vcontainer`

---

## Scope hierarchy

```
ProjectScope (lifetime: game session, loaded in Core_Network.unity)
├── ItemRegistry          Singleton
├── RecipeRegistry        Singleton
├── SaveSystem            Singleton
└── AudioManager          Singleton

HomeBaseScope (lifetime: HomeBase scenes loaded)
├── FactorySimulation     Scoped
├── BeltNetwork           Scoped
├── PowerGrid             Scoped
└── BuildingGrid          Scoped

BuildingScope (lifetime: per reclaimed building)
├── FaunaWaveController   Scoped
└── MEPSystemManager      Scoped
```

Child scopes can resolve types from parent scopes. Parent scopes cannot resolve child scope types.

---

## Registration: ProjectScope

```csharp
// Assets/_Slopworks/Scripts/Core/ProjectLifetimeScope.cs
public class ProjectLifetimeScope : LifetimeScope
{
    [SerializeField] private ItemRegistrySO _itemRegistrySO;
    [SerializeField] private RecipeRegistrySO _recipeRegistrySO;

    protected override void Configure(IContainerBuilder builder)
    {
        // ScriptableObjects: register as the interfaces they implement
        builder.RegisterInstance(_itemRegistrySO).AsImplementedInterfaces();
        builder.RegisterInstance(_recipeRegistrySO).AsImplementedInterfaces();

        // Systems: VContainer creates and injects these
        builder.Register<SaveSystem>(Lifetime.Singleton);
        builder.Register<AudioManager>(Lifetime.Singleton);
    }
}
```

Attach `ProjectLifetimeScope` to a GameObject in `Core_Network.unity`. It persists for the entire session.

---

## Registration: HomeBaseScope

```csharp
// Assets/_Slopworks/Scripts/Automation/HomeBaseLifetimeScope.cs
public class HomeBaseLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<FactorySimulation>(Lifetime.Scoped);
        builder.Register<BeltNetwork>(Lifetime.Scoped);
        builder.Register<PowerGrid>(Lifetime.Scoped);
        builder.Register<BuildingGrid>(Lifetime.Scoped);
    }
}
```

```csharp
// Assets/_Slopworks/Scripts/World/BuildingLifetimeScope.cs
public class BuildingLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<FaunaWaveController>(Lifetime.Scoped);
        builder.Register<MEPSystemManager>(Lifetime.Scoped);
    }
}
```

**Two-developer workflow:** Joe and Kevin each own their scope files. Separate files = no merge conflicts on registration.

---

## Consumer pattern

Classes declare dependencies in the constructor. VContainer injects them automatically:

```csharp
public class BeltSegment : ITickable
{
    private readonly IItemRegistry _items;
    private readonly PowerGrid _power;

    // VContainer calls this constructor and injects from the current scope
    public BeltSegment(IItemRegistry items, PowerGrid power)
    {
        _items = items;
        _power = power;
    }

    public void Tick()
    {
        if (!_power.HasPower(PowerDraw)) return;
        // belt tick logic using _items
    }
}
```

No `FindObjectOfType`, no `ServiceLocator.Get<T>()`, no static references.

---

## MonoBehaviour injection

For MonoBehaviours managed by FishNet (NetworkBehaviours, NetworkObjects), VContainer can't call the constructor. Use `[Inject]` on fields or methods:

```csharp
public class MachineNetworkState : NetworkBehaviour
{
    [Inject] private IItemRegistry _items;
    [Inject] private RecipeRegistry _recipes;

    // VContainer injects these after the object is created
}
```

Or method injection (clearer for complex dependencies):

```csharp
public class MachineNetworkState : NetworkBehaviour
{
    private IItemRegistry _items;
    private RecipeRegistry _recipes;

    [Inject]
    public void Construct(IItemRegistry items, RecipeRegistry recipes)
    {
        _items = items;
        _recipes = recipes;
    }
}
```

---

## Testing with DI

Constructor injection makes classes testable in EditMode without starting a full Unity scene:

```csharp
[Test]
public void belt_ticks_when_powered()
{
    // Construct test doubles — no VContainer needed in EditMode
    var mockItems = Substitute.For<IItemRegistry>();
    var mockPower = Substitute.For<PowerGrid>();
    mockPower.HasPower(Arg.Any<float>()).Returns(true);

    var belt = new BeltSegment(mockItems, mockPower);
    belt.InsertItem(new BeltItem { definition = TestItems.IronIngot, offset = 0f });
    belt.Tick();

    Assert.AreEqual(1f, belt.Items[0].offset);
}
```

This is why constructor injection is required for all simulation classes. It enables the bug-fixing workflow (write a failing test first).

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| `FindObjectOfType` in game code | Inject the dependency via constructor or `[Inject]` |
| Static singleton state between tests | VContainer scopes reset; injected classes reset naturally |
| Two devs editing same LifetimeScope file | One scope file per subsystem owner |
| NetworkBehaviour constructor injection | Use `[Inject]` attribute on fields or methods |
| ProjectScope trying to resolve a HomeBase-scoped type | Respect hierarchy — child scopes can resolve parent types, not vice versa |
| Forgetting to configure `Parent` on child LifetimeScopes | Set `ParentReference` to `ProjectLifetimeScope` on HomeBase/Building scopes |
