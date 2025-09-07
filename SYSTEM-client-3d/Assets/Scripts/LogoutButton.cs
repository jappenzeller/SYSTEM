using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SYSTEM.Game;

public class LogoutButton : MonoBehaviour
{
    private Button logoutButton;
    
    void Start()
    {
        // Try to find the button component
        logoutButton = GetComponent<Button>();
        if (logoutButton == null)
        {
            Debug.LogError("LogoutButton requires a Button component!");
            return;
        }
        
        // Add click listener
        logoutButton.onClick.AddListener(HandleLogout);
    }
    
    void OnDestroy()
    {
        if (logoutButton != null)
        {
            logoutButton.onClick.RemoveListener(HandleLogout);
        }
    }
    
    private void HandleLogout()
    {
        // Debug.Log("Logout button clicked");
        
        // Check if shift is held using the new Input System
        bool shiftHeld = Keyboard.current?.leftShiftKey.isPressed ?? false;
        
        // Optionally show a confirmation dialog
        if (shiftHeld)
        {
            // Holding shift = instant logout
            GameManager.Logout();
        }
        else
        {
            // Normal click = show confirmation (for now just logout)
            GameManager.Logout();
        }
    }
}