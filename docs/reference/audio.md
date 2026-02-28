# Audio reference

FMOD Studio for all audio in Slopworks. This covers the package choice, integration setup, event naming conventions, adaptive factory audio, and the two-developer workflow.

---

## Package choice: FMOD, not Unity Audio

Unity's built-in Audio Mixer works for simple games. Slopworks needs:
- Adaptive music that responds to factory production rate and threat level
- Factory ambience driven by hundreds of machine-state parameters
- Per-area mixing (factory floor vs exterior vs building interiors)
- Real-time parameter automation (belt speed → whir pitch, furnace temp → roar intensity)

Built-in Audio can't do this without painful workarounds. FMOD Studio is designed for exactly this.

**Licensing:** Free for independent developers with revenue under $200k/year. No royalties.

**Packages:**
- FMOD Studio: download from fmod.com/studio
- FMOD Unity Integration: via fmod.com or Unity Package Manager

---

## Integration setup

1. Download FMOD Studio and the FMOD Unity Integration package
2. Import the integration package into Unity
3. In FMOD Studio, set the build output path to `Assets/StreamingAssets/FMOD`
4. In Unity, Window > FMOD Studio > Edit Settings:
   - Bank load type: From user-specified paths
   - Bank path: `Assets/StreamingAssets/FMOD`

FMOD builds banks (`.bank` files) from Studio. Unity loads them at runtime. C# scripts reference events by path strings.

---

## Event naming convention

```
event:/music/base-theme           — main factory theme
event:/music/threat-escalation    — threat-driven layer
event:/ambient/factory-floor      — continuous factory hum, parameter-driven
event:/ambient/exterior           — wind, distant fauna
event:/machine/smelter-loop       — looping machine audio (per type)
event:/machine/assembler-loop
event:/machine/belt-loop
event:/machine/power-down         — one-shot state change
event:/machine/power-up
event:/ui/button-click
event:/ui/error
event:/combat/weapon-fire
event:/combat/fauna-hurt
event:/combat/explosion
```

Define event paths as constants — never use raw strings in game code:

```csharp
public static class AudioEvents
{
    public const string FactoryFloor = "event:/ambient/factory-floor";
    public const string MachineSmelterLoop = "event:/machine/smelter-loop";
    public const string MachinePowerDown = "event:/machine/power-down";
}
```

---

## Adaptive factory audio

The factory floor ambient event uses FMOD parameters tied to game state:

```
Parameter: machine_count    — 0–50, drives how busy the factory sounds
Parameter: power_level      — 0–1, drives electrical hum intensity
Parameter: threat_level     — 0–1, shifts music layers
Parameter: belt_speed       — 0–1, drives belt loop pitch
```

Update parameters from SyncVar callbacks (client-side only):

```csharp
public class FactoryAudioManager : MonoBehaviour
{
    private FMOD.Studio.EventInstance _factoryAmbient;

    private void Start()
    {
        _factoryAmbient = FMODUnity.RuntimeManager.CreateInstance(AudioEvents.FactoryFloor);
        _factoryAmbient.start();
    }

    // Called from SyncVar OnChange callbacks on clients
    public void OnMachineCountChanged(int count)
    {
        _factoryAmbient.setParameterByName("machine_count", count);
    }

    public void OnPowerLevelChanged(float level)
    {
        _factoryAmbient.setParameterByName("power_level", level);
    }

    public void OnThreatLevelChanged(float threat)
    {
        _factoryAmbient.setParameterByName("threat_level", threat);
    }

    private void OnDestroy()
    {
        _factoryAmbient.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _factoryAmbient.release();
    }
}
```

---

## Machine audio

Each machine plays a loop driven by its SyncVar status:

```csharp
public class MachineAudio : MonoBehaviour
{
    private FMOD.Studio.EventInstance _machineLoop;
    private MachineNetworkState _machine;

    [SerializeField] private string _loopEventPath;  // e.g. AudioEvents.MachineSmelterLoop

    private void OnEnable()
    {
        _machine = GetComponent<MachineNetworkState>();
        _machine.OnStatusChanged += OnStatusChanged;
        _machineLoop = FMODUnity.RuntimeManager.CreateInstance(_loopEventPath);
    }

    private void OnStatusChanged(MachineStatus old, MachineStatus next, bool asServer)
    {
        // audio is always client-only
        if (asServer) return;

        switch (next)
        {
            case MachineStatus.Working:
                _machineLoop.start();
                break;
            case MachineStatus.Idle:
            case MachineStatus.Blocked:
                _machineLoop.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                break;
            case MachineStatus.Offline:
                _machineLoop.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                FMODUnity.RuntimeManager.PlayOneShot(AudioEvents.MachinePowerDown, transform.position);
                break;
        }
    }

    private void OnDisable()
    {
        _machineLoop.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _machineLoop.release();
        _machine.OnStatusChanged -= OnStatusChanged;
    }
}
```

---

## Audio authority: clients only

The server never plays audio. Audio guards are the inverse of simulation guards:

```csharp
// simulation code: server only
if (!IsServerInitialized) return;

// audio code: clients only
private void OnStatusChanged(MachineStatus old, MachineStatus next, bool asServer)
{
    if (asServer) return;  // server doesn't play audio
    PlayAudioForStatus(next);
}
```

All `FMODUnity.RuntimeManager` calls happen on clients only.

---

## Two-developer audio workflow

FMOD Studio project files (`.fspro`) are text format and can be committed to source control. Banks are binary build artifacts — don't commit them.

```
# .gitignore additions
Assets/StreamingAssets/FMOD/
!Assets/StreamingAssets/FMOD/.placeholder
```

Distribute banks via Git LFS or direct transfer.

**Recommendation:** Kevin owns the FMOD Studio project (he's closer to the mechanical systems). Joe requests audio changes via `docs/audio-requests.md`.

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| Playing audio on server | `if (asServer) return;` in SyncVar callbacks |
| Forgetting `release()` on looping instances | Use `PlayOneShot` for one-shots; release loops in `OnDisable` |
| Two devs editing FMOD project | One owner; request changes in audio-requests.md |
| Bank files in Git | Add to `.gitignore`, distribute separately |
| Raw event path strings in game code | Define in `AudioEvents` constants class |
