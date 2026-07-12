using UnityEngine;

namespace com.vivuga.food
{
        public class RotateY: MonoBehaviour
        {
                [SerializeField] private float speed = 30f;

                private bool isRotating = true;

                void Update()
                {
                        if (!isRotating)
                                return;

                        transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
                }

                public void ToggleRotation()
                {
                        isRotating = !isRotating;

                        if (!isRotating)
                        {
                                ResetRotation();
                        }
                }

                public void SetRotation(bool state)
                {
                        isRotating = state;

                        if (!isRotating)
                        {
                                ResetRotation();
                        }
                }

                private void ResetRotation()
                {
                        Vector3 euler = transform.eulerAngles;
                        euler.y = 0f;
                        transform.eulerAngles = euler;
                }
        }
}