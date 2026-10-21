#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Herramienta de Editor para construir y configurar automáticamente la escena del simulador.
/// Crea o completa la jerarquía del brazo articulado cumpliendo todos los requisitos de la consigna:
/// - 8 Articulation Bodies (Base fija, Torreta Revolute, Brazo Revolute, Antebrazo Revolute, Soporte Fixed, Dedos Prismatic, Péndulo Spherical pasivo).
/// - Asignación de materiales (Color base garra, Color piso).
/// - Parámetros de Drive (stiffness, damping, forceLimit) equilibrados y estables.
/// - Interfaz de Usuario completa (4 Sliders, 3 Botones, Panel de Telemetría).
/// - 3 Cubos con Rigidbody y plataforma de descarga para la maniobra.
/// </summary>
public static class RobotArmSceneBuilder
{
    private const string MAT_GARRA_PATH = "Assets/Prefabs/Color base garra.mat";
    private const string MAT_PISO_PATH = "Assets/Prefabs/Color piso.mat";

    [MenuItem("Simulador/Construir y Configurar Escena Completa")]
    public static void BuildCompleteScene()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Construir Simulador ArticulationBody");
        int groupIndex = Undo.GetCurrentGroup();

        // 1. Cargar materiales existentes
        Material matGarra = AssetDatabase.LoadAssetAtPath<Material>(MAT_GARRA_PATH);
        Material matPiso = AssetDatabase.LoadAssetAtPath<Material>(MAT_PISO_PATH);

        if (matGarra == null)
        {
            matGarra = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.4f, 0.8f) };
        }
        if (matPiso == null)
        {
            matPiso = new Material(Shader.Find("Standard")) { color = new Color(0.3f, 0.3f, 0.3f) };
        }

        // Material físico de alta fricción para agarre firme
        PhysicMaterial highFrictionMat = new PhysicMaterial("FriccionRobusta")
        {
            staticFriction = 1.0f,
            dynamicFriction = 0.9f,
            frictionCombine = PhysicMaterialCombine.Maximum,
            bounciness = 0f,
            bounceCombine = PhysicMaterialCombine.Minimum
        };

        // 2. Configurar o Buscar Piso
        GameObject piso = GameObject.Find("piso");
        if (piso == null) piso = GameObject.Find("Piso");
        if (piso == null)
        {
            piso = GameObject.CreatePrimitive(PrimitiveType.Plane);
            piso.name = "piso";
            piso.transform.position = Vector3.zero;
            piso.transform.localScale = new Vector3(2f, 1f, 2f);
            Undo.RegisterCreatedObjectUndo(piso, "Crear Piso");
        }
        piso.GetComponent<MeshRenderer>().sharedMaterial = matPiso;
        var pisoCol = piso.GetComponent<Collider>();
        if (pisoCol != null) pisoCol.sharedMaterial = highFrictionMat;

        // 3. Configurar Base Raíz (Immovable)
        GameObject baseObj = GameObject.Find("base");
        if (baseObj == null) baseObj = GameObject.Find("Base");
        if (baseObj == null)
        {
            baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "base";
            baseObj.transform.position = new Vector3(0f, 0.25f, 0f);
            baseObj.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
            Undo.RegisterCreatedObjectUndo(baseObj, "Crear Base");
        }
        else
        {
            baseObj.transform.position = new Vector3(0f, 0.25f, 0f);
            baseObj.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
        }
        baseObj.GetComponent<MeshRenderer>().sharedMaterial = matGarra;

        var baseAb = GetOrAddComponent<ArticulationBody>(baseObj);
        baseAb.immovable = true;
        baseAb.mass = 20f;
        baseAb.linearDamping = 0.05f;
        baseAb.angularDamping = 0.05f;

        var armManager = GetOrAddComponent<RobotArmManager>(baseObj);

        // 4. Configurar SobreBase / Torreta (Revolute Joint - Eje Y)
        GameObject sobreBase = GameObject.Find("sobre base");
        if (sobreBase == null) sobreBase = GameObject.Find("SobreBase") ?? GameObject.Find("sobrebase");
        if (sobreBase == null)
        {
            sobreBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sobreBase.name = "sobre base";
            Undo.RegisterCreatedObjectUndo(sobreBase, "Crear SobreBase");
        }
        sobreBase.transform.parent = baseObj.transform;
        sobreBase.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        sobreBase.transform.localRotation = Quaternion.identity;
        sobreBase.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
        sobreBase.GetComponent<MeshRenderer>().sharedMaterial = matGarra;

        var turretAb = GetOrAddComponent<ArticulationBody>(sobreBase);
        turretAb.jointType = ArticulationJointType.RevoluteJoint;
        turretAb.anchorRotation = Quaternion.Euler(0f, 90f, 0f); // Eje Y para giro horizontal
        turretAb.parentAnchorRotation = Quaternion.identity;
        turretAb.mass = 8f;
        var turretCtrl = GetOrAddComponent<RobotJointController>(sobreBase);
        SetPrivateField(turretCtrl, "jointName", "Base (Yaw)");
        SetPrivateField(turretCtrl, "stiffness", 12000f);
        SetPrivateField(turretCtrl, "damping", 1200f);
        SetPrivateField(turretCtrl, "forceLimit", 15000f);
        SetPrivateField(turretCtrl, "lowerLimit", -150f);
        SetPrivateField(turretCtrl, "upperLimit", 150f);
        SetPrivateField(turretCtrl, "smoothSpeed", 90f);
        turretCtrl.ConfigureDrive();

        // 5. Configurar Brazo / Pluma (Revolute Joint - Eje X)
        Transform brazoT = sobreBase.transform.Find("Brazo");
        GameObject brazo = brazoT != null ? brazoT.gameObject : null;
        if (brazo == null)
        {
            brazo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brazo.name = "Brazo";
            brazo.transform.parent = sobreBase.transform;
            Undo.RegisterCreatedObjectUndo(brazo, "Crear Brazo");
        }
        brazo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        brazo.transform.localRotation = Quaternion.identity;
        brazo.transform.localScale = new Vector3(0.25f, 1.2f, 0.25f);
        brazo.GetComponent<MeshRenderer>().sharedMaterial = matGarra;

        var brazoAb = GetOrAddComponent<ArticulationBody>(brazo);
        brazoAb.jointType = ArticulationJointType.RevoluteJoint;
        brazoAb.anchorRotation = Quaternion.identity; // Eje X por defecto en ArticulationBody
        brazoAb.parentAnchorRotation = Quaternion.identity;
        brazoAb.anchorPosition = new Vector3(0f, -0.6f, 0f); // Pivote en la base del brazo
        brazoAb.parentAnchorPosition = new Vector3(0f, 0.15f, 0f);
        brazoAb.mass = 6f;
        var brazoCtrl = GetOrAddComponent<RobotJointController>(brazo);
        SetPrivateField(brazoCtrl, "jointName", "Brazo (Pitch)");
        SetPrivateField(brazoCtrl, "stiffness", 15000f);
        SetPrivateField(brazoCtrl, "damping", 1500f);
        SetPrivateField(brazoCtrl, "forceLimit", 20000f);
        SetPrivateField(brazoCtrl, "lowerLimit", -60f);
        SetPrivateField(brazoCtrl, "upperLimit", 60f);
        SetPrivateField(brazoCtrl, "smoothSpeed", 60f);
        brazoCtrl.ConfigureDrive();

        // 6. Configurar Antebrazo / Codo (Revolute Joint - Eje X)
        Transform antebrazoT = brazo.transform.Find("Antebrazo");
        GameObject antebrazo = antebrazoT != null ? antebrazoT.gameObject : null;
        if (antebrazo == null)
        {
            antebrazo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            antebrazo.name = "Antebrazo";
            antebrazo.transform.parent = brazo.transform;
            Undo.RegisterCreatedObjectUndo(antebrazo, "Crear Antebrazo");
        }
        antebrazo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        antebrazo.transform.localRotation = Quaternion.identity;
        antebrazo.transform.localScale = new Vector3(0.2f, 1.0f, 0.2f);
        antebrazo.GetComponent<MeshRenderer>().sharedMaterial = matGarra;

        var antebrazoAb = GetOrAddComponent<ArticulationBody>(antebrazo);
        antebrazoAb.jointType = ArticulationJointType.RevoluteJoint;
        antebrazoAb.anchorRotation = Quaternion.identity;
        antebrazoAb.anchorPosition = new Vector3(0f, -0.5f, 0f); // Pivote en el codo
        antebrazoAb.parentAnchorPosition = new Vector3(0f, 0.6f, 0f);
        antebrazoAb.mass = 4f;
        var antebrazoCtrl = GetOrAddComponent<RobotJointController>(antebrazo);
        SetPrivateField(antebrazoCtrl, "jointName", "Antebrazo");
        SetPrivateField(antebrazoCtrl, "stiffness", 10000f);
        SetPrivateField(antebrazoCtrl, "damping", 1000f);
        SetPrivateField(antebrazoCtrl, "forceLimit", 12000f);
        SetPrivateField(antebrazoCtrl, "lowerLimit", -90f);
        SetPrivateField(antebrazoCtrl, "upperLimit", 90f);
        SetPrivateField(antebrazoCtrl, "smoothSpeed", 75f);
        antebrazoCtrl.ConfigureDrive();

        // 7. Configurar Muñeca / Soporte Garra (Fixed Joint)
        Transform soporteGarraT = antebrazo.transform.Find("SoporteGarra");
        GameObject soporteGarra = soporteGarraT != null ? soporteGarraT.gameObject : null;
        if (soporteGarra == null)
        {
            soporteGarra = GameObject.CreatePrimitive(PrimitiveType.Cube);
            soporteGarra.name = "SoporteGarra";
            soporteGarra.transform.parent = antebrazo.transform;
            Undo.RegisterCreatedObjectUndo(soporteGarra, "Crear Soporte Garra");
        }
        soporteGarra.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        soporteGarra.transform.localRotation = Quaternion.identity;
        soporteGarra.transform.localScale = new Vector3(0.25f, 0.1f, 0.35f);
        soporteGarra.GetComponent<MeshRenderer>().sharedMaterial = matGarra;

        var soporteAb = GetOrAddComponent<ArticulationBody>(soporteGarra);
        soporteAb.jointType = ArticulationJointType.FixedJoint;
        soporteAb.mass = 1.5f;

        // 8. Dedo Izquierdo (Prismatic Joint - Desplazamiento Z)
        Transform dedoIzqT = soporteGarra.transform.Find("DedoIzquierdo");
        GameObject dedoIzq = dedoIzqT != null ? dedoIzqT.gameObject : null;
        if (dedoIzq == null)
        {
            dedoIzq = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dedoIzq.name = "DedoIzquierdo";
            dedoIzq.transform.parent = soporteGarra.transform;
            Undo.RegisterCreatedObjectUndo(dedoIzq, "Crear Dedo Izquierdo");
        }
        dedoIzq.transform.localPosition = new Vector3(0f, 0.15f, 0.1f);
        dedoIzq.transform.localRotation = Quaternion.identity;
        dedoIzq.transform.localScale = new Vector3(0.08f, 0.25f, 0.05f);
        dedoIzq.GetComponent<MeshRenderer>().sharedMaterial = matGarra;
        var colIzq = dedoIzq.GetComponent<Collider>();
        if (colIzq != null) colIzq.sharedMaterial = highFrictionMat;

        var dedoIzqAb = GetOrAddComponent<ArticulationBody>(dedoIzq);
        dedoIzqAb.jointType = ArticulationJointType.PrismaticJoint;
        dedoIzqAb.anchorRotation = Quaternion.Euler(0f, 90f, 0f); // Dirección Z local
        dedoIzqAb.mass = 0.5f;
        var dedoIzqCtrl = GetOrAddComponent<RobotJointController>(dedoIzq);
        SetPrivateField(dedoIzqCtrl, "jointName", "Dedo Izq (Prism)");
        SetPrivateField(dedoIzqCtrl, "stiffness", 4000f);
        SetPrivateField(dedoIzqCtrl, "damping", 400f);
        SetPrivateField(dedoIzqCtrl, "forceLimit", 5000f);
        SetPrivateField(dedoIzqCtrl, "lowerLimit", 0f);
        SetPrivateField(dedoIzqCtrl, "upperLimit", 0.06f);
        SetPrivateField(dedoIzqCtrl, "smoothSpeed", 0.2f);
        dedoIzqCtrl.ConfigureDrive();

        // 9. Dedo Derecho (Prismatic Joint - Desplazamiento -Z)
        Transform dedoDerT = soporteGarra.transform.Find("DedoDerecho");
        GameObject dedoDer = dedoDerT != null ? dedoDerT.gameObject : null;
        if (dedoDer == null)
        {
            dedoDer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dedoDer.name = "DedoDerecho";
            dedoDer.transform.parent = soporteGarra.transform;
            Undo.RegisterCreatedObjectUndo(dedoDer, "Crear Dedo Derecho");
        }
        dedoDer.transform.localPosition = new Vector3(0f, 0.15f, -0.1f);
        dedoDer.transform.localRotation = Quaternion.identity;
        dedoDer.transform.localScale = new Vector3(0.08f, 0.25f, 0.05f);
        dedoDer.GetComponent<MeshRenderer>().sharedMaterial = matGarra;
        var colDer = dedoDer.GetComponent<Collider>();
        if (colDer != null) colDer.sharedMaterial = highFrictionMat;

        var dedoDerAb = GetOrAddComponent<ArticulationBody>(dedoDer);
        dedoDerAb.jointType = ArticulationJointType.PrismaticJoint;
        dedoDerAb.anchorRotation = Quaternion.Euler(0f, 90f, 0f);
        dedoDerAb.mass = 0.5f;
        var dedoDerCtrl = GetOrAddComponent<RobotJointController>(dedoDer);
        SetPrivateField(dedoDerCtrl, "jointName", "Dedo Der (Prism)");
        SetPrivateField(dedoDerCtrl, "stiffness", 4000f);
        SetPrivateField(dedoDerCtrl, "damping", 400f);
        SetPrivateField(dedoDerCtrl, "forceLimit", 5000f);
        SetPrivateField(dedoDerCtrl, "lowerLimit", -0.06f);
        SetPrivateField(dedoDerCtrl, "upperLimit", 0f);
        SetPrivateField(dedoDerCtrl, "smoothSpeed", 0.2f);
        dedoDerCtrl.ConfigureDrive();

        // 10. Joint Pasivo (Péndulo Esférico - Spherical Joint)
        Transform penduloT = soporteGarra.transform.Find("PenduloPasivo");
        GameObject pendulo = penduloT != null ? penduloT.gameObject : null;
        if (pendulo == null)
        {
            pendulo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pendulo.name = "PenduloPasivo";
            pendulo.transform.parent = soporteGarra.transform;
            Undo.RegisterCreatedObjectUndo(pendulo, "Crear Pendulo Pasivo");
        }
        pendulo.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        pendulo.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        pendulo.GetComponent<MeshRenderer>().sharedMaterial = matPiso;

        var penduloAb = GetOrAddComponent<ArticulationBody>(pendulo);
        penduloAb.jointType = ArticulationJointType.SphericalJoint;
        penduloAb.mass = 0.3f;
        penduloAb.linearDamping = 0.2f;
        penduloAb.angularDamping = 0.2f;

        // Drive sin rigidez (stiffness = 0) para que sea 100% pasivo movido por gravedad e inercia
        ArticulationDrive passiveDrive = penduloAb.xDrive;
        passiveDrive.stiffness = 0f;
        passiveDrive.damping = 0.05f;
        passiveDrive.forceLimit = 0f;
        penduloAb.xDrive = passiveDrive;
        penduloAb.yDrive = passiveDrive;
        penduloAb.zDrive = passiveDrive;

        // Vincular referencias en RobotArmManager
        SetPrivateField(armManager, "baseJoint", turretCtrl);
        SetPrivateField(armManager, "shoulderJoint", brazoCtrl);
        SetPrivateField(armManager, "elbowJoint", antebrazoCtrl);
        SetPrivateField(armManager, "leftFingerJoint", dedoIzqCtrl);
        SetPrivateField(armManager, "rightFingerJoint", dedoDerCtrl);
        SetPrivateField(armManager, "passivePendulum", penduloAb);
        SetPrivateField(armManager, "maxGripperStroke", 0.05f);

        // 11. Crear Zona de Descarga / Meta
        GameObject targetZoneObj = GameObject.Find("ZonaDescarga");
        if (targetZoneObj == null)
        {
            targetZoneObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetZoneObj.name = "ZonaDescarga";
            targetZoneObj.transform.position = new Vector3(-1.2f, 0.02f, 0f);
            targetZoneObj.transform.localScale = new Vector3(0.8f, 0.04f, 0.8f);
            Undo.RegisterCreatedObjectUndo(targetZoneObj, "Crear Zona Descarga");
        }
        var targetRenderer = targetZoneObj.GetComponent<MeshRenderer>();
        var matZone = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.8f, 0.3f, 0.7f) };
        targetRenderer.sharedMaterial = matZone;

        var targetCol = targetZoneObj.GetComponent<BoxCollider>();
        targetCol.isTrigger = true;
        targetCol.size = new Vector3(1f, 10f, 1f); // Trigger vertical para capturar cubos
        targetCol.center = new Vector3(0f, 5f, 0f);

        var taskTargetZone = GetOrAddComponent<TaskTargetZone>(targetZoneObj);

        // 12. Crear Cubos Interactuables (Rigidbodies)
        GameObject cubesRoot = GameObject.Find("CubosInteractuables");
        if (cubesRoot == null)
        {
            cubesRoot = new GameObject("CubosInteractuables");
            Undo.RegisterCreatedObjectUndo(cubesRoot, "Crear Root Cubos");
        }
        var cubeManager = GetOrAddComponent<CubeManager>(cubesRoot);

        Color[] cubeColors = new Color[]
        {
            new Color(0.9f, 0.2f, 0.2f), // Rojo
            new Color(0.9f, 0.8f, 0.1f), // Amarillo
            new Color(0.1f, 0.7f, 0.9f)  // Cyan
        };

        Vector3[] cubePositions = new Vector3[]
        {
            new Vector3(0.9f, 0.15f, -0.3f),
            new Vector3(1.1f, 0.15f, 0f),
            new Vector3(0.9f, 0.15f, 0.3f)
        };

        for (int i = 0; i < 3; i++)
        {
            string cubeName = $"Cubo_Carga_{i + 1}";
            Transform cT = cubesRoot.transform.Find(cubeName);
            GameObject cube = cT != null ? cT.gameObject : null;
            if (cube == null)
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = cubeName;
                cube.transform.parent = cubesRoot.transform;
                Undo.RegisterCreatedObjectUndo(cube, "Crear Cubo");
            }

            cube.transform.position = cubePositions[i];
            cube.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);

            var matCube = new Material(Shader.Find("Standard")) { color = cubeColors[i] };
            cube.GetComponent<MeshRenderer>().sharedMaterial = matCube;

            var col = cube.GetComponent<Collider>();
            col.sharedMaterial = highFrictionMat;

            var rb = GetOrAddComponent<Rigidbody>(cube);
            rb.mass = 0.4f;
            rb.drag = 0.5f;
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            cubeManager.RegisterCube(rb);
        }

        // 13. Crear Canvas UI Completo
        CreateCanvasUI(armManager, cubeManager, taskTargetZone);

        // 14. Ajustar Cámara Principal
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = new Vector3(0f, 2.2f, -3.2f);
            mainCam.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
        }

        Undo.CollapseUndoOperations(groupIndex);
        EditorUtility.SetDirty(baseObj);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
        );

        Debug.Log("<color=green><b>[Simulador]</b> ¡Escena completa construida con éxito! Cumple todos los requisitos de ArticulationBody.</color>");
    }

    private static void CreateCanvasUI(RobotArmManager armManager, CubeManager cubeManager, TaskTargetZone targetZone)
    {
        // Verificar EventSystem
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(es, "Crear EventSystem");
        }

        GameObject canvasObj = GameObject.Find("SimuladorCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("SimuladorCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Crear Canvas");
        }

        Canvas canvas = GetOrAddComponent<Canvas>(canvasObj);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = GetOrAddComponent<CanvasScaler>(canvasObj);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        GetOrAddComponent<GraphicRaycaster>(canvasObj);

        var uiController = GetOrAddComponent<RobotUIController>(canvasObj);
        SetPrivateField(uiController, "armManager", armManager);
        SetPrivateField(uiController, "cubeManager", cubeManager);
        SetPrivateField(uiController, "targetZone", targetZone);

        // Limpiar hijos previos para reconstrucción limpia
        while (canvasObj.transform.childCount > 0)
        {
            Object.DestroyImmediate(canvasObj.transform.GetChild(0).gameObject);
        }

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // --- Panel Izquierdo: Controles (Sliders + Botones) ---
        GameObject panelControles = CreateUIPanel(canvasObj.transform, "PanelControles",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(380f, 620f));
        panelControles.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.15f, 0.9f);

        CreateUIText(panelControles.transform, "TituloControles", "CONTROL DE ARTICULACIONES",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(340f, 30f), 18, FontStyle.Bold, Color.white, defaultFont);

        // Sliders
        Slider sBase = CreateLabeledSlider(panelControles.transform, "SliderBase", "1. Base (Yaw) [-150° a 150°]", -70f, defaultFont);
        Slider sShoulder = CreateLabeledSlider(panelControles.transform, "SliderBrazo", "2. Brazo (Pitch) [-60° a 60°]", -150f, defaultFont);
        Slider sElbow = CreateLabeledSlider(panelControles.transform, "SliderAntebrazo", "3. Antebrazo (Pitch) [-90° a 90°]", -230f, defaultFont);
        Slider sGripper = CreateLabeledSlider(panelControles.transform, "SliderGarra", "4. Garra (Pinzas) [0 a 1]", -310f, defaultFont);

        SetPrivateField(uiController, "sliderBase", sBase);
        SetPrivateField(uiController, "sliderShoulder", sShoulder);
        SetPrivateField(uiController, "sliderElbow", sElbow);
        SetPrivateField(uiController, "sliderGripper", sGripper);

        // Botones
        Button btnToggle = CreateUIButton(panelControles.transform, "BtnToggleGarra", "Alternar Garra (Abrir/Cerrar)", -390f, new Color(0.2f, 0.6f, 0.9f), defaultFont);
        Button btnResetArm = CreateUIButton(panelControles.transform, "BtnResetArm", "Restablecer Brazo (Home)", -450f, new Color(0.85f, 0.55f, 0.15f), defaultFont);
        Button btnResetCubes = CreateUIButton(panelControles.transform, "BtnResetCubes", "Reiniciar Cubos / Tarea", -510f, new Color(0.8f, 0.25f, 0.25f), defaultFont);

        SetPrivateField(uiController, "btnToggleGripper", btnToggle);
        SetPrivateField(uiController, "btnResetArm", btnResetArm);
        SetPrivateField(uiController, "btnResetCubes", btnResetCubes);

        // --- Panel Derecho: Telemetría en Pantalla (Target vs Real) ---
        GameObject panelTelemetria = CreateUIPanel(canvasObj.transform, "PanelTelemetria",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(500f, 320f));
        panelTelemetria.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
        panelTelemetria.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.92f);

        Text txtTelemetria = CreateUIText(panelTelemetria.transform, "TxtTelemetria", "Cargando telemetría...",
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-30f, -30f), 14, FontStyle.Normal, Color.white, defaultFont);
        txtTelemetria.alignment = TextAnchor.UpperLeft;
        txtTelemetria.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? defaultFont;
        SetPrivateField(uiController, "telemetryText", txtTelemetria);

        // --- Panel Superior: Estado de la Tarea / Misión ---
        GameObject panelTarea = CreateUIPanel(canvasObj.transform, "PanelTarea",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(480f, 80f));
        panelTarea.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
        panelTarea.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.18f, 0.9f);

        Text txtTarea = CreateUIText(panelTarea.transform, "TxtTarea", "Tarea: Trasladar cubos a la plataforma de meta",
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-20f, -10f), 15, FontStyle.Bold, Color.yellow, defaultFont);
        txtTarea.alignment = TextAnchor.MiddleCenter;
        SetPrivateField(uiController, "taskStatusText", txtTarea);
    }

    #region Helpers para UI
    private static GameObject CreateUIPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return panel;
    }

    private static Text CreateUIText(Transform parent, string name, string content, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, int fontSize, FontStyle style, Color color, Font font)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        Text text = textObj.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return text;
    }

    private static Slider CreateLabeledSlider(Transform parent, string name, string labelText, float posY, Font font)
    {
        CreateUIText(parent, name + "_Label", labelText,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, posY), new Vector2(-40f, 20f), 13, FontStyle.Bold, new Color(0.9f, 0.9f, 0.9f), font);

        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);
        Slider slider = sliderObj.AddComponent<Slider>();

        RectTransform rt = sliderObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(0f, posY - 25f);
        rt.sizeDelta = new Vector2(-40f, 20f);

        // Fondo
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(sliderObj.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.sizeDelta = new Vector2(-20f, 0f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.6f, 1f);
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.sizeDelta = Vector2.zero;

        // Handle
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRt = handleArea.AddComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.sizeDelta = new Vector2(-20f, 0f);

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 0f);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private static Button CreateUIButton(Transform parent, string name, string labelText, float posY, Color bgColor, Font font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;
        Button btn = btnObj.AddComponent<Button>();

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(0f, posY);
        rt.sizeDelta = new Vector2(-40f, 40f);

        Text txt = CreateUIText(btnObj.transform, "Text", labelText,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 14, FontStyle.Bold, Color.white, font);
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
    #endregion
}
#endif
