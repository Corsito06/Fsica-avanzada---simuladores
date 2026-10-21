using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona los cubos de carga (Rigidbodies) que el brazo manipulador debe sujetar,
/// trasladar y apilar. Permite reiniciar sus posiciones mediante botón de UI o automáticamente
/// si se caen del escenario.
/// </summary>
public class CubeManager : MonoBehaviour
{
    [System.Serializable]
    public class CubeItem
    {
        public Rigidbody body;
        [HideInInspector] public Vector3 initialPosition;
        [HideInInspector] public Quaternion initialRotation;
    }

    [Header("Cubos de la Tarea (Rigidbodies)")]
    [Tooltip("Lista de cubos interactuables en la escena.")]
    [SerializeField] private List<CubeItem> cubes = new List<CubeItem>();

    [Header("Propiedades Físicas de Agarre")]
    [Tooltip("Fricción estática alta para evitar que los cubos se deslicen de la garra.")]
    [SerializeField] private float staticFriction = 1.0f;

    [Tooltip("Fricción dinámica.")]
    [SerializeField] private float dynamicFriction = 0.9f;

    [Header("Reaparición / Respawn")]
    [Tooltip("Altura límite por debajo de la cual el cubo se reinicia automáticamente si se cae.")]
    [SerializeField] private float fallThresholdY = -2f;

    private PhysicMaterial highFrictionMaterial;

    private void Awake()
    {
        CreateHighFrictionMaterial();
        RecordInitialStates();
        ApplyFrictionToCubes();
    }

    /// <summary>
    /// Crea en memoria un material físico optimizado para el agarre con pinzas.
    /// </summary>
    private void CreateHighFrictionMaterial()
    {
        highFrictionMaterial = new PhysicMaterial("FriccionGarra")
        {
            staticFriction = staticFriction,
            dynamicFriction = dynamicFriction,
            frictionCombine = PhysicMaterialCombine.Maximum,
            bounciness = 0f,
            bounceCombine = PhysicMaterialCombine.Minimum
        };
    }

    /// <summary>
    /// Guarda las posiciones y orientaciones iniciales de cada cubo para el botón de reinicio.
    /// </summary>
    private void RecordInitialStates()
    {
        foreach (var item in cubes)
        {
            if (item.body != null)
            {
                item.initialPosition = item.body.position;
                item.initialRotation = item.body.rotation;
            }
        }
    }

    /// <summary>
    /// Aplica el material físico de alta fricción a los colisionadores de los cubos.
    /// </summary>
    private void ApplyFrictionToCubes()
    {
        foreach (var item in cubes)
        {
            if (item.body != null)
            {
                Collider col = item.body.GetComponent<Collider>();
                if (col != null)
                {
                    col.sharedMaterial = highFrictionMaterial;
                }
            }
        }
    }

    private void Update()
    {
        // Verificar si algún cubo se cayó al vacío y regresarlo
        for (int i = 0; i < cubes.Count; i++)
        {
            if (cubes[i].body != null && cubes[i].body.position.y < fallThresholdY)
            {
                ResetCube(cubes[i]);
            }
        }
    }

    /// <summary>
    /// Restablece un cubo individual a su estado original sin velocidad.
    /// </summary>
    private void ResetCube(CubeItem item)
    {
        if (item.body == null) return;

        item.body.velocity = Vector3.zero;
        item.body.angularVelocity = Vector3.zero;
        item.body.position = item.initialPosition;
        item.body.rotation = item.initialRotation;
    }

    /// <summary>
    /// Reinicia todos los cubos a sus posiciones iniciales (vinculable al botón de UI).
    /// </summary>
    public void ResetAllCubes()
    {
        foreach (var item in cubes)
        {
            ResetCube(item);
        }
    }

    /// <summary>
    /// Registra dinámicamente un nuevo cubo si fue creado en tiempo de ejecución.
    /// </summary>
    public void RegisterCube(Rigidbody rb)
    {
        if (rb == null) return;

        CubeItem item = new CubeItem
        {
            body = rb,
            initialPosition = rb.position,
            initialRotation = rb.rotation
        };

        Collider col = rb.GetComponent<Collider>();
        if (col != null)
        {
            if (highFrictionMaterial == null) CreateHighFrictionMaterial();
            col.sharedMaterial = highFrictionMaterial;
        }

        cubes.Add(item);
    }
}
