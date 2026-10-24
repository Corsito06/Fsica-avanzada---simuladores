#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Herramienta de Editor que repara automáticamente el brazo del usuario ("Base") en la escena:
/// - Alinea DedoIzquierdo y DedoDerecho a la misma altura y corrige el eje prismático horizontal (Z).
/// - Construye el Péndulo Pasivo completo con su soporte, soga (cilindro) y esfera, con SphericalJoint sin fricción.
/// - Normaliza las escalas de los eslabones (1,1,1) pasando la malla a hijos "_Visual" para evitar deformaciones al rotar.
/// - Crea los cubos interactuables, la zona de descarga y el Canvas de UI conectado al RobotArmManager.
/// </summary>
[InitializeOnLoad]
public static class RobotArmSceneBuilder
{
    private const string CUBES_ROOT = "CubosInteractuables";
    private const string ZONE_NAME = "ZonaDescarga";
    private const string CANVAS_NAME = "SimuladorCanvas";
    private const string ASSET_DIR = "Assets/Prefabs";

    private const float FINGER_STROKE = 0.085f;

    static RobotArmSceneBuilder()
    {
        EditorApplication.delayCall += AutoRepairIfNeeded;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += AutoRepairIfNeeded;
        }
    }

    private static void AutoRepairIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GameObject baseObj = GameObject.Find("Base");
        if (baseObj == null)
            return;

        // Verificar si hace falta reparar (ej: aún tiene escala deformada en SoporteGarra o falta la Soga del péndulo o falta el Canvas)
        Transform soporteGarra = FindDeepChild(baseObj.transform, "SoporteGarra");
        Transform pendulo = FindDeepChild(baseObj.transform, "PenduloPasivo");
        bool needsArmRepair = false;

        if (soporteGarra != null && Vector3.Distance(soporteGarra.localScale, Vector3.one) > 0.01f)
            needsArmRepair = true;

        if (pendulo == null || pendulo.Find("Soga") == null || pendulo.GetComponent<ArticulationBody>() == null)
            needsArmRepair = true;

        Transform dedoIzq = FindDeepChild(baseObj.transform, "DedoIzquierdo");
        Transform dedoDer = FindDeepChild(baseObj.transform, "DedoDerecho");
        if (dedoIzq != null && dedoDer != null && Mathf.Abs(dedoIzq.localPosition.y - dedoDer.localPosition.y) > 0.001f)
            needsArmRepair = true;

        bool needsEnvironment = GameObject.Find(CANVAS_NAME) == null || GameObject.Find(CUBES_ROOT) == null || GameObject.Find(ZONE_NAME) == null;

        if (needsArmRepair || needsEnvironment)
        {
            RepairUserScene();
        }
    }

    [MenuItem("Simulador/Reparar mi brazo actual (Garra + Péndulo + UI)")]
    public static void RepairUserScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[Simulador] Salí de Play Mode para aplicar la reparación permanente en la escena.");
            return;
        }

        RobotArmManager manager = RepairOrBuildArm();
        if (manager == null) return;

        CubeManager cubes;
        TaskTargetZone zone;
        BuildCubesAndZone(manager.transform.position, out cubes, out zone);
        BuildUI(manager, cubes, zone);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=green><b>[Simulador]</b> ¡Brazo reparado! Dedo izquierdo alineado, péndulo con soga creado y controles listos. Dale a Play.</color>");
    }

    [MenuItem("Simulador/Construir TODO (brazo + cubos + interfaz)")]
    public static void BuildAll()
    {
        RepairUserScene();
    }

    public static RobotArmManager RepairOrBuildArm()
    {
        if (!AssetDatabase.IsValidFolder(ASSET_DIR))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        Material matBase = LoadOrCreateMaterial("Color base garra", new Color(0.15f, 0.6f, 0.15f));
        Material matArm = LoadOrCreateMaterial("Color brazo", new Color(0.9f, 0.9f, 0.92f));
        Material matDark = LoadOrCreateMaterial("Color soga", new Color(0.15f, 0.15f, 0.15f));
        Material matBall = LoadOrCreateMaterial("Color pendulo", new Color(0.85f, 0.18f, 0.18f));
        PhysicMaterial grip = LoadOrCreatePhysicMaterial();

        float floorY = FindFloorY();

        GameObject baseObj = GameObject.Find("Base");
        if (baseObj == null)
        {
            baseObj = new GameObject("Base");
            baseObj.transform.position = new Vector3(-0.12f, floorY, -2.52f);
        }

        Vector3 baseWorldPos = new Vector3(baseObj.transform.position.x, floorY, baseObj.transform.position.z);

        // Obtener o crear cada objeto manteniendo los nombres exactos de la jerarquía del usuario
        GameObject sobrebase = FindOrCreateChild(baseObj.transform, "Sobrebase");
        GameObject brazo = FindOrCreateChild(sobrebase.transform, "Brazo");
        GameObject antebrazo = FindOrCreateChild(brazo.transform, "Antebrazo");
        GameObject soporteGarra = FindOrCreateChild(antebrazo.transform, "SoporteGarra");
        GameObject dedoIzq = FindOrCreateChild(soporteGarra.transform, "DedoIzquierdo");
        GameObject dedoDer = FindOrCreateChild(soporteGarra.transform, "DedoDerecho");
        GameObject pendulo = FindOrCreateChild(soporteGarra.transform, "PenduloPasivo");

        // 1) BASE (Raíz inmóvil, escala 1,1,1)
        StripDirectMeshIfHasChildren(baseObj);
        baseObj.transform.position = baseWorldPos;
        baseObj.transform.rotation = Quaternion.identity;
        baseObj.transform.localScale = Vector3.one;
        EnsureChildVisual(baseObj.transform, "Base_Visual", PrimitiveType.Cube,
            new Vector3(0f, 0.155f, 0f), new Vector3(1.0f, 0.31f, 1.0f), matBase, null, true);

        ArticulationBody baseAb = GetOrAdd<ArticulationBody>(baseObj);
        baseAb.immovable = true;
        baseAb.mass = 50f;
        baseAb.solverIterations = 30;
        baseAb.solverVelocityIterations = 12;

        RobotArmManager manager = GetOrAdd<RobotArmManager>(baseObj);

        // 2) SOBREBASE (Revolute - Giro horizontal Yaw alrededor de Y)
        StripDirectMeshIfHasChildren(sobrebase);
        ResetLinkTransform(sobrebase.transform, new Vector3(0f, 0.31f, 0f));
        EnsureChildVisual(sobrebase.transform, "Sobrebase_Visual", PrimitiveType.Cube,
            new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.30f, 0.5f), matBase, null, true);

        ConfigureLinkJoint(sobrebase, ArticulationJointType.RevoluteJoint, Quaternion.Euler(0f, 0f, 90f), 4f);
        RobotJointController baseCtrl = ConfigureController(sobrebase, "Base (Yaw)", 15000f, 1500f, 25000f, -170f, 170f, 80f);

        // 3) BRAZO (Revolute - Inclinación Pitch alrededor de X)
        StripDirectMeshIfHasChildren(brazo);
        ResetLinkTransform(brazo.transform, new Vector3(0f, 0.30f, 0f));
        EnsureChildVisual(brazo.transform, "Brazo_Visual", PrimitiveType.Cube,
            new Vector3(0f, 0.30f, 0f), new Vector3(0.14f, 0.60f, 0.14f), matArm, null, true);

        ConfigureLinkJoint(brazo, ArticulationJointType.RevoluteJoint, Quaternion.identity, 4f);
        RobotJointController shoulderCtrl = ConfigureController(brazo, "Brazo (Pitch)", 45000f, 4500f, 60000f, -90f, 90f, 60f);

        // 4) ANTEBRAZO (Revolute - Flexión Pitch alrededor de X)
        StripDirectMeshIfHasChildren(antebrazo);
        ResetLinkTransform(antebrazo.transform, new Vector3(0f, 0.60f, 0f));
        EnsureChildVisual(antebrazo.transform, "Antebrazo_Visual", PrimitiveType.Cube,
            new Vector3(0f, 0.42f, 0f), new Vector3(0.12f, 0.84f, 0.12f), matArm, null, true);

        ConfigureLinkJoint(antebrazo, ArticulationJointType.RevoluteJoint, Quaternion.identity, 3f);
        RobotJointController elbowCtrl = ConfigureController(antebrazo, "Antebrazo (Pitch)", 35000f, 3500f, 45000f, -120f, 120f, 70f);

        // 5) SOPORTE GARRA (Fixed - Palma soldada al extremo del antebrazo, escala 1,1,1)
        StripDirectMeshIfHasChildren(soporteGarra);
        ResetLinkTransform(soporteGarra.transform, new Vector3(0f, 0.84f, 0f));
        EnsureChildVisual(soporteGarra.transform, "SoporteGarra_Visual", PrimitiveType.Cube,
            new Vector3(0f, 0.0125f, 0f), new Vector3(0.15f, 0.025f, 0.26f), matArm, grip, true);
        ConfigureLinkJoint(soporteGarra, ArticulationJointType.FixedJoint, Quaternion.identity, 1f);

        // 6) DEDO IZQUIERDO Y DEDO DERECHO (Prismatic sobre eje horizontal Z, a la MISMA altura exacta)
        RobotJointController leftCtrl = ConfigureFingerDirect(
            dedoIzq, "Dedo Izq (Prismatic)",
            new Vector3(0f, 0.085f, -0.115f),
            new Vector3(0.15f, 0.17f, 0.025f),
            0f, FINGER_STROKE, matArm, grip);

        RobotJointController rightCtrl = ConfigureFingerDirect(
            dedoDer, "Dedo Der (Prismatic)",
            new Vector3(0f, 0.085f, +0.115f),
            new Vector3(0.15f, 0.17f, 0.025f),
            -FINGER_STROKE, 0f, matArm, grip);

        // 7) PÉNDULO PASIVO (Soporte lateral + Soga cilíndrica + Esfera en la punta, SphericalJoint libre)
        EnsureChildVisual(soporteGarra.transform, "Soporte_Pendulo", PrimitiveType.Cube,
            new Vector3(0.16f, 0.0125f, 0f), new Vector3(0.22f, 0.02f, 0.02f), matDark, null, false);

        StripDirectMeshIfHasChildren(pendulo);
        ResetLinkTransform(pendulo.transform, new Vector3(0.25f, 0.0125f, 0f));

        // Soga (cilindro desde el pivote (0,0,0) hasta (0,-0.28,0))
        EnsureChildVisual(pendulo.transform, "Soga", PrimitiveType.Cylinder,
            new Vector3(0f, -0.14f, 0f), new Vector3(0.015f, 0.14f, 0.015f), matDark, null, false);

        // Esfera (bola en el extremo inferior de la soga)
        EnsureChildVisual(pendulo.transform, "Esfera", PrimitiveType.Sphere,
            new Vector3(0f, -0.31f, 0f), new Vector3(0.11f, 0.11f, 0.11f), matBall, grip, true);

        ArticulationBody pendAb = ConfigureLinkJoint(pendulo, ArticulationJointType.SphericalJoint, Quaternion.identity, 0.35f);
        pendAb.swingYLock = ArticulationDofLock.FreeMotion;
        pendAb.swingZLock = ArticulationDofLock.FreeMotion;
        pendAb.twistLock = ArticulationDofLock.FreeMotion;
        pendAb.linearDamping = 0.05f;
        pendAb.angularDamping = 0.4f;
        pendAb.jointFriction = 0f;
        pendAb.useGravity = true;

        ArticulationDrive zeroDrive = new ArticulationDrive
        {
            stiffness = 0f,
            damping = 0f,
            forceLimit = 0f,
            target = 0f
        };
        pendAb.xDrive = zeroDrive;
        pendAb.yDrive = zeroDrive;
        pendAb.zDrive = zeroDrive;

        GetOrAdd<PassivePendulum>(pendulo);

        // 8) Conectar referencias en RobotArmManager
        SetRef(manager, "baseJoint", baseCtrl);
        SetRef(manager, "shoulderJoint", shoulderCtrl);
        SetRef(manager, "elbowJoint", elbowCtrl);
        SetRef(manager, "leftFingerJoint", leftCtrl);
        SetRef(manager, "rightFingerJoint", rightCtrl);
        SetRef(manager, "passivePendulum", pendAb);
        SetFloat(manager, "maxGripperStroke", FINGER_STROKE);
        SetFloat(manager, "homeBaseAngle", 0f);
        SetFloat(manager, "homeShoulderAngle", 25f);
        SetFloat(manager, "homeElbowAngle", 35f);
        SetFloat(manager, "homeGripperOpen", 1f);

        EditorUtility.SetDirty(baseObj);
        return manager;
    }

    private static RobotJointController ConfigureFingerDirect(
        GameObject fingerObj, string jointLabel, Vector3 localPos, Vector3 localScale,
        float lower, float upper, Material mat, PhysicMaterial grip)
    {
        // Eliminar cualquier hijo visual viejo si existiera
        Transform oldVis = fingerObj.transform.Find(fingerObj.name + "_Visual");
        if (oldVis != null) Object.DestroyImmediate(oldVis.gameObject);

        fingerObj.transform.localPosition = localPos;
        fingerObj.transform.localRotation = Quaternion.identity;
        fingerObj.transform.localScale = localScale;

        MeshFilter mf = GetOrAdd<MeshFilter>(fingerObj);
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mf.sharedMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tempCube);

        MeshRenderer mr = GetOrAdd<MeshRenderer>(fingerObj);
        mr.sharedMaterial = mat;

        BoxCollider bc = GetOrAdd<BoxCollider>(fingerObj);
        bc.size = Vector3.one;
        bc.center = Vector3.zero;
        bc.sharedMaterial = grip;

        Quaternion horizontalZ = Quaternion.Euler(0f, -90f, 0f);
        ArticulationBody ab = ConfigureLinkJoint(fingerObj, ArticulationJointType.PrismaticJoint, horizontalZ, 0.25f);
        ab.linearLockX = ArticulationDofLock.LimitedMotion;
        ab.linearLockY = ArticulationDofLock.LockedMotion;
        ab.linearLockZ = ArticulationDofLock.LockedMotion;
        ab.matchAnchors = true;

        return ConfigureController(fingerObj, jointLabel, 5000f, 500f, 6000f, lower, upper, 0.35f);
    }

    public static void BuildCubesAndZone(Vector3 robotPos, out CubeManager cubeManager, out TaskTargetZone zone)
    {
        PhysicMaterial grip = LoadOrCreatePhysicMaterial();

        GameObject oldZone = GameObject.Find(ZONE_NAME);
        if (oldZone != null) Object.DestroyImmediate(oldZone);
        GameObject oldCubes = GameObject.Find(CUBES_ROOT);
        if (oldCubes != null) Object.DestroyImmediate(oldCubes);

        // Zona de descarga a 90 grados del brazo (en +X) a distancia de alcance (1.15m)
        Vector3 zonePos = robotPos + new Vector3(1.15f, 0f, 0f);
        GameObject zoneObj = new GameObject(ZONE_NAME);
        zoneObj.transform.position = zonePos;

        EnsureChildVisual(zoneObj.transform, "Plataforma_Visual", PrimitiveType.Cube,
            new Vector3(0f, 0.015f, 0f), new Vector3(0.75f, 0.03f, 0.75f),
            LoadOrCreateMaterial("Color zona", new Color(0.2f, 0.7f, 0.9f)), grip, true);

        GameObject trigger = new GameObject("Trigger");
        trigger.transform.SetParent(zoneObj.transform, false);
        BoxCollider bc = trigger.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.75f, 0.6f, 0.75f);
        bc.center = new Vector3(0f, 0.3f, 0f);
        zone = trigger.AddComponent<TaskTargetZone>();

        // Cubos interactuables frente al brazo (+Z, a 1.12m de la base)
        GameObject cubesRoot = new GameObject(CUBES_ROOT);
        cubeManager = cubesRoot.AddComponent<CubeManager>();

        Color[] colors = { new Color(0.9f, 0.2f, 0.2f), new Color(0.95f, 0.8f, 0.1f), new Color(0.2f, 0.4f, 0.95f) };
        string[] colorNames = { "rojo", "amarillo", "azul" };
        float[] xOffsets = { -0.25f, 0f, 0.25f };

        var so = new SerializedObject(cubeManager);
        SerializedProperty list = so.FindProperty("cubes");
        list.arraySize = 3;

        for (int i = 0; i < 3; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cubo_" + (i + 1);
            cube.transform.SetParent(cubesRoot.transform, false);
            cube.transform.position = robotPos + new Vector3(xOffsets[i], 0.065f, 1.12f);
            cube.transform.localScale = Vector3.one * 0.11f;
            cube.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMaterial("Color cubo " + colorNames[i], colors[i]);
            cube.GetComponent<Collider>().sharedMaterial = grip;

            Rigidbody rb = cube.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.drag = 0.3f;
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            SerializedProperty item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("body").objectReferenceValue = rb;
            item.FindPropertyRelative("initialPosition").vector3Value = cube.transform.position;
            item.FindPropertyRelative("initialRotation").quaternionValue = cube.transform.rotation;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cubeManager);
    }

    public static void BuildUI(RobotArmManager manager, CubeManager cubes, TaskTargetZone zone)
    {
        GameObject old = GameObject.Find(CANVAS_NAME);
        if (old != null) Object.DestroyImmediate(old);

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        GameObject canvasObj = new GameObject(CANVAS_NAME, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RobotUIController ui = canvasObj.AddComponent<RobotUIController>();
        DefaultControls.Resources res = UIResources();

        RectTransform left = MakePanel(canvasObj.transform, "PanelControles", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(20f, 0f), 440f, new Color(0.08f, 0.1f, 0.13f, 0.88f));
        AddLabel(left, "CONTROL DEL BRAZO", 22, FontStyle.Bold, Color.white, 32f);

        Slider sBase = AddSlider(left, res, "1. Base (Giro Yaw)");
        Slider sShoulder = AddSlider(left, res, "2. Brazo (Hombro Pitch)");
        Slider sElbow = AddSlider(left, res, "3. Antebrazo (Codo Pitch)");
        Slider sGripper = AddSlider(left, res, "4. Garra (0 = Cerrada, 1 = Abierta)");
        Button bToggle = AddButton(left, res, "Abrir / Cerrar garra", new Color(0.2f, 0.55f, 0.9f));
        Button bArm = AddButton(left, res, "Posición de Reposo (Reset)", new Color(0.85f, 0.55f, 0.15f));
        Button bCubes = AddButton(left, res, "Reiniciar Cubos", new Color(0.8f, 0.25f, 0.25f));

        RectTransform right = MakePanel(canvasObj.transform, "PanelTelemetria", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-20f, -20f), 580f, new Color(0.08f, 0.1f, 0.13f, 0.88f));
        Text telemetry = AddLabel(right, "Telemetría...", 16, FontStyle.Normal, Color.white, 0f);
        telemetry.alignment = TextAnchor.UpperLeft;

        RectTransform top = MakePanel(canvasObj.transform, "PanelTarea", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -20f), 520f, new Color(0.08f, 0.1f, 0.13f, 0.88f));
        Text task = AddLabel(top, "Tarea", 20, FontStyle.Bold, new Color(1f, 0.9f, 0.3f), 0f);
        task.alignment = TextAnchor.MiddleCenter;

        SetRef(ui, "armManager", manager);
        SetRef(ui, "cubeManager", cubes);
        SetRef(ui, "targetZone", zone);
        SetRef(ui, "sliderBase", sBase);
        SetRef(ui, "sliderShoulder", sShoulder);
        SetRef(ui, "sliderElbow", sElbow);
        SetRef(ui, "sliderGripper", sGripper);
        SetRef(ui, "btnToggleGripper", bToggle);
        SetRef(ui, "btnResetArm", bArm);
        SetRef(ui, "btnResetCubes", bCubes);
        SetRef(ui, "telemetryText", telemetry);
        SetRef(ui, "taskStatusText", task);
    }

    // ------------------------------------------------------------------ HELPERS

    private static float FindFloorY()
    {
        GameObject piso = GameObject.Find("piso");
        if (piso == null) piso = GameObject.Find("Piso");
        if (piso != null)
        {
            Collider c = piso.GetComponent<Collider>();
            return c != null ? c.bounds.max.y : piso.transform.position.y;
        }
        return 0.606f;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = FindDeepChild(parent.root, name);
            if (t != null) t.SetParent(parent, false);
        }
        if (t != null) return t.gameObject;

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StripDirectMeshIfHasChildren(GameObject go)
    {
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null) Object.DestroyImmediate(mf);
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null) Object.DestroyImmediate(mr);
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    private static void ResetLinkTransform(Transform t, Vector3 localPos)
    {
        t.localPosition = localPos;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    private static GameObject EnsureChildVisual(Transform parent, string visualName, PrimitiveType type,
        Vector3 localPos, Vector3 localScale, Material mat, PhysicMaterial physMat, bool keepCollider)
    {
        Transform existing = parent.Find(visualName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        GameObject go = GameObject.CreatePrimitive(type);
        go.name = visualName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        Collider col = go.GetComponent<Collider>();
        if (!keepCollider)
        {
            if (col != null) Object.DestroyImmediate(col);
        }
        else if (col != null && physMat != null)
        {
            col.sharedMaterial = physMat;
        }
        return go;
    }

    private static ArticulationBody ConfigureLinkJoint(GameObject link, ArticulationJointType type, Quaternion axis, float mass)
    {
        ArticulationBody ab = GetOrAdd<ArticulationBody>(link);
        ab.jointType = type;
        ab.mass = mass;
        ab.anchorPosition = Vector3.zero;
        ab.anchorRotation = axis;
        ab.parentAnchorPosition = link.transform.localPosition;
        ab.parentAnchorRotation = axis;
        ab.matchAnchors = true;
        return ab;
    }

    private static RobotJointController ConfigureController(GameObject link, string jointName,
        float stiffness, float damping, float forceLimit, float lower, float upper, float smoothSpeed)
    {
        RobotJointController c = GetOrAdd<RobotJointController>(link);
        SetString(c, "jointName", jointName);
        SetFloat(c, "stiffness", stiffness);
        SetFloat(c, "damping", damping);
        SetFloat(c, "forceLimit", forceLimit);
        SetFloat(c, "lowerLimit", lower);
        SetFloat(c, "upperLimit", upper);
        SetFloat(c, "smoothSpeed", smoothSpeed);
        SetFloat(c, "targetValue", 0f);
        c.ConfigureDrive();
        return c;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static DefaultControls.Resources UIResources()
    {
        return new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
    }

    private static RectTransform MakePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 pos, float width, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(width, 0f);

        VerticalLayoutGroup v = go.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 12, 12);
        v.spacing = 6f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        ContentSizeFitter f = go.GetComponent<ContentSizeFitter>();
        f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    private static Text AddLabel(Transform parent, string text, int size, FontStyle style, Color color, float height)
    {
        GameObject go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (height > 0f)
        {
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }
        return t;
    }

    private static Slider AddSlider(Transform parent, DefaultControls.Resources res, string label)
    {
        AddLabel(parent, label, 16, FontStyle.Bold, new Color(0.9f, 0.9f, 0.9f), 22f);
        GameObject s = DefaultControls.CreateSlider(res);
        s.transform.SetParent(parent, false);
        LayoutElement le = s.AddComponent<LayoutElement>();
        le.preferredHeight = 24f;
        return s.GetComponent<Slider>();
    }

    private static Button AddButton(Transform parent, DefaultControls.Resources res, string label, Color color)
    {
        GameObject b = DefaultControls.CreateButton(res);
        b.transform.SetParent(parent, false);
        b.GetComponent<Image>().color = color;
        LayoutElement le = b.AddComponent<LayoutElement>();
        le.preferredHeight = 38f;
        Text t = b.GetComponentInChildren<Text>();
        t.text = label;
        t.fontSize = 16;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        return b.GetComponent<Button>();
    }

    private static Material LoadOrCreateMaterial(string name, Color color)
    {
        string path = ASSET_DIR + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static PhysicMaterial LoadOrCreatePhysicMaterial()
    {
        string path = ASSET_DIR + "/Friccion agarre.physicMaterial";
        PhysicMaterial pm = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if (pm != null) return pm;
        pm = new PhysicMaterial("Friccion agarre")
        {
            staticFriction = 1.2f,
            dynamicFriction = 1.0f,
            frictionCombine = PhysicMaterialCombine.Maximum,
            bounciness = 0f,
            bounceCombine = PhysicMaterialCombine.Minimum
        };
        AssetDatabase.CreateAsset(pm, path);
        return pm;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
