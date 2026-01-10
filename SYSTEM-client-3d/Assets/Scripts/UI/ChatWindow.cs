using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using SYSTEM.Game;

namespace SYSTEM.UI
{
    /// <summary>
    /// Controls the Chat window for sending messages to QAI.
    /// Press G to toggle the chat window.
    /// Type a message and click "Chat" to send it.
    /// If within 15 units of QAI, QAI will respond with a chat bubble.
    /// </summary>
    public class ChatWindow : MonoBehaviour
    {
        private UIDocument uiDocument;
        private VisualElement chatWindow;
        private Button closeButton;
        private Button sendButton;
        private TextField chatInput;
        private bool isVisible = false;

        private SYSTEM.Debug.CursorController cursorController;
        private PlayerController playerController;

        void Start()
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[ChatWindow] UIDocument component not found!");
                return;
            }

            cursorController = Object.FindFirstObjectByType<SYSTEM.Debug.CursorController>();
            playerController = Object.FindFirstObjectByType<PlayerController>();
            InitializeUI();
        }

        void InitializeUI()
        {
            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[ChatWindow] Root visual element is null!");
                return;
            }

            chatWindow = root.Q<VisualElement>("chat-window");
            closeButton = root.Q<Button>("close-button");
            sendButton = root.Q<Button>("send-button");
            chatInput = root.Q<TextField>("chat-input");

            if (closeButton != null)
            {
                closeButton.RegisterCallback<ClickEvent>(evt => Hide());
            }

            if (sendButton != null)
            {
                sendButton.RegisterCallback<ClickEvent>(evt => SendMessage());
            }

            // Allow Enter key to send message
            if (chatInput != null)
            {
                chatInput.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        SendMessage();
                        evt.StopPropagation();
                    }
                });
            }

            SystemDebug.Log(SystemDebug.Category.Network, "[ChatWindow] Initialized - Press G to toggle chat");
        }

        void Update()
        {
            if (Keyboard.current == null) return;

            // G to toggle chat window
            if (Keyboard.current.gKey.wasPressedThisFrame)
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
            if (chatWindow == null) return;

            chatWindow.RemoveFromClassList("hidden");
            isVisible = true;

            // Unlock cursor so player can interact with the window
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Disable camera/player input directly
            if (playerController == null)
            {
                playerController = Object.FindFirstObjectByType<PlayerController>();
            }
            if (playerController != null)
            {
                playerController.enableMouseLook = false;
                playerController.SetInputEnabled(false);
            }

            // Clear previous input and focus
            if (chatInput != null)
            {
                chatInput.value = "";
                chatInput.Focus();
            }

            SystemDebug.Log(SystemDebug.Category.Network, "[ChatWindow] Opened");
        }

        public void Hide()
        {
            if (chatWindow == null) return;

            chatWindow.AddToClassList("hidden");
            isVisible = false;

            // Lock cursor back to game controls
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Re-enable camera/player input
            if (playerController == null)
            {
                playerController = Object.FindFirstObjectByType<PlayerController>();
            }
            if (playerController != null)
            {
                playerController.enableMouseLook = true;
                playerController.SetInputEnabled(true);
            }

            SystemDebug.Log(SystemDebug.Category.Network, "[ChatWindow] Closed");
        }

        private void SendMessage()
        {
            if (chatInput == null) return;

            string message = chatInput.value?.Trim();
            if (string.IsNullOrEmpty(message))
            {
                Hide();
                return;
            }

            // Send message to server
            try
            {
                if (GameManager.Instance != null && GameManager.IsConnected())
                {
                    // Call the server reducer to send player chat message
                    GameManager.Conn.Reducers.SendPlayerChat(message);
                    SystemDebug.Log(SystemDebug.Category.Network, $"[ChatWindow] Sent message: {message}");
                }
                else
                {
                    SystemDebug.LogWarning(SystemDebug.Category.Network, "[ChatWindow] Not connected to server");
                }
            }
            catch (System.Exception ex)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, $"[ChatWindow] Failed to send message: {ex.Message}");
            }

            // Close window after sending
            Hide();
        }

        public bool IsVisible => isVisible;
    }
}
