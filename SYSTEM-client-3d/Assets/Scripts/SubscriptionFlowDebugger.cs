// SubscriptionFlowDebugger.cs - Fixed version handling nullable Identity
using UnityEngine;
using System.Collections;
using SpacetimeDB.Types;

public class SubscriptionFlowDebugger : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(DebugSubscriptionFlow());
    }
    
    IEnumerator DebugSubscriptionFlow()
    {
        yield return new WaitForSeconds(3f);
        
        Debug.Log("=== SUBSCRIPTION FLOW DEBUG ===");
        
        // Check PlayerSubscriptionController
        var playerSub = FindFirstObjectByType<PlayerSubscriptionController>();
        if (playerSub != null)
        {
            Debug.Log($"PlayerSubscriptionController - isSubscribed: {playerSub.GetType().GetField("isSubscribed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(playerSub)}");
            Debug.Log($"PlayerSubscriptionController - PlayerCount: {playerSub.PlayerCount}");
            Debug.Log($"PlayerSubscriptionController - LocalPlayer: {playerSub.GetLocalPlayer()?.Name ?? "NULL"}");
        }
        
        // Check if EventBus has any subscribers
        Debug.Log("Checking EventBus subscriptions...");
        
        // Force check for local player
        if (GameManager.IsConnected() && GameManager.LocalIdentity.HasValue)
        {
            // Fix: Handle nullable Identity properly
            var localPlayer = GameManager.Conn.Db.Player.Identity.Find(GameManager.LocalIdentity.Value);
            if (localPlayer != null)
            {
                Debug.Log($"Found local player in DB: {localPlayer.Name} at ({localPlayer.Position.X},{localPlayer.Position.Y},{localPlayer.Position.Z})");
                
                // Manually publish event to test
                Debug.Log("Manually publishing LocalPlayerSpawnedEvent...");
                EventBus.Publish(new LocalPlayerSpawnedEvent { Player = localPlayer });
            }
        }
        
        yield return new WaitForSeconds(1f);
        
        // Check if player spawned
        var playerObj = GameObject.Find("LocalPlayer");
        Debug.Log($"LocalPlayer GameObject exists: {playerObj != null}");
        
        Debug.Log("=== END SUBSCRIPTION FLOW DEBUG ===");
    }
}