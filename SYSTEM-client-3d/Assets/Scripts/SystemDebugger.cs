using UnityEngine;
using System.Collections;

public class SystemDebugger : MonoBehaviour
{
    void Start()
    {
        Debug.Log($"[SystemDebugger] Started in scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        StartCoroutine(DebugSystemState());
    }

    IEnumerator DebugSystemState()
    {
        yield return new WaitForSeconds(1f);
        
        Debug.Log("=== SYSTEM STATE DEBUG ===");
        
        // Check GameManager
        Debug.Log($"GameManager.Instance exists: {GameManager.Instance != null}");
        Debug.Log($"GameManager.IsConnected: {GameManager.IsConnected()}");
        if (GameManager.LocalIdentity != null)
        {
            Debug.Log($"GameManager.LocalIdentity: {GameManager.LocalIdentity}");
        }
        
        // Check GameData
        Debug.Log($"GameData.Instance exists: {GameData.Instance != null}");
        if (GameData.Instance != null)
        {
            var coords = GameData.Instance.GetCurrentWorldCoords();
            Debug.Log($"Current World: ({coords.X},{coords.Y},{coords.Z})");
            Debug.Log($"Username: {GameData.Instance.Username}");
        }
        
        // Check SubscriptionOrchestrator
        Debug.Log($"SubscriptionOrchestrator.Instance exists: {SubscriptionOrchestrator.Instance != null}");
        
        // Check WorldManager
        var worldManager = FindObjectOfType<WorldManager>();
        Debug.Log($"WorldManager exists: {worldManager != null}");
        
        // Check for subscription controllers
        var circuitController = FindObjectOfType<WorldCircuitSubscriptionController>();
        var energyController = FindObjectOfType<EnergySubscriptionController>();
        var playerController = FindObjectOfType<PlayerSubscriptionController>();
        
        Debug.Log($"WorldCircuitSubscriptionController exists: {circuitController != null}");
        Debug.Log($"EnergySubscriptionController exists: {energyController != null}");
        Debug.Log($"PlayerSubscriptionController exists: {playerController != null}");
        
        // Check for players in database
        if (GameManager.IsConnected())
        {
            yield return new WaitForSeconds(1f);
            
            var players = GameManager.Conn.Db.Player.Iter();
            int count = 0;
            foreach (var player in players)
            {
                count++;
                Debug.Log($"Player in DB: {player.Name} at world ({player.CurrentWorld.X},{player.CurrentWorld.Y},{player.CurrentWorld.Z})");
            }
            Debug.Log($"Total players in database: {count}");
        }
        
        Debug.Log("=== END DEBUG ===");
    }
}