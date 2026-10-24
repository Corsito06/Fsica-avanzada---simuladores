using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Administrador principal del Brazo Robótico Articulado.
/// Coordina todos los joints del mecanismo, expone métodos de control para la UI,
/// administra la apertura/cierre sincronizado de la garra y la postura de reposo (Reset).
/// </summary>
[SelectionBase]
public class RobotArmManager : MonoBehaviour
{
    [Header("Joints Principales (Controlados)")]
    [Tooltip("Articulación de la base/torreta (Revolute - Giro horizontal).")]
    [SerializeField] private RobotJointController baseJoint;

    [Tooltip("Articulación del hombro/pluma (Revolute - Inclinación vertical).")]
    [SerializeField] private RobotJointController shoulderJoint;

    [Tooltip("Articulación del codo/antebrazo (Revolute - Flexión vertical).")]
    [SerializeField] private RobotJointController elbowJoint;

    [Header("Mecanismo de la Garra (Prismático)")]
    [Tooltip("Dedo izquierdo de la pinza (Prismatic).")]
    [SerializeField] private RobotJointController leftFingerJoint;

    [Tooltip("Dedo derecho de la pinza (Prismatic).")]
    [SerializeField] private RobotJointController rightFingerJoint;

    [Tooltip("Recorrido máximo de cierre de cada dedo en metros.")]
    [SerializeField] private float maxGripperStroke = 0.08f;

    [Header("Joint Pasivo (Gravedad / Inercia)")]
    [Tooltip("Elemento esférico pasivo sin motor que oscila libremente colgado de una soga.")]
    [SerializeField] private ArticulationBody passivePendulum;

    [Header("Posición de Reposo / Home")]
    [SerializeField] private float homeBaseAngle = 0f;
    [SerializeField] private float homeShoulderAngle = 20f;
    [SerializeField] private float homeElbowAngle = 25f;
    [SerializeField] private float homeGripperOpen = 1f; // 1 = Abierto, 0 = Cerrado

    // Estado interno
    private bool isGripperOpen = true;
    private float currentGripperNormalized = 1f;

    // Propiedades públicas
    public RobotJointController BaseJoint => baseJoint;
    public RobotJointController ShoulderJoint => shoulderJoint;
    public RobotJointController ElbowJoint => elbowJoint;
    public RobotJointController LeftFingerJoint => leftFingerJoint;
    public RobotJointController RightFingerJoint => rightFingerJoint;
    public ArticulationBody PassivePendulum => passivePendulum;
    public bool IsGripperOpen => isGripperOpen;
    public float CurrentGripperNormalized => currentGripperNormalized;

    private void Awake()
    {
        EnsureHierarchyAndJointsAligned();
    }

    private void Start()
    {
        // Posicionar el brazo en la postura inicial
        ResetArmPositions();
    }

    /// <summary>
    /// Verifica y alinea automáticamente los dedos de la garra y el péndulo pasivo
    /// para que ambos dedos queden a la misma altura y se desplacen horizontalmente sobre Z.
    /// </summary>
    public void EnsureHierarchyAndJointsAligned()
    {
        if (leftFingerJoint != null && rightFingerJoint != null)
        {
            Transform lTr = leftFingerJoint.transform;
            Transform rTr = rightFingerJoint.transform;

            // Tomamos la altura (Y) y escala del dedo derecho como referencia para alinear el izquierdo
            float absZ = Mathf.Max(Mathf.Abs(rTr.localPosition.z), Mathf.Abs(lTr.localPosition.z));
            if (absZ < 0.01f) absZ = 0.115f;

            lTr.localPosition = new Vector3(rTr.localPosition.x, rTr.localPosition.y, -absZ);
            rTr.localPosition = new Vector3(rTr.localPosition.x, rTr.localPosition.y, absZ);
            lTr.localRotation = Quaternion.identity;
            rTr.localRotation = Quaternion.identity;
            lTr.localScale = rTr.localScale;

            leftFingerJoint.ConfigureLimits(0f, maxGripperStroke);
            rightFingerJoint.ConfigureLimits(-maxGripperStroke, 0f);
            leftFingerJoint.ConfigureDrive();
            rightFingerJoint.ConfigureDrive();
        }

        if (passivePendulum == null)
        {
            Transform pendTr = FindDeepChild(transform, "PenduloPasivo");
            if (pendTr != null)
            {
                passivePendulum = pendTr.GetComponent<ArticulationBody>();
                if (passivePendulum == null)
                {
                    passivePendulum = pendTr.gameObject.AddComponent<ArticulationBody>();
                    passivePendulum.jointType = ArticulationJointType.SphericalJoint;
                    passivePendulum.mass = 0.3f;
                    passivePendulum.jointFriction = 0f;
                    passivePendulum.angularDamping = 0.5f;
                }
                if (pendTr.GetComponent<PassivePendulum>() == null)
                {
                    pendTr.gameObject.AddComponent<PassivePendulum>();
                }
            }
        }
    }

    private static Transform FindDeepChild(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName) return child;
            Transform found = FindDeepChild(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Controla el ángulo de la base (rotación horizontal - Yaw).
    /// </summary>
    public void SetBaseTarget(float angle)
    {
        if (baseJoint != null)
            baseJoint.SetTarget(angle);
    }

    /// <summary>
    /// Controla el ángulo del hombro / pluma (elevación - Pitch).
    /// </summary>
    public void SetShoulderTarget(float angle)
    {
        if (shoulderJoint != null)
            shoulderJoint.SetTarget(angle);
    }

    /// <summary>
    /// Controla el ángulo del codo / antebrazo (flexión - Pitch).
    /// </summary>
    public void SetElbowTarget(float angle)
    {
        if (elbowJoint != null)
            elbowJoint.SetTarget(angle);
    }

    /// <summary>
    /// Controla la apertura de la garra de 0 (completamente cerrada) a 1 (completamente abierta).
    /// En reposo (target = 0) ambos dedos están abiertos en los bordes de SoporteGarra.
    /// Al cerrar, DedoIzquierdo (z < 0) avanza +closeOffset en +Z y DedoDerecho (z > 0) avanza -closeOffset en -Z.
    /// </summary>
    public void SetGripperTarget(float normalizedOpen)
    {
        currentGripperNormalized = Mathf.Clamp01(normalizedOpen);
        isGripperOpen = currentGripperNormalized > 0.4f;

        float closeOffset = (1f - currentGripperNormalized) * maxGripperStroke;

        if (leftFingerJoint != null)
            leftFingerJoint.SetTarget(closeOffset);

        if (rightFingerJoint != null)
            rightFingerJoint.SetTarget(-closeOffset);
    }

    /// <summary>
    /// Alterna entre garra abierta y garra cerrada (para botón de UI).
    /// </summary>
    public void ToggleGripper()
    {
        if (isGripperOpen)
        {
            SetGripperTarget(0f); // Cerrar
        }
        else
        {
            SetGripperTarget(1f); // Abrir
        }
    }

    /// <summary>
    /// Restablece todas las articulaciones del brazo a su posición segura de reposo (Home).
    /// </summary>
    public void ResetArmPositions()
    {
        SetBaseTarget(homeBaseAngle);
        SetShoulderTarget(homeShoulderAngle);
        SetElbowTarget(homeElbowAngle);
        SetGripperTarget(homeGripperOpen);
    }

    /// <summary>
    /// Devuelve la lista de joints controlados para que la UI o el inspector lean la telemetría.
    /// </summary>
    public List<RobotJointController> GetAllControlledJoints()
    {
        var list = new List<RobotJointController>();
        if (baseJoint != null) list.Add(baseJoint);
        if (shoulderJoint != null) list.Add(shoulderJoint);
        if (elbowJoint != null) list.Add(elbowJoint);
        if (leftFingerJoint != null) list.Add(leftFingerJoint);
        if (rightFingerJoint != null) list.Add(rightFingerJoint);
        return list;
    }

    /// <summary>
    /// Retorna la oscilación del joint pasivo (péndulo esférico) en grados respecto a la vertical.
    /// </summary>
    public float GetPassivePendulumAngle()
    {
        if (passivePendulum == null) return 0f;
        return Vector3.Angle(passivePendulum.transform.up, Vector3.up);
    }
}
