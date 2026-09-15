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
        public float maxDistance = 40f; // el GameManager lo sube más todavía si el tablero lo necesita.
        public float zoomSpeed = 0.01f; // el scroll del nuevo Input System entrega valores grandes (~120)
        public float orbitSpeed = 0.15f; // sensibilidad del delta del mouse en píxeles
        public float initialPitch = 45f; // ángulo isométrico clásico

        private float yaw = 45f;
        private float pitch;

        // Antes el "target" se creaba recién en Start(). GameManager.Awake()
        // llama a PanTo(...) (para centrar la cámara en el tablero y luego
        // calcular la distancia necesaria) ANTES de que el Start() de este
        // componente llegue a ejecutarse -- Unity corre todos los Awake() de
        // la escena antes que cualquier Start(). Si en el Inspector el campo
        // "target" se dejó vacío (como en esta escena), esa llamada a PanTo()
        // encontraba target == null y lanzaba una NullReferenceException que
        // interrumpía CentrarCamaraEnTablero() justo antes de la línea que
        // recalcula "distance"/"maxDistance" -- por eso la cámara se quedaba
        // fija en lo que tenía puesto en el Editor y no se alejaba nunca en
        // tableros grandes (20x20), aunque el cálculo trigonométrico en sí
        // estaba bien. Crear el target en Awake() (que sí corre antes que el
        // Awake() de GameManager, porque ambos son Awake) y, por si el orden
        // real llegara a variar, dejar además una comprobación de seguridad
        // en PanTo(), resuelve el problema de raíz.
        private void Awake()
        {
            if (target == null)
            {
                var go = new GameObject("CameraTarget");
                target = go.transform;
                target.position = Vector3.zero;
            }
        }

        private void Start()
        {
            pitch = initialPitch;
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
            // Salvaguarda extra: si algo llegara a llamar a PanTo() antes de
            // que Awake() haya corrido (por ejemplo si este componente está
            // deshabilitado en la escena), no truena con NullReferenceException.
            if (target == null)
            {
                var go = new GameObject("CameraTarget");
                target = go.transform;
            }
            target.position = worldPosition;
        }
    }
}