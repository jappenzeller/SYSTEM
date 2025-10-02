using UnityEngine;

namespace SYSTEM.Game
{
    /// <summary>
    /// Ensures the WavePacketMiningSystem is properly initialized in the scene
    /// </summary>
    public class MiningSystemInitializer : MonoBehaviour
    {
        private static MiningSystemInitializer instance;

        void Awake()
        {
            // Singleton pattern
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Check if WavePacketMiningSystem exists
            var miningSystem = GetComponent<WavePacketMiningSystem>();
            if (miningSystem == null)
            {
                UnityEngine.Debug.Log("[MiningSystemInitializer] Adding WavePacketMiningSystem component");
                miningSystem = gameObject.AddComponent<WavePacketMiningSystem>();
            }

            UnityEngine.Debug.Log("[MiningSystemInitializer] Mining system ready - Press E near an orb to mine!");
        }

        public static void EnsureMiningSystem()
        {
            if (instance == null)
            {
                // Create the mining system GameObject if it doesn't exist
                GameObject miningSystemGO = new GameObject("MiningSystem");
                instance = miningSystemGO.AddComponent<MiningSystemInitializer>();
                UnityEngine.Debug.Log("[MiningSystemInitializer] Created MiningSystem GameObject");
            }
        }
    }
}