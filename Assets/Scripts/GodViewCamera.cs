
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1100)]
public class GodViewCamera : MonoBehaviour
{

    [Header("Optional references")]
    public SimulationPanel simulationPanel;

    public MassBody focusedTarget;

    public float flySpeed = 5f;
    public float boostMultiplier = 10f;
    
    public float precisionMultiplier = 0.1f;
    public float lookSensitivity = 0.15f;
    
    public float initialDistance = 20f;
    
    public float minimumDistance = 0.02f;
    public float maximumDistance = 100000f;
    
    public float zoomSensitivity = 0.2f;
    public bool followFocusedBody = true;

    public bool blockClockPanel = true;
    public Rect clockPanel = new Rect(12, 12, 460, 220);

    public float scrollUnitsPerNotch = 120f;    

    Camera view;

    Vector3 pivot;
    Vector3 previousBodyPosition;

    float distance;
    float yaw; 
    float pitch;
    bool looking;
     
    MassBody following;

     struct Controls
    {
        public Vector2 pointer;
        public Vector2 delta;
        public Vector3 movement;

        public float wheel;
        
        public bool rightPressed;
        public bool rightHeld;
        public bool middleHeld;
        public bool leftHeld;
        
        public bool alt;
        
        public bool boost;
        public bool precision;
        public bool focus;
        
        public bool frameAll;
        public bool next;
        public bool escape;
       }

    
    
       // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        view = GetComponent<Camera>();
        view.orthographic = false;

        minimumDistance = Mathf.Max(0.0001f, minimumDistance);

        maximumDistance = Mathf.Max(minimumDistance, maximumDistance);

        distance = Mathf.Clamp(initialDistance, minimumDistance, maximumDistance);
        flySpeed = Mathf.Clamp(flySpeed, 0.001f, 100000f);

        yaw = transform.eulerAngles.y;

        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.x), -89f, 89f);

        pivot = transform.position + Rotation() * Vector3.forward * distance;

        if (simulationPanel == null)
        {
            simulationPanel = FindFirstObjectByType<SimulationPanel>();

        }

        if (Available(focusedTarget))
        FocusBody(focusedTarget);
    }



    // Update is called once per frame
    void Update()
    {
        
        if (!Application.isFocused)
        return;
        
        Controls input = ReadControls();

        if (!input.rightHeld) 
        ReleaseMouse();

        if (input.escape)
        {
            ReleaseMouse();
            return;
        }

        if (PlanetEditorPanel.BlocksCamera(input.pointer))
        {
            ReleaseMouse();
            return;
        }

        if (input.frameAll)
        FrameAllBodies();

        if (input.next)
        FocusNextBody();

        if (input.focus)
        {
            MassBody body = focusedTarget;

            if (!Available(body) && simulationPanel != null)
            body = simulationPanel.observedBody;

            if (Available(body))
            FocusBody(body);
            else
            FrameAllBodies();
        
        }
        bool overUI = IsOverInterface(input.pointer);

        if (input.rightPressed && !overUI)
        {
            looking = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            input.delta = Vector2.zero;

        }

        if (looking)
        {
            Vector3 CameraPosition = pivot - Rotation() * Vector3.forward * distance;

            Rotate(input.delta);

            pivot = CameraPosition + Rotation() * Vector3.forward * distance;

            following = null;

            if (input.wheel != 0f)
            {
                flySpeed = Mathf.Clamp(flySpeed * Mathf.Exp(input.wheel * 0.35f), 0.001f, 100000f);
            }

            Vector3 movement = Rotation() * new Vector3(input.movement.x, 0f, input.movement.z) + Vector3.up * input.movement.y;

            float multiplier = input.boost ? boostMultiplier : 1f;

            if (input.precision)
            multiplier *= precisionMultiplier;

            pivot += Vector3.ClampMagnitude(movement, 1f) * flySpeed * multiplier * Time.unscaledDeltaTime;

        }

        else if (!overUI)
        {
            if (input.alt && input.leftHeld)
            Rotate(input.delta);

            if (input.middleHeld)
            {
                float worldUnitsPerPixel = 2f * distance * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, view.pixelHeight);

                Quaternion rotation = Rotation();

                pivot -= 
                (rotation * Vector3.right * input.delta.x + rotation * Vector3.up * input.delta.y) * worldUnitsPerPixel;

                following = null;

            }

            distance = Mathf.Clamp(distance * Mathf.Exp(-input.wheel * zoomSensitivity), minimumDistance, maximumDistance);
        }        
    }

    void LateUpdate()
    {
        if (view == null)
        return;

        if (Available(following) && followFocusedBody)
        {
            Vector3 position = Centre(following);
            pivot += position - previousBodyPosition;

            previousBodyPosition = position;

        }
        else
        {
            following = null;
        }

        Quaternion rotation = Rotation();

        transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);

        view.nearClipPlane = Mathf.Clamp(distance * 0.001f, 0.0001f, 1f);

        view.farClipPlane = Mathf.Clamp(distance * 100f, 1000f, 1000000f);
    }

    void Rotate(Vector2 delta)
    {
        yaw = Mathf.Repeat(yaw + delta.x * lookSensitivity, 360f);

        pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -89f, 89f);
    }

    Quaternion Rotation()
    {
        return Quaternion.Euler(pitch, yaw, 0f);

    }

    static Vector3 Centre(MassBody body)
    {
        return body.Body != null ? body.Body.worldCenterOfMass : body.transform.position;
            }

            static Bounds VisibleBounds(MassBody body)
    {
        Bounds bounds = new Bounds(Centre(body), Vector3.one * 0.1f);
        bool found = false;

        foreach ( Renderer renderer in body.GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;

            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    void FrameBounds(Bounds bounds)
    {
        float verticalHalfAngle = view.fieldOfView * Mathf.Deg2Rad * 0.5f;

        float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * Mathf.Max(0.01f, view.aspect));

        float limitingHalfAngle = Mathf.Min(verticalHalfAngle, horizontalHalfAngle);

        float radius = Mathf.Max(0.01f, bounds.extents.magnitude);

        pivot = bounds.center;

        distance = Mathf.Clamp(
            radius / Mathf.Sin(limitingHalfAngle) * 1.2f, minimumDistance, maximumDistance);

    }

    public void FocusBody(MassBody body)
    {
        if (view == null || !Available(body)) 
        return;

        focusedTarget = body;

        FrameBounds(VisibleBounds(body));

        following = followFocusedBody ? body : null;
        previousBodyPosition = Centre(body);

    }

    public void FrameAllBodies()
    {
        if (view == null)
        return;
        bool found = false;
        Bounds bounds = new Bounds();

        foreach (MassBody body in MassBody.All)
        {
            if (!Available(body))
            continue;

            Bounds next = VisibleBounds(body);

            if (!found)
            {
                bounds = next;
                found = true;
            
            }
            else
            {
                bounds.Encapsulate(next);

            }
        }

        if (found)
        {
            following = null;
            focusedTarget = null;

            FrameBounds(bounds);
        }
    }

    public void FocusNextBody()
    {
        int count = MassBody.All.Count;
        int start = MassBody.All.IndexOf(focusedTarget);

        for (int offset = 1; offset <= count; offset++)
        {
            int index = (start + offset + count) % count;

            MassBody body = MassBody.All[index];

            if (Available(body))
            {
                FocusBody(body);
                return;
            }
        }
    }

    bool IsOverInterface(Vector2 screenPosition)
    {
        Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);

        if (simulationPanel != null && simulationPanel.isActiveAndEnabled && simulationPanel.panel.Contains(guiPosition))
        {
            return true;

        }

        return blockClockPanel && clockPanel.Contains(guiPosition);
    }

    Controls ReadControls()
{
    Controls result = new Controls();

#if ENABLE_INPUT_SYSTEM
    Mouse mouse = Mouse.current;
    Keyboard keyboard = Keyboard.current;

    if (mouse != null)
    {
        result.pointer = mouse.position.ReadValue();
        result.delta = mouse.delta.ReadValue();

        result.wheel = Mathf.Clamp(
            mouse.scroll.ReadValue().y /
            Mathf.Max(1f, scrollUnitsPerNotch),
            -4f,
            4f
        );

        result.rightPressed =
            mouse.rightButton.wasPressedThisFrame;

        result.rightHeld = mouse.rightButton.isPressed;
        result.middleHeld = mouse.middleButton.isPressed;
        result.leftHeld = mouse.leftButton.isPressed;
    }

    if (keyboard != null)
    {
        result.movement = new Vector3(
            (keyboard.dKey.isPressed ? 1f : 0f) -
            (keyboard.aKey.isPressed ? 1f : 0f),

            (keyboard.eKey.isPressed ? 1f : 0f) -
            (keyboard.qKey.isPressed ? 1f : 0f),

            (keyboard.wKey.isPressed ? 1f : 0f) -
            (keyboard.sKey.isPressed ? 1f : 0f)
        );

        result.alt =
            keyboard.leftAltKey.isPressed ||
            keyboard.rightAltKey.isPressed;

        result.boost =
            keyboard.leftShiftKey.isPressed ||
            keyboard.rightShiftKey.isPressed;

        result.precision =
            keyboard.leftCtrlKey.isPressed ||
            keyboard.rightCtrlKey.isPressed;

        result.focus = keyboard.fKey.wasPressedThisFrame;
        result.frameAll = keyboard.homeKey.wasPressedThisFrame;
        result.next = keyboard.tabKey.wasPressedThisFrame;
        result.escape = keyboard.escapeKey.wasPressedThisFrame;
    }

#elif ENABLE_LEGACY_INPUT_MANAGER
    result.pointer = Input.mousePosition;

    result.delta = new Vector2(
        Input.GetAxisRaw("Mouse X"),
        Input.GetAxisRaw("Mouse Y")
    ) * 10f;

    result.wheel = Mathf.Clamp(
        Input.mouseScrollDelta.y,
        -4f,
        4f
    );

    result.rightPressed = Input.GetMouseButtonDown(1);
    result.rightHeld = Input.GetMouseButton(1);
    result.middleHeld = Input.GetMouseButton(2);
    result.leftHeld = Input.GetMouseButton(0);

    result.movement = new Vector3(
        (Input.GetKey(KeyCode.D) ? 1f : 0f) -
        (Input.GetKey(KeyCode.A) ? 1f : 0f),

        (Input.GetKey(KeyCode.E) ? 1f : 0f) -
        (Input.GetKey(KeyCode.Q) ? 1f : 0f),

        (Input.GetKey(KeyCode.W) ? 1f : 0f) -
        (Input.GetKey(KeyCode.S) ? 1f : 0f)
    );

    result.alt =
        Input.GetKey(KeyCode.LeftAlt) ||
        Input.GetKey(KeyCode.RightAlt);

    result.boost =
        Input.GetKey(KeyCode.LeftShift) ||
        Input.GetKey(KeyCode.RightShift);

    result.precision =
        Input.GetKey(KeyCode.LeftControl) ||
        Input.GetKey(KeyCode.RightControl);

    result.focus = Input.GetKeyDown(KeyCode.F);
    result.frameAll = Input.GetKeyDown(KeyCode.Home);
    result.next = Input.GetKeyDown(KeyCode.Tab);
    result.escape = Input.GetKeyDown(KeyCode.Escape);
#endif

    return result;
}

static bool Available(MassBody body)
    {
        return body != null && body.isActiveAndEnabled;
    }

    void ReleaseMouse()
    {
        if (!looking)
        return;
        looking = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused)
        ReleaseMouse();
    }

    void OnDisable()
    {
        ReleaseMouse();
    }
}
