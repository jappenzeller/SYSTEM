// FrameTickManager.cs - Centralized frame tick management for SpaceTimeDB
using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using SpacetimeDB.Types;
using Debug = UnityEngine.Debug;

/// <summary>
/// Manages SpaceTimeDB connection frame ticks with performance monitoring and adaptive strategies
/// </summary>
public class FrameTickManager : MonoBehaviour
{
    private static FrameTickManager instance;
    public static FrameTickManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("FrameTickManager");
                instance = go.AddComponent<FrameTickManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    
    [Header("Tick Configuration")]
    [SerializeField] private TickMode tickMode = TickMode.EveryFrame;
    [SerializeField] private float fixedTickInterval = 0.016f; // 60 Hz
    [SerializeField] private float adaptiveTargetMs = 2.0f; // Target 2ms per tick
    
    [Header("Performance Monitoring")]
    [SerializeField] private bool enablePerfMonitoring = true;
    [SerializeField] private int perfSampleSize = 60; // 1 second at 60fps
    
    [Header("Current Stats")]
    [SerializeField] private float averageTickTime;
    [SerializeField] private float maxTickTime;
    [SerializeField] private int messagesProcessedPerSecond;
    [SerializeField] private TickMode currentActiveMode;
    
    private DbConnection connection;
    private Stopwatch tickStopwatch = new Stopwatch();
    private float[] tickTimeSamples;
    private int sampleIndex = 0;
    private int messagesProcessedThisSecond = 0;
    private float messageCountTimer = 0f;
    private Coroutine fixedTickCoroutine;
    
    public enum TickMode
    {
        EveryFrame,      // Call FrameTick every Update()
        FixedInterval,   // Call at fixed intervals
        Adaptive,        // Adjust based on performance
        Manual,          // Only tick when manually called
        Background       // Run on background thread (advanced)
    }
    
    // Events for monitoring
    public event Action<float> OnTickCompleted;
    public event Action<TickMode> OnTickModeChanged;
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        tickTimeSamples = new float[perfSampleSize];
    }
    
    /// <summary>
    /// Initialize the frame tick manager with a connection
    /// </summary>
    public void Initialize(DbConnection conn)
    {
        if (conn == null)
        {
            Debug.LogError("[FrameTickManager] Cannot initialize with null connection");
            return;
        }
        
        connection = conn;
        currentActiveMode = tickMode;
        
        Debug.Log($"[FrameTickManager] Initialized with mode: {tickMode}");
        
        // Start the appropriate tick mode
        SetTickMode(tickMode);
    }
    
    /// <summary>
    /// Change the tick mode at runtime
    /// </summary>
    public void SetTickMode(TickMode mode)
    {
        if (currentActiveMode == mode) return;
        
        // Stop current mode
        StopCurrentMode();
        
        // Start new mode
        tickMode = mode;
        currentActiveMode = mode;
        
        switch (mode)
        {
            case TickMode.EveryFrame:
                // Update() will handle it
                break;
                
            case TickMode.FixedInterval:
                fixedTickCoroutine = StartCoroutine(FixedIntervalTick());
                break;
                
            case TickMode.Adaptive:
                fixedTickCoroutine = StartCoroutine(AdaptiveTick());
                break;
                
            case TickMode.Manual:
                // User will call ManualTick()
                break;
                
            case TickMode.Background:
                Debug.LogWarning("[FrameTickManager] Background mode not recommended - SpaceTimeDB requires main thread");
                // Fall back to fixed interval
                fixedTickCoroutine = StartCoroutine(FixedIntervalTick());
                break;
        }
        
        OnTickModeChanged?.Invoke(mode);
        Debug.Log($"[FrameTickManager] Switched to {mode} mode");
    }
    
    void Update()
    {
        if (connection == null || !connection.IsActive) return;
        
        // Handle message counting
        if (enablePerfMonitoring)
        {
            messageCountTimer += Time.deltaTime;
            if (messageCountTimer >= 1f)
            {
                messagesProcessedPerSecond = messagesProcessedThisSecond;
                messagesProcessedThisSecond = 0;
                messageCountTimer = 0f;
            }
        }
        
        // Process tick based on mode
        if (currentActiveMode == TickMode.EveryFrame)
        {
            PerformTick();
        }
    }
    
    /// <summary>
    /// Manually trigger a frame tick (for Manual mode or testing)
    /// </summary>
    public void ManualTick()
    {
        if (connection == null || !connection.IsActive)
        {
            Debug.LogWarning("[FrameTickManager] Cannot tick - no active connection");
            return;
        }
        
        PerformTick();
    }
    
    /// <summary>
    /// Core tick execution with performance monitoring
    /// </summary>
    private void PerformTick()
    {
        if (!connection.IsActive) return;
        
        if (enablePerfMonitoring)
        {
            tickStopwatch.Restart();
        }
        
        try
        {
            // This is where SpaceTimeDB processes all pending messages
            connection.FrameTick();
            
            // Count as processed (rough estimate)
            messagesProcessedThisSecond++;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FrameTickManager] Error during FrameTick: {e}");
        }
        
        if (enablePerfMonitoring)
        {
            tickStopwatch.Stop();
            float tickTime = (float)tickStopwatch.Elapsed.TotalMilliseconds;
            RecordTickTime(tickTime);
            OnTickCompleted?.Invoke(tickTime);
        }
    }
    
    /// <summary>
    /// Fixed interval tick coroutine
    /// </summary>
    private IEnumerator FixedIntervalTick()
    {
        WaitForSeconds wait = new WaitForSeconds(fixedTickInterval);
        
        while (connection != null && connection.IsActive)
        {
            PerformTick();
            yield return wait;
        }
    }
    
    /// <summary>
    /// Adaptive tick that adjusts frequency based on load
    /// </summary>
    private IEnumerator AdaptiveTick()
    {
        float currentInterval = fixedTickInterval;
        
        while (connection != null && connection.IsActive)
        {
            PerformTick();
            
            // Adjust interval based on performance
            if (averageTickTime > adaptiveTargetMs * 1.5f)
            {
                // Slow down if ticks are taking too long
                currentInterval = Mathf.Min(currentInterval * 1.1f, 0.1f); // Max 100ms
            }
            else if (averageTickTime < adaptiveTargetMs * 0.5f)
            {
                // Speed up if ticks are fast
                currentInterval = Mathf.Max(currentInterval * 0.9f, 0.001f); // Min 1ms
            }
            
            yield return new WaitForSeconds(currentInterval);
        }
    }
    
    /// <summary>
    /// Record tick time for performance monitoring
    /// </summary>
    private void RecordTickTime(float tickTime)
    {
        tickTimeSamples[sampleIndex] = tickTime;
        sampleIndex = (sampleIndex + 1) % tickTimeSamples.Length;
        
        // Calculate average
        float sum = 0f;
        maxTickTime = 0f;
        int validSamples = 0;
        
        for (int i = 0; i < tickTimeSamples.Length; i++)
        {
            if (tickTimeSamples[i] > 0)
            {
                sum += tickTimeSamples[i];
                maxTickTime = Mathf.Max(maxTickTime, tickTimeSamples[i]);
                validSamples++;
            }
        }
        
        if (validSamples > 0)
        {
            averageTickTime = sum / validSamples;
        }
    }
    
    /// <summary>
    /// Stop the current tick mode
    /// </summary>
    private void StopCurrentMode()
    {
        if (fixedTickCoroutine != null)
        {
            StopCoroutine(fixedTickCoroutine);
            fixedTickCoroutine = null;
        }
    }
    
    /// <summary>
    /// Get current performance stats
    /// </summary>
    public PerformanceStats GetPerformanceStats()
    {
        return new PerformanceStats
        {
            AverageTickTimeMs = averageTickTime,
            MaxTickTimeMs = maxTickTime,
            MessagesPerSecond = messagesProcessedPerSecond,
            CurrentMode = currentActiveMode
        };
    }
    
    /// <summary>
    /// Pause frame ticking
    /// </summary>
    public void Pause()
    {
        StopCurrentMode();
        Debug.Log("[FrameTickManager] Paused");
    }
    
    /// <summary>
    /// Resume frame ticking
    /// </summary>
    public void Resume()
    {
        SetTickMode(tickMode);
        Debug.Log("[FrameTickManager] Resumed");
    }
    
    void OnDestroy()
    {
        StopCurrentMode();
        connection = null;
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Pause();
        }
        else
        {
            Resume();
        }
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        // Optionally adjust tick rate based on focus
        if (!hasFocus && currentActiveMode == TickMode.EveryFrame)
        {
            // Could switch to FixedInterval when unfocused to save CPU
        }
    }
    
    [Serializable]
    public struct PerformanceStats
    {
        public float AverageTickTimeMs;
        public float MaxTickTimeMs;
        public int MessagesPerSecond;
        public TickMode CurrentMode;
    }
}

// Extension to make GameManager use FrameTickManager
public partial class GameManager
{
    private void InitializeFrameTicking()
    {
        // Initialize the frame tick manager when connection is established
        FrameTickManager.Instance.Initialize(Conn);
        
        // Optionally subscribe to performance events
        FrameTickManager.Instance.OnTickCompleted += OnFrameTickCompleted;
    }
    
    private void OnFrameTickCompleted(float tickTimeMs)
    {
        // Log warnings for slow ticks
        if (tickTimeMs > 16.0f) // More than one frame at 60fps
        {
            Debug.LogWarning($"[GameManager] Slow frame tick detected: {tickTimeMs:F2}ms");
        }
    }
    
    // Remove Update() method from GameManager since FrameTickManager handles it
    // void Update() { } // REMOVED
}