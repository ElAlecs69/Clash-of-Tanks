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
    //
    // IMPORTANTE: este script asume que la Camera de este GameObject está en
    // modo Orthographic (Projection = Orthographic en el Inspector, como está
    // configurada la Main Camera de la escena). Antes había además una rama
    // para cámara en perspectiva (basada en fieldOfView y en "distance" como
    // zoom); se quitó por completo -- ese código nunca se usaba con la cámara
    // real del proyecto y solo agregaba confusión. Si en algún momento hace
    // falta perspectiva de nuevo, hay que reintroducir esa rama a propósito.
    public class OrbitZoomCamera : MonoBehaviour
    {
        public Transform target;

        [Header("Distancia física de la cámara (no es zoom)")]
        [Tooltip("Qué tan lejos, en línea recta, se coloca la cámara respecto del target. En modo Orthographic esto NO cambia el zoom (eso lo controla 'Tamaño Ortográfico' de abajo) -- solo necesita ser lo bastante grande para que la cámara no quede clipeada por el Near/Far Plane.")]
        public float distance = 20f;

        [Header("Zoom (tamaño ortográfico)")]
        [Tooltip("Tamaño ortográfico inicial/actual de la cámara. Es el equivalente al 'zoom' real cuando Projection = Orthographic.")]
        public float orthographicSize = 8f;
        public float minOrthographicSize = 3f;
        public float maxOrthographicSize = 40f; // GameManager lo sube más todavía si el tablero lo necesita.
        public float zoomSpeed = 0.01f; // el scroll del nuevo Input System entrega valores grandes (~120)

        [Header("Órbita")]
        public float orbitSpeed = 0.15f; // sensibilidad del delta del mouse en píxeles
        public float initialPitch = 45f; // ángulo isométrico clásico

        private float yaw = 45f;
        private float pitch;
        private Camera camaraUnity;

        // Antes el "target" se creaba recién en Start(). GameManager.Awake()
        // llama a PanTo(...) (para centrar la cámara en el tablero y luego
        // calcular el tamaño ortográfico necesario) ANTES de que el Start() de
        // este componente llegue a ejecutarse -- Unity corre todos los Awake()
        // de la escena antes que cualquier Start(). Si en el Inspector el campo
        // "target" se dejó vacío, esa llamada a PanTo() encontraba target ==
        // null y lanzaba una NullReferenceException. Crear el target en
        // Awake() (que sí corre antes que el Awake() de GameManager, porque
        // ambos son Awake) y, por si el orden real llegara a variar, dejar
        // además una comprobación de seguridad en PanTo(), resuelve el
        // problema de raíz.
        private void Awake()
        {
            if (target == null)
            {
                var go = new GameObject("CameraTarget");
                target = go.transform;
                target.position = Vector3.zero;
            }

            camaraUnity = GetComponent<Camera>();
        }

        private void Start()
        {
            pitch = initialPitch;

            if (camaraUnity != null)
            {
                if (!camaraUnity.orthographic)
                {
                    Debug.LogWarning("OrbitZoomCamera: la Camera de este GameObject no está en modo Orthographic. Este script fue simplificado para trabajar solo en ese modo -- cambiá 'Projection' a Orthographic en el Inspector de la Camera.");
                }

                camaraUnity.orthographicSize = orthographicSize;
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
            if (mouse == null || camaraUnity == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                // El zoom real en modo Orthographic es el tamaño ortográfico,
                // no "distance" (que solo posiciona la cámara físicamente).
                orthographicSize -= scroll * zoomSpeed;
                orthographicSize = Mathf.Clamp(orthographicSize, minOrthographicSize, maxOrthographicSize);
                camaraUnity.orthographicSize = orthographicSize;
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

        // Usado por GameManager para fijar el zoom (tamaño ortográfico)
        // necesario para que el tablero completo entre en cámara, y para
        // subir el límite máximo si un tablero grande lo pide.
        public void SetOrthographicSize(float size)
        {
            if (camaraUnity == null) camaraUnity = GetComponent<Camera>();
            if (size > maxOrthographicSize) maxOrthographicSize = size;

            orthographicSize = Mathf.Clamp(size, minOrthographicSize, maxOrthographicSize);
            if (camaraUnity != null) camaraUnity.orthographicSize = orthographicSize;
        }
    }
}
