using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TEMPORARY: Simple test script to verify R key input is working.
/// Attach this to any GameObject in the scene to test.
/// DELETE THIS FILE after testing is complete.
/// </summary>
public class TestRKeyInput : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("[TestRKeyInput] ===== R KEY INPUT TEST STARTED =====");
        Debug.Log("[TestRKeyInput] This script will test if R key input is detected");
    }

    void Update()
    {
        // Test raw keyboard input
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Debug.Log("═══════════════════════════════════════════════════════");
            Debug.Log("[TestRKeyInput] ⌨️ R KEY PRESSED (detected via Keyboard.current)");
            Debug.Log("═══════════════════════════════════════════════════════");
        }
    }
}
