using System;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Base class for all controllers that need SpacetimeDB subscriptions
/// </summary>
public abstract class SubscribableController : MonoBehaviour
{
    protected DbConnection conn => GameManager.Conn;
    protected SubscriptionHandle currentSubscription;
    protected bool isSubscribed = false;
    
    // Called by SubscriptionOrchestrator
    public abstract void Subscribe(WorldCoords worldCoords);
    public abstract void Unsubscribe();
    public abstract string GetControllerName();
    
    protected virtual void OnSubscriptionApplied()
    {
        isSubscribed = true;
        // Debug.Log($"[{GetControllerName()}] Subscription applied successfully");
    }
    
    protected virtual void OnSubscriptionError(Exception error)
    {
        isSubscribed = false;
        Debug.LogError($"[{GetControllerName()}] Subscription error: {error}");
    }
    
    protected virtual void OnDestroy()
    {
        Unsubscribe();
    }
}