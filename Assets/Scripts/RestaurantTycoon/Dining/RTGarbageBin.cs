using UnityEngine;
using System.Collections;

namespace RestaurantTycoon
{
    /// <summary>
    /// Garbage disposal bin for the restaurant scene.
    /// Player must linger inside the trigger for <see cref="disposeDelay"/> seconds
    /// before all carried items are disposed, preventing accidental drops while walking by.
    /// </summary>
    public class RTGarbageBin : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private float disposeDelay = 0.5f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem disposeParticles;

        private Coroutine disposeCoroutine;

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            RTPlayerCarryController carryController = other.GetComponent<RTPlayerCarryController>();
            if (carryController == null)
                carryController = other.GetComponentInParent<RTPlayerCarryController>();

            if (carryController == null || !carryController.IsCarrying) return;

            if (disposeCoroutine != null) StopCoroutine(disposeCoroutine);
            disposeCoroutine = StartCoroutine(DelayedDispose(carryController));
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            if (disposeCoroutine != null)
            {
                StopCoroutine(disposeCoroutine);
                disposeCoroutine = null;
            }
        }

        private IEnumerator DelayedDispose(RTPlayerCarryController carryController)
        {
            yield return new WaitForSeconds(disposeDelay);

            if (carryController == null || !carryController.IsCarrying)
            {
                disposeCoroutine = null;
                yield break;
            }

            int disposed = carryController.DisposeAll();

            if (disposed > 0)
            {
                if (disposeParticles != null)
                    disposeParticles.Play();

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFX(SoundEffect.GarbageDrop);

                Debug.Log($"[RTGarbageBin] Disposed {disposed} item(s)");
            }

            disposeCoroutine = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.5f, 0.8f, 0.5f));
        }
    }
}
