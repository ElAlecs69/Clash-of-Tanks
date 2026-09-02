using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TanksGame.Gameplay;

namespace TanksGame.UI
{
    // Genera en tiempo de ejecución una UI mínima: un botón "Siguiente turno" y un
    // panel de texto con la vida/misiles de cada tanque y el log de la última ronda.
    // Es un placeholder funcional; la versión final debería reemplazarse por el panel
    // de instrucciones descrito en DESIGN_GUIDE.md.
    //
    // Uso: agrega este componente a un GameObject vacío y arrastra el GameManager
    // de tu escena al campo "Game Manager".
    public class GameplayUI : MonoBehaviour
    {
        public GameManager gameManager;

        private Text hudTexto;
        private Text logTexto;

        private void Start()
        {
            if (gameManager == null)
            {
                Debug.LogError("GameplayUI: falta asignar 'Game Manager' en el Inspector.");
                enabled = false;
                return;
            }

            ConstruirUI();
            ActualizarHud();
        }

        private void ConstruirUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("Canvas_TanksGame");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600, 900);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            // --- Botón "Siguiente turno" (abajo, centrado) ---
            var botonGo = new GameObject("BotonSiguienteTurno");
            botonGo.transform.SetParent(canvasGo.transform, false);
            var botonImagen = botonGo.AddComponent<Image>();
            botonImagen.color = new Color(0.85f, 0.65f, 0.15f);
            var boton = botonGo.AddComponent<Button>();

            var botonRect = botonGo.GetComponent<RectTransform>();
            botonRect.sizeDelta = new Vector2(260, 70);
            botonRect.anchorMin = new Vector2(0.5f, 0f);
            botonRect.anchorMax = new Vector2(0.5f, 0f);
            botonRect.anchoredPosition = new Vector2(0, 60);

            var botonTextoGo = new GameObject("Texto");
            botonTextoGo.transform.SetParent(botonGo.transform, false);
            var botonTexto = botonTextoGo.AddComponent<Text>();
            botonTexto.text = "SIGUIENTE TURNO";
            botonTexto.alignment = TextAnchor.MiddleCenter;
            botonTexto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            botonTexto.fontSize = 22;
            botonTexto.fontStyle = FontStyle.Bold;
            botonTexto.color = Color.black;
            var botonTextoRect = botonTextoGo.GetComponent<RectTransform>();
            botonTextoRect.anchorMin = Vector2.zero;
            botonTextoRect.anchorMax = Vector2.one;
            botonTextoRect.offsetMin = Vector2.zero;
            botonTextoRect.offsetMax = Vector2.zero;

            boton.onClick.AddListener(OnSiguienteTurno);

            // --- Panel de vida/misiles (arriba a la izquierda) ---
            hudTexto = CrearPanelDeTexto(canvasGo.transform, "PanelVida",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -20),
                new Vector2(420, 140), TextAnchor.UpperLeft);

            // --- Panel de log de la última ronda (arriba a la derecha) ---
            logTexto = CrearPanelDeTexto(canvasGo.transform, "PanelLog",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20, -20),
                new Vector2(480, 220), TextAnchor.UpperRight);
        }

        private Text CrearPanelDeTexto(Transform padre, string nombre, Vector2 anchor,
            Vector2 anchorMax, Vector2 posicion, Vector2 tamano, TextAnchor alineacion)
        {
            var fondoGo = new GameObject(nombre);
            fondoGo.transform.SetParent(padre, false);
            var fondo = fondoGo.AddComponent<Image>();
            fondo.color = new Color(0f, 0f, 0f, 0.55f);

            var rect = fondoGo.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchorMax;
            rect.pivot = anchor;
            rect.sizeDelta = tamano;
            rect.anchoredPosition = posicion;

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(fondoGo.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 18;
            texto.alignment = alineacion;
            texto.color = Color.white;

            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = new Vector2(10, 10);
            textoRect.offsetMax = new Vector2(-10, -10);

            return texto;
        }

        private void OnSiguienteTurno()
        {
            gameManager.EjecutarSiguienteTurno();
            ActualizarHud();
        }

        private void ActualizarHud()
        {
            if (gameManager.Agentes == null) return;

            var sb = new StringBuilder();
            foreach (var agente in gameManager.Agentes)
            {
                var tanque = agente.Tank;
                sb.AppendLine($"Jugador {tanque.PlayerId}: {(tanque.IsAlive ? $"{tanque.Health}% vida" : "DESTRUIDO")}"
                    + $" | Misiles: {tanque.Missiles} | Escudo: {(tanque.ShieldActive ? "activo" : "-")}");
            }
            hudTexto.text = sb.ToString();

            var log = gameManager.Turno?.LastRoundLog;
            if (log != null && log.Count > 0)
            {
                int desde = Mathf.Max(0, log.Count() - 8);
                logTexto.text = string.Join("\n", log.Skip(desde));
            }
            else
            {
                logTexto.text = "(sin turnos ejecutados aún)";
            }
        }
    }
}
