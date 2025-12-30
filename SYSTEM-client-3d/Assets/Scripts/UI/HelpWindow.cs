using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace SYSTEM.UI
{
    /// <summary>
    /// Controls the Help window that displays game controls.
    /// Press F1 to toggle the help window.
    /// A persistent "F1 - Help" hint is always visible in the top-left corner.
    /// </summary>
    public class HelpWindow : MonoBehaviour
    {
        private UIDocument uiDocument;
        private VisualElement helpWindow;
        private Button closeButton;
        private bool isVisible = false;

        private SYSTEM.Debug.CursorController cursorController;

        void Start()
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                SystemDebug.LogError(SystemDebug.Category.EventBus, "[HelpWindow] UIDocument component not found!");
                return;
            }

            cursorController = Object.FindFirstObjectByType<SYSTEM.Debug.CursorController>();
            InitializeUI();
        }

        void InitializeUI()
        {
            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                SystemDebug.LogError(SystemDebug.Category.EventBus, "[HelpWindow] Root visual element is null!");
                return;
            }

            helpWindow = root.Q<VisualElement>("help-window");
            closeButton = root.Q<Button>("close-button");

            if (closeButton != null)
            {
                closeButton.RegisterCallback<ClickEvent>(evt => Hide());
            }

            SystemDebug.Log(SystemDebug.Category.EventBus, "[HelpWindow] Initialized - Press F1 to toggle help");
        }

        void Update()
        {
            if (Keyboard.current == null) return;

            // F1 to toggle help window
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                Toggle();
            }

            // Escape to close if visible
            if (isVisible && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
        }

        public void Toggle()
        {
            if (isVisible)
                Hide();
            else
                Show();
        }

        public void Show()
        {
            if (helpWindow == null) return;

            helpWindow.RemoveFromClassList("hidden");
            isVisible = true;

            // Unlock cursor so player can interact with the window
            if (cursorController != null)
            {
                cursorController.ForceUnlock();
            }

            SystemDebug.Log(SystemDebug.Category.EventBus, "[HelpWindow] Opened");
        }

        public void Hide()
        {
            if (helpWindow == null) return;

            helpWindow.AddToClassList("hidden");
            isVisible = false;

            // Lock cursor back to game controls
            if (cursorController != null)
            {
                cursorController.ForceLock();
            }

            SystemDebug.Log(SystemDebug.Category.EventBus, "[HelpWindow] Closed");
        }

        public bool IsVisible => isVisible;
    }
}
