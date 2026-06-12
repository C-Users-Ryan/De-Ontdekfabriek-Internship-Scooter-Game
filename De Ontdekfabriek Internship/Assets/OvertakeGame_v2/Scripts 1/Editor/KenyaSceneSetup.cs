using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using OvertakeGame;

/// <summary>
/// Builds the full "Impact Makers Around The World Kenya" scene hierarchy from scratch.
/// Run from the menu: TATOE → Setup Kenya Scene
/// Safe to re-run — uses GetOrCreate so existing objects are reused, not duplicated.
/// </summary>
public static class KenyaSceneSetup
{
    [MenuItem("TATOE/Setup Kenya Scene")]
    public static void SetupScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Contains("Kenya") && !scene.name.Contains("Impact") && !scene.name.Contains("TATOE"))
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Setup Kenya Scene",
                $"Active scene is '{scene.name}'.\n\nThis setup is intended for the 'Impact Makers Around The World Kenya' scene. Continue anyway?",
                "Yes, set it up", "Cancel");
            if (!proceed) return;
        }

        Debug.Log("[KenyaSceneSetup] Starting scene setup...");

        // ── Tags ─────────────────────────────────────────────────────────────
        EnsureTag("Traffic");
        EnsureTag("Pothole");
        EnsureTag("Rock");

        // ── Load shared assets ───────────────────────────────────────────────
        var roadConfig  = AssetDatabase.LoadAssetAtPath<RoadSideConfig>("Assets/OvertakeGame_v2/RoadSideConfig.asset");
        var hatchback   = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OvertakeGame_v2/Prefab/Hatchback.prefab");
        var pickup      = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OvertakeGame_v2/Prefab/Pickup.prefab");
        var taxi        = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OvertakeGame_v2/Prefab/Taxi.prefab");
        var vanBig      = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OvertakeGame_v2/Prefab/VanBig.prefab");
        var potholePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OvertakeGame_v2/Prefab/Pothole.prefab");

        var rockPrefabs = new List<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/OvertakeGame_v2/Prefab/Rocks" }))
            rockPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        if (rockPrefabs.Count == 0) // fallback to root Rock.prefab
        {
            var r = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OvertakeGame_v2/Prefab/Rock.prefab");
            if (r != null) rockPrefabs.Add(r);
        }

        var tilePrefabs = new List<GameObject>();
        var roadSegments = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OvertakeGame_v2/prefab corners/Road Segments.prefab");
        if (roadSegments != null) tilePrefabs.Add(roadSegments);
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/OvertakeGame_v2/Tiles" }))
        {
            if (tilePrefabs.Count >= 4) break;
            tilePrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        // ── 1. GAME MANAGER ──────────────────────────────────────────────────
        var gmGO   = GetOrCreate("GameManager");
        var gm     = GetOrAdd<OvertakeGame.GameManager>(gmGO);
        var ws     = GetOrAdd<WorldSpeed>(gmGO);
        var tm     = GetOrAdd<TimerManager>(gmGO);
        var sm     = GetOrAdd<ScoreManager>(gmGO);
        var warn   = GetOrAdd<WarningSystem>(gmGO);
        GetOrAdd<AudioManager>(gmGO);
        GetOrAdd<AnalyticsManager>(gmGO);

        // WorldSpeed config
        ws.baseSpeed         = 10f;
        ws.maxSpeed          = 28f;
        ws.accelerationForce = 15f;
        ws.brakeForce        = 20f;

        // GameManager toggles
        gm.gameOverOnCollision = true;
        gm.useSessionTimer     = true;
        gm.sessionDuration     = 120f;
        gm.deductOnCollision   = true;
        gm.deductOnWrongLane   = true;
        gm.deductOnSpeeding    = true;
        gm.deductOnPothole     = true;
        gm.deductOnRock        = true;
        gm.warnOnWrongLane     = true;
        gm.warnOnSpeeding      = true;
        gm.warnOnCollision     = true;
        gm.warnOnPothole       = true;
        gm.warnOnRock          = true;

        // ScoreManager config
        sm.startingScore           = 1000;
        sm.collisionDeduction      = 50;
        sm.potholeDeduction        = 20;
        sm.rockDeduction           = 15;
        sm.wrongLaneDeductionPerSecond = 10f;
        sm.speedingDeductionPerSecond  = 8f;

        // ── 2. PLAYER ────────────────────────────────────────────────────────
        var playerGO = GetOrCreate("Player");
        playerGO.transform.position = Vector3.zero;

        var rb = GetOrAdd<Rigidbody>(playerGO);
        rb.constraints   = RigidbodyConstraints.FreezeRotation
                         | RigidbodyConstraints.FreezePositionY
                         | RigidbodyConstraints.FreezePositionZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity    = false;
        rb.mass          = 1f;

        var capsule = GetOrAdd<CapsuleCollider>(playerGO);
        capsule.isTrigger = false;
        capsule.height    = 1.8f;
        capsule.radius    = 0.4f;
        capsule.center    = new Vector3(0f, 0.9f, 0f);

        var pc        = GetOrAdd<PlayerController>(playerGO);
        var gyro      = GetOrAdd<GyroscopeSteering>(playerGO);
        var wrongLane = GetOrAdd<WrongLaneDetector>(playerGO);
        var overtake  = GetOrAdd<OvertakeDetector>(playerGO);
        var reward    = GetOrAdd<RewardSystem>(playerGO);
        GetOrAdd<SpeedMonitor>(playerGO);
        GetOrAdd<OvertakeCollisionHandler>(playerGO);

        // PlayerController config
        pc.lateralSpeed        = 6f;
        pc.lateralAcceleration = 30f;
        pc.roadHalfWidth       = 4f;

        // WrongLaneDetector config
        wrongLane.roadConfig   = roadConfig;
        wrongLane.centreLaneX  = 0f;
        wrongLane.graceBuffer  = 0.2f;
        wrongLane.gracePeriod  = 0.5f;

        // OvertakeDetector config
        overtake.overtakeThreshold = 3f;
        overtake.rewardSystem      = reward;

        // RewardSystem config
        reward.scoreManager     = sm;
        reward.laneDetector     = wrongLane;
        reward.rewardOvertaking = true;
        reward.overtakePoints   = 25;

        // Gyroscope config
        gyro.enableGyroSteering          = true;
        gyro.enableHorizonLock           = true;
        gyro.horizonLockStrength         = 1f;
        gyro.maxTiltAngle                = 25f;
        gyro.deadZone                    = 3f;
        gyro.simulateOnDesktop           = true;
        gyro.mouseSimulationSensitivity  = 60f;

        // Player mesh (cube placeholder — replace with actual scooter model)
        if (playerGO.GetComponentInChildren<MeshRenderer>() == null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "ScooterMesh_Placeholder";
            cube.transform.SetParent(playerGO.transform, false);
            cube.transform.localScale    = new Vector3(0.8f, 1.2f, 1.8f);
            cube.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        }

        // ── 3. CAMERA RIG ────────────────────────────────────────────────────
        var cameraRigGO = GetOrCreateChild(playerGO, "CameraRig");
        cameraRigGO.transform.localPosition    = new Vector3(0f, 2.5f, -8f);
        cameraRigGO.transform.localEulerAngles = new Vector3(15f, 0f, 0f);

        var camChildGO = GetOrCreateChild(cameraRigGO, "Main Camera");
        camChildGO.tag = "MainCamera";
        camChildGO.transform.localPosition = Vector3.zero;
        camChildGO.transform.localRotation = Quaternion.identity;
        var cam = GetOrAdd<Camera>(camChildGO);
        cam.fieldOfView    = 70f;
        cam.nearClipPlane  = 0.1f;
        cam.farClipPlane   = 500f;
        if (camChildGO.GetComponent<AudioListener>() == null)
            camChildGO.AddComponent<AudioListener>();

        // Wire gyroscope camera reference now that the rig exists
        gyro.playerController = pc;
        gyro.cameraRig        = cameraRigGO.transform;

        // ── 4. ROAD SYSTEM ───────────────────────────────────────────────────
        var roadGO   = GetOrCreate("RoadSystem");
        var recycler = GetOrAdd<RoadTileRecycler>(roadGO);
        recycler.tilePrefabs      = tilePrefabs;
        recycler.poolSize         = 5;
        recycler.tileLength       = 100f;
        recycler.firstTileStartZ  = -20f;
        recycler.recycleOffset    = 10f;
        recycler.playerTransform  = playerGO.transform;

        if (tilePrefabs.Count == 0)
            Debug.LogWarning("[KenyaSceneSetup] No tile prefabs found. Assign tile prefabs to RoadSystem → RoadTileRecycler manually.");

        // ── 5. MANAGERS ──────────────────────────────────────────────────────
        var managersGO = GetOrCreate("Managers");

        // Traffic Manager
        var trafficGO = GetOrCreateChild(managersGO, "TrafficManager");
        var traffic   = GetOrAdd<TrafficManager>(trafficGO);
        traffic.roadConfig             = roadConfig;
        traffic.playerLaneX            = 1.5f;
        traffic.oncomingLaneX          = -1.5f;
        traffic.poolSizePerLane        = 8;
        traffic.sameDirectionSpawnMin  = 3f;
        traffic.sameDirectionSpawnMax  = 6f;
        traffic.oncomingSpawnMin       = 1.5f;
        traffic.oncomingSpawnMax       = 3.5f;
        traffic.oncomingSpawnDistance  = 120f;
        traffic.despawnDistanceBehind  = 30f;
        traffic.prewarmStartDistance   = 15f;
        traffic.prewarmEndDistance     = 200f;
        traffic.sameDirectionSpeedMin  = 3f;
        traffic.sameDirectionSpeedMax  = 7f;
        traffic.oncomingSpeedMin       = 5f;
        traffic.oncomingSpeedMax       = 10f;

        var sameDir  = new List<GameObject>();
        var oncoming = new List<GameObject>();
        if (hatchback != null) sameDir.Add(hatchback);
        if (pickup    != null) sameDir.Add(pickup);
        if (taxi      != null) { sameDir.Add(taxi); oncoming.Add(taxi); }
        if (vanBig    != null) oncoming.Add(vanBig);
        traffic.sameDirectionPrefabs = sameDir;
        traffic.oncomingPrefabs      = oncoming;

        if (sameDir.Count == 0)
            Debug.LogWarning("[KenyaSceneSetup] No same-direction vehicle prefabs found.");

        // Pothole Manager
        var potholeManagerGO = GetOrCreateChild(managersGO, "PotholeManager");
        var potholeManager   = GetOrAdd<PotholeManager>(potholeManagerGO);
        potholeManager.potholePrefab        = potholePrefab;
        potholeManager.poolSize             = 12;
        potholeManager.spawnDistanceAhead   = 70f;
        potholeManager.despawnDistanceBehind = 20f;
        potholeManager.spawnIntervalMin     = 3f;
        potholeManager.spawnIntervalMax     = 6f;
        potholeManager.rampDensityOverTime  = true;
        potholeManager.rampDuration         = 90f;
        potholeManager.spawnSide            = PotholeManager.LaneSide.PlayerLaneOnly;

        if (potholePrefab == null)
            Debug.LogWarning("[KenyaSceneSetup] Pothole prefab not found at expected path.");

        // Rock Manager
        var rockManagerGO = GetOrCreateChild(managersGO, "RockManager");
        var rockManager   = GetOrAdd<RockManager>(rockManagerGO);
        rockManager.rockPrefabs           = rockPrefabs;
        rockManager.poolSize              = 20;
        rockManager.spawnDistanceAhead    = 80f;
        rockManager.despawnDistanceBehind = 20f;
        rockManager.spawnIntervalMin      = 4f;
        rockManager.spawnIntervalMax      = 8f;
        rockManager.enableClusters        = true;
        rockManager.clusterChance         = 0.25f;
        rockManager.spawnZone             = RockManager.SpawnZone.PlayerLaneOnly;

        // Checkpoint Manager
        var checkpointGO     = GetOrCreateChild(managersGO, "CheckpointManager");
        var checkpointManager = GetOrAdd<CheckpointManager>(checkpointGO);
        checkpointManager.checkpointSpawnDistance = 80f;
        checkpointManager.checkpointY             = 0f;
        checkpointManager.pauseDuration           = 5f;
        checkpointManager.checkpointBrakeRate     = 25f;
        checkpointManager.timerManager            = tm;
        checkpointManager.trafficManager          = traffic;
        checkpointManager.scoreManager            = sm;
        checkpointManager.gameManager             = gm;

        // Checkpoint prefab — create one if it doesn't exist yet
        var cpPrefabPath   = "Assets/OvertakeGame_v2/prefab corners/CheckpointTrigger.prefab";
        var cpPrefab       = AssetDatabase.LoadAssetAtPath<GameObject>(cpPrefabPath);
        if (cpPrefab == null)
        {
            var cpTemp = new GameObject("CheckpointTrigger");
            var cpBox  = cpTemp.AddComponent<BoxCollider>();
            cpBox.isTrigger = true;
            cpBox.size      = new Vector3(10f, 3f, 0.5f);
            cpTemp.AddComponent<CheckpointTrigger>();
            cpPrefab = PrefabUtility.SaveAsPrefabAsset(cpTemp, cpPrefabPath);
            Object.DestroyImmediate(cpTemp);
            Debug.Log($"[KenyaSceneSetup] Created CheckpointTrigger prefab at {cpPrefabPath}");
        }
        checkpointManager.checkpointPrefab = cpPrefab;

        // Day Cycle Manager
        var dayGO = GetOrCreateChild(managersGO, "DayCycleManager");
        GetOrAdd<DayCycleManager>(dayGO);

        // ── 6. UI CANVAS ─────────────────────────────────────────────────────
        var canvasGO = GetOrCreate("UI Canvas");
        var canvas   = GetOrAdd<Canvas>(canvasGO);
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = GetOrAdd<CanvasScaler>(canvasGO);
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 1024f);
        scaler.matchWidthOrHeight  = 0.5f;
        GetOrAdd<GraphicRaycaster>(canvasGO);
        var uiManager = GetOrAdd<UIManager>(canvasGO);
        var goScreen  = GetOrAdd<GameOverScreen>(canvasGO);
        GetOrAdd<SwahiliUI>(canvasGO);

        // HUD — score + timer
        var hudGO = GetOrCreateChild(canvasGO, "HUD");
        StretchToParent(hudGO);

        var scoreTxt = MakeTMPText(hudGO, "ScoreText", "SCORE  1000", 28, TextAlignmentOptions.TopLeft);
        Anchor(scoreTxt.gameObject, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 1f),
               new Vector2(10f, -10f), new Vector2(300f, 50f));

        var timerPanelGO = GetOrCreateChild(hudGO, "TimerPanel");
        Anchor(timerPanelGO, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
               new Vector2(0f, -10f), new Vector2(160f, 50f));

        var timerTxt = MakeTMPText(timerPanelGO, "TimerText", "2:00", 32, TextAlignmentOptions.Center);
        StretchToParent(timerTxt.gameObject);

        // Game Over panel
        var gameOverPanel = MakePanel(canvasGO, "GameOverPanel", new Color(0f, 0f, 0f, 0.88f));
        gameOverPanel.SetActive(false);
        var gameOverScoreTxt = MakeTMPText(gameOverPanel, "GameOverScoreText", "GAME OVER\n0", 52, TextAlignmentOptions.Center);
        Anchor(gameOverScoreTxt.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0f, 60f), new Vector2(600f, 130f));
        var restartGameOverBtn = MakeButton(gameOverPanel, "RestartButtonGameOver", "Play Again");
        Anchor(restartGameOverBtn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0f, -60f), new Vector2(260f, 70f));

        // Finish panel
        var finishPanel = MakePanel(canvasGO, "FinishPanel", new Color(0f, 0.18f, 0f, 0.88f));
        finishPanel.SetActive(false);
        var finishScoreTxt = MakeTMPText(finishPanel, "FinishScoreText", "SCORE: 0", 52, TextAlignmentOptions.Center);
        Anchor(finishScoreTxt.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0f, 60f), new Vector2(600f, 80f));
        var restartFinishBtn = MakeButton(finishPanel, "RestartButtonFinish", "Play Again");
        Anchor(restartFinishBtn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0f, -60f), new Vector2(260f, 70f));

        // Checkpoint panel
        var checkpointPanel = MakePanel(canvasGO, "CheckpointPanel", new Color(0f, 0f, 0.22f, 0.88f));
        checkpointPanel.SetActive(false);
        var cpRoundTxt = MakeTMPText(checkpointPanel, "CheckpointRoundText", "Round 1 Complete!", 40, TextAlignmentOptions.Center);
        Anchor(cpRoundTxt.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0f, 80f), new Vector2(700f, 70f));
        var cpScoreTxt = MakeTMPText(checkpointPanel, "CheckpointScoreText", "SCORE: 0", 34, TextAlignmentOptions.Center);
        Anchor(cpScoreTxt.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0f, 10f), new Vector2(500f, 60f));
        var cpCountdownTxt = MakeTMPText(checkpointPanel, "CheckpointCountdownText", "Next round in 5s", 28, TextAlignmentOptions.Center);
        Anchor(cpCountdownTxt.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
               new Vector2(0f, -50f), new Vector2(500f, 50f));

        // Wire UIManager
        uiManager.scoreText                = scoreTxt;
        uiManager.timerText                = timerTxt;
        uiManager.timerPanel               = timerPanelGO;
        uiManager.gameOverPanel            = gameOverPanel;
        uiManager.gameOverScoreText        = gameOverScoreTxt;
        uiManager.restartButtonGameOver    = restartGameOverBtn.GetComponent<Button>();
        uiManager.finishPanel              = finishPanel;
        uiManager.finishScoreText          = finishScoreTxt;
        uiManager.restartButtonFinish      = restartFinishBtn.GetComponent<Button>();
        uiManager.checkpointPanel          = checkpointPanel;
        uiManager.checkpointRoundText      = cpRoundTxt;
        uiManager.checkpointScoreText      = cpScoreTxt;
        uiManager.checkpointCountdownText  = cpCountdownTxt;
        uiManager.scoreManager             = sm;
        uiManager.timerManager             = tm;

        checkpointManager.uiManager = uiManager;

        // ── 7. WIRE GAME MANAGER REFERENCES ──────────────────────────────────
        gm.scoreManager      = sm;
        gm.timerManager      = tm;
        gm.uiManager         = uiManager;
        gm.trafficManager    = traffic;
        gm.warningSystem     = warn;
        gm.checkpointManager = checkpointManager;
        gm.potholeManager    = potholeManager;
        gm.rockManager       = rockManager;

        // ── 8. DIRECTIONAL LIGHT ─────────────────────────────────────────────
        var lightGO = GetOrCreate("Directional Light");
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = GetOrAdd<Light>(lightGO);
        light.type      = LightType.Directional;
        light.intensity = 1f;
        light.color     = new Color(1f, 0.95f, 0.84f);

        // ── 9. SAVE ───────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[KenyaSceneSetup] ✓ Scene setup complete.");
        Debug.Log("[KenyaSceneSetup] Things to check manually:");
        Debug.Log("  • RoadSystem → RoadTileRecycler: assign real road tile prefabs if Road Segments.prefab is not enough");
        Debug.Log("  • Player → ScooterMesh_Placeholder: replace cube with real scooter model");
        Debug.Log("  • DayCycleManager: assign URP Volume references in Inspector for day cycle blending");
        Debug.Log("  • Press Play and use WASD to verify everything runs");
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    static GameObject GetOrCreate(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go : new GameObject(name);
    }

    static GameObject GetOrCreateChild(GameObject parent, string childName)
    {
        var t = parent.transform.Find(childName);
        if (t != null) return t.gameObject;
        var go = new GameObject(childName);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
        => go.GetComponent<T>() ?? go.AddComponent<T>();

    static void StretchToParent(GameObject go)
    {
        var rt      = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }

    static void Anchor(GameObject go, Vector2 pivot, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var rt           = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.pivot         = pivot;
        rt.anchorMin     = anchorMin;
        rt.anchorMax     = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta     = sizeDelta;
    }

    static TextMeshProUGUI MakeTMPText(GameObject parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        var go  = GetOrCreateChild(parent, name);
        var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = Color.white;
        tmp.alignment = alignment;
        return tmp;
    }

    static GameObject MakePanel(GameObject parent, string name, Color color)
    {
        var go  = GetOrCreateChild(parent, name);
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = color;
        StretchToParent(go);
        return go;
    }

    static GameObject MakeButton(GameObject parent, string name, string label)
    {
        var go  = GetOrCreateChild(parent, name);
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        if (go.GetComponent<Button>() == null) go.AddComponent<Button>();

        var labelGO = GetOrCreateChild(go, "Label");
        StretchToParent(labelGO);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>() ?? labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 26f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return go;
    }

    static void EnsureTag(string tagName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tagsProp = tagManager.FindProperty("tags");
        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName) return;
        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
        tagManager.ApplyModifiedProperties();
        Debug.Log($"[KenyaSceneSetup] Added tag: {tagName}");
    }
}
