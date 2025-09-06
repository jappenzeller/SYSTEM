// FrameTickMonitorUI.cs - Visual performance monitor for SpaceTimeDB frame ticks
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class FrameTickMonitorUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject monitorPanel;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI modeText;
    [SerializeField] private Slider tickTimeSlider;
    [SerializeField] private Image tickTimeBar;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Dropdown modeDropdown;
    
    [Header("Graph Settings")]
    [SerializeField] private LineRenderer performanceGraph;
    [SerializeField] private int graphSamples = 100;
    [SerializeField] private float graphHeight = 100f;
    [SerializeField] private float graphWidth = 300f;
    
    [Header("Warning Thresholds")]
    [SerializeField] private float warningThresholdMs = 8.0f;
    [SerializeField] private float criticalThresholdMs = 16.0f;
    [SerializeField] private Color normalColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;
    
    private Queue<float> tickTimeHistory = new Queue<float>();
    private bool isVisible = false;
    private FrameTickManager tickManager;
    
    void Start()
    {
        tickManager = FrameTickManager.Instance;
        
        // Setup UI
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleVisibility);
        }
        
        if (modeDropdown != null)
        {
            SetupModeDropdown();
        }
        
        // Subscribe to events
        tickManager.OnTickCompleted += OnTickCompleted;
        tickManager.OnTickModeChanged += OnTickModeChanged;
        
        // Hide by default
        SetVisibility(false);
        
        // Setup graph
        if (performanceGraph != null)
        {
            performanceGraph.positionCount = graphSamples;
            performanceGraph.startWidth = 2f;
            performanceGraph.endWidth = 2f;
        }
    }
    
    void Update()
    {
        // Toggle with F3 key using new Input System
        if (Keyboard.current?.f3Key.wasPressedThisFrame ?? false)
        {
            ToggleVisibility();
        }
        
        if (isVisible)
        {
            UpdateDisplay();
        }
    }
    
    private void SetupModeDropdown()
    {
        modeDropdown.ClearOptions();
        
        List<string> options = new List<string>
        {
            "Every Frame",
            "Fixed Interval",
            "Adaptive",
            "Manual"
        };
        
        modeDropdown.AddOptions(options);
        modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
    }
    
    private void OnModeDropdownChanged(int index)
    {
        FrameTickManager.TickMode newMode = (FrameTickManager.TickMode)index;
        tickManager.SetTickMode(newMode);
    }
    
    private void OnTickCompleted(float tickTimeMs)
    {
        // Add to history
        tickTimeHistory.Enqueue(tickTimeMs);
        
        // Maintain history size
        while (tickTimeHistory.Count > graphSamples)
        {
            tickTimeHistory.Dequeue();
        }
        
        // Update slider if visible
        if (isVisible && tickTimeSlider != null)
        {
            tickTimeSlider.value = Mathf.Clamp01(tickTimeMs / criticalThresholdMs);
            
            // Update color based on performance
            if (tickTimeBar != null)
            {
                if (tickTimeMs > criticalThresholdMs)
                    tickTimeBar.color = criticalColor;
                else if (tickTimeMs > warningThresholdMs)
                    tickTimeBar.color = warningColor;
                else
                    tickTimeBar.color = normalColor;
            }
        }
    }
    
    private void OnTickModeChanged(FrameTickManager.TickMode mode)
    {
        if (modeText != null)
        {
            modeText.text = $"Mode: {mode}";
        }
        
        if (modeDropdown != null)
        {
            modeDropdown.SetValueWithoutNotify((int)mode);
        }
    }
    
    private void UpdateDisplay()
    {
        var stats = tickManager.GetPerformanceStats();
        
        if (statsText != null)
        {
            statsText.text = $"Avg: {stats.AverageTickTimeMs:F2}ms\n" +
                           $"Max: {stats.MaxTickTimeMs:F2}ms\n" +
                           $"Msgs/s: {stats.MessagesPerSecond}\n" +
                           $"FPS: {Mathf.RoundToInt(1f / Time.deltaTime)}";
        }
        
        UpdateGraph();
    }
    
    private void UpdateGraph()
    {
        if (performanceGraph == null || tickTimeHistory.Count < 2) return;
        
        float[] samples = new float[tickTimeHistory.Count];
        tickTimeHistory.CopyTo(samples, 0);
        
        Vector3[] positions = new Vector3[samples.Length];
        
        for (int i = 0; i < samples.Length; i++)
        {
            float x = (i / (float)(graphSamples - 1)) * graphWidth;
            float y = Mathf.Clamp(samples[i] / criticalThresholdMs, 0, 2) * graphHeight;
            positions[i] = new Vector3(x, y, 0);
        }
        
        performanceGraph.positionCount = positions.Length;
        performanceGraph.SetPositions(positions);
        
        // Update graph color based on average
        float avg = 0;
        foreach (float sample in samples)
        {
            avg += sample;
        }
        avg /= samples.Length;
        
        if (avg > criticalThresholdMs)
            performanceGraph.startColor = performanceGraph.endColor = criticalColor;
        else if (avg > warningThresholdMs)
            performanceGraph.startColor = performanceGraph.endColor = warningColor;
        else
            performanceGraph.startColor = performanceGraph.endColor = normalColor;
    }
    
    private void ToggleVisibility()
    {
        SetVisibility(!isVisible);
    }
    
    private void SetVisibility(bool visible)
    {
        isVisible = visible;
        if (monitorPanel != null)
        {
            monitorPanel.SetActive(visible);
        }
    }
    
    void OnDestroy()
    {
        if (tickManager != null)
        {
            tickManager.OnTickCompleted -= OnTickCompleted;
            tickManager.OnTickModeChanged -= OnTickModeChanged;
        }
    }
}

// Optional: Simplified performance display for production
public class SimpleFrameTickDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI displayText;
    #pragma warning disable 0414 // Field is used in production builds
    [SerializeField] private bool showInProduction = false;
    #pragma warning restore 0414
    
    private FrameTickManager tickManager;
    private float updateInterval = 0.5f;
    private float timer = 0;
    
    void Start()
    {
        #if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        if (!showInProduction)
        {
            gameObject.SetActive(false);
            return;
        }
        #endif
        
        tickManager = FrameTickManager.Instance;
    }
    
    void Update()
    {
        timer += Time.deltaTime;
        
        if (timer >= updateInterval)
        {
            timer = 0;
            UpdateDisplay();
        }
    }
    
    void UpdateDisplay()
    {
        if (displayText != null && tickManager != null)
        {
            var stats = tickManager.GetPerformanceStats();
            displayText.text = $"SpaceTimeDB: {stats.AverageTickTimeMs:F1}ms";
            
            // Color code based on performance
            if (stats.AverageTickTimeMs > 16f)
                displayText.color = Color.red;
            else if (stats.AverageTickTimeMs > 8f)
                displayText.color = Color.yellow;
            else
                displayText.color = Color.green;
        }
    }
}