using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Administrador principal del Brazo Robótico Articulado.
/// Coordina todos los joints del mecanismo, expone métodos de control para la UI,
/// administra la apertura/cierre sincronizado de la garra y la postura de reposo (Reset).
/// </summary>
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

    [Tooltip("Apertura máxima de los dedos en metros (recorrido del joint prismático).")]
    [SerializeField] private float maxGripperStroke = 0.05f;

    [Header("Joint Pasivo (Gravedad / Inercia)")]
    [Tooltip("Elemento esférico pasivo sin motor que oscila libremente por inercia y gravedad.")]
    [SerializeField] private ArticulationBody passivePendulum;

    [Header("Posición de Reposo / Home")]
    [SerializeField] private float homeBaseAngle = 0f;
    [SerializeField] private float homeShoulderAngle = 20f;
    [SerializeField] private float homeElbowAngle = -20f;
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

    private void Start()
    {
        // Posicionar el brazo en la postura inicial
        ResetArmPositions();
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
    /// Desplaza ambos dedos prismáticos de forma simétrica.
    /// </summary>
    public void SetGripperTarget(float normalizedOpen)
    {
        currentGripperNormalized = Mathf.Clamp01(normalizedOpen);
        isGripperOpen = currentGripperNormalized > 0.4f;

        float targetOffset = currentGripperNormalized * maxGripperStroke;

        // Dedo izquierdo se desplaza hacia +targetOffset, derecho hacia -targetOffset
        if (leftFingerJoint != null)
            leftFingerJoint.SetTarget(targetOffset);

        if (rightFingerJoint != null)
            rightFingerJoint.SetTarget(-targetOffset);
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
