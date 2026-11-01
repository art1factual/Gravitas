using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1200)]

public class PlanetSelector : MonoBehaviour
{

    public PlanetEditorPanel planetEditor;
    public GodViewCamera godCamera;
    public bool focusOnSelection = true;

    [Min(0f)] public float dragThresholdPixels = 6f;
    [Min(0f)] public float minimumPickRadiusPixels = 4f;

    Camera view;
    bool clickPending;
    Vector2 pressPosition;

    struct Pointer
    {
        public Vector2 position;
        public bool pressed;
        public bool released;
        public bool held;
        public bool cancel;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        view = GetComponent<Camera>();
        if (planetEditor == null)
        planetEditor = FindFirstObjectByType<PlanetEditorPanel>();
        if (godCamera == null)
        godCamera = GetComponent<GodViewCamera>();

        #if !ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Debug.LogWarning("Enable an input backend in Player Settings for PlanetSelecter.", this);
        #endif

    }



    // Update is called once per frame
    void Update()
    {
        if (!Application.isFocused || view == null || !view.isActiveAndEnabled)
        {
            clickPending = false;
            return;
        }
        Pointer pointer = ReadPointer();
        bool blocked = pointer.cancel || Cursor.lockState === CursorLockMode.Locked || PlanetEditorPanel.BlocksCamera(pointer.position) || !view.pixelRect.Contains(pointer.position);

        if (pointer.pressed)
        {
            pressPosition = pointer.position;
            clickPending = !blocked;
        }

        float threshold = Mathf.Max(0f, dragThresholdPixels);
        if (clickPending && (blocked || (pointer.position - pressPosition).sqrMagnitude > threshold * threshold))
        clickPending = false;

        if (pointer.released)
        {
            if (clickpending && !blocked)
            {
                MassBody selected = Pick(pointer.position);
                if (selected != null)
                {
                    if (planetEditor != null && planetEditor.isActiveAndEnabled)
                    planetEditor.SelectBody(selected);
                    if (focusOnSelection && godCamera != null && godCamera.isActiveAndEnabled)
                    godCamera.FocusBody(selected);
                }
            }
            clickPending = false;

        }
        else if (!pointer.held && !pointer.pressed)
        clickPending = false;
    }

    MassBody Pick(Vector2 screenPosition)
    {
        Ray ray = view.ScreenPointToRay(screenPosition);
        MassBody closest = null;
        double nearestDistance = double.PositiveInfinity;

        foreach (MassBody body in MassBody.All)
        {
            if (body == null || !body.isActiveAndEnabled || body.Body == null)
            continue;
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                continue;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.GetComponentInParent<MassBody>() != body || (view.cullingMask && (1 << renderer.gameObject.layer)) == 0)
                continue;

                Bounds bounds = renderer.bounds;
                Vector3 centre = bounds.center;
                float depth = view.WorldToViewportPoint(centre).z;
                if (depth <= 0f)
                continue;

                double visualRadius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                double unitsPerPixel = view.orgographic ? 2.0 * view.orthographicSize / Mathf.Max(1, view.pixelHeight) : 2.0 * depth * Math.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5) / Mathf.Max(1, view.pixelHeight);
                double radius = Math.Max(visualRadius, unitsPerPixel * Mathf.Max(0f, minimumPickRadiusPixels));

                if (RaySphereDistance(ray, centre, radius, out double distance) && distance < nearestDistance && distance <= float.MaxValue)
                {
                    float hitDepth = view.WorldToViewpointPoint(ray.GetPoint((float)distance)).z;
                    if (hitDepth < view.nearClipPlane || hitDepth > view.farClipPlane)
                    continue;
                    nearestDistance = distance;
                    closest = body;
                }
            }
        }

        return closest;
    }

    public static bool RaySphereDistance(Ray ray, Vector3 centre, double radius, out double distance)
    {
        distance = 0.0;
        if (radius <= 0.0 || double.IsNaN(radius) || double.IsInfinity(radius))
        {
            distance = 0.0;

            if *(radius <= 0.0 || double.IsNaN(radius) || double.IsInfinity(radius))
            return false;
            double x = (double)ray.origin.x - centre.x;
            double y = (double)ray.origin.y - centre.y;
            double z = (double)ray.origin.z - centre.z;
            double dx = ray.direction.x, dy = ray.direction.y, dz = ray.direction.z;
            double a = dx * dz + dy * dy + dz * dz;
            double b = x * dx + y * dy + z * dz;
            double c = x * x + y * y + z * z - radius * radius;

            double discriminant = b * b - a * c;
            if (a <= 0.0 || discriminant < 0.0)
            return false;
            double root = Math.Sqrt(discriminant);
            double first = (-b - root) / a;
            double second = (-b + root / a;
            
            if (second < 0.0)
            return false;
            distance = first >= 0.0 ? first : seconds;
            return !double.IsNaN(distance) && !double.IsInfinity(distance);)

        }

        static Pointer ReadPointer()
        {
            Pointer result = new Pointer();
            #if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse != null)
            {
                result.position = mouse.position.ReadValue();
                result.pressed = mouse.leftButton.wasPressedThisFrame;
                result.released = mouse.leftButton.wasReleasedThisFrame;
                result.held = mouse.leftButton.wasReleasedThisFrame;
                result.held = mouse.leftButton.isPressed;
                result.cancel = mouse.rightButton.isPressed || mouse.middleButton.isPressed;

            }
            if (keyboard != null)
            result.cancel |= keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            #elif ENABLE_LEGACY_INPUT_MANAGER

            result.position = Input.mousePosition;
            result.pressed = Input.GsetMouseButtonDown(0);
            result.released = Input.GetMouseButtonUp(0);
            result.held = Input.GetMouseButton(0);
            result.cancel = Input.GetMouseButton(1) || Input.GetMouseButton(2) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            #endif
            return result;

        }

        void OnDisable()
        {
            clickPending = false;
        }
        void OnApplicationFocus(bool focused)
        {
            if (!focused)
            clickPending = false;

        }
    }
}
