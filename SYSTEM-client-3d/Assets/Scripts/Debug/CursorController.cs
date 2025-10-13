using UnityEngine;
using UnityEngine.InputSystem;

namespace SYSTEM.Debug
{
    /// <summary>
    /// Simple cursor lock/unlock with Tab key
    /// Allows player to interact with UI windows
    /// </summary>
    public class CursorController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool startLocked = true;

        private bool cursorLocked = true;
        private PlayerController playerController;

        void Start()
        {
            // Find the local player controller (may be null if player spawns later)
            playerController = FindFirstObjectByType<PlayerController>();

            if (startLocked)
                LockCursor();
            else
                UnlockCursor();
        }

        void Update()
        {
            // Check if keyboard exists
            if (Keyboard.current == null)
            {
                return; // No keyboard available
            }

            // Toggle with Tab
            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                if (cursorLocked)
                    UnlockCursor();
                else
                    LockCursor();
            }

            // Quick unlock with Escape (safety)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                UnlockCursor();
            }
        }

        void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorLocked = true;

            // Enable camera control when cursor is locked
            if (playerController != null)
            {
                playerController.enableMouseLook = true;
                playerController.SetInputEnabled(true);
                SystemDebug.Log(SystemDebug.Category.Input, "Cursor locked - camera controls enabled");
            }
            else
            {
                SystemDebug.LogWarning(SystemDebug.Category.Input, "Cursor locked but PlayerController not found");
            }

            SystemDebug.Log(SystemDebug.Category.Input, "Cursor locked - Press Tab to unlock");
        }

        void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorLocked = false;

            // Disable camera control when cursor is unlocked
            // Lazy-find pattern: handle cases where PlayerController spawns after Start()
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerController>();
                if (playerController == null)
                {
                    SystemDebug.LogWarning(SystemDebug.Category.Input, "Cannot find PlayerController to disable mouse look");
                    return;
                }
            }

            playerController.enableMouseLook = false;
            playerController.SetInputEnabled(false);

            SystemDebug.Log(SystemDebug.Category.Input, "Cursor unlocked - camera controls disabled - Press Tab to lock");
        }

        // Allow UI windows to unlock cursor
        public void ForceUnlock()
        {
            UnlockCursor();
        }

        // Allow UI windows to lock cursor
        public void ForceLock()
        {
            LockCursor();
        }
    }
}
