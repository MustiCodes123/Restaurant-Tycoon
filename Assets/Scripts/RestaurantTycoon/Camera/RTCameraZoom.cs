using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

namespace RestaurantTycoon
{
    /// <summary>
    /// Handles zoom in / zoom out for a CinemachineCamera via two UI buttons.
    /// Attach to any GameObject in the scene and assign the camera and buttons in the Inspector.
    /// </summary>
    public class RTCameraZoom : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private CinemachineCamera virtualCamera;

        [Header("Zoom Limits")]
        [SerializeField] private float zoomMin = 20f;
        [SerializeField] private float zoomMax = 70f;

        [Header("Zoom Behaviour")]
        [Tooltip("How many degrees of FOV each button press changes.")]
        [SerializeField] private float zoomStep = 5f;
        [Tooltip("How smoothly the camera interpolates to the target FOV.")]
        [SerializeField] private float zoomSmoothSpeed = 8f;

        [Header("Buttons")]
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;

        private float targetFOV;

        private void Start()
        {
            if (virtualCamera == null)
                virtualCamera = GetComponent<CinemachineCamera>();

            if (virtualCamera != null)
                targetFOV = virtualCamera.Lens.FieldOfView;

            if (zoomInButton != null)
                zoomInButton.onClick.AddListener(ZoomIn);

            if (zoomOutButton != null)
                zoomOutButton.onClick.AddListener(ZoomOut);
        }

        private void Update()
        {
            if (virtualCamera == null) return;

            var lens = virtualCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFOV, Time.deltaTime * zoomSmoothSpeed);
            virtualCamera.Lens = lens;
        }

        public void ZoomIn()
        {
            targetFOV = Mathf.Clamp(targetFOV - zoomStep, zoomMin, zoomMax);
        }

        public void ZoomOut()
        {
            targetFOV = Mathf.Clamp(targetFOV + zoomStep, zoomMin, zoomMax);
        }
    }
}
