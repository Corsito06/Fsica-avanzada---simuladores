using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Péndulo pasivo colgado de la soga: un ArticulationBody de tipo Spherical SIN motor
/// (stiffness = 0) que oscila solo por gravedad e inercia cuando el brazo se mueve.
/// Evita que la bola choque contra las piezas del propio robot (se atravesaría con los
/// eslabones) pero sigue chocando con el piso y con los cubos.
/// </summary>
[RequireComponent(typeof(ArticulationBody))]
public class PassivePendulum : MonoBehaviour
{
    /// <summary>Inclinación de la soga respecto a la vertical, en grados (0 = colgando quieto).</summary>
    public float SwingAngle => Vector3.Angle(transform.up, Vector3.up);

    private void Awake()
    {
        IgnoreRobotCollisions();
    }

    private void IgnoreRobotCollisions()
    {
        // La raíz de la articulación es el ArticulationBody más alto de la jerarquía
        ArticulationBody[] chain = GetComponentsInParent<ArticulationBody>();
        Transform root = chain[chain.Length - 1].transform;

        Collider[] mine = GetComponentsInChildren<Collider>();
        Collider[] robot = root.GetComponentsInChildren<Collider>();
        var mineSet = new HashSet<Collider>(mine);

        foreach (Collider m in mine)
        {
            foreach (Collider r in robot)
            {
                if (!mineSet.Contains(r))
                    Physics.IgnoreCollision(m, r, true);
            }
        }
    }
}
