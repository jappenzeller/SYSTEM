using UnityEngine;

public class PlayerInstanceLogger : MonoBehaviour
{
    void Awake()
    {
        // This message will appear in the console every time an object with this script is instantiated.
        // The stack trace will show exactly what code path led to this object's creation.
        Debug.LogWarning($"[PlayerInstanceLogger.Awake] PLAYER OBJECT AWAKENED: Name='{gameObject.name}', InstanceID='{gameObject.GetInstanceID()}'. " +
                         $"ActiveInHierarchy={gameObject.activeInHierarchy}, IsActiveAndEnabled={this.isActiveAndEnabled}. " +
                         $"Creation StackTrace: \n{System.Environment.StackTrace}");
    }

    void OnEnable()
    {
        // This helps identify if a disabled object is being re-enabled.
        Debug.LogWarning($"[PlayerInstanceLogger.OnEnable] PLAYER OBJECT ENABLED: Name='{gameObject.name}', InstanceID='{gameObject.GetInstanceID()}'.");
    }

    void Start()
    {
   //     Debug.Log($"[PlayerInstanceLogger.Start] PLAYER OBJECT STARTED: Name='{gameObject.name}', InstanceID='{gameObject.GetInstanceID()}'.");
    }
}
