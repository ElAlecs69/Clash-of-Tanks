using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TanksGame.UI
{
    // Pantalla de inicio, generada por código igual que GameplayUI.
    // Uso: crea una escena nueva (ej. "MenuPrincipal"), agrega un GameObject vacío
    // y este componente, y agrega tanto esta escena como la del juego
    // ("SampleScene" u otra) en File > Build Settings > Scenes In Build.
    public class MenuPrincipal : MonoBehaviour
    {
        [Header("Escena a cargar al iniciar partida")]
        public string nombreEscenaJuego = "SampleScene";

        [Header("Textos")]
        public string tituloJuego = "CLASH OF TANKS";

        private GameObject panelOpciones;

        private void Start()
        {
            ConstruirUI();
        }

        private void ConstruirUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("Canvas_MenuPrincipal");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600, 900);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            // --- Fondo oscuro a pantalla completa ---
            var fondoGo = new GameObject("Fondo");
            fondoGo.transform.SetParent(canvasGo.transform, false);
            var fondoImagen = fondoGo.AddComponent<Image>();
            fondoImagen.color = new Color(0.03f, 0.03f, 0.05f);
            var fondoRect = fondoGo.GetComponent<RectTransform>();
            fondoRect.anchorMin = Vector2.zero;
            fondoRect.anchorMax = Vector2.one;
            fondoRect.offsetMin = Vector2.zero;
            fondoRect.offsetMax = Vector2.zero;

            // --- Título ---
            var tituloGo = new GameObject("Titulo");
            tituloGo.transform.SetParent(canvasGo.transform, false);
            var tituloTexto = tituloGo.AddComponent<Text>();
            tituloTexto.text = tituloJuego;
            tituloTexto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tituloTexto.fontSize = 64;
            tituloTexto.fontStyle = FontStyle.Bold;
            tituloTexto.alignment = TextAnchor.MiddleCenter;
            tituloTexto.color = new Color(0.92f, 0.92f, 0.95f);
            var tituloRect = tituloGo.GetComponent<RectTransform>();
            tituloRect.anchorMin = new Vector2(0.5f, 1f);
            tituloRect.anchorMax = new Vector2(0.5f, 1f);
            tituloRect.pivot = new Vector2(0.5f, 1f);
            tituloRect.sizeDelta = new Vector2(900, 120);
            tituloRect.anchoredPosition = new Vector2(0, -140);

            // --- Lista de botones, apilados y centrados ---
            var listaGo = new GameObject("Botones");
            listaGo.transform.SetParent(canvasGo.transform, false);
            var listaRect = listaGo.AddComponent<RectTransform>();
            listaRect.anchorMin = new Vector2(0.5f, 0.5f);
            listaRect.anchorMax = new Vector2(0.5f, 0.5f);
            listaRect.pivot = new Vector2(0.5f, 0.5f);
            listaRect.sizeDelta = new Vector2(400, 220);
            listaRect.anchoredPosition = new Vector2(0, -40);

            CrearBotonDeMenu(listaRect, "INICIAR JUEGO", new Vector2(0, 80), OnIniciarJuego);
            CrearBotonDeMenu(listaRect, "OPCIONES", new Vector2(0, 0), OnOpciones);
            CrearBotonDeMenu(listaRect, "SALIR", new Vector2(0, -80), OnSalir);

            // --- Panel de opciones (oculto por defecto, placeholder) ---
            ConstruirPanelOpciones(canvasGo.transform);
        }

        // Botón minimalista: sin fondo visible, solo texto que resalta al pasar el mouse
        // (estilo cercano al menú de Hollow Knight, sin usar imágenes/artes todavía).
        private void CrearBotonDeMenu(Transform padre, string texto, Vector2 posicion, UnityEngine.Events.UnityAction accion)
        {
            var botonGo = new GameObject($"Boton_{texto}");
            botonGo.transform.SetParent(padre, false);

            var imagen = botonGo.AddComponent<Image>();
            imagen.color = new Color(1f, 1f, 1f, 0f); // Transparente: solo ocupa el área clicable.

            var boton = botonGo.AddComponent<Button>();
            var colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(0.85f, 0.65f, 0.15f); // Resalta en dorado al pasar el mouse.
            colores.pressedColor = new Color(0.65f, 0.48f, 0.1f);
            boton.colors = colores;
            boton.onClick.AddListener(accion);

            var rect = botonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360, 60);
            rect.anchoredPosition = posicion;

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(botonGo.transform, false);
            var textoUI = textoGo.AddComponent<Text>();
            textoUI.text = texto;
            textoUI.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoUI.fontSize = 28;
            textoUI.fontStyle = FontStyle.Bold;
            textoUI.alignment = TextAnchor.MiddleCenter;
            textoUI.color = Color.white;

            // El texto también debe ser "Graphic" objetivo del botón para que el
            // Button.colors tiña el texto (no solo la imagen transparente).
            boton.targetGraphic = imagen;

            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = Vector2.zero;
            textoRect.offsetMax = Vector2.zero;
        }

        private void ConstruirPanelOpciones(Transform padre)
        {
            panelOpciones = new GameObject("PanelOpciones");
            panelOpciones.transform.SetParent(padre, false);
            var fondo = panelOpciones.AddComponent<Image>();
            fondo.color = new Color(0f, 0f, 0f, 0.85f);

            var rect = panelOpciones.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(500, 300);
            rect.anchoredPosition = Vector2.zero;

            var textoGo = new GameObject("Texto");
            textoGo.transform.SetParent(panelOpciones.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.text = "OPCIONES\n(pendiente de implementar)";
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 24;
            texto.alignment = TextAnchor.UpperCenter;
            texto.color = Color.white;
            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = new Vector2(0f, 0.4f);
            textoRect.anchorMax = new Vector2(1f, 1f);
            textoRect.offsetMin = new Vector2(20, 0);
            textoRect.offsetMax = new Vector2(-20, -20);

            CrearBotonDeMenu(panelOpciones.transform, "VOLVER", new Vector2(0, -100), OnCerrarOpciones);

            panelOpciones.SetActive(false);
        }

        private void OnIniciarJuego()
        {
            SceneManager.LoadScene(nombreEscenaJuego);
        }

        private void OnOpciones()
        {
            panelOpciones.SetActive(true);
        }

        private void OnCerrarOpciones()
        {
            panelOpciones.SetActive(false);
        }

        private void OnSalir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
