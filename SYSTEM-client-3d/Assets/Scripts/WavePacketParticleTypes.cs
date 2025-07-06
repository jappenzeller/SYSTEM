using UnityEngine;
using SpacetimeDB.Types;
using System.Collections.Generic;

/// <summary>
/// Visual representation of a wave packet particle in flight
/// </summary>
public class WavePacketParticle : MonoBehaviour
{
    public ulong PacketId { get; set; }
    public WavePacketSignature Signature { get; set; }
    public Renderer Renderer { get; private set; }
    public Light Light { get; private set; }
    public TrailRenderer Trail { get; private set; }
    
    void Awake()
    {
        Renderer = GetComponent<Renderer>();
        Light = GetComponent<Light>();
        Trail = GetComponent<TrailRenderer>();
    }
    
    public void Reset()
    {
        PacketId = 0;
        if (Trail != null) Trail.Clear();
    }
    
    public void SetColor(Color color)
    {
        if (Renderer != null)
        {
            Renderer.material.color = color;
            Renderer.material.SetColor("_EmissionColor", color * 2f);
        }
        
        if (Light != null)
        {
            Light.color = color;
        }
        
        if (Trail != null)
        {
            Trail.startColor = color;
            Trail.endColor = color * 0.5f;
        }
    }
}

/// <summary>
/// Object pool for wave packet particles
/// </summary>
public class WavePacketParticlePool : MonoBehaviour
{
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private int poolSize = 20;
    
    private Queue<WavePacketParticle> pool = new Queue<WavePacketParticle>();
    
    void Start()
    {
        // Pre-populate pool
        for (int i = 0; i < poolSize; i++)
        {
            CreateParticle();
        }
    }
    
    private void CreateParticle()
    {
        GameObject obj = particlePrefab != null ? Instantiate(particlePrefab) : new GameObject("WavePacketParticle");
        obj.transform.SetParent(transform);
        obj.SetActive(false);
        
        WavePacketParticle particle = obj.GetComponent<WavePacketParticle>();
        if (particle == null)
        {
            particle = obj.AddComponent<WavePacketParticle>();
        }
        
        pool.Enqueue(particle);
    }
    
    public WavePacketParticle Get()
    {
        if (pool.Count == 0)
        {
            CreateParticle();
        }
        
        WavePacketParticle particle = pool.Dequeue();
        particle.gameObject.SetActive(true);
        particle.Reset();
        return particle;
    }
    
    public void Return(WavePacketParticle particle)
    {
        if (particle != null && particle.gameObject != null)
        {
            particle.gameObject.SetActive(false);
            particle.Reset();
            pool.Enqueue(particle);
        }
    }
}