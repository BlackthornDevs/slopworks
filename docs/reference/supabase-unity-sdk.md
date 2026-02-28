# Supabase Unity SDK reference

Supabase C# SDK (supabase-csharp) for persistence in Slopworks. This covers installation, authentication, async patterns with UniTask, JSONB upsert for world state chunks, and thread safety.

**Note:** The vertical slice plan defers Supabase in favor of local JSON saves. This reference covers the full integration for when Supabase is introduced.

---

## SDK choice: supabase-csharp over raw REST

The [supabase-csharp](https://github.com/supabase-community/supabase-csharp) community library provides:
- Type-safe model-based queries
- Built-in async/await with UniTask support
- Integrated auth (GoTrue), database (Postgrest), and storage
- Active maintenance

Raw REST calls are appropriate only for performance-critical one-off queries. For all other database operations, use the SDK.

---

## Installation

Install via NuGet (requires .NET Standard 2.0 compatibility):

1. Add `Supabase` package via Package Manager
2. Install required dependencies:
   - `Newtonsoft.Json` (Unity-specific version from jillejr/Newtonsoft.Json-for-Unity)
   - `UniTask` via git UPM: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`

**Critical setting:** Project Settings > Player > Managed Stripping Level: **Off** or **Minimal**. Aggressive stripping removes types needed for JSON serialization.

---

## Initialization

```csharp
// Load from StreamingAssets — never hardcode credentials
public async UniTask InitializeAsync()
{
    var config = LoadConfig("supabase-config.json");
    _client = new Supabase.Client(config.Url, config.AnonKey);
    await _client.InitializeAsync();
}

private SupabaseConfig LoadConfig(string fileName)
{
    string path = Path.Combine(Application.streamingAssetsPath, fileName);

    // Desktop: direct file read
    if (!path.StartsWith("jar:") && !path.StartsWith("http"))
        return JsonConvert.DeserializeObject<SupabaseConfig>(File.ReadAllText(path));

    // Android / WebGL: must use UnityWebRequest (call from async context)
    throw new PlatformNotSupportedException("use async LoadConfigAsync for this platform");
}
```

Config file: `Assets/StreamingAssets/supabase-config.json` (gitignored — copy from template):
```json
{
  "url": "https://your-project.supabase.co",
  "anonKey": "eyJhbGc..."
}
```

---

## Authentication

Anonymous sign-in for new players, with Steam identity linking when available:

```csharp
// Anonymous: session is device-specific (no cross-device persistence)
public async UniTask<bool> SignInAnonymouslyAsync()
{
    try
    {
        var session = await _client.Auth.SignInAnonymously();
        _currentUserId = session.User.Id;
        return true;
    }
    catch (Exception ex)
    {
        Debug.LogError($"anonymous sign-in failed: {ex.Message}");
        return false;
    }
}

// Link Steam identity to the anonymous user after first session
public async UniTask LinkSteamAsync(string steamToken)
{
    await _client.Auth.LinkIdentity(new SignInWithIdToken
    {
        IdToken = steamToken,
        Provider = "steam"
    });
}
```

**Rate limit:** 30 anonymous sign-ups per IP per hour on Supabase free tier. Add Cloudflare Turnstile if abuse is a concern.

---

## Async pattern: UniTask

**Use UniTask exclusively. Never use `async void` or raw `Task` in Unity game code.**

```csharp
// CORRECT: UniTask return type
public async UniTask SaveGameAsync(string worldId)
{
    await UpsertWorldChunksAsync(worldId, _chunks);
}

// WRONG: async void (exceptions are silent, crashes app)
public async void BadSave(string worldId) { ... }

// WRONG: Task (heap allocation, not Unity-aware)
public async Task AlsoBad(string worldId) { ... }
```

Always use cancellation tokens for operations that may outlive the requesting object:

```csharp
private CancellationTokenSource _cts;

private async UniTask SaveWithCancellationAsync()
{
    _cts = new CancellationTokenSource();
    try
    {
        await _client.From<WorldStateChunk>().Upsert(_chunk, _cts.Token);
    }
    catch (OperationCanceledException)
    {
        Debug.Log("save cancelled");
    }
}

private void OnDestroy() => _cts?.Cancel();
```

---

## JSONB upsert for world state

The `world_state` table uses a composite key `(world_id, chunk_key)` with a JSONB `data` column.

```csharp
[Table("world_state")]
public class WorldStateChunk : BaseModel
{
    [Column("world_id")]    public string WorldId { get; set; }
    [Column("chunk_key")]   public string ChunkKey { get; set; }
    [Column("data")]        public JObject Data { get; set; }
    [Column("build_version")] public string BuildVersion { get; set; }
    [Column("updated_at")]  public DateTime UpdatedAt { get; set; }
}

// Upsert single chunk
public async UniTask UpsertChunkAsync(string worldId, string chunkKey, JObject data)
{
    var chunk = new WorldStateChunk
    {
        WorldId = worldId,
        ChunkKey = chunkKey,
        Data = data,
        BuildVersion = "joe",
        UpdatedAt = DateTime.UtcNow
    };

    await _client
        .From<WorldStateChunk>()
        .OnConflict("world_id,chunk_key")
        .Upsert(chunk);
}

// Batch upsert (scene autosave)
public async UniTask UpsertChunkBatchAsync(string worldId, Dictionary<string, JObject> chunks)
{
    var models = chunks.Select(kvp => new WorldStateChunk
    {
        WorldId = worldId,
        ChunkKey = kvp.Key,
        Data = kvp.Value,
        BuildVersion = "joe",
        UpdatedAt = DateTime.UtcNow
    }).ToList();

    await _client
        .From<WorldStateChunk>()
        .OnConflict("world_id,chunk_key")
        .Upsert(models);
}
```

**Important:** Postgrest (the underlying query builder) can only replace the entire JSONB blob via Upsert. For partial JSONB updates, fetch → merge → upsert. For chunk-based saves, full blob replacement is acceptable since chunks are small.

---

## Session boundary tracking

```csharp
[Table("game_sessions")]
public class GameSession : BaseModel
{
    [Column("player_id")]     public string PlayerId { get; set; }
    [Column("status")]        public string Status { get; set; }
    [Column("started_at")]    public DateTime StartedAt { get; set; }
    [Column("ended_at")]      public DateTime? EndedAt { get; set; }
    [Column("build_version")] public string BuildVersion { get; set; }
}

// FishNet server callbacks → Supabase
public void OnPlayerJoined(int fishnetClientId)
{
    _ = CreateSessionAsync(fishnetClientId);
}

public void OnPlayerDisconnected(int fishnetClientId)
{
    _ = FinalizeSessionAsync(fishnetClientId);
}

private async UniTask CreateSessionAsync(int clientId)
{
    var session = new GameSession
    {
        PlayerId = _clientIdToUserId[clientId],
        Status = "active",
        StartedAt = DateTime.UtcNow,
        BuildVersion = "joe"
    };
    await _client.From<GameSession>().Insert(session);
}
```

---

## Thread safety

All Supabase client calls must run on the main thread (or be awaited back to it). FishNet server callbacks run on the main thread by default, so direct `await` chains are safe.

Never call Supabase from background threads without dispatching back:

```csharp
// SAFE: called from FishNet callback (main thread) and awaited
[ServerRpc]
private void SavePlayerRpc()
{
    _ = SavePlayerStateAsync();    // UniTask — stays on main thread
}

// UNSAFE: explicit background thread calling client
_ = Task.Run(() => _client.From<WorldStateChunk>().Insert(...));  // DON'T
```

---

## Pitfall quick reference

| Pitfall | Fix |
|---------|-----|
| `async void` for fire-and-forget saves | Use `_ = SomeUniTask()` or `UniTask.Forget()` |
| Raw `Task` instead of `UniTask` | UniTask is zero-allocation; Task allocates heap |
| Hardcoded credentials in code | Load from `StreamingAssets/supabase-config.json` |
| Full table scan on disconnect | Always filter by `player_id` before fetching sessions |
| Partial JSONB update via Upsert | Fetch + merge + upsert; Postgrest can't patch JSONB fields |
| Managed code stripping removing serialization types | Set stripping level to Minimal |
| Supabase call from background thread | Await on main thread; don't use `Task.Run` |
