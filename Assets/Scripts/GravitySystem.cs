using System.Collections.Generic;
using UnityEngine;

public class GravitySystem : MonoBehaviour
{
    [Header("SpawnPlanet")]
    [SerializeField] GameObject planetPrefab;
    [SerializeField] float mass;
    [SerializeField] int planetCount = 50;

    [Header("SpawnShip")]
    [SerializeField] GameObject shipPrefab;
    Vector3 velocity;
    [SerializeField] int shipCount = 50;

    [Header("SpawnLimits")]
    [SerializeField] Vector3 limitX;
    [SerializeField] Vector3 limitY;

    [Header("computeShader")]
    public ComputeShader shader;
    public List<Ship> ships;
    public List<Planet> planets;

    ComputeBuffer shipBuffer;
    ComputeBuffer planetBuffer;
    ComputeBuffer forceBuffer;

    Vector3[] forces;

    int kernel;

    struct ShipData
    {
        public Vector3 position;
        public float mass;
    }

    struct PlanetData
    {
        public Vector3 position;
        public float mass;
    }

    void Start()
    {
        kernel = shader.FindKernel("CSMain");

        SpawnPlanet();
        SpawnShip();

        shipBuffer = new ComputeBuffer(ships.Count, sizeof(float) * 4);
        planetBuffer = new ComputeBuffer(planets.Count, sizeof(float) * 4);
        forceBuffer = new ComputeBuffer(ships.Count, sizeof(float) * 3);

        forces = new Vector3[ships.Count];
    }

    void Update()
    {
        ShipData[] shipData = new ShipData[ships.Count];
        for (int i = 0; i < ships.Count; i++)
        {
            shipData[i].position = ships[i].transform.position;
            shipData[i].mass = ships[i].mass;
        }

        PlanetData[] planetData = new PlanetData[planets.Count];
        for (int i = 0; i < planets.Count; i++)
        {
            planetData[i].position = planets[i].transform.position;
            planetData[i].mass = planets[i].mass;
        }

        shipBuffer.SetData(shipData);
        planetBuffer.SetData(planetData);

        shader.SetBuffer(kernel, "Ships", shipBuffer);
        shader.SetBuffer(kernel, "Planets", planetBuffer);
        shader.SetBuffer(kernel, "OutForces", forceBuffer);
        shader.SetInt("ShipCount", ships.Count);
        shader.SetInt("PlanetCount", planets.Count);

        int groups = Mathf.CeilToInt(ships.Count / 64f);
        shader.Dispatch(kernel, groups, 1, 1);

        forceBuffer.GetData(forces);

        for (int i = 0; i < ships.Count; i++)
        {
            ships[i].gravityForce = forces[i];
        }
    }

    void SpawnPlanet()
    {
        for(int i = 0;i < planetCount;i++)
        {
            float x = Random.Range(limitX.x, limitX.y);
            float y = Random.Range(limitY.x, limitY.y);
            Vector3 spawnPos = new Vector3(x, y, 0);
            GameObject _obj = Instantiate(planetPrefab, spawnPos, Quaternion.identity);
            if(_obj.TryGetComponent(out Planet planet))
            {
                mass = Random.Range(.1f, .5f);
                planet.mass = mass;
                _obj.transform.localScale = new Vector3(mass * 2, mass * 2);
                planets.Add(planet);
            }
        }
    }

    void SpawnShip()
    {
        for (int i = 0; i < shipCount; i++)
        {
            float x = Random.Range(limitX.x, limitX.y);
            float y = Random.Range(limitY.x, limitY.y);
            Vector3 spawnPos = new Vector3(x, y, 0);
            GameObject _obj = Instantiate(shipPrefab, spawnPos, Quaternion.identity);
            if (_obj.TryGetComponent(out Ship ship))
            {
                velocity = Random.insideUnitCircle.normalized;
                ship.velocity = velocity;
                ships.Add(ship);
            }
        }
    }


    void OnDestroy()
    {
        shipBuffer.Release();
        planetBuffer.Release();
        forceBuffer.Release();
    }
}
