using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.Game;

namespace SYSTEM.Game
{
    public class MiningVisualizationManager : MonoBehaviour
    {
        private static MiningVisualizationManager _instance;
        public static MiningVisualizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MiningVisualizationManager");
                    _instance = go.AddComponent<MiningVisualizationManager>();
                }
                return _instance;
            }
        }

        [Header("Mining Visualization")]
        [SerializeField] private Material worldSphereMaterial;
        [SerializeField] private float updateInterval = 0.1f; // How often to update shader

        [Header("Crystal Colors")]
        [SerializeField] private Color redCrystalColor = new Color(1f, 0f, 0f, 1f);
        [SerializeField] private Color greenCrystalColor = new Color(0f, 1f, 0f, 1f);
        [SerializeField] private Color blueCrystalColor = new Color(0f, 0f, 1f, 1f);

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Tracking
        private class MiningPlayer
        {
            public ulong playerId;
            public Transform transform;
            public CrystalType crystalType;
            public Vector3 localPosition; // Position in world sphere's local space
        }

        private Dictionary<ulong, MiningPlayer> miningPlayers = new Dictionary<ulong, MiningPlayer>();
        private Transform worldSphereTransform;
        private MaterialPropertyBlock propertyBlock;

        // Shader property IDs for performance
        private int miningPositionsPropertyId;
        private int miningColorsPropertyId;

        // Arrays for shader data
        private const int MAX_MINING_PLAYERS = 10;
        private Vector4[] miningPositions = new Vector4[MAX_MINING_PLAYERS];
        private Vector4[] miningColors = new Vector4[MAX_MINING_PLAYERS];

        private float lastUpdateTime;
        private DbConnection conn;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            propertyBlock = new MaterialPropertyBlock();

            // Cache shader property IDs
            miningPositionsPropertyId = Shader.PropertyToID("_MiningPositions");
            miningColorsPropertyId = Shader.PropertyToID("_MiningColors");

            // Get connection
            conn = GameManager.Conn;
            if (conn == null)
            {
                // UnityEngine.Debug.LogError("MiningVisualizationManager: No database connection!");
                enabled = false;
                return;
            }
        }

        void Start()
        {
            StartCoroutine(DelayedInit());
        }

        IEnumerator DelayedInit()
        {
            // Wait for world to be spawned
            yield return new WaitForSeconds(1f);

            // Find world sphere
            GameObject centerWorld = GameObject.Find("CenterWorld");
            if (centerWorld != null)
            {
                worldSphereTransform = centerWorld.transform;

                // Get the renderer and material
                Renderer renderer = centerWorld.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    worldSphereMaterial = renderer.material;
                    // UnityEngine.Debug.Log("MiningVisualizationManager: Found world sphere material");
                }
                else
                {
                    // UnityEngine.Debug.LogWarning("MiningVisualizationManager: No renderer found on world sphere");
                }
            }
            else
            {
                // UnityEngine.Debug.LogWarning("MiningVisualizationManager: CenterWorld not found, will retry...");
                yield return new WaitForSeconds(2f);
                StartCoroutine(DelayedInit());
            }

            // Subscribe to mining events
            SubscribeToEvents();
        }

        void OnEnable()
        {
            SubscribeToEvents();
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void SubscribeToEvents()
        {
            if (conn != null)
            {
                // Subscribe to mining state changes
                conn.Reducers.OnStartMining += OnPlayerStartedMining;
                conn.Reducers.OnStopMining += OnPlayerStoppedMining;

                // Subscribe to player disconnects
                conn.Db.Player.OnDelete += OnPlayerDeleted;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (conn != null)
            {
                conn.Reducers.OnStartMining -= OnPlayerStartedMining;
                conn.Reducers.OnStopMining -= OnPlayerStoppedMining;
                conn.Db.Player.OnDelete -= OnPlayerDeleted;
            }
        }

        private void OnPlayerStartedMining(ReducerEventContext ctx, ulong orbId, CrystalType crystalType)
        {
            // For now, we'll track the local player only since we can't get caller identity from context
            // This is a limitation of the current SpacetimeDB C# SDK
            var localPlayer = GameManager.GetLocalPlayer();
            if (localPlayer == null)
            {
                return;
            }

            // Check if this is the local player by checking if they're mining
            // We'll enhance this when the SDK provides caller identity
            Player playerData = localPlayer;


            // Find the player's GameObject
            GameObject playerObject = GameObject.Find($"Player_{playerData.PlayerId}");
            if (playerObject == null)
            {
                // UnityEngine.Debug.LogWarning($"MiningVisualizationManager: Could not find GameObject for player {playerData.PlayerId}");
                return;
            }

            // Add to mining players
            if (!miningPlayers.ContainsKey(playerData.PlayerId))
            {
                var miningPlayer = new MiningPlayer
                {
                    playerId = playerData.PlayerId,
                    transform = playerObject.transform,
                    crystalType = crystalType,
                    localPosition = Vector3.zero
                };

                miningPlayers[playerData.PlayerId] = miningPlayer;

                if (showDebugInfo)
                {
                    // UnityEngine.Debug.Log($"MiningVisualizationManager: Player {playerData.Name} started mining with {crystalType} crystal");
                }
            }

            UpdateShaderData();
        }

        private void OnPlayerStoppedMining(ReducerEventContext ctx)
        {
            // For now, we'll track the local player only
            var localPlayer = GameManager.GetLocalPlayer();
            if (localPlayer == null)
            {
                return;
            }

            Player playerData = localPlayer;

            if (playerData != null && miningPlayers.ContainsKey(playerData.PlayerId))
            {
                miningPlayers.Remove(playerData.PlayerId);

                if (showDebugInfo)
                {
                    // UnityEngine.Debug.Log($"MiningVisualizationManager: Player {playerData.Name} stopped mining");
                }

                UpdateShaderData();
            }
        }

        private void OnPlayerDeleted(EventContext ctx, Player player)
        {
            if (miningPlayers.ContainsKey(player.PlayerId))
            {
                miningPlayers.Remove(player.PlayerId);
                UpdateShaderData();
            }
        }

        void Update()
        {
            // Update positions at interval
            if (Time.time - lastUpdateTime > updateInterval)
            {
                UpdatePlayerPositions();
                UpdateShaderData();
                lastUpdateTime = Time.time;
            }
        }

        private void UpdatePlayerPositions()
        {
            if (worldSphereTransform == null) return;

            foreach (var kvp in miningPlayers)
            {
                var miningPlayer = kvp.Value;
                if (miningPlayer.transform != null)
                {
                    // Convert world position to world sphere's local space
                    miningPlayer.localPosition = worldSphereTransform.InverseTransformPoint(miningPlayer.transform.position);
                }
            }
        }

        private void UpdateShaderData()
        {
            if (worldSphereMaterial == null || worldSphereTransform == null) return;

            // Clear arrays
            for (int i = 0; i < MAX_MINING_PLAYERS; i++)
            {
                miningPositions[i] = Vector4.zero;
                miningColors[i] = Vector4.zero;
            }

            // Fill arrays with active mining players
            int index = 0;
            foreach (var kvp in miningPlayers)
            {
                if (index >= MAX_MINING_PLAYERS) break;

                var miningPlayer = kvp.Value;

                // Set position (xyz) and active flag (w)
                miningPositions[index] = new Vector4(
                    miningPlayer.localPosition.x,
                    miningPlayer.localPosition.y,
                    miningPlayer.localPosition.z,
                    1f // Active
                );

                // Set color based on crystal type
                Color crystalColor = GetCrystalColor(miningPlayer.crystalType);
                miningColors[index] = new Vector4(
                    crystalColor.r,
                    crystalColor.g,
                    crystalColor.b,
                    1f // Full intensity
                );

                index++;
            }

            // Apply to material using property block (avoids material instances)
            if (worldSphereMaterial != null)
            {
                propertyBlock.SetVectorArray(miningPositionsPropertyId, miningPositions);
                propertyBlock.SetVectorArray(miningColorsPropertyId, miningColors);

                // Apply to the world sphere renderer
                Renderer renderer = worldSphereTransform.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private Color GetCrystalColor(CrystalType crystalType)
        {
            // Only Red, Green, Blue are defined in the CrystalType enum
            switch (crystalType)
            {
                case CrystalType.Red:
                    return redCrystalColor;
                case CrystalType.Green:
                    return greenCrystalColor;
                case CrystalType.Blue:
                    return blueCrystalColor;
                default:
                    return Color.white;
            }
        }

        // Public API for manual control
        public void AddMiningPlayer(ulong playerId, Transform playerTransform, CrystalType crystalType)
        {
            if (!miningPlayers.ContainsKey(playerId))
            {
                miningPlayers[playerId] = new MiningPlayer
                {
                    playerId = playerId,
                    transform = playerTransform,
                    crystalType = crystalType,
                    localPosition = Vector3.zero
                };
                UpdateShaderData();
            }
        }

        public void RemoveMiningPlayer(ulong playerId)
        {
            if (miningPlayers.Remove(playerId))
            {
                UpdateShaderData();
            }
        }

        public bool IsPlayerMining(ulong playerId)
        {
            return miningPlayers.ContainsKey(playerId);
        }

        void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 400, 300, 200));
            GUILayout.Label($"Mining Visualization Manager");
            GUILayout.Label($"Active Mining Players: {miningPlayers.Count}");

            foreach (var kvp in miningPlayers)
            {
                var player = kvp.Value;
                GUILayout.Label($"  Player {player.playerId}: {player.crystalType}");
            }

            GUILayout.EndArea();
        }
    }
}
