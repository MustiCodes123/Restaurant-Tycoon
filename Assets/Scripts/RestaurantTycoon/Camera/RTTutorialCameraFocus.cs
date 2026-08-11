using System.Collections.Generic;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Plays short cinematic focus beats for tutorials and newly unlocked content.
    /// It temporarily drives the existing CinemachineCamera through a runtime anchor,
    /// then restores the normal follow/look targets when the beat finishes.
    /// </summary>
    public class RTTutorialCameraFocus : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private CinemachineCamera virtualCamera;

        [Header("Timing")]
        [SerializeField] private float moveDuration = 1f;
        [SerializeField] private float holdDuration = 0.8f;
        [SerializeField] private float returnDuration = 0.9f;
        [SerializeField] private Ease moveEase = Ease.InOutSine;

        [Header("Zoom")]
        [SerializeField] private float focusFieldOfView = 38f;

        private static RTTutorialCameraFocus instance;

        private readonly Queue<Transform[]> queuedFocuses = new Queue<Transform[]>();
        private Transform focusAnchor;
        private Sequence activeSequence;
        private RTCameraZoom cameraZoom;
        private Transform originalFollow;
        private Transform originalLookAt;
        private bool originalCustomLookAt;
        private float originalFieldOfView;
        private bool isPlaying;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            EnsureCamera();
            EnsureAnchor();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            activeSequence?.Kill();
            if (focusAnchor != null)
                Destroy(focusAnchor.gameObject);
        }

        public static void Play(Transform target)
        {
            if (target == null) return;

            RTTutorialCameraFocus focus = GetOrCreate();
            if (focus == null) return;

            focus.queuedFocuses.Clear();
            focus.CancelActiveFocus();
            focus.queuedFocuses.Enqueue(new[] { target });
            focus.TryPlayNext();
        }

        public static void PlaySequence(params Transform[] targets)
        {
            RTTutorialCameraFocus focus = GetOrCreate();
            if (focus == null) return;

            Transform[] validTargets = FilterTargets(targets);
            if (validTargets.Length == 0) return;

            focus.queuedFocuses.Enqueue(validTargets);
            focus.TryPlayNext();
        }

        public static void PlaySequence(IEnumerable<Transform> targets)
        {
            if (targets == null) return;

            List<Transform> list = new List<Transform>();
            foreach (Transform target in targets)
                if (target != null)
                    list.Add(target);

            PlaySequence(list.ToArray());
        }

        private static RTTutorialCameraFocus GetOrCreate()
        {
            if (instance != null)
                return instance;

            CinemachineCamera camera = FindFirstObjectByType<CinemachineCamera>();
            if (camera == null)
            {
                Debug.LogWarning("[RTTutorialCameraFocus] No CinemachineCamera found for tutorial focus.");
                return null;
            }

            return camera.gameObject.AddComponent<RTTutorialCameraFocus>();
        }

        private static Transform[] FilterTargets(Transform[] targets)
        {
            if (targets == null || targets.Length == 0)
                return new Transform[0];

            List<Transform> validTargets = new List<Transform>();
            foreach (Transform target in targets)
                if (target != null)
                    validTargets.Add(target);

            return validTargets.ToArray();
        }

        private void TryPlayNext()
        {
            if (isPlaying || queuedFocuses.Count == 0)
                return;

            Transform[] targets = queuedFocuses.Dequeue();
            if (targets.Length == 0)
            {
                TryPlayNext();
                return;
            }

            PlayTargets(targets);
        }

        private void CancelActiveFocus()
        {
            if (activeSequence != null)
            {
                Sequence sequence = activeSequence;
                activeSequence = null;
                sequence.Kill();
            }
        }

        private void PlayTargets(Transform[] targets)
        {
            if (!EnsureCamera())
                return;

            EnsureAnchor();
            CacheOriginalCameraState();

            isPlaying = true;
            cameraZoom = virtualCamera.GetComponent<RTCameraZoom>();
            if (cameraZoom != null)
                cameraZoom.SetExternalControl(true);

            focusAnchor.position = GetReturnPosition();
            virtualCamera.Follow = focusAnchor;
            virtualCamera.LookAt = focusAnchor;

            activeSequence?.Kill();
            activeSequence = DOTween.Sequence().SetTarget(this);

            foreach (Transform target in targets)
            {
                activeSequence
                    .Append(focusAnchor.DOMove(target.position, moveDuration).SetEase(moveEase))
                    .Join(DOTween.To(GetFieldOfView, SetFieldOfView, focusFieldOfView, moveDuration).SetEase(moveEase))
                    .AppendInterval(holdDuration);
            }

            activeSequence
                .Append(focusAnchor.DOMove(GetReturnPosition(), returnDuration).SetEase(moveEase))
                .Join(DOTween.To(GetFieldOfView, SetFieldOfView, originalFieldOfView, returnDuration).SetEase(moveEase))
                .OnComplete(FinishFocus)
                .OnKill(() =>
                {
                    if (isPlaying)
                        FinishFocus();
                });
        }

        private bool EnsureCamera()
        {
            if (virtualCamera == null)
                virtualCamera = GetComponent<CinemachineCamera>();

            if (virtualCamera == null)
                virtualCamera = FindFirstObjectByType<CinemachineCamera>();

            return virtualCamera != null;
        }

        private void EnsureAnchor()
        {
            if (focusAnchor != null) return;

            GameObject anchor = new GameObject("RT Tutorial Camera Focus Anchor");
            anchor.hideFlags = HideFlags.HideAndDontSave;
            focusAnchor = anchor.transform;
        }

        private void CacheOriginalCameraState()
        {
            originalFollow = virtualCamera.Target.TrackingTarget;
            originalLookAt = virtualCamera.Target.LookAtTarget;
            originalCustomLookAt = virtualCamera.Target.CustomLookAtTarget;
            originalFieldOfView = virtualCamera.Lens.FieldOfView;
        }

        private Vector3 GetReturnPosition()
        {
            if (originalFollow != null)
                return originalFollow.position;

            if (virtualCamera != null)
                return virtualCamera.transform.position;

            return Vector3.zero;
        }

        private float GetFieldOfView()
        {
            return virtualCamera != null ? virtualCamera.Lens.FieldOfView : originalFieldOfView;
        }

        private void SetFieldOfView(float value)
        {
            if (virtualCamera == null) return;

            LensSettings lens = virtualCamera.Lens;
            lens.FieldOfView = value;
            virtualCamera.Lens = lens;
        }

        private void FinishFocus()
        {
            if (!isPlaying) return;

            isPlaying = false;
            activeSequence = null;

            if (virtualCamera != null)
            {
                virtualCamera.Follow = originalFollow;
                virtualCamera.Target.LookAtTarget = originalLookAt;
                virtualCamera.Target.CustomLookAtTarget = originalCustomLookAt;
                SetFieldOfView(originalFieldOfView);
            }

            if (cameraZoom != null)
            {
                cameraZoom.SyncToCurrentFieldOfView();
                cameraZoom.SetExternalControl(false);
                cameraZoom = null;
            }

            TryPlayNext();
        }
    }
}
