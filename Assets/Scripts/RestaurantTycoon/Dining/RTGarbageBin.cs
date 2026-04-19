using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Garbage disposal bin for the restaurant scene.
    /// When the player enters the trigger, all Garbage-type items
    /// are automatically disposed from the carry stack.
    /// </summary>
    public class RTGarbageBin : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Effects")]
        [SerializeField] private ParticleSystem disposeParticles;

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            RTPlayerCarryController carryController = other.GetComponent<RTPlayerCarryController>();
            if (carryController == null)
                carryController = other.GetComponentInParent<RTPlayerCarryController>();

            if (carryController == null || !carryController.IsCarrying) return;

            int disposed = carryController.DisposeAllOfType(CarryableType.Garbage);

            if (disposed > 0)
            {
                if (disposeParticles != null)
                    disposeParticles.Play();

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(SoundEffect.GarbageDrop);

                Debug.Log($"[RTGarbageBin] Disposed {disposed} dirty dishes");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 0.8f, 0.5f));
        }
    }
}
