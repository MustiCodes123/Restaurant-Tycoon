using UnityEngine;
namespace com.vivuga.food
{
        public class CameraViewSwitcher: MonoBehaviour
        {
                [System.Serializable]
                public class CameraPoint
                {
                        public Vector3 position;
                        public Vector3 rotation;
                }

                [SerializeField] private Camera targetCamera;
                [SerializeField] private CameraPoint[] cameraPoints;

                public void SwitchToPoint(int index)
                {
                        if (targetCamera == null)
                                return;
                        if (cameraPoints == null || index < 0 || index >= cameraPoints.Length)
                                return;

                        targetCamera.transform.position = cameraPoints[index].position;
                        targetCamera.transform.eulerAngles = cameraPoints[index].rotation;
                }
        }
}