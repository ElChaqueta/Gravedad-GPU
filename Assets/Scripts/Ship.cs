using System.Collections.Generic;
using UnityEngine;

public class Ship : MonoBehaviour
{
    public Vector3 velocity;
    public float mass;

    public Vector3 gravityForce;

    void Update()
    {
        velocity += gravityForce * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.up = velocity.normalized;
        transform.Rotate(transform.up);
    }
}
