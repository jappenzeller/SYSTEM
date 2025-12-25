using UnityEngine;
using SYSTEM.Debug;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Animates a wave packet flying from orb to player
    /// Auto-destroys on reaching target
    /// </summary>
    public class FlyingPacket : MonoBehaviour
    {
        public Vector3 targetPosition;
        public float speed = 5f;
        public ulong packetId;

        private float startTime;
        private Vector3 startPosition;
        private float journeyLength;

        void Start()
        {
            startPosition = transform.position;
            startTime = Time.time;
            journeyLength = Vector3.Distance(startPosition, targetPosition);
        }

        void Update()
        {
            // Calculate distance traveled
            float distanceCovered = (Time.time - startTime) * speed;
            float fractionOfJourney = distanceCovered / journeyLength;

            // Move toward target
            transform.position = Vector3.Lerp(startPosition, targetPosition, fractionOfJourney);

            // Check if reached target
            if (fractionOfJourney >= 1f)
            {
                OnReachedTarget();
                Destroy(gameObject);
            }
        }

        void OnReachedTarget()
        {
            // Event or callback could go here
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"[FlyingPacket] Packet {packetId} reached target");
        }
    }
}
