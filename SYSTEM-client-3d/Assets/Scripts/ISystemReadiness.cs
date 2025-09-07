using System;

/// <summary>
/// Interface for components that need to declare system dependencies and readiness state
/// </summary>
public interface ISystemReadiness
{
    /// <summary>
    /// Unique name identifying this system
    /// </summary>
    string SystemName { get; }
    
    /// <summary>
    /// Array of system names this component depends on
    /// </summary>
    string[] RequiredSystems { get; }
    
    /// <summary>
    /// Whether this system has completed initialization and is ready
    /// </summary>
    bool IsReady { get; }
    
    /// <summary>
    /// Event fired when this system becomes ready
    /// </summary>
    event System.Action<string> OnSystemReady;
    
    /// <summary>
    /// Optional timeout in seconds for this system to become ready
    /// </summary>
    float InitializationTimeout { get; }
    
    /// <summary>
    /// Called when all required dependencies are ready
    /// </summary>
    void OnDependenciesReady();
    
    /// <summary>
    /// Called if initialization times out
    /// </summary>
    void OnInitializationTimeout();
    
    /// <summary>
    /// Optional method to check if a conditional dependency is needed
    /// </summary>
    bool IsSystemRequired(string systemName);
}

/// <summary>
/// Extension of ISystemReadiness for components that can handle missing optional dependencies
/// </summary>
public interface ISystemReadinessOptional : ISystemReadiness
{
    /// <summary>
    /// Array of optional system names that would enhance functionality if present
    /// </summary>
    string[] OptionalSystems { get; }
    
    /// <summary>
    /// Called when an optional dependency becomes available
    /// </summary>
    void OnOptionalSystemReady(string systemName);
}