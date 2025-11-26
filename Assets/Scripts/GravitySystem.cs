using System.Collections.Generic;
using UnityEngine;


public class GravitySystem : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject planetPrefab;
    [SerializeField] GameObject shipPrefab;

    [Header("Counts")]
    [SerializeField] int planetCount = 50;
    [SerializeField] int shipCount = 50;

    [Header("Spawn Ranges (min, max)")]
    // Se usan Vector2 para representar (min, max) de cada eje para que sea más claro en código.
    [SerializeField] Vector2 limitX = new Vector2(-10, 10);
    [SerializeField] Vector2 limitY = new Vector2(-10, 10);

    [Header("Compute")]
    public ComputeShader shader;
    const int THREAD_GROUP_SIZE = 64;

    // Referencias a escena (pobladas en tiempo de ejecución / inspector)
    public List<Ship> ships;
    public List<Planet> planets;

    // Buffers para compute shader
    ComputeBuffer shipBuffer;
    ComputeBuffer planetBuffer;
    ComputeBuffer forceBuffer;
    Vector3[] computedForces;

    int kernelIndex;

    // Estructuras para la GPU (diferentes nombres para parecer otro script)
    struct ShipGpu
    {
        public Vector3 position;
        public float mass;
    }

    struct PlanetGpu
    {
        public Vector3 position;
        public float mass;
    }

    void Awake()
    {
        // Asegurar listas inicializadas si no se asignaron desde el inspector
        if (ships == null) ships = new List<Ship>();
        if (planets == null) planets = new List<Planet>();
    }

    void Start()
    {
        kernelIndex = shader.FindKernel("CSMain");

        PopulateSceneObjects();
        CreateComputeBuffers();
    }

    void Update()
    {
        if (ships.Count == 0 || planets.Count == 0) return;

        UploadSceneToGpu();
        DispatchShader();
        DownloadAndApplyForces();
    }

    // ----- Creación y spawn de objetos -----
    void PopulateSceneObjects()
    {
        SpawnPlanets(planetCount);
        SpawnShips(shipCount);
    }

    void SpawnPlanets(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(limitX.x, limitX.y);
            float y = Random.Range(limitY.x, limitY.y);
            Vector3 pos = new Vector3(x, y, 0f);

            GameObject go = Instantiate(planetPrefab, pos, Quaternion.identity);
            if (go.TryGetComponent<Planet>(out Planet p))
            {
                float m = Random.Range(0.1f, 0.5f);
                p.mass = m;
                go.transform.localScale = new Vector3(m * 2f, m * 2f, 1f);
                planets.Add(p);
            }
            else
            {
                Destroy(go);
            }
        }
    }

    void SpawnShips(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(limitX.x, limitX.y);
            float y = Random.Range(limitY.x, limitY.y);
            Vector3 pos = new Vector3(x, y, 0f);

            GameObject go = Instantiate(shipPrefab, pos, Quaternion.identity);
            if (go.TryGetComponent<Ship>(out Ship s))
            {
                // Random.insideUnitCircle -> Vector2, se convierte implícitamente a Vector3 (z = 0)
                s.velocity = Random.insideUnitCircle.normalized;
                ships.Add(s);
            }
            else
            {
                Destroy(go);
            }
        }
    }

    // ----- Buffers GPU -----
    void CreateComputeBuffers()
    {
        ReleaseBuffersIfAny();

        int shipStructSize = sizeof(float) * 4;   // Vector3 + float
        int planetStructSize = sizeof(float) * 4; // Vector3 + float
        int forceStructSize = sizeof(float) * 3;  // Vector3

        shipBuffer = new ComputeBuffer(ships.Count, shipStructSize);
        planetBuffer = new ComputeBuffer(planets.Count, planetStructSize);
        forceBuffer = new ComputeBuffer(ships.Count, forceStructSize);

        computedForces = new Vector3[ships.Count];
    }

    void ReleaseBuffersIfAny()
    {
        if (shipBuffer != null) { shipBuffer.Release(); shipBuffer = null; }
        if (planetBuffer != null) { planetBuffer.Release(); planetBuffer = null; }
        if (forceBuffer != null) { forceBuffer.Release(); forceBuffer = null; }
    }

    // ----- Interacción con el compute shader -----
    void UploadSceneToGpu()
    {
        // Llenar arrays intermedios desde objetos de la escena
        ShipGpu[] shipArray = new ShipGpu[ships.Count];
        for (int i = 0; i < ships.Count; i++)
        {
            shipArray[i].position = ships[i].transform.position;
            shipArray[i].mass = ships[i].mass;
        }

        PlanetGpu[] planetArray = new PlanetGpu[planets.Count];
        for (int i = 0; i < planets.Count; i++)
        {
            planetArray[i].position = planets[i].transform.position;
            planetArray[i].mass = planets[i].mass;
        }

        shipBuffer.SetData(shipArray);
        planetBuffer.SetData(planetArray);
    }

    void DispatchShader()
    {
        shader.SetBuffer(kernelIndex, "Ships", shipBuffer);
        shader.SetBuffer(kernelIndex, "Planets", planetBuffer);
        shader.SetBuffer(kernelIndex, "OutForces", forceBuffer);
        shader.SetInt("ShipCount", ships.Count);
        shader.SetInt("PlanetCount", planets.Count);

        int groups = Mathf.CeilToInt(ships.Count / (float)THREAD_GROUP_SIZE);
        shader.Dispatch(kernelIndex, groups, 1, 1);
    }

    void DownloadAndApplyForces()
    {
        forceBuffer.GetData(computedForces);

        for (int i = 0; i < ships.Count; i++)
        {
            ships[i].gravityForce = computedForces[i];
        }
    }

    void OnDestroy()
    {
        ReleaseBuffersIfAny();
    }
}
