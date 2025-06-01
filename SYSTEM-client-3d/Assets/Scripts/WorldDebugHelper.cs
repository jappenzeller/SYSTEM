using UnityEngine;
using UnityEngine.InputSystem;
using SpacetimeDB.Types;

/// <summary>
/// Debug helper to diagnose world scene issues - Updated for new Input System
/// </summary>
public class WorldDebugHelper : MonoBehaviour
{
    private string debugLog = "";
    // Input System actions
    private InputAction refreshDebugAction;
    private InputAction debugCameraYawAction;
    private InputAction debugCameraPitchAction;

    [Header("Debug Camera Controls")]
    public float debugCameraRotationSpeed = 60.0f;
    private bool initialDebugLogged = false; // Declare as a class field
    
    void Start()
    {
       // DebugWorldState();
        SetupInputActions();
    }

    void SetupInputActions()
    {
        // Create input actions for debug controls
        refreshDebugAction = new InputAction("RefreshDebug", InputActionType.Button, "<Keyboard>/o");

        debugCameraYawAction = new InputAction("DebugCameraYaw", InputActionType.Value, "<Keyboard>/leftArrow");
        debugCameraYawAction.AddBinding("<Keyboard>/rightArrow").WithProcessor("invert"); // Right arrow for positive

        debugCameraPitchAction = new InputAction("DebugCameraPitch", InputActionType.Value, "<Keyboard>/downArrow");
        debugCameraPitchAction.AddBinding("<Keyboard>/upArrow").WithProcessor("invert"); // Up arrow for positive

        // Enable all actions
        refreshDebugAction.Enable();
        debugCameraYawAction.Enable();
        debugCameraPitchAction.Enable();
    }
    
    void OnEnable()
    {
        refreshDebugAction?.Enable();
        debugCameraYawAction?.Enable();
        debugCameraPitchAction?.Enable();
    }
    
    void OnDisable()
    {
        refreshDebugAction?.Disable();
        debugCameraYawAction?.Disable();
        debugCameraPitchAction?.Disable();
    }
    
    void DebugWorldState()
    {
        debugLog = "=== WORLD DEBUG INFO ===\n";
        
        // Check GameData
        if (GameData.Instance != null)
        {
            var coords = GameData.Instance.GetCurrentWorldCoords();
            debugLog += $"GameData Present - World: ({coords.X},{coords.Y},{coords.Z})\n";
            debugLog += $"Username: {GameData.Instance.Username}\n";
            debugLog += $"IsLoggedIn: {GameData.Instance.IsLoggedIn}\n";
        }
        else
        {
            debugLog += "ERROR: GameData.Instance is NULL!\n";
        }
        
        // Check GameManager connection
        if (GameManager.Instance != null)
        {
            debugLog += $"GameManager Present - Connected: {GameManager.IsConnected()}\n";
            if (GameManager.LocalIdentity != null)
            {
                debugLog += $"Local Identity: {GameManager.LocalIdentity}\n";
            }
            
            // Check for player in database
            if (GameManager.IsConnected())
            {
                var player = GameManager.Instance.GetCurrentPlayer();
                if (player != null)
                {
                    debugLog += $"Player Found: {player.Name} at ({player.Position.X},{player.Position.Y},{player.Position.Z})\n";
                    debugLog += $"Player World: ({player.CurrentWorld.X},{player.CurrentWorld.Y},{player.CurrentWorld.Z})\n";
                }
                else
                {
                    debugLog += "ERROR: No player found in database!\n";
                }
            }
        }
        else
        {
            debugLog += "ERROR: GameManager.Instance is NULL!\n";
        }
        
        // Check WorldManager
        var worldManager = FindFirstObjectByType<WorldManager>();
        if (worldManager != null)
        {
            debugLog += "WorldManager Found\n";
        }
        else
        {
            debugLog += "ERROR: No WorldManager in scene!\n";
        }
        
        // Check for existing players
        var playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        debugLog += $"PlayerController count in scene: {playerControllers.Length}\n";
        foreach (var pc in playerControllers)
        {
            debugLog += $"  - Player: {pc.name} at {pc.transform.position}\n";
        }
        
        // Check camera
        if (Camera.main != null)
        {
            debugLog += $"Main Camera Found at: {Camera.main.transform.position}\n";
            debugLog += $"  - Rotation (Euler): {Camera.main.transform.eulerAngles}\n";
            debugLog += $"  - Forward: {Camera.main.transform.forward}\n";
            if (Camera.main.transform.parent != null)
            {
                debugLog += $"  - Parent: {Camera.main.transform.parent.name}\n";
                debugLog += $"  - Local Position: {Camera.main.transform.localPosition}\n";
                debugLog += $"  - Local Rotation (Euler): {Camera.main.transform.localEulerAngles}\n";
            }
            else
            {
                debugLog += "  - Parent: None\n";
            }
        }
        else
        {
            debugLog += "ERROR: No main camera found!\n";
        }
        
        debugLog += "--- End of World Debug Info ---\n";
        Debug.Log(debugLog);
    }
    
    void Update()
    {

        if (!initialDebugLogged && Camera.main != null)
        {
            Debug.Log("[WorldDebugHelper.Update] Camera.main is now available. Performing initial DebugWorldState.");
            DebugWorldState();
            initialDebugLogged = true;
        }
        // Check for input using new Input System
        if (refreshDebugAction != null && refreshDebugAction.WasPressedThisFrame())
        {
            Debug.Log("'O' key pressed, refreshing debug world state to console.");
            DebugWorldState();
        }

        HandleDebugCameraRotation();
    }

    void HandleDebugCameraRotation()
    {
        if (Camera.main == null) return;

        float yawInput = debugCameraYawAction.ReadValue<float>();
        float pitchInput = debugCameraPitchAction.ReadValue<float>();

        // Handle Yaw (rotating the camera's parent, typically the player)
        if (Mathf.Abs(yawInput) > 0.01f)
        {
            if (Camera.main.transform.parent != null)
            {
                // Multiplying yawInput by -1 because rightArrow is positive due to "invert" processor,
                // and we want positive rotation for right.
                Camera.main.transform.parent.Rotate(Vector3.up, -yawInput * debugCameraRotationSpeed * Time.deltaTime);
            }
            // else if you want to yaw an unparented camera:
            // Camera.main.transform.Rotate(Vector3.up, -yawInput * debugCameraRotationSpeed * Time.deltaTime, Space.World);
        }

        // Handle Pitch (rotating the camera locally)
        if (Mathf.Abs(pitchInput) > 0.01f)
        {
            Transform camTransform = Camera.main.transform;
            float currentLocalPitch = camTransform.localEulerAngles.x;
            if (currentLocalPitch > 180f) currentLocalPitch -= 360f; // Normalize to -180 to 180

            // pitchInput is positive for Up Arrow. We want Up Arrow to pitch camera up.
            // Rotating around local X-axis (transform.right): negative rotation pitches up.
            // Multiplying pitchInput by -1 because upArrow is positive due to "invert" processor.
            float pitchDelta = -pitchInput * debugCameraRotationSpeed * Time.deltaTime;
            currentLocalPitch -= pitchDelta; 

            currentLocalPitch = Mathf.Clamp(currentLocalPitch, -89.0f, 89.0f);
            camTransform.localRotation = Quaternion.Euler(currentLocalPitch, 0, 0); // Assumes camera's local yaw/roll is 0
        }
    }
    
    void OnDestroy()
    {
        // Clean up input actions
        refreshDebugAction?.Disable();
        debugCameraYawAction?.Disable();
        debugCameraPitchAction?.Disable();

        refreshDebugAction?.Dispose();
        debugCameraYawAction?.Dispose();
        debugCameraPitchAction?.Dispose();
    }
}