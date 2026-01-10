using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages chat bubble display above players.
    /// Listens for BroadcastMessageReceivedEvent and displays messages
    /// in "slow mode" - one phrase at a time, 2 seconds each.
    /// </summary>
    public class ChatBubbleController : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("How long each phrase is displayed (seconds)")]
        public float phraseDisplayTime = 2.0f;

        // Queue of phrases for each player (by player ID)
        private Dictionary<ulong, Queue<string>> _playerPhraseQueues = new();

        // Currently displaying phrase for each player
        private Dictionary<ulong, Coroutine> _activeDisplays = new();

        // Cached reference to WorldManager
        private WorldManager _worldManager;

        void OnEnable()
        {
            GameEventBus.Instance.Subscribe<BroadcastMessageReceivedEvent>(OnBroadcastMessageReceived);
        }

        void OnDisable()
        {
            GameEventBus.Instance.Unsubscribe<BroadcastMessageReceivedEvent>(OnBroadcastMessageReceived);
        }

        void OnBroadcastMessageReceived(BroadcastMessageReceivedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[ChatBubble] Received message from player {evt.SenderPlayerId}: {evt.Content}");

            // Split message into phrases
            var phrases = SplitIntoPhrases(evt.Content);
            if (phrases.Count == 0) return;

            // Queue phrases for this player
            if (!_playerPhraseQueues.ContainsKey(evt.SenderPlayerId))
            {
                _playerPhraseQueues[evt.SenderPlayerId] = new Queue<string>();
            }

            foreach (var phrase in phrases)
            {
                _playerPhraseQueues[evt.SenderPlayerId].Enqueue(phrase);
            }

            // Start display if not already running for this player
            if (!_activeDisplays.ContainsKey(evt.SenderPlayerId))
            {
                _activeDisplays[evt.SenderPlayerId] = StartCoroutine(DisplayPhrases(evt.SenderPlayerId));
            }
        }

        /// <summary>
        /// Split a message into phrases on punctuation marks (. ! ? , ;)
        /// </summary>
        private List<string> SplitIntoPhrases(string message)
        {
            var phrases = new List<string>();

            // Split on natural pause points - periods, commas, and other punctuation
            // Keep the punctuation with the phrase
            var parts = Regex.Split(message, @"(?<=[.!?,;])\s+");

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    phrases.Add(trimmed);
                }
            }

            // If no split occurred (no punctuation), use the whole message
            if (phrases.Count == 0 && !string.IsNullOrWhiteSpace(message))
            {
                phrases.Add(message.Trim());
            }

            return phrases;
        }

        /// <summary>
        /// Coroutine to display phrases one at a time for a player
        /// </summary>
        private IEnumerator DisplayPhrases(ulong playerId)
        {
            // Find the player's GameObject
            var playerController = FindPlayerController(playerId);
            if (playerController == null)
            {
                SystemDebug.LogWarning(SystemDebug.Category.Network,
                    $"[ChatBubble] Player {playerId} not found in world");
                _activeDisplays.Remove(playerId);
                yield break;
            }

            while (_playerPhraseQueues.TryGetValue(playerId, out var queue) && queue.Count > 0)
            {
                var phrase = queue.Dequeue();

                // Display the phrase
                playerController.SetStatus(phrase);
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[ChatBubble] Displaying: \"{phrase}\" for {phraseDisplayTime}s");

                // Wait for display time
                yield return new WaitForSeconds(phraseDisplayTime);
            }

            // Clear status when done
            playerController.ClearStatus();
            _activeDisplays.Remove(playerId);

            // Clean up empty queue
            if (_playerPhraseQueues.ContainsKey(playerId) &&
                _playerPhraseQueues[playerId].Count == 0)
            {
                _playerPhraseQueues.Remove(playerId);
            }
        }

        /// <summary>
        /// Find the PlayerController for a given player ID
        /// </summary>
        private PlayerController FindPlayerController(ulong playerId)
        {
            // Lazy-find WorldManager if not cached
            if (_worldManager == null)
            {
                _worldManager = FindFirstObjectByType<WorldManager>();
            }

            if (_worldManager == null)
            {
                return null;
            }

            var players = _worldManager.GetAllPlayers();
            if (players.TryGetValue(playerId, out var playerObj))
            {
                return playerObj.GetComponent<PlayerController>();
            }

            return null;
        }
    }
}
