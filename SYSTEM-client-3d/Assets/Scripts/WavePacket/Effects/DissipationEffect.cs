using UnityEngine;

namespace SYSTEM.WavePacket.Effects
{
    /// <summary>
    /// Controls particle effect playback for wave packet source dissipation.
    /// Parameterizes particle color based on the dissipated frequency.
    /// </summary>
    public class DissipationEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particleSystem;

        private void Awake()
        {
            // Auto-find particle system if not assigned
            if (particleSystem == null)
            {
                particleSystem = GetComponent<ParticleSystem>();
            }
        }

        /// <summary>
        /// Play the dissipation effect with the specified frequency color.
        /// </summary>
        /// <param name="frequencyColor">Color matching the dissipated frequency</param>
        public void Play(Color frequencyColor)
        {
            if (particleSystem == null)
            {
                SystemDebug.LogWarning(SystemDebug.Category.SourceVisualization,
                    "DissipationEffect: No ParticleSystem assigned");
                return;
            }

            // Set particle start color to match frequency
            var main = particleSystem.main;
            main.startColor = frequencyColor;

            // Play burst
            particleSystem.Play();

            SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                $"Playing dissipation effect with color {frequencyColor}");
        }

        /// <summary>
        /// Check if the particle system has finished playing.
        /// </summary>
        public bool IsPlaying => particleSystem != null && particleSystem.isPlaying;
    }
}
