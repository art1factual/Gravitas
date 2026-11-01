using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlanetEditorPanel : MonoBehaviour
{
    public SimulationPanel simulationPanel;
    public GodViewCamera godCamera;
    public Material newPlanetMaterial;

    public Vector2 margin = new Vector2(12f, 12f);
    public float panelWidth = 380f;
    public float panelHeight = 680f;

    public MassBody selectedBody;

    static PlanetEditorPanel activePanel;
    static readonly CultureInfo Numbers = CultureInfo.InvariantCulture;
    readonly List<MassBody> bodies = new List<MassBody>();

    [Header("Canvas Customization")]
    public TMP_Dropdown bodyDropdown;
    public TMP_InputField bodyNameInput;
    public TMP_InputField massKgInput;
    public TMP_InputField radiusKmInput;
    public TMP_InputField magnificationInput;

    public TMP_InputField positionXAU;
    public TMP_InputField positionYAU;
    public TMP_InputField positionZAU;

    public TMP_InputField velocityXKmS;
    public TMP_InputField velocityYKmS;
    public TMP_InputField velocityZKmS;
    public TMP_Text statusText;
    public TMP_Text bodyReadout;
    

    readonly string[] positionText = { "10", "0", "0" };
    readonly string[] velocityText = { "0", "0", "0.172" };

    string nameText = "New Planet";
    string massText = "3.00349e-6";
    string visualRadiusText = "0.05";
    string message = "Select a body, or fill in the following fields and add a planet.";
    Vector2 scroll;
    bool editingText;

    Rect PanelRect
    {
        get
        {
            float width = Mathf.Clamp(panelWidth, 200f, Mathf.Max(200f, Screen.width - 24f));
            float height = Mathf.Clamp(panelHeight, 180f, Mathf.Max(180f, Screen.height - 24f));
            return new Rect(Mathf.Max(0f, Screen.width - width - margin.x), Mathf.Max(0f, margin.y), width, height);

        }
    } 



    public static bool BlocksCamera(Vector2 pointer)
    {
        if (activePanel == null || !activePanel.isActiveAndEnabled)
        return false;

        Vector2 guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
        return activePanel.editingText || activePanel.PanelRect.Contains(guiPoint);
 
    }

    void OnEnable()
    {
        if (activePanel != null && activePanel != this)
        {
            Debug.LogError("Use only one PlanetEditor Panel at a given scene.", this);
            enabled = false;
            return;

        }
        activePanel = this;
    }

    void Start()
    {
        if (simulationPanel == null)
        simulationPanel = FindFirstObjectByType<SimulationPanel>();
        if (godCamera == null)
        godCamera = FindFirstObjectByType<GodViewCamera>();
        if (Available(selectedBody))
        LoadSelected();
    }
    
    void Update()
    {
        bodies.Clear();
        foreach (MassBody body in MassBody.All)
        if (Available(body))
        bodies.Add(body);

        if (selectedBody != null && !Available(selectedBody))
        selectedBody = null;
    }

    static bool Available (MassBody body)
    {
        return body != null && body.isActiveAndEnabled && body.Body != null;
    }

    static string Format(float value)
    {
        return value.ToString("G9", Numbers);
    }

    static void WriteVector(string[] fields, Vector3 value)
    {
        fields[0] = Format(value.x);
        fields[1] = Format(value.y);
        fields[2] = Format(value.z);
    }

    bool ReadNumber(string text, string label, out float value)
    {
        if (float.TryParse(text, NumberStyles.Float, Numbers, out value) && !float.IsNaN(value) && !float.IsInfinity(value))
        return true;

        message = label + ": enter a finite number. Decimal point must be utilized; 3e-6 is allowed.";;
        return false;
    }

    bool ReadVector(string[] fields, string label, out Vector3 value)
    {
        value = Vector3.zero;
        if (!ReadNumber(fields[0], label + " X", out float x) || !ReadNumber(fields[1], label + " Y", out float y) || !ReadNumber(fields[2], label + " Z", out float z))
        return false;
        value = new Vector3(x, y, z);
        return true;
    }

    bool ReadMass(out float mass)
    {
        if (!ReadNumber(massText, "Mass", out mass))
        return false;

        if (mass < 1e-9f)
        {
            message = "Mass must be at least 1e-9 simulated unity.";
            return false;

        }
        return true;
    }

    bool CanEditSelected()
    {
        if (!Available(selectedBody))
        {
            message = "Selected an active MassBody.";
            return false;
        }
        return true;
    }

    void FinishEdit(string text)
    {
        message = text;
        if (Event.current != null)
        GUI.FocusControl(null);
        editingText = false;
        if (simulationPanel != null)
        simulationPanel.ResetRevolutionCounter();
    }

    public void SelectBody(MassBody body)
    {
        if (body == null || !body.isActiveAndEnabled || body.Body == null)
        return;
        selectedBody = body;
        LoadSelected();
    }
    void LoadSelected()
    {
        if (!CanEditSelected())
        return;
        nameText = selectedBody.name;
        massText = Format(selectedBody.mass);
        WriteVector(positionText, selectedBody.Body.position);
        WriteVector(velocityText, selectedBody.Body.linearVelocity);
        if (Event.current != null)
        GUI.FocusControl(null);
        editingText = false;
        message = "Loaded a snapshot. Editing fields alone does not change the simulated output.";

    }

    void ApplyProperties()
    {
        if (!CanEditSelected() || !ReadMass(out float mass))
        return;
        selectedBody.name = string.IsNullOrWhiteSpace(nameText) ? "Planet" : nameText.Trim();
        selectedBody.mass = mass;
        selectedBody.Body.mass = mass;
        selectedBody.Body.WakeUp();
        FinishEdit("Name and mass applied. Position and velocity were preserved.");
    }

    void ApplyPosition()
    {
        if (!CanEditSelected() || !ReadVector(positionText, "Position", out Vector3 position))
        return;

        selectedBody.Body.position = position;

        selectedBody.transform.position = position;

        Physics.SyncTransforms();

        selectedBody.Body.WakeUp();
        foreach (TrailRenderer trail in selectedBody.GetComponentsInChildren<TrailRenderer>())
        trail.Clear();
        FinishEdit("Body teleported. velocity was Preserved; Old Trails Cleared.");

    }
    void ApplyVelocity()
    {
        if (!CanEditSelected() || !ReadVector(velocityText, "Velocity", out Vector3 velocity))
        return;
        if (selectedBody.Body.isKinematic)
        {
        message = "Turn off Is Kinematic on this body's Rigidbody before velocity setup.";
        return;
        }

        selectedBody.initialVelocity = velocity;
        selectedBody.Body.linearVelocity = velocity;
        selectedBody.Body.WakeUp();
        FinishEdit("Velocity was applied once. MassBody continues to calculate the gravity as usual outcomes.");

    }

    void AddPlanet()
    {
        if (newPlanetMaterial == null)
        {
            message = "Assign a material in the inspector first.";
            return;

        }

        if (!ReadMass(out float mass)
        || !ReadVector(positionText, "Position", out Vector3 position) ||
        !ReadVector(velocityText, "Velocity", out Vector3 velocity) || !ReadNumber(visualRadiusText, "Visual radius", out float radius))
        return;

        if (radius < 0.0001f || radius > 100000f)
        {
            message = "Visual Radius must range from 0.0001 to 100000 Unity units.";
            return;
        }

        GameObject root = new GameObject (string.IsNullOrWhiteSpace(nameText) ? "Planet" : nameText.Trim());
        root.SetActive(false);
        root.transform.position = position;
        Rigidbody rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = false;
        rigidbody.linearDamping = 0f;
        rigidbody.angularDamping = 0f;
        rigidbody.constraints = RigidbodyConstraints.None;
        rigidbody.mass = mass;

        MassBody body = root.AddComponent<MassBody>();
        body.mass = mass;
        body.initialVelocity = velocity;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "PlanetVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * (2f * radius);
        visual.GetComponent<MeshRenderer>().sharedMaterial = newPlanetMaterial;
        Collider visualCollider = visual.GetComponent<Collider>();
        visualCollider.enabled = false;
        Destroy(visualCollider);

        root.SetActive(true);
        rigidbody.linearVelocity = velocity;
        selectedBody = body;
        LoadSelected();
        FinishEdit("Added a planet. The sphere radius is visual only, thus collisions are not enabled.");




        
    }

    

    static string TextField(string id, string label, string value)
    {
        GUILayout.Label(label);
        GUI.SetNextControlName("PlanetEditor." + id);
        return GUILayout.TextField(value, 64);
    }

    static void VectorFields(string id, string label, string[] values)
    {
        GUILayout.Label(label);
        GUILayout.BeginHorizontal();
        string[] axes = { "X", "Y", "Z" };
        for (int i = 0; i < 3; i++)
        {
            GUILayout.Label(axes[i], GUILayout.Width(12f));
            GUI.SetNextControlName("PlanetEditor." + 1f + axes[i]);
            values[i] = GUILayout.TextField(values[i], 32, GUILayout.MinWidth(45f));

        }
        GUILayout.EndHorizontal();
    }

    void OnGUI()
    {
        Rect rectangle = PanelRect;

        if (Event.current.type == EventType.MouseDown && !rectangle.Contains(Event.current.mousePosition))
        {
            GUI.FocusControl(null);
            editingText = false;
        }

        GUILayout.BeginArea(rectangle, GUI.skin.box);
        GUILayout.Label("Planet ediot - runtime changes");
        GUILayout.Label("Mass: simulation units. Position: Unity units.");
        GUILayout.Label("Velocity: Unity units / simulation second.");

        if (simulationPanel != null && GUILayout.Button("toggle simulation pause"))
        simulationPanel.TogglePause();

        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("Select any active mass object:");
        foreach (MassBody body in bodies)
        {
            if (!Available(body))
            continue;
            if (GUILayout.Button((body == selectedBody ? "> " : "") + body.name))
            {
                selectedBody = body;
                LoadSelected();
            }
        }

        GUILayout.Space(8f);
        GUILayout.Label("Selected: " + (Available(selectedBody) ? selectedBody.name : "None"));
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && Available(selectedBody);
        if (GUILayout.Button("Reload current values"))
        LoadSelected();
        GUI.enabled = previousEnabled;

        nameText = TextField("Name", "Name", nameText);
        massText = TextField("Mass", "Mass (scientific notation allowed)", massText);
        GUI.enabled = previousEnabled && Available(selectedBody);
        if (GUILayout.Button("Apply name and mass"))
        ApplyProperties();
        GUI.enabled = previousEnabled;

        VectorFields("Position", "World Position (root Rigidbody)", positionText);
        GUI.enabled = previousEnabled && Available(selectedBody);
        if (GUILayout.Button("Teleport selected body"))
        ApplyPosition();
        GUI.enabled = previousEnabled;

        VectorFields("Velocity", "Velocity in the simulation's World Frame", velocityText);
        GUI.enabled = previousEnabled && Available(selectedBody);
        if (GUILayout.Button("Set selected velocity"))
        ApplyVelocity();
        if (godCamera != null && GUILayout.Button("Focus camera on the selected body"))
        {
            GUI.FocusControl(null);
            editingText = false;
            godCamera.FocusBody(selectedBody);

        }
        GUI.enabled = previousEnabled;

        GUILayout.Space(8f);
        GUILayout.Label("Create a new body using the fields above:");
        visualRadiusText = TextField("Radius", "New sphere's VISUAL radius only", visualRadiusText);
        if (newPlanetMaterial == null)
        GUILayout.Label("Assign new Planet Material to enable creation.");
        GUI.enabled = previousEnabled && newPlanetMaterial != null;
        if (GUILayout.Button("Add new Planet"))
        AddPlanet();
        GUI.enabled = previousEnabled;

        GUILayout.Space(8f);
        GUILayout.Label(message);
        GUILayout.Label("Pause to arrange the bodies. Click outside this panel to release the text focus.");
        GUILayout.Label("Manual changes alter the experiment's energy and momentum.");
        GUILayout.Label("Changes during Play are not saved when Play Ends.");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        string focused = GUI.GetNameOfFocusedControl();
        editingText = !string.IsNullOrEmpty(focused) && focused.StartsWith("PlanetEditor.");

    }

    void OnDisable()
    {
        if (activePanel == this)
        activePanel = null; 
        editingText = false;
    }
}