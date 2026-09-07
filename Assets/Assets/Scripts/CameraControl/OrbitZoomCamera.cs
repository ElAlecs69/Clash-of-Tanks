using UnityEngine;
using UnityEngine.InputSystem;

namespace TanksGame.CameraControl
{
    // Cámara isométrica con órbita libre y zoom, usando el Input System nuevo de Unity.
    // Botón derecho + arrastrar = orbitar. Rueda del mouse = zoom.
    //
    // Requiere el paquete "Input System" (Window > Package Manager > Input System).
    // En Project Settings > Player > Active Input Handling debe estar en
    // "Input System Package (New)" o "Both".
    public class OrbitZoomCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 12f;
        public float minDistance = 5f;
        public float maxDistance = 25f;
        public float zoomSpeed = 0.01f; // el scroll del nuevo Input System entrega valores grandes (~120)
        public float orbitSpeed = 0.15f; // sensibilidad del delta del mouse en píxeles
        public float initialPitch = 45f; // ángulo isométrico clásico

        private float yaw = 45f;
        private float pitch;

        private void Start()
        {
            pitch = initialPitch;
            if (target == null)
            {
                var go = new GameObject("CameraTarget");
                target = go.transform;
                target.position = Vector3.zero;
            }
            UpdatePosition();
        }

        private void Update()
        {
            HandleOrbitInput();
            HandleZoomInput();
            UpdatePosition();
        }

        private void HandleOrbitInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * orbitSpeed;
                pitch -= delta.y * orbitSpeed;
                pitch = Mathf.Clamp(pitch, 15f, 80f);
            }
        }

        private void HandleZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }

        private void UpdatePosition()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var offset = rotation * new Vector3(0f, 0f, -distance);
            transform.position = target.position + offset;
            transform.LookAt(target.position);
        }

        public void PanTo(Vector3 worldPosition)
        {
            target.position = worldPosition;
        }
    }
}
