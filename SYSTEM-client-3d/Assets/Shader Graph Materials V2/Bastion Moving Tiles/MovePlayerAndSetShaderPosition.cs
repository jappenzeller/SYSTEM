using UnityEngine;

public class MovePlayerAndSetShaderPosition : MonoBehaviour
{
    void Update()
    {
        Shader.SetGlobalVector("_Player_Position", transform.position);
        transform.position = new Vector3((Mathf.Sin(Time.time)) + 2, 1.67f, 0);
    }
}
