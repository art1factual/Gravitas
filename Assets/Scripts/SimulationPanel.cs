using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public class SimulationPanel : MonoBehaviour
{


    public MassBody referenceBody;
    public MassBody observedBody;

    public double metresPerUnityUnit = 14959787070.0;
    public double secondsPerSimulationSecond = 86400.0;

    public Vector3 orbitPlaneNormal = Vector3.zero;

    public Rect panel = new Rect(12, 240, 480, 430);

    readonly List<MassBody> bodies = new List<MassBody>();
    readonly float[] speeds = { 0.25f, 1f, 5f, 20f, };

    Vector2 scroll;

    double elapsedSimulationSeconds;
    double accumalatedAngle;
    double previousAngle;
    
    Vector3 planeX;
    Vector3 planeY;
    Vector3 previousPlaneSetting;

    MassBody trackedReference; 
    MassBody trackedObserved;

    bool planeReady;
    bool hasPreviousAngle;
    bool paused;

    float playbackSpeed = 1f;

    void OnEnable()
    {
        ApplyPlaybackSpeed();
    }

    void Start()
    {
        RefreshBodies();
        ResetRevolutionCounter();
    }

    void FixedUpdate()
    {
        elapsedSimulationSeconds += Time.fixedDeltaTime;

        SampleRevolution();
    }

    void LateUpdate()
    {
        RefreshBodies();

        SampleRevolution();
    }

    static bool IsAvailable(MassBody body)
    {
        return body != null && body.isActiveAndEnabled && body.Body != null;
    }

    bool PairReady()
    {
        return IsAvailable(referenceBody) && IsAvailable(observedBody) && referenceBody != observedBody;
    }

    void RefreshBodies()
    {
        bodies.Clear();

        foreach (MassBody body in MassBody.All)
        {
            if (IsAvailable(body))
            bodies.Add(body);
        }

        if (bodies.Count == 0)
        {
            referenceBody = null;
            observedBody = null;
            return;
        }

        if (!bodies.Contains(referenceBody))
        {
            referenceBody = bodies[0];

            foreach (MassBody body in bodies)
            {
                if (body.mass > referenceBody.mass)
                referenceBody = body;
            }
        }

        if (!bodies.Contains(observedBody) || observedBody == referenceBody)
        {
            observedBody = null;
            foreach (MassBody body in bodies)
            {
                if (body != referenceBody)
                {
                    observedBody = body;
                    break;
                }
            }
        }
    }

    public void NextReference()
    {
        SelectNextBody(true);
    }


    public void NextObserved()
    {
        SelectNextBody(false);
    }

    void SelectNextBody(bool changeReference)
    {
        RefreshBodies();

        if (bodies.Count < 2)
        return;

        MassBody current = changeReference ? referenceBody : observedBody;

        int index = (bodies.IndexOf(current) + 1) % bodies.Count;
        
        MassBody next = bodies[index];

        if (changeReference)
        {
            if (next == observedBody)
            observedBody = referenceBody; 
            referenceBody = next;

        }
        else
        {
            if (next == referenceBody)
            referenceBody = observedBody;

            observedBody = next;

        }

        ResetRevolutionCounter();
    }
    public void ResetRevolutionCounter()
    {
        trackedReference = referenceBody;
        trackedObserved = observedBody;
        previousPlaneSetting = orbitPlaneNormal;

        accumalatedAngle = 0.0;
        hasPreviousAngle = false;
        planeReady = false;

        if (!PairReady())
        return;

        Vector3 separation = observedBody.Body.worldCenterOfMass - referenceBody.Body.worldCenterOfMass;

        Vector3 relativeVelocity = observedBody.Body.linearVelocity - referenceBody.Body.linearVelocity;

        Vector3 normal = orbitPlaneNormal;
        if (normal.sqrMagnitude < 1e-20f)
        {
            normal = Vector3.Cross(separation, relativeVelocity);
        }

        if (normal.sqrMagnitude < 1e-20f)
        return;

        normal.Normalize();

        planeX = Vector3.ProjectOnPlane(separation, normal);

        if (planeX.sqrMagnitude < 1e-20f)
        return;

        planeX.Normalize();
        planeY = Vector3.Cross(normal, planeX).normalized;

        previousAngle = System.Math.Atan2(Dot(separation, planeY), Dot(separation, planeX)
        );
        planeReady = true;
        hasPreviousAngle = true;
        
    }

    void SampleRevolution()
    {
        if (referenceBody != trackedReference || observedBody != trackedObserved || orbitPlaneNormal != previousPlaneSetting)
        {
            ResetRevolutionCounter();
        }

        if (!planeReady || !PairReady())
        return;

        Vector3 separation = observedBody.Body.worldCenterOfMass - referenceBody.Body.worldCenterOfMass;

        double x = Dot(separation, planeX);
        double y = Dot(separation, planeY);

        if (x * x + y * y < 1e-20)
        {
            hasPreviousAngle = false;
            return;

        }
        double angle = System.Math.Atan2(y, x);


        if (hasPreviousAngle)
        {
            double change = angle - previousAngle;

            if (change > System.Math.PI)
            change -= 2.0 * System.Math.PI;

            if (change < -System.Math.PI)
            change += 2.0 * System.Math.PI;

            accumalatedAngle += change;
        }
        previousAngle = angle;
        hasPreviousAngle = true;



}

    static double Dot(Vector3 a, Vector3 b)
    {
        return (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;
    }

    public void TogglePause()
    {
        paused = !paused;
        ApplyPlaybackSpeed();

    }

    public void SetSpeed(float speed)
    {
        playbackSpeed = Mathf.Clamp(speed, 0.1f, 20f);
        ApplyPlaybackSpeed();
    }

    void ApplyPlaybackSpeed()
    {
        Time.timeScale = paused ? 0f : playbackSpeed;
    }

    public void ResetExperiment()
    {
        int index = SceneManager.GetActiveScene().buildIndex;

        if (index < 0)
        {
            Debug.LogError("Save the scene and add it to the " + "Build Profiles Scene List.",
            this);

            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(index);
    }
    void OnGUI()
    {
        GUILayout.BeginArea(panel, GUI.skin.box);

        GUILayout.Label("Planet Simulation");
        GUILayout.Label($"Active Mass Objects: {bodies.Count}");

        GUILayout.Label(paused ? "Paused" : $"Playback: {playbackSpeed:F2}x"
        );

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(paused ? "Resume" : "Pause"))
        TogglePause();

        bool previousEnabled = GUI.enabled;

        GUI.enabled = previousEnabled && SceneManager.GetActiveScene().buildIndex >= 0;

        if (GUILayout.Button("Reset experiment"))
        ResetExperiment();

        GUI.enabled = previousEnabled;

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();

        foreach (float speed in speeds)
        {
            if (GUILayout.Button($"{speed:G}x"))
            SetSpeed(speed);
        }

        GUILayout.EndHorizontal();
        scroll = GUILayout.BeginScrollView(scroll);

        double elapsedPhysicalSeconds = elapsedSimulationSeconds * secondsPerSimulationSecond;

        GUILayout.Label($"Elapsed days: " + $"{elapsedPhysicalSeconds / 86400.0:F3}");

        GUILayout.BeginHorizontal();

        GUI.enabled = previousEnabled && bodies.Count >= 2;

        if (GUILayout.Button("Next reference"))
        NextReference();

        if (GUILayout.Button("Next Observed Body"))
        NextObserved();

        GUI.enabled = previousEnabled;

        GUILayout.EndHorizontal();

        GUILayout.Label("Reference:" + (referenceBody != null ? referenceBody.name : "None"));

        GUILayout.Label("Observed: " + (observedBody != null ? observedBody.name : "none")
        );

        if (PairReady() && metresPerUnityUnit > 0.0 && secondsPerSimulationSecond > 0.0)
        {
            Vector3 separation = observedBody.Body.worldCenterOfMass - referenceBody.Body.worldCenterOfMass;

            Vector3 relativeVelocity = observedBody.Body.linearVelocity - referenceBody.Body.linearVelocity;

            double distanceMetres = System.Math.Sqrt(Dot(separation, separation)) * metresPerUnityUnit;

            double speedMetresPerSecond = System.Math.Sqrt(Dot(relativeVelocity, relativeVelocity)) * metresPerUnityUnit / secondsPerSimulationSecond;

            GUILayout.Label($"Separation: " + $"{distanceMetres / 149597870700.0:G8} AU");

            GUILayout.Label($"Relative speed:  {speedMetresPerSecond / 1000.0:G6} km/s");
        

        if (planeReady)
        {
            double revolutions = System.Math.Abs(accumalatedAngle) / (2.0 * System.Math.PI);

            GUILayout.Label($"Net revolutions: {revolutions:F4}");

            GUILayout.Label($"Completed turns (net): " + $"{System.Math.Floor(revolutions):F0}");
            
        }
        else
        {
            GUILayout.Label("No orbital plane detected. " + "Set Orbit Plane Normal if Needed."
            );
        
        }
    }

    
    else
    {
        GUILayout.Label("Two active Massbody objects and " + "positive unit conversions are required."
        );
    }

    if (GUILayout.Button("Restart revolution counter"))
    ResetRevolutionCounter();

    if ( SceneManager.GetActiveScene().buildIndex < 0)
{
    GUILayout.Label("Add this scene to the Build Profiles " + "Scene List to enable experiment reset.");

}

GUILayout.EndScrollView();
GUILayout.EndArea();
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
    }
}