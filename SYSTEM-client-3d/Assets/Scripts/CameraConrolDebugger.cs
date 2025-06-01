using UnityEngine;
using System.Collections.Generic;

public class CameraControlDebugger : MonoBehaviour
{
    private Camera mainCamera;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private List<string> cameraEvents = new List<string>();
    
    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[CameraDebug] No main camera found!");
            return;
        }
        
        lastPosition = mainCamera.transform.position;
        lastRotation = mainCamera.transform.rotation;
    }
    
    void LateUpdate()
    {
        if (mainCamera == null) return;
        
        // Check if camera moved
        if (Vector3.Distance(mainCamera.transform.position, lastPosition) > 0.01f)
        {
            string msg = $"Camera moved from {lastPosition} to {mainCamera.transform.position}";
            Debug.Log($"[CameraDebug] {msg}");
            AddEvent(msg);
        }
        
        // Check if camera rotated
        if (Quaternion.Angle(mainCamera.transform.rotation, lastRotation) > 0.1f)
        {
            string msg = $"Camera rotated - Forward: {mainCamera.transform.forward}";
            Debug.Log($"[CameraDebug] {msg}");
            AddEvent(msg);
        }
        
        lastPosition = mainCamera.transform.position;
        lastRotation = mainCamera.transform.rotation;
    }
    
    void AddEvent(string evt)
    {
        cameraEvents.Add($"{Time.time:F2}: {evt}");
        if (cameraEvents.Count > 10)
            cameraEvents.RemoveAt(0);
    }
    
    void OnGUI()
    {
        GUI.Label(new Rect(10, 150, 600, 20), $"Camera Parent: {mainCamera?.transform.parent?.name ?? "None"}");
        GUI.Label(new Rect(10, 170, 600, 20), $"Camera Pos: {mainCamera?.transform.position.ToString("F1")}");
        GUI.Label(new Rect(10, 190, 600, 20), $"Camera Forward: {mainCamera?.transform.forward.ToString("F2")}");
        
        // Show recent events
        int y = 220;
        GUI.Label(new Rect(10, y, 200, 20), "Recent Camera Events:");
        y += 20;
        foreach (var evt in cameraEvents)
        {
            GUI.Label(new Rect(10, y, 600, 20), evt);
            y += 20;
        }
    }
}