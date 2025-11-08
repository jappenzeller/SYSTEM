using UnityEngine;

/// <summary>
/// Component for controlling debug categories in the Inspector
/// </summary>
public class DebugController : MonoBehaviour
{
    [Header("Debug Categories")]
    [SerializeField] private bool connection = false;
    [SerializeField] private bool eventBus = false;
    [SerializeField] private bool orbSystem = false;
    [SerializeField] private bool orbVisualization = false;
    [SerializeField] private bool spireSystem = false;
    [SerializeField] private bool spireVisualization = false;
    [SerializeField] private bool storageSystem = false;
    [SerializeField] private bool storageVisualization = false;
    [SerializeField] private bool input = false;
    [SerializeField] private bool playerSystem = false;
    [SerializeField] private bool worldSystem = false;
    [SerializeField] private bool mining = false;
    [SerializeField] private bool session = false;
    [SerializeField] private bool subscription = false;
    [SerializeField] private bool reducer = false;
    [SerializeField] private bool network = false;
    [SerializeField] private bool performance = false;

    [Header("Quick Controls")]
    [SerializeField] private bool enableAll = false;
    [SerializeField] private bool disableAll = false;

    void Awake()
    {
        ApplySettings();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplySettings();
        }
    }

    void ApplySettings()
    {
        SystemDebug.Category categories = SystemDebug.Category.None;

        if (enableAll)
        {
            categories = SystemDebug.Category.All;
            // Reset the toggle
            enableAll = false;
        }
        else if (disableAll)
        {
            categories = SystemDebug.Category.None;
            // Reset all checkboxes
            connection = eventBus = orbSystem = orbVisualization = spireSystem = spireVisualization = input = playerSystem = worldSystem = false;
            mining = session = subscription = reducer = network = performance = storageSystem = storageVisualization = false;
            // Reset the toggle
            disableAll = false;
        }
        else
        {
            if (connection) categories |= SystemDebug.Category.Connection;
            if (eventBus) categories |= SystemDebug.Category.EventBus;
            if (orbSystem) categories |= SystemDebug.Category.OrbSystem;
            if (orbVisualization) categories |= SystemDebug.Category.OrbVisualization;
            if (spireSystem) categories |= SystemDebug.Category.SpireSystem;
            if (spireVisualization) categories |= SystemDebug.Category.SpireVisualization;
            if (input) categories |= SystemDebug.Category.Input;
            if (playerSystem) categories |= SystemDebug.Category.PlayerSystem;
            if (worldSystem) categories |= SystemDebug.Category.WorldSystem;
            if (mining) categories |= SystemDebug.Category.Mining;
            if (session) categories |= SystemDebug.Category.Session;
            if (subscription) categories |= SystemDebug.Category.Subscription;
            if (reducer) categories |= SystemDebug.Category.Reducer;
            if (network) categories |= SystemDebug.Category.Network;
            if (performance) categories |= SystemDebug.Category.Performance;
            if (storageSystem) categories |= SystemDebug.Category.StorageSystem;
            if (storageVisualization) categories |= SystemDebug.Category.StorageVisualization;
        }

        SystemDebug.SetCategories(categories);
    }
}