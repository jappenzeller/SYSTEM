using UnityEngine;

[System.Serializable]
public struct Tunnel
{
    public Vector3 start;
    public Vector3 end;
    public float radius;
    public Color color;
    
    public Tunnel(Vector3 start, Vector3 end, float radius = 1f)
    {
        this.start = start;
        this.end = end;
        this.radius = radius;
        this.color = Color.cyan; // Default tunnel color
    }
    
    public float Length => Vector3.Distance(start, end);
    public Vector3 Direction => (end - start).normalized;
    public Vector3 Center => (start + end) / 2f;
    
    // Helper methods for tunnel visualization
    public Quaternion GetRotation()
    {
        return Quaternion.LookRotation(Direction);
    }
    
    public Vector3 GetScale()
    {
        return new Vector3(radius * 2f, radius * 2f, Length);
    }
}