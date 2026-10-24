using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controlador de la Interfaz de Usuario (UI) para el simulador de ArticulationBody.
/// Vincula los 4 Sliders de control a las articulaciones, gestiona los botones de acción
/// y actualiza en tiempo real el panel de telemetría (Target vs Real por joint) requerido por la consigna.
/// </summary>
public class RobotUIController : MonoBehaviour
{
    [Header("Referencias a Sistemas")]
    [SerializeField] private RobotArmManager armManager;
    [SerializeField] private CubeManager cubeManager;
    [SerializeField] private TaskTargetZone targetZone;

    [Header("Sliders de Entrada (4 requeridos)")]
    [Tooltip("Slider 1: Rotación de la Base / Torreta (Yaw).")]
    [SerializeField] private Slider sliderBase;

    [Tooltip("Slider 2: Elevación del Brazo / Pluma (Pitch).")]
    [SerializeField] private Slider sliderShoulder;

    [Tooltip("Slider 3: Flexión del Antebrazo / Codo (Pitch).")]
    [SerializeField] private Slider sliderElbow;

    [Tooltip("Slider 4: Apertura y cierre de la Garra (Prismático).")]
    [SerializeField] private Slider sliderGripper;

    [Header("Botones de Acción (2 requeridos + extras)")]
    [Tooltip("Botón 1: Alternar Garra (Abrir / Cerrar rápido).")]
    [SerializeField] private Button btnToggleGripper;

    [Tooltip("Botón 2: Restablecer postura del brazo (Home).")]
    [SerializeField] private Button btnResetArm;

    [Tooltip("Botón 3 (Recomendado): Restablecer cubos a su posición inicial.")]
    [SerializeField] private Button btnResetCubes;

    [Header("Panel de Telemetría en Pantalla")]
    [Tooltip("Texto para mostrar la lectura de estado (Target vs Real) por joint.")]
    [SerializeField] private Text telemetryText;

    [Tooltip("Texto alternativo si se usa TextMeshPro.")]
    [SerializeField] private TMPro.TMP_Text telemetryTMPText;

    [Tooltip("Texto de estado de la tarea de traslado/apilado.")]
    [SerializeField] private Text taskStatusText;

    [Tooltip("Texto TMP de estado de la tarea.")]
    [SerializeField] private TMPro.TMP_Text taskStatusTMPText;

    // Flag para evitar loops infinitos de actualización UI <-> Arm
    private bool isUpdatingSlidersFromCode = false;
    private readonly StringBuilder sb = new StringBuilder();

    private void Start()
    {
        InitializeSliders();
        InitializeButtons();
    }

    private void InitializeSliders()
    {
        if (armManager == null) return;

        // Configurar rangos de los sliders según los límites de los joints
        if (sliderBase != null && armManager.BaseJoint != null)
        {
            sliderBase.minValue = armManager.BaseJoint.LowerLimit;
            sliderBase.maxValue = armManager.BaseJoint.UpperLimit;
            sliderBase.value = armManager.BaseJoint.TargetValue;
            sliderBase.onValueChanged.AddListener(OnBaseSliderChanged);
        }

        if (sliderShoulder != null && armManager.ShoulderJoint != null)
        {
            sliderShoulder.minValue = armManager.ShoulderJoint.LowerLimit;
            sliderShoulder.maxValue = armManager.ShoulderJoint.UpperLimit;
            sliderShoulder.value = armManager.ShoulderJoint.TargetValue;
            sliderShoulder.onValueChanged.AddListener(OnShoulderSliderChanged);
        }

        if (sliderElbow != null && armManager.ElbowJoint != null)
        {
            sliderElbow.minValue = armManager.ElbowJoint.LowerLimit;
            sliderElbow.maxValue = armManager.ElbowJoint.UpperLimit;
            sliderElbow.value = armManager.ElbowJoint.TargetValue;
            sliderElbow.onValueChanged.AddListener(OnElbowSliderChanged);
        }

        if (sliderGripper != null)
        {
            sliderGripper.minValue = 0f;
            sliderGripper.maxValue = 1f;
            sliderGripper.value = armManager.CurrentGripperNormalized;
            sliderGripper.onValueChanged.AddListener(OnGripperSliderChanged);
        }
    }

    private void InitializeButtons()
    {
        if (btnToggleGripper != null)
        {
            btnToggleGripper.onClick.AddListener(OnToggleGripperClicked);
        }

        if (btnResetArm != null)
        {
            btnResetArm.onClick.AddListener(OnResetArmClicked);
        }

        if (btnResetCubes != null)
        {
            btnResetCubes.onClick.AddListener(OnResetCubesClicked);
        }
    }

    #region Callbacks de Sliders
    private void OnBaseSliderChanged(float val)
    {
        if (isUpdatingSlidersFromCode || armManager == null) return;
        armManager.SetBaseTarget(val);
    }

    private void OnShoulderSliderChanged(float val)
    {
        if (isUpdatingSlidersFromCode || armManager == null) return;
        armManager.SetShoulderTarget(val);
    }

    private void OnElbowSliderChanged(float val)
    {
        if (isUpdatingSlidersFromCode || armManager == null) return;
        armManager.SetElbowTarget(val);
    }

    private void OnGripperSliderChanged(float val)
    {
        if (isUpdatingSlidersFromCode || armManager == null) return;
        armManager.SetGripperTarget(val);
    }
    #endregion

    #region Callbacks de Botones
    private void OnToggleGripperClicked()
    {
        if (armManager == null) return;
        armManager.ToggleGripper();
        SyncSlidersWithArm();
    }

    private void OnResetArmClicked()
    {
        if (armManager == null) return;
        armManager.ResetArmPositions();
        SyncSlidersWithArm();
    }

    private void OnResetCubesClicked()
    {
        if (cubeManager != null)
        {
            cubeManager.ResetAllCubes();
        }
        if (targetZone != null)
        {
            targetZone.ResetZone();
        }
    }
    #endregion

    /// <summary>
    /// Sincroniza visualmente los Sliders cuando el brazo cambia de postura por código (ej: Reset).
    /// </summary>
    public void SyncSlidersWithArm()
    {
        if (armManager == null) return;
        isUpdatingSlidersFromCode = true;

        if (sliderBase != null && armManager.BaseJoint != null)
            sliderBase.value = armManager.BaseJoint.TargetValue;

        if (sliderShoulder != null && armManager.ShoulderJoint != null)
            sliderShoulder.value = armManager.ShoulderJoint.TargetValue;

        if (sliderElbow != null && armManager.ElbowJoint != null)
            sliderElbow.value = armManager.ElbowJoint.TargetValue;

        if (sliderGripper != null)
            sliderGripper.value = armManager.CurrentGripperNormalized;

        isUpdatingSlidersFromCode = false;
    }

    private void Update()
    {
        UpdateTelemetryDisplay();
        UpdateTaskStatusDisplay();
    }

    /// <summary>
    /// Actualiza el texto de lectura en pantalla:
    /// Muestra Target, Valor Real y Error de seguimiento por cada joint.
    /// Requisito mínimo del TP: "Lectura de estado: Target y valor real por joint en pantalla".
    /// </summary>
    private void UpdateTelemetryDisplay()
    {
        if (telemetryText == null && telemetryTMPText == null) return;
        if (armManager == null) return;

        sb.Clear();
        sb.AppendLine("=== TELEMETRÍA DEL ROBOT (ArticulationBody) ===");
        sb.AppendLine("Articulación: Target | Real | Error");
        sb.AppendLine("--------------------------------------------");

        var joints = armManager.GetAllControlledJoints();
        foreach (var joint in joints)
        {
            string unit = (joint.JointType == ArticulationJointType.PrismaticJoint) ? "m" : "°";
            string fmt = (joint.JointType == ArticulationJointType.PrismaticJoint) ? "F3" : "F1";
            sb.AppendLine(string.Format("{0}: {1}{4} | {2}{4} | {3}{4}",
                joint.JointName,
                joint.TargetValue.ToString(fmt),
                joint.CurrentPosition.ToString(fmt),
                joint.TrackingError.ToString(fmt),
                unit));
        }

        // Joint Pasivo
        float passiveAngle = armManager.GetPassivePendulumAngle();
        sb.AppendLine("--------------------------------------------");
        sb.AppendLine(string.Format("Péndulo Pasivo (Spherical): Inclinación = {0:F1}°", passiveAngle));
        sb.AppendLine(string.Format("Estado Pinza: {0} ({1:P0})",
            armManager.IsGripperOpen ? "ABIERTA" : "CERRADA",
            armManager.CurrentGripperNormalized));

        string result = sb.ToString();

        if (telemetryText != null)
            telemetryText.text = result;

        if (telemetryTMPText != null)
            telemetryTMPText.text = result;
    }

    private void UpdateTaskStatusDisplay()
    {
        if (taskStatusText == null && taskStatusTMPText == null) return;
        if (targetZone == null) return;

        string msg;
        if (targetZone.IsTaskCompleted)
        {
            msg = $"<color=green>¡MANIOBRA COMPLETADA!</color>\nCubos en zona: {targetZone.CubesInZoneCount} / {targetZone.TargetCubeCount}";
        }
        else
        {
            msg = $"Tarea: Trasladar cubos a la plataforma\nProgreso: {targetZone.CubesInZoneCount} / {targetZone.TargetCubeCount}";
        }

        if (taskStatusText != null)
            taskStatusText.text = msg;

        if (taskStatusTMPText != null)
            taskStatusTMPText.text = msg;
    }
}
