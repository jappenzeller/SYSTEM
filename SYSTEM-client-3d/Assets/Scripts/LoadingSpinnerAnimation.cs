using UnityEngine;
using UnityEngine.UIElements;

public class LoadingSpinnerAnimation : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private float rotationSpeed = 360f; // Degrees per second
    
    private VisualElement spinner;
    private bool isAnimating = false;
    
    void Start()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
        
        SetupSpinner();
    }
    
    void OnEnable()
    {
        SetupSpinner();
    }
    
    private void SetupSpinner()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            return;
            
        spinner = uiDocument.rootVisualElement.Q<VisualElement>("loading-spinner");
        
        if (spinner != null)
        {
            // Check if loading overlay is visible
            var loadingOverlay = uiDocument.rootVisualElement.Q<VisualElement>("loading-overlay");
            if (loadingOverlay != null && !loadingOverlay.ClassListContains("hidden"))
            {
                StartAnimation();
            }
        }
    }
    
    void Update()
    {
        if (isAnimating && spinner != null)
        {
            // Rotate the spinner
            float currentRotation = spinner.transform.rotation.eulerAngles.z;
            float newRotation = currentRotation + (rotationSpeed * Time.deltaTime);
            
            spinner.transform.rotation = Quaternion.Euler(0, 0, newRotation);
        }
    }
    
    public void StartAnimation()
    {
        isAnimating = true;
        
        // Also check if the loading overlay is visible
        var loadingOverlay = uiDocument.rootVisualElement.Q<VisualElement>("loading-overlay");
        if (loadingOverlay != null)
        {
            // Use a scheduled callback to check visibility
            loadingOverlay.schedule.Execute(() =>
            {
                isAnimating = !loadingOverlay.ClassListContains("hidden");
            }).Every(100); // Check every 100ms
        }
    }
    
    public void StopAnimation()
    {
        isAnimating = false;
    }
}