using UnityEngine;

namespace Mandato.Presentation
{
    public class CameraMenu : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float cameraRotationSpeed = 5f;

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (targetCamera != null)
            {
                targetCamera.transform.Rotate(new Vector3(0f, cameraRotationSpeed * Time.deltaTime, 0f), Space.World);
            }
        }
    }
}
