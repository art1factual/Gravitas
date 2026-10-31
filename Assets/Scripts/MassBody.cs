using System.Collections.Generic;

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MassBody : MonoBehaviour
{
    
    public static readonly List<MassBody> All = new List<MassBody>();

    public const float G = 1f;
    public const float C = 50f;

    public const float Softening = 0.5f;

    [Min(1e-9f)]
    public float mass = 1f;

    public Vector3 initialVelocity;
    
    [Header("Clock readout")]
    public float clockRate = 1f;
    public double properTime;
    public bool clockModelValid;

    public Rigidbody Body { get; private set;}

    float GravitatingMass => Mathf.Max(1e-9f, mass);

    void Awake()
    {
        Body = GetComponent<Rigidbody>();

        Body.useGravity = false;
        Body.mass = GravitatingMass;

    }

    void OnEnable()
    {
        if(!All.Contains(this))
        All.Add(this);

    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        Body.linearVelocity = initialVelocity;

    }

    void FixedUpdate()
    {
        Body.mass = GravitatingMass;

        Vector3 acceleration = Vector3.zero;
        Vector3 position = Body.worldCenterOfMass;

        foreach (MassBody other in All)
        {
            if (!other || other == this || !other.Body)
            continue;

            Vector3 direction = other.Body.worldCenterOfMass - position;

            float distanceSquared = direction.sqrMagnitude + Softening * Softening;

            float denominator = distanceSquared * Mathf.Sqrt(distanceSquared);

            acceleration += G * other.GravitatingMass * direction / denominator;
            
        }

        if (!Body.isKinematic)
        {
            Body.AddForce(acceleration, ForceMode.Acceleration);

        }
        
         }

    public static float PotentialAt(Vector3 position)
    {
        float potential = 0f;
        
        foreach (MassBody source in All)
        {
            if (!source || !source.Body)
            continue;

            Vector3 offset = position - source.Body.worldCenterOfMass;

            float distance = Mathf.Sqrt(offset.sqrMagnitude + Softening * Softening);

            potential -= G * source.GravitatingMass / distance;
            }
            return potential;
    }

    void LateUpdate()
{
    float potential = PotentialAt(Body.worldCenterOfMass);
    float speedSquared = Body.linearVelocity.sqrMagnitude;

    float gravitationalTerm = potential / (C * C);
    float velocityTerm = speedSquared / (C * C);

    clockModelValid = Mathf.Abs(gravitationalTerm) < 0.02f && velocityTerm < 0.01f;

    if (!clockModelValid)
        {
            clockRate = float.NaN;
            return;
        }

        clockRate = 1f + gravitationalTerm - 0.5f * velocityTerm;
        properTime += Time.deltaTime * clockRate;
}
        void OnGUI()
        {
            if(All.Count == 0 || All[0] != this)
            return;

            GUILayout.BeginArea(new Rect(12, 12, 460, 220), GUI.skin.box);

            GUILayout.Label("Mass objects and local clocks");

            foreach (MassBody source in All)
            {
                if(!source)
                continue;

                string rate = source.clockModelValid ? source.clockRate.ToString("F6") : "outside weak field range";
                GUILayout.Label($"{source.name}: rate {rate}, " + $"proper time {source.properTime:F3}");

        }

    GUILayout.EndArea();
}
}