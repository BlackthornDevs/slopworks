using UnityEditor;
using UnityEditor.AI;
using UnityEngine;

public static class PlaytestSetup
{
    [MenuItem("Slopworks/Setup Playtest Scene")]
    public static void SetupPlaytest()
    {
        FixGround();
        var weaponDef = CreateWeaponDefinition();
        var faunaDef = CreateFaunaDefinition();
        var enemyDiedEvent = CreateEnemyDiedEvent();
        WirePlayerWeapon(weaponDef);
        WirePlayerHealth();
        SetupPlayerInventory();
        WireEnemyDefinition(faunaDef, enemyDiedEvent);
        var enemyPrefab = SaveEnemyPrefab();
        SetupWaveSystem(enemyDiedEvent, enemyPrefab);
        SetupHUD();
        SetupItemRegistry();
        BakeNavMesh();

        Debug.Log("playtest setup complete");
    }

    private static WeaponDefinitionSO CreateWeaponDefinition()
    {
        string folder = "Assets/_Slopworks/ScriptableObjects/Weapons";
        EnsureFolder(folder);

        string path = folder + "/Test_Rifle.asset";
        var existing = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(path);
        if (existing != null)
        {
            Debug.Log("weapon definition already exists at " + path);
            return existing;
        }

        var def = ScriptableObject.CreateInstance<WeaponDefinitionSO>();
        def.weaponId = "test_rifle";
        def.damage = 25f;
        def.fireRate = 2f;
        def.range = 50f;
        def.damageType = DamageType.Kinetic;
        def.magazineSize = 12;
        def.reloadTime = 1.5f;

        AssetDatabase.CreateAsset(def, path);
        Debug.Log("created weapon definition at " + path);
        return def;
    }

    private static FaunaDefinitionSO CreateFaunaDefinition()
    {
        string folder = "Assets/_Slopworks/ScriptableObjects/Fauna";
        EnsureFolder(folder);

        string path = folder + "/Test_Grunt.asset";
        var existing = AssetDatabase.LoadAssetAtPath<FaunaDefinitionSO>(path);
        if (existing != null)
        {
            Debug.Log("fauna definition already exists at " + path);
            return existing;
        }

        var def = ScriptableObject.CreateInstance<FaunaDefinitionSO>();
        def.faunaId = "test_grunt";
        def.maxHealth = 50f;
        def.moveSpeed = 3f;
        def.attackDamage = 10f;
        def.attackRange = 2.5f;
        def.attackCooldown = 1.5f;
        def.sightRange = 15f;
        def.sightAngle = 120f;
        def.hearingRange = 8f;
        def.attackDamageType = DamageType.Kinetic;

        // pack behavior
        def.alertRange = 20f;
        def.strafeSpeed = 2.5f;
        def.strafeRadius = 3f;
        def.baseBravery = 0.5f;
        def.fleeConfidenceThreshold = 0.3f;
        def.coverSearchRadius = 10f;

        AssetDatabase.CreateAsset(def, path);
        Debug.Log("created fauna definition at " + path);
        return def;
    }

    private static void WirePlayerWeapon(WeaponDefinitionSO weaponDef)
    {
        var player = GameObject.Find("PlayerCharacter");
        if (player == null)
        {
            Debug.LogError("PlayerCharacter not found in scene");
            return;
        }

        // add HealthBehaviour if missing (for enemy attacks)
        var weapon = player.GetComponent<WeaponBehaviour>();
        if (weapon == null)
            weapon = player.AddComponent<WeaponBehaviour>();

        var fpsCam = player.transform.Find("FPSCamera");
        if (fpsCam == null)
        {
            Debug.LogError("FPSCamera not found on player");
            return;
        }

        var so = new SerializedObject(weapon);
        so.FindProperty("_weaponDefinition").objectReferenceValue = weaponDef;
        so.FindProperty("_camera").objectReferenceValue = fpsCam.GetComponent<Camera>();
        so.ApplyModifiedProperties();

        // camera effects
        var camObj = fpsCam.gameObject;
        if (camObj.GetComponent<CameraRecoil>() == null)
            camObj.AddComponent<CameraRecoil>();
        if (camObj.GetComponent<CameraShake>() == null)
            camObj.AddComponent<CameraShake>();

        // muzzle flash as child of camera
        var muzzle = camObj.transform.Find("MuzzleFlashPoint");
        if (muzzle == null)
        {
            var muzzleObj = new GameObject("MuzzleFlashPoint");
            muzzleObj.transform.SetParent(camObj.transform);
            muzzleObj.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);
            muzzleObj.AddComponent<MuzzleFlash>();
        }

        EditorUtility.SetDirty(player);
        Debug.Log("player weapon and effects wired");
    }

    private static void WirePlayerHealth()
    {
        var player = GameObject.Find("PlayerCharacter");
        if (player == null) return;

        var health = player.GetComponent<HealthBehaviour>();
        if (health == null)
        {
            health = player.AddComponent<HealthBehaviour>();
            var so = new SerializedObject(health);
            so.FindProperty("_maxHealth").floatValue = 100f;
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(player);
        Debug.Log("player health wired");
    }

    private static GameEventSO CreateEnemyDiedEvent()
    {
        string folder = "Assets/_Slopworks/ScriptableObjects/Events";
        EnsureFolder(folder);

        string path = folder + "/EnemyDied.asset";
        var existing = AssetDatabase.LoadAssetAtPath<GameEventSO>(path);
        if (existing != null)
        {
            Debug.Log("enemy died event already exists at " + path);
            return existing;
        }

        var evt = ScriptableObject.CreateInstance<GameEventSO>();
        AssetDatabase.CreateAsset(evt, path);
        Debug.Log("created enemy died event at " + path);
        return evt;
    }

    private static void WireEnemyDefinition(FaunaDefinitionSO faunaDef, GameEventSO enemyDiedEvent)
    {
        var enemy = GameObject.Find("Enemy_Basic");
        if (enemy == null)
        {
            Debug.LogWarning("Enemy_Basic not found in scene — skipping fauna wiring");
            return;
        }

        var controller = enemy.GetComponent<FaunaController>();
        if (controller == null)
        {
            Debug.LogError("FaunaController not found on Enemy_Basic");
            return;
        }

        var so = new SerializedObject(controller);
        so.FindProperty("_def").objectReferenceValue = faunaDef;
        so.FindProperty("_onDeathEvent").objectReferenceValue = enemyDiedEvent;
        so.ApplyModifiedProperties();

        // also wire HealthBehaviour max health to match definition
        var health = enemy.GetComponent<HealthBehaviour>();
        if (health != null)
        {
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("_maxHealth").floatValue = faunaDef.maxHealth;
            healthSo.ApplyModifiedProperties();
        }

        // enemy hit effects
        if (enemy.GetComponent<EnemyHitFlash>() == null)
            enemy.AddComponent<EnemyHitFlash>();
        if (enemy.GetComponent<EnemyKnockback>() == null)
            enemy.AddComponent<EnemyKnockback>();

        EditorUtility.SetDirty(enemy);
        Debug.Log("enemy fauna definition and effects wired");
    }

    private static GameObject SaveEnemyPrefab()
    {
        string folder = "Assets/_Slopworks/Prefabs/Enemies";
        EnsureFolder(folder);

        string path = folder + "/Enemy_Basic.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            Debug.Log("enemy prefab already exists at " + path);
            return existing;
        }

        var enemy = GameObject.Find("Enemy_Basic");
        if (enemy == null)
        {
            Debug.LogWarning("Enemy_Basic not found — cannot create prefab");
            return null;
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(enemy, path);
        Debug.Log("saved enemy prefab at " + path);
        return prefab;
    }

    private static void SetupWaveSystem(GameEventSO enemyDiedEvent, GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("no enemy prefab — skipping wave setup");
            return;
        }

        // create spawn points at arena edges
        var spawnParent = GameObject.Find("SpawnPoints");
        if (spawnParent == null)
        {
            spawnParent = new GameObject("SpawnPoints");
            Vector3[] positions =
            {
                new Vector3(30, 0, 30),
                new Vector3(-30, 0, 30),
                new Vector3(30, 0, -30),
                new Vector3(-30, 0, -30),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var point = new GameObject("SpawnPoint_" + i);
                point.transform.SetParent(spawnParent.transform);
                point.transform.position = positions[i];
                point.transform.LookAt(Vector3.zero);
            }

            EditorUtility.SetDirty(spawnParent);
            Debug.Log("created 4 spawn points");
        }

        // find or create wave controller
        var waveObj = GameObject.Find("WaveController");
        if (waveObj == null)
        {
            waveObj = new GameObject("WaveController");

            // add EnemySpawner
            var spawner = waveObj.AddComponent<EnemySpawner>();
            var spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("_enemyPrefab").objectReferenceValue = enemyPrefab;

            var spawnPointsProp = spawnerSo.FindProperty("_spawnPoints");
            var points = spawnParent.GetComponentsInChildren<Transform>();
            // skip the parent transform (index 0)
            int childCount = points.Length - 1;
            spawnPointsProp.arraySize = childCount;
            for (int i = 0; i < childCount; i++)
                spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = points[i + 1];
            spawnerSo.ApplyModifiedProperties();

            // add WaveControllerBehaviour
            var wcb = waveObj.AddComponent<WaveControllerBehaviour>();
            var wcbSo = new SerializedObject(wcb);
            wcbSo.FindProperty("_spawner").objectReferenceValue = spawner;
            wcbSo.FindProperty("_enemyDiedEvent").objectReferenceValue = enemyDiedEvent;

            // auto-start first wave after 3 seconds
            wcbSo.FindProperty("_autoStartDelay").floatValue = 3f;

            // configure 3 waves
            var wavesProp = wcbSo.FindProperty("_waves");
            wavesProp.arraySize = 3;

            SetWave(wavesProp.GetArrayElementAtIndex(0), 3, 1f, 5f);
            SetWave(wavesProp.GetArrayElementAtIndex(1), 5, 0.8f, 5f);
            SetWave(wavesProp.GetArrayElementAtIndex(2), 8, 0.5f, 0f);

            wcbSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(waveObj);
            Debug.Log("created wave controller with 3 waves (3, 5, 8 enemies)");
        }
        else
        {
            Debug.Log("wave controller already exists");
        }
    }

    private static void SetWave(SerializedProperty waveProp,
        int enemyCount, float spawnDelay, float timeBetween)
    {
        waveProp.FindPropertyRelative("enemyCount").intValue = enemyCount;
        waveProp.FindPropertyRelative("spawnDelay").floatValue = spawnDelay;
        waveProp.FindPropertyRelative("timeBetweenWaves").floatValue = timeBetween;
    }

    private static void SetupHUD()
    {
        var canvas = GameObject.Find("HUD_Canvas");
        if (canvas == null)
        {
            Debug.LogWarning("HUD_Canvas not found — skipping HUD setup");
            return;
        }

        var hud = canvas.GetComponent<PlayerHUD>();
        if (hud == null)
        {
            hud = canvas.AddComponent<PlayerHUD>();
            Debug.Log("added PlayerHUD to HUD_Canvas");
        }

        var hitMarker = canvas.GetComponent<HitMarkerUI>();
        if (hitMarker == null)
        {
            hitMarker = canvas.AddComponent<HitMarkerUI>();
            Debug.Log("added HitMarkerUI to HUD_Canvas");
        }

        // wire all references via serialized fields — replaces runtime GameObject.Find
        var player = GameObject.Find("PlayerCharacter");
        if (player != null)
        {
            var weapon = player.GetComponent<WeaponBehaviour>();
            if (weapon != null)
            {
                var weaponSo = new SerializedObject(weapon);
                weaponSo.FindProperty("_hitMarker").objectReferenceValue = hitMarker;
                weaponSo.ApplyModifiedProperties();
            }

            var hudSo = new SerializedObject(hud);
            var playerHealth = player.GetComponent<HealthBehaviour>();
            if (playerHealth != null)
                hudSo.FindProperty("_playerHealthBehaviour").objectReferenceValue = playerHealth;
            if (weapon != null)
                hudSo.FindProperty("_playerWeaponBehaviour").objectReferenceValue = weapon;

            var fpsCam = player.transform.Find("FPSCamera");
            if (fpsCam != null)
            {
                var shake = fpsCam.GetComponent<CameraShake>();
                if (shake != null)
                    hudSo.FindProperty("_cameraShake").objectReferenceValue = shake;
            }

            hudSo.ApplyModifiedProperties();
        }

        var waveObj = GameObject.Find("WaveController");
        if (waveObj != null)
        {
            var wcb = waveObj.GetComponent<WaveControllerBehaviour>();
            if (wcb != null)
            {
                var hudSo = new SerializedObject(hud);
                hudSo.FindProperty("_waveControllerBehaviour").objectReferenceValue = wcb;
                hudSo.ApplyModifiedProperties();
            }
        }

        // wire inventory and camera for hotbar + interaction prompt
        if (player != null)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            var fpsCam = player.transform.Find("FPSCamera");
            if (inventory != null || fpsCam != null)
            {
                var hudSo = new SerializedObject(hud);
                if (inventory != null)
                    hudSo.FindProperty("_playerInventory").objectReferenceValue = inventory;
                if (fpsCam != null)
                    hudSo.FindProperty("_playerCamera").objectReferenceValue = fpsCam.GetComponent<Camera>();
                hudSo.ApplyModifiedProperties();
                Debug.Log("wired inventory and camera on PlayerHUD");
            }
        }

        // add InventoryUI for Tab-toggle inventory grid
        var inventoryUI = canvas.GetComponent<InventoryUI>();
        if (inventoryUI == null)
        {
            inventoryUI = canvas.AddComponent<InventoryUI>();
            Debug.Log("added InventoryUI to HUD_Canvas");
        }

        // add RecipeSelectionUI for machine interaction
        var recipeUI = canvas.GetComponent<RecipeSelectionUI>();
        if (recipeUI == null)
        {
            recipeUI = canvas.AddComponent<RecipeSelectionUI>();
            Debug.Log("added RecipeSelectionUI to HUD_Canvas");
        }

        EditorUtility.SetDirty(canvas);
    }

    private static void SetupPlayerInventory()
    {
        var player = GameObject.Find("PlayerCharacter");
        if (player == null) return;

        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            inventory = player.AddComponent<PlayerInventory>();
            Debug.Log("added PlayerInventory to player");
        }

        var pickup = player.GetComponent<ItemPickupTrigger>();
        if (pickup == null)
        {
            pickup = player.AddComponent<ItemPickupTrigger>();
            Debug.Log("added ItemPickupTrigger to player");
        }

        EditorUtility.SetDirty(player);
    }

    private static void SetupItemRegistry()
    {
        var existing = Object.FindAnyObjectByType<ItemRegistry>();
        if (existing != null)
        {
            Debug.Log("item registry already exists");
            return;
        }

        var registryObj = new GameObject("ItemRegistry");
        registryObj.AddComponent<ItemRegistry>();
        EditorUtility.SetDirty(registryObj);
        Debug.Log("created ItemRegistry (empty, add items via inspector)");
    }

    private static void FixGround()
    {
        var existing = GameObject.Find("Ground");
        if (existing != null)
        {
            var mf = existing.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Debug.Log("ground already has valid mesh");
                return;
            }
            Object.DestroyImmediate(existing);
            Debug.Log("deleted broken ground object");
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.layer = PhysicsLayers.Terrain;
        ground.isStatic = true;
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10f, 1f, 10f);

        EditorUtility.SetDirty(ground);
        Debug.Log("created ground plane (100x100, layer Terrain, static)");
    }

    private static void BakeNavMesh()
    {
        NavMeshBuilder.BuildNavMesh();
        Debug.Log("navmesh baked");
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
