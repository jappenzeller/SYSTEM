using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Central coordinator for all system dependencies and initialization sequencing
/// </summary>
public class SystemReadinessManager : MonoBehaviour
{
    #region Singleton
    
    private static SystemReadinessManager instance;
        public static SystemReadinessManager Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("SystemReadinessManager");
                    instance = go.AddComponent<SystemReadinessManager>();
                    DontDestroyOnLoad(go);
                    instance.Initialize();
                }
                return instance;
            }
        }
        
        #endregion
        
        #region Internal Types
        
        private class SystemInfo
        {
            public ISystemReadiness System { get; set; }
            public HashSet<string> RequiredDependencies { get; set; } = new HashSet<string>();
            public HashSet<string> OptionalDependencies { get; set; } = new HashSet<string>();
            public HashSet<string> WaitingFor { get; set; } = new HashSet<string>();
            public float RegistrationTime { get; set; }
            public bool IsReady { get; set; }
            public bool TimedOut { get; set; }
            public List<string> DependencyChain { get; set; } = new List<string>();
        }
        
        private class DependencyGraphNode
        {
            public string SystemName { get; set; }
            public List<string> Dependencies { get; set; } = new List<string>();
            public List<string> Dependents { get; set; } = new List<string>();
            public int Depth { get; set; }
        }
        
        #endregion
        
        #region Fields
        
        [Header("Configuration")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private bool enableDebugUI = true;
        [SerializeField] private float defaultTimeout = 30f;
        [SerializeField] private float checkInterval = 0.1f;
        
        [Header("Debug Visualization")]
        [SerializeField] private KeyCode toggleDebugKey = KeyCode.F9;
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color waitingColor = Color.yellow;
        [SerializeField] private Color timedOutColor = Color.red;
        [SerializeField] private Color missingColor = Color.gray;
        
        // System registry
        private readonly Dictionary<string, SystemInfo> registeredSystems = new Dictionary<string, SystemInfo>();
        private readonly Dictionary<string, List<Action<string>>> systemReadyCallbacks = new Dictionary<string, List<Action<string>>>();
        private readonly object registryLock = new object();
        
        // Debug tracking
        private readonly List<string> initializationLog = new List<string>();
        private bool showDebugUI = false;
        private float lastUpdateTime;
        
        // Statistics
        private int totalSystemsRegistered = 0;
        private int systemsReady = 0;
        private int systemsTimedOut = 0;
        private float averageInitTime = 0f;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        void Initialize()
        {
            LogDebug("SystemReadinessManager initialized");
            StartCoroutine(SystemCheckLoop());
        }
        
        void Update()
        {
            if (Input.GetKeyDown(toggleDebugKey))
            {
                showDebugUI = !showDebugUI;
            }
        }
        
        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Register a system that implements ISystemReadiness
        /// </summary>
        public static void RegisterSystem(ISystemReadiness system)
        {
            if (Instance == null) return;
            Instance.RegisterSystemInternal(system);
        }
        
        /// <summary>
        /// Check if a system is ready
        /// </summary>
        public static bool IsSystemReady(string systemName)
        {
            if (Instance == null) return false;
            return Instance.IsSystemReadyInternal(systemName);
        }
        
        /// <summary>
        /// Wait for a system to be ready (callback-based)
        /// </summary>
        public static void WaitForSystem(string systemName, Action<string> onReady)
        {
            if (Instance == null) return;
            Instance.WaitForSystemInternal(systemName, onReady);
        }
        
        /// <summary>
        /// Wait for multiple systems to be ready
        /// </summary>
        public static void WaitForSystems(string[] systemNames, Action onAllReady)
        {
            if (Instance == null) return;
            Instance.WaitForSystemsInternal(systemNames, onAllReady);
        }
        
        /// <summary>
        /// Mark a system as ready (for systems that don't implement ISystemReadiness)
        /// </summary>
        public static void MarkSystemReady(string systemName)
        {
            if (Instance == null) return;
            Instance.MarkSystemReadyInternal(systemName);
        }
        
        /// <summary>
        /// Get dependency graph for debugging
        /// </summary>
        public static Dictionary<string, List<string>> GetDependencyGraph()
        {
            if (Instance == null) return new Dictionary<string, List<string>>();
            return Instance.GetDependencyGraphInternal();
        }
        
        /// <summary>
        /// Get systems waiting on a specific system
        /// </summary>
        public static List<string> GetSystemsWaitingFor(string systemName)
        {
            if (Instance == null) return new List<string>();
            return Instance.GetSystemsWaitingForInternal(systemName);
        }
        
        #endregion
        
        #region Internal Implementation
        
        private void RegisterSystemInternal(ISystemReadiness system)
        {
            if (system == null) return;
            
            lock (registryLock)
            {
                string systemName = system.SystemName;
                
                if (registeredSystems.ContainsKey(systemName))
                {
                    LogWarning($"System '{systemName}' already registered");
                    return;
                }
                
                var info = new SystemInfo
                {
                    System = system,
                    RegistrationTime = Time.time,
                    IsReady = system.IsReady
                };
                
                // Parse required dependencies
                if (system.RequiredSystems != null)
                {
                    foreach (string dep in system.RequiredSystems)
                    {
                        if (!string.IsNullOrEmpty(dep))
                        {
                            info.RequiredDependencies.Add(dep);
                            if (!IsSystemReadyInternal(dep))
                            {
                                info.WaitingFor.Add(dep);
                            }
                        }
                    }
                }
                
                // Parse optional dependencies
                if (system is ISystemReadinessOptional optional && optional.OptionalSystems != null)
                {
                    foreach (string dep in optional.OptionalSystems)
                    {
                        if (!string.IsNullOrEmpty(dep))
                        {
                            info.OptionalDependencies.Add(dep);
                        }
                    }
                }
                
                registeredSystems[systemName] = info;
                totalSystemsRegistered++;
                
                // Subscribe to system's ready event
                system.OnSystemReady += OnSystemBecameReady;
                
                LogDebug($"Registered system '{systemName}' with {info.RequiredDependencies.Count} required and {info.OptionalDependencies.Count} optional dependencies");
                
                // Check if dependencies are already satisfied
                if (info.WaitingFor.Count == 0 && !info.IsReady)
                {
                    NotifySystemDependenciesReady(systemName);
                }
                
                // If system is already ready, notify waiters
                if (info.IsReady)
                {
                    OnSystemBecameReady(systemName);
                }
            }
        }
        
        private bool IsSystemReadyInternal(string systemName)
        {
            lock (registryLock)
            {
                if (registeredSystems.TryGetValue(systemName, out SystemInfo info))
                {
                    return info.IsReady && !info.TimedOut;
                }
                return false;
            }
        }
        
        private void WaitForSystemInternal(string systemName, Action<string> onReady)
        {
            lock (registryLock)
            {
                // If system is already ready, call immediately
                if (IsSystemReadyInternal(systemName))
                {
                    onReady?.Invoke(systemName);
                    return;
                }
                
                // Add to callback list
                if (!systemReadyCallbacks.TryGetValue(systemName, out List<Action<string>> callbacks))
                {
                    callbacks = new List<Action<string>>();
                    systemReadyCallbacks[systemName] = callbacks;
                }
                
                callbacks.Add(onReady);
                LogDebug($"Added callback waiting for system '{systemName}'");
            }
        }
        
        private void WaitForSystemsInternal(string[] systemNames, Action onAllReady)
        {
            if (systemNames == null || systemNames.Length == 0)
            {
                onAllReady?.Invoke();
                return;
            }
            
            int remainingCount = systemNames.Length;
            
            foreach (string systemName in systemNames)
            {
                WaitForSystemInternal(systemName, (name) =>
                {
                    remainingCount--;
                    if (remainingCount <= 0)
                    {
                        onAllReady?.Invoke();
                    }
                });
            }
        }
        
        private void MarkSystemReadyInternal(string systemName)
        {
            lock (registryLock)
            {
                // Create a simple system info if not registered
                if (!registeredSystems.ContainsKey(systemName))
                {
                    registeredSystems[systemName] = new SystemInfo
                    {
                        System = null,
                        RegistrationTime = Time.time,
                        IsReady = true
                    };
                    totalSystemsRegistered++;
                }
                else
                {
                    registeredSystems[systemName].IsReady = true;
                }
                
                OnSystemBecameReady(systemName);
            }
        }
        
        private void OnSystemBecameReady(string systemName)
        {
            lock (registryLock)
            {
                if (!registeredSystems.TryGetValue(systemName, out SystemInfo info))
                {
                    return;
                }
                
                if (!info.IsReady)
                {
                    info.IsReady = true;
                    systemsReady++;
                    
                    float initTime = Time.time - info.RegistrationTime;
                    averageInitTime = (averageInitTime * (systemsReady - 1) + initTime) / systemsReady;
                    
                    LogDebug($"System '{systemName}' became ready (init time: {initTime:F2}s)");
                }
                
                // Notify callbacks
                if (systemReadyCallbacks.TryGetValue(systemName, out List<Action<string>> callbacks))
                {
                    foreach (var callback in callbacks.ToList())
                    {
                        try
                        {
                            callback?.Invoke(systemName);
                        }
                        catch (Exception e)
                        {
                            LogError($"Error in ready callback for system '{systemName}': {e.Message}");
                        }
                    }
                    systemReadyCallbacks.Remove(systemName);
                }
                
                // Check if this unblocks other systems
                CheckDependentSystems(systemName);
                
                // Notify optional dependency listeners
                NotifyOptionalDependencyReady(systemName);
            }
        }
        
        private void CheckDependentSystems(string readySystemName)
        {
            List<string> systemsToNotify = new List<string>();
            
            foreach (var kvp in registeredSystems)
            {
                if (kvp.Value.IsReady || kvp.Value.TimedOut) continue;
                
                if (kvp.Value.WaitingFor.Contains(readySystemName))
                {
                    kvp.Value.WaitingFor.Remove(readySystemName);
                    
                    if (kvp.Value.WaitingFor.Count == 0)
                    {
                        systemsToNotify.Add(kvp.Key);
                    }
                }
            }
            
            foreach (string systemName in systemsToNotify)
            {
                NotifySystemDependenciesReady(systemName);
            }
        }
        
        private void NotifySystemDependenciesReady(string systemName)
        {
            if (!registeredSystems.TryGetValue(systemName, out SystemInfo info)) return;
            if (info.System == null) return;
            
            LogDebug($"All dependencies ready for system '{systemName}', notifying...");
            
            try
            {
                info.System.OnDependenciesReady();
            }
            catch (Exception e)
            {
                LogError($"Error in OnDependenciesReady for system '{systemName}': {e.Message}");
            }
        }
        
        private void NotifyOptionalDependencyReady(string dependencyName)
        {
            foreach (var kvp in registeredSystems)
            {
                if (kvp.Value.System is ISystemReadinessOptional optional)
                {
                    if (kvp.Value.OptionalDependencies.Contains(dependencyName))
                    {
                        try
                        {
                            optional.OnOptionalSystemReady(dependencyName);
                        }
                        catch (Exception e)
                        {
                            LogError($"Error notifying optional dependency for system '{kvp.Key}': {e.Message}");
                        }
                    }
                }
            }
        }
        
        private IEnumerator SystemCheckLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(checkInterval);
                
                if (Time.time - lastUpdateTime > checkInterval)
                {
                    lastUpdateTime = Time.time;
                    CheckTimeouts();
                }
            }
        }
        
        private void CheckTimeouts()
        {
            lock (registryLock)
            {
                foreach (var kvp in registeredSystems)
                {
                    var info = kvp.Value;
                    
                    if (info.IsReady || info.TimedOut || info.System == null) continue;
                    
                    float timeout = info.System.InitializationTimeout > 0 ? 
                        info.System.InitializationTimeout : defaultTimeout;
                    
                    float elapsedTime = Time.time - info.RegistrationTime;
                    
                    if (elapsedTime > timeout)
                    {
                        info.TimedOut = true;
                        systemsTimedOut++;
                        
                        LogWarning($"System '{kvp.Key}' timed out after {elapsedTime:F2}s (waiting for: {string.Join(", ", info.WaitingFor)})");
                        
                        try
                        {
                            info.System.OnInitializationTimeout();
                        }
                        catch (Exception e)
                        {
                            LogError($"Error in timeout handler for system '{kvp.Key}': {e.Message}");
                        }
                    }
                }
            }
        }
        
        private Dictionary<string, List<string>> GetDependencyGraphInternal()
        {
            lock (registryLock)
            {
                var graph = new Dictionary<string, List<string>>();
                
                foreach (var kvp in registeredSystems)
                {
                    graph[kvp.Key] = kvp.Value.RequiredDependencies.ToList();
                }
                
                return graph;
            }
        }
        
        private List<string> GetSystemsWaitingForInternal(string systemName)
        {
            lock (registryLock)
            {
                var waiting = new List<string>();
                
                foreach (var kvp in registeredSystems)
                {
                    if (kvp.Value.WaitingFor.Contains(systemName))
                    {
                        waiting.Add(kvp.Key);
                    }
                }
                
                return waiting;
            }
        }
        
        #endregion
        
        #region Debug UI
        
        void OnGUI()
        {
            if (!enableDebugUI || !showDebugUI) return;
            
            // Create styles
            GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
            bgStyle.normal.background = MakeTexture(1, 1, new Color(0, 0, 0, 0.9f));
            
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 16;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;
            
            // Calculate size
            float width = 500;
            float height = 700;
            float x = 10;
            float y = 10;
            
            // Background
            GUI.Box(new Rect(x, y, width, height), "", bgStyle);
            
            // Title
            GUI.Label(new Rect(x + 10, y + 10, width - 20, 25), "System Readiness Manager", titleStyle);
            y += 40;
            
            // Statistics
            GUI.Label(new Rect(x + 10, y, width - 20, 20), $"Systems: {systemsReady}/{totalSystemsRegistered} ready, {systemsTimedOut} timed out", labelStyle);
            y += 20;
            GUI.Label(new Rect(x + 10, y, width - 20, 20), $"Average Init Time: {averageInitTime:F2}s", labelStyle);
            y += 30;
            
            // System list
            GUI.Label(new Rect(x + 10, y, width - 20, 20), "=== Registered Systems ===", titleStyle);
            y += 25;
            
            float scrollHeight = height - (y - 10) - 50;
            float contentHeight = registeredSystems.Count * 60;
            
            Vector2 scrollPos = GUI.BeginScrollView(
                new Rect(x + 10, y, width - 20, scrollHeight),
                Vector2.zero,
                new Rect(0, 0, width - 40, contentHeight)
            );
            
            float itemY = 0;
            
            lock (registryLock)
            {
                foreach (var kvp in registeredSystems.OrderBy(k => k.Value.RegistrationTime))
                {
                    var info = kvp.Value;
                    
                    // Determine color
                    Color statusColor = missingColor;
                    string status = "Missing";
                    
                    if (info.TimedOut)
                    {
                        statusColor = timedOutColor;
                        status = "TIMEOUT";
                    }
                    else if (info.IsReady)
                    {
                        statusColor = readyColor;
                        status = "READY";
                    }
                    else if (info.WaitingFor.Count > 0)
                    {
                        statusColor = waitingColor;
                        status = "WAITING";
                    }
                    
                    // System name and status
                    GUIStyle systemStyle = new GUIStyle(labelStyle);
                    systemStyle.normal.textColor = statusColor;
                    systemStyle.fontStyle = FontStyle.Bold;
                    
                    GUI.Label(new Rect(0, itemY, width - 40, 20), $"{kvp.Key} [{status}]", systemStyle);
                    itemY += 20;
                    
                    // Dependencies
                    if (info.RequiredDependencies.Count > 0)
                    {
                        string deps = "Requires: " + string.Join(", ", info.RequiredDependencies);
                        GUI.Label(new Rect(10, itemY, width - 50, 20), deps, labelStyle);
                        itemY += 20;
                    }
                    
                    // Waiting for
                    if (info.WaitingFor.Count > 0)
                    {
                        GUIStyle waitingStyle = new GUIStyle(labelStyle);
                        waitingStyle.normal.textColor = warningColor;
                        string waiting = "Waiting: " + string.Join(", ", info.WaitingFor);
                        GUI.Label(new Rect(10, itemY, width - 50, 20), waiting, waitingStyle);
                        itemY += 20;
                    }
                    
                    itemY += 10; // Spacing
                }
            }
            
            GUI.EndScrollView();
            
            // Controls hint
            GUIStyle hintStyle = new GUIStyle(labelStyle);
            hintStyle.fontSize = 10;
            hintStyle.normal.textColor = Color.gray;
            GUI.Label(new Rect(x + 10, height - 30, width - 20, 20), 
                $"Press {toggleDebugKey} to toggle this UI", hintStyle);
        }
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        #endregion
        
        #region Logging
        
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[SystemReadiness] {message}");
                initializationLog.Add($"[{Time.time:F2}] {message}");
                
                // Keep log size manageable
                if (initializationLog.Count > 100)
                {
                    initializationLog.RemoveAt(0);
                }
            }
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[SystemReadiness] {message}");
            initializationLog.Add($"[{Time.time:F2}] WARNING: {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[SystemReadiness] {message}");
            initializationLog.Add($"[{Time.time:F2}] ERROR: {message}");
        }
        
        #endregion
}