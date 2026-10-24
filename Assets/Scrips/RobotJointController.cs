using UnityEngine;

/// <summary>
/// Controlador individual para un Joint de tipo ArticulationBody.
/// Gestiona la configuración del Drive (stiffness, damping, forceLimit),
/// el suavizado del objetivo (para evitar tirones en WebGL/Física)
/// y la lectura en tiempo real de posición, objetivo y error de seguimiento.
/// </summary>
[SelectionBase]
[RequireComponent(typeof(ArticulationBody))]
public class RobotJointController : MonoBehaviour
{
    [Header("Identificación")]
    [SerializeField] private string jointName = "Joint";

    [Header("Parámetros del Drive")]
    [Tooltip("Rigidez del resorte del motor (Stiffness).")]
    [SerializeField] private float stiffness = 10000f;

    [Tooltip("Amortiguación del motor (Damping).")]
    [SerializeField] private float damping = 1000f;

    [Tooltip("Límite de fuerza o torque que puede ejercer el joint.")]
    [SerializeField] private float forceLimit = 10000f;

    [Header("Límites de Movimiento")]
    [Tooltip("Límite inferior (grados para revolute, metros para prismatic).")]
    [SerializeField] private float lowerLimit = -90f;

    [Tooltip("Límite superior (grados para revolute, metros para prismatic).")]
    [SerializeField] private float upperLimit = 90f;

    [Header("Suavizado de Entrada")]
    [Tooltip("Si está activo, interpola suavemente hacia el target para evitar saltos violentos.")]
    [SerializeField] private bool useSmoothing = true;

    [Tooltip("Velocidad de aproximación al target (grados/s o m/s).")]
    [SerializeField] private float smoothSpeed = 120f;

    [Header("Objetivo Actual (Editable en Inspector o por Slider)")]
    [Tooltip("Objetivo actual del motor (grados para Revolute, metros para Prismatic).")]
    [SerializeField] private float targetValue = 0f;

    private ArticulationBody articulationBody;
    private float currentSmoothedTarget = 0f;

    // Propiedades públicas para telemetría y UI
    public string JointName => jointName;
    public float LowerLimit => lowerLimit;
    public float UpperLimit => upperLimit;
    public float TargetValue => targetValue;
    public float SmoothedTarget => currentSmoothedTarget;
    public ArticulationJointType JointType => articulationBody != null ? articulationBody.jointType : ArticulationJointType.FixedJoint;

    /// <summary>
    /// Retorna la posición real actual de la articulación:
    /// - Grados si es Revolute
    /// - Metros si es Prismatic
    /// </summary>
    public float CurrentPosition
    {
        get
        {
            if (articulationBody == null || articulationBody.jointPosition.dofCount == 0)
                return 0f;

            if (articulationBody.jointType == ArticulationJointType.RevoluteJoint)
            {
                return articulationBody.jointPosition[0] * Mathf.Rad2Deg;
            }
            else if (articulationBody.jointType == ArticulationJointType.PrismaticJoint)
            {
                return articulationBody.jointPosition[0];
            }
            return 0f;
        }
    }

    /// <summary>
    /// Error absoluto entre el objetivo deseado y la posición física real.
    /// </summary>
    public float TrackingError => Mathf.Abs(targetValue - CurrentPosition);

    private void Awake()
    {
        articulationBody = GetComponent<ArticulationBody>();
        ConfigureDrive();
    }

    /// <summary>
    /// Ajusta los límites inferior y superior del joint.
    /// </summary>
    public void ConfigureLimits(float min, float max)
    {
        lowerLimit = min;
        upperLimit = max;
    }

    /// <summary>
    /// Configura y aplica los parámetros del ArticulationDrive a la articulación.
    /// </summary>
    public void ConfigureDrive()
    {
        if (articulationBody == null) articulationBody = GetComponent<ArticulationBody>();
        if (articulationBody == null) return;

        if (articulationBody.jointType == ArticulationJointType.FixedJoint)
            return;

        if (articulationBody.jointType == ArticulationJointType.RevoluteJoint)
        {
            articulationBody.twistLock = ArticulationDofLock.LimitedMotion;
        }
        else if (articulationBody.jointType == ArticulationJointType.PrismaticJoint)
        {
            articulationBody.linearLockX = ArticulationDofLock.LimitedMotion;
            articulationBody.linearLockY = ArticulationDofLock.LockedMotion;
            articulationBody.linearLockZ = ArticulationDofLock.LockedMotion;

            // Sincronizar el ancla con la posición actual del dedo y asegurar eje horizontal Z (sin componente vertical)
            Quaternion horizontalZ = Quaternion.Euler(0f, -90f, 0f);
            articulationBody.anchorPosition = Vector3.zero;
            articulationBody.anchorRotation = horizontalZ;
            articulationBody.parentAnchorPosition = transform.localPosition;
            articulationBody.parentAnchorRotation = horizontalZ;
            articulationBody.matchAnchors = true;
        }

        targetValue = Mathf.Clamp(targetValue, lowerLimit, upperLimit);

        ArticulationDrive drive = articulationBody.xDrive;
        drive.lowerLimit = lowerLimit;
        drive.upperLimit = upperLimit;
        drive.stiffness = stiffness;
        drive.damping = damping;
        drive.forceLimit = forceLimit;
        drive.target = targetValue;

        articulationBody.xDrive = drive;
        currentSmoothedTarget = targetValue;
    }

    /// <summary>
    /// Establece un nuevo objetivo (Target) para la articulación.
    /// </summary>
    public void SetTarget(float newTarget)
    {
        targetValue = Mathf.Clamp(newTarget, lowerLimit, upperLimit);
        if (!useSmoothing)
        {
            currentSmoothedTarget = targetValue;
        }
    }

    /// <summary>
    /// Fuerza el target instantáneamente sin suavizado (útil para reset).
    /// </summary>
    public void ForceTarget(float newTarget)
    {
        targetValue = Mathf.Clamp(newTarget, lowerLimit, upperLimit);
        currentSmoothedTarget = targetValue;

        if (articulationBody != null && articulationBody.jointType != ArticulationJointType.FixedJoint)
        {
            ArticulationDrive drive = articulationBody.xDrive;
            drive.target = currentSmoothedTarget;
            articulationBody.xDrive = drive;
        }
    }

    private void FixedUpdate()
    {
        if (articulationBody == null || articulationBody.jointType == ArticulationJointType.FixedJoint)
            return;

        targetValue = Mathf.Clamp(targetValue, lowerLimit, upperLimit);

        if (useSmoothing)
        {
            currentSmoothedTarget = Mathf.MoveTowards(currentSmoothedTarget, targetValue, smoothSpeed * Time.fixedDeltaTime);
        }
        else
        {
            currentSmoothedTarget = targetValue;
        }

        ArticulationDrive drive = articulationBody.xDrive;
        drive.target = currentSmoothedTarget;
        articulationBody.xDrive = drive;
    }
}
