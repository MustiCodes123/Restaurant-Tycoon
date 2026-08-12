using DG.Tweening;
using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Lightweight visual helper for future unlock previews. Attach this to the
    /// locked tile root and assign the lock icon child if it should subtly pulse.
    /// </summary>
    public class RTLockedUnlockPreview : MonoBehaviour
    {
        [Header("Preview")]
        [SerializeField] private GameObject previewRoot;
        [SerializeField] private GameObject lockIconRoot;
        [Tooltip("Disable colliders while the locked preview is visible so it never blocks gameplay.")]
        [SerializeField] private bool disableCollidersWhileVisible = true;

        [Header("Lock Icon Pulse")]
        [SerializeField] private Transform pulseTarget;
        [SerializeField] private float pulseScale = 1.08f;
        [SerializeField] private float pulseDuration = 0.75f;
        [SerializeField] private Ease pulseEase = Ease.InOutSine;

        private Collider[] cachedColliders;
        private Vector3 originalPulseScale = Vector3.one;
        private Tween pulseTween;
        private bool initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized)
                return;

            initialized = true;

            if (previewRoot == null)
                previewRoot = gameObject;

            if (pulseTarget == null && lockIconRoot != null)
                pulseTarget = lockIconRoot.transform;

            if (pulseTarget != null)
                originalPulseScale = pulseTarget.localScale;

            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnDisable()
        {
            StopPulse();
        }

        private void OnDestroy()
        {
            StopPulse();
        }

        public void ShowLocked()
        {
            EnsureInitialized();

            if (previewRoot != null)
                previewRoot.SetActive(true);

            if (lockIconRoot != null)
                lockIconRoot.SetActive(true);

            SetCollidersEnabled(!disableCollidersWhileVisible);
            StartPulse();
        }

        public void HideLocked()
        {
            EnsureInitialized();
            StopPulse();

            if (lockIconRoot != null)
                lockIconRoot.SetActive(false);

            if (previewRoot != null)
                previewRoot.SetActive(false);
        }

        private void StartPulse()
        {
            EnsureInitialized();

            if (pulseTarget == null)
                return;

            StopPulse();
            pulseTarget.localScale = originalPulseScale;
            pulseTween = pulseTarget
                .DOScale(originalPulseScale * pulseScale, pulseDuration)
                .SetEase(pulseEase)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopPulse()
        {
            pulseTween?.Kill();
            pulseTween = null;

            if (pulseTarget != null)
                pulseTarget.localScale = originalPulseScale;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            EnsureInitialized();

            if (cachedColliders == null)
                return;

            foreach (var collider in cachedColliders)
            {
                if (collider != null)
                    collider.enabled = enabled;
            }
        }
    }
}
