using UnityEngine;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Simple trajectory controller for flying wave packets
    /// Moves packet from current position to target with smooth motion
    /// </summary>
    public class PacketTrajectory : MonoBehaviour
    {
        private Vector3 targetPosition;
        private float speed = 5f;
        private bool isActive = false;

        public void Initialize(Vector3 target, float moveSpeed)
        {
            targetPosition = target;
            speed = moveSpeed;
            isActive = true;
        }

        void Update()
        {
            if (!isActive) return;

            // Move towards target
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

            // Check if reached target
            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                isActive = false;
                Destroy(gameObject);
            }
        }
    }
}
