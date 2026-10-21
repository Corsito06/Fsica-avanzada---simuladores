using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona de destino o plataforma de descarga para la maniobra de traslado/apilado.
/// Detecta mediante Trigger cuántos cubos han sido depositados con éxito en el área de meta.
/// </summary>
public class TaskTargetZone : MonoBehaviour
{
    [Header("Objetivo de la Maniobra")]
    [Tooltip("Cantidad objetivo de cubos para considerar la maniobra finalizada.")]
    [SerializeField] private int targetCubeCount = 3;

    private readonly HashSet<Rigidbody> cubesInZone = new HashSet<Rigidbody>();

    public int CubesInZoneCount => cubesInZone.Count;
    public int TargetCubeCount => targetCubeCount;
    public bool IsTaskCompleted => cubesInZone.Count >= targetCubeCount;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && !rb.isKinematic && other.GetComponent<ArticulationBody>() == null)
        {
            cubesInZone.Add(rb);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            cubesInZone.Remove(rb);
        }
    }

    /// <summary>
    /// Limpia el conteo cuando los cubos se reinician.
    /// </summary>
    public void ResetZone()
    {
        cubesInZone.Clear();
    }
}
