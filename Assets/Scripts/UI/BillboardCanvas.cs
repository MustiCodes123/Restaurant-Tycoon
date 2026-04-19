using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private Vector3 _rotationOffset;
    [SerializeField] private bool _useCameraForward = true;
    [SerializeField] private bool _flipBackward;

    private void Awake()
    {
        if (_targetCamera == null) _targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_targetCamera == null) return;

        if (_useCameraForward)
        {
            transform.forward = _targetCamera.transform.forward;
        }
        else
        {
            transform.LookAt(transform.position + _targetCamera.transform.rotation * Vector3.forward, _targetCamera.transform.rotation * Vector3.up);
        }

        if (_flipBackward) transform.Rotate(0, 180, 0);
        
        transform.Rotate(_rotationOffset);
    }
}