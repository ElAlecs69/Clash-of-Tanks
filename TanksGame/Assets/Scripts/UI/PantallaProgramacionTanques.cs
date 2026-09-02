using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TanksGame.UI
{
    // Esqueleto de la pantalla "Programación de Tanques" (barra superior + historial +
    // terminal/editor + vista previa del tanque + personalización de skin), generado por
    // código SOLO para dejar cada elemento en su posición y tamaño correctos.
    //
    // A propósito usa colores planos y rectángulos simples: la idea es que reemplaces cada
    // pieza por tu arte final (fondo, iconos, fuente) directo en el Inspector después,
    // sin tener que rearmar el layout. Cada GameObject tiene un nombre descriptivo para
    // que sea fácil encontrar qué tocar (ej. "ImagenVistaPreviaTanque", "BotonAbrirTerminal").
    public class PantallaProgramacionTanques : MonoBehaviour
    {
        [Header("Colores placeholder (se usan solo si no asignas un sprite abajo)")]
        public Color colorFondoPantalla = new Color(0.05f, 0.05f, 0.05f);
        public Color colorPanel = new Color(0.11f, 0.11f, 0.12f);
        public Color colorBoton = new Color(0.2f, 0.2f, 0.21f);
        public Color colorBotonAccentoRojo = new Color(0.55f, 0.14f, 0.14f);
        public Color colorBotonAccentoVerde = new Color(0.16f, 0.45f, 0.2f);
        public Color colorTexto = new Color(0.92f, 0.92f, 0.92f);
        public Color colorTextoSecundario = new Color(0.6f, 0.6f, 0.62f);
        public Color colorSwatch = new Color(0.3f, 0.3f, 0.32f);

        [Header("Arte real (opcional). Arrastra tus PNG acá; si dejas un campo vacío, se usa el color de arriba.")]
        public Sprite spriteFondoPantalla;
        public Sprite spriteMarcoPanel;      // Se reutiliza en la barra superior y los 4 paneles.
        public Sprite spriteFondoBoton;      // Se reutiliza en los 5 botones de acción.
        public Sprite spriteIconoEmblema;
        public Sprite spriteIconoHistorial;  // Se reutiliza en cada fila del historial.
        public Sprite spriteVistaPreviaTanque;

        // Nombres de ejemplo para la lista de "Historial de Scripts" (podés reemplazar
        // esto por datos reales más adelante).
        private readonly string[] historialEjemplo =
        {
            "ataque_base.txt", "defensa.txt", "explorador.txt", "francotirador.txt",
            "asalto_nocturno.txt", "guardian.txt", "rapido_y_furioso.txt", "prueba_final.txt"
        };

        private Text textoLineaCaracterEstado;

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

            var canvasGo = new GameObject("Canvas_ProgramacionTanques");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1536, 1024); // mismo tamaño que tu mockup.
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            var fondo = CrearRect(canvasGo.transform, "Fondo", Vector2.zero, Vector2.zero, colorFondoPantalla,
                spriteFondoPantalla, sliced: false);
            var fondoRect = fondo.GetComponent<RectTransform>();
            fondoRect.anchorMin = Vector2.zero;
            fondoRect.anchorMax = Vector2.one;
            fondoRect.offsetMin = Vector2.zero;
            fondoRect.offsetMax = Vector2.zero;

            ConstruirBarraSuperior(canvasGo.transform);
            ConstruirPanelHistorial(canvasGo.transform);
            ConstruirPanelTerminal(canvasGo.transform);
            ConstruirPanelVistaPrevia(canvasGo.transform);
            ConstruirPanelPersonalizarSkin(canvasGo.transform);
        }

        // ------------------------------------------------------------------
        // BARRA SUPERIOR: título + 5 botones de acción.
        // ------------------------------------------------------------------

        private void ConstruirBarraSuperior(Transform padre)
        {
            var barra = CrearRect(padre, "BarraSuperior", new Vector2(0, 0), new Vector2(1536, 215), colorPanel, spriteMarcoPanel);

            CrearRect(barra.transform, "IconoEmblema", new Vector2(15, 10), new Vector2(70, 70), colorBotonAccentoRojo,
                spriteIconoEmblema, sliced: false);
            CrearTexto(barra.transform, "TituloPantalla", "PROGRAMACIÓN DE TANQUES",
                new Vector2(100, 10), new Vector2(700, 60), 32, FontStyle.Bold, TextAnchor.MiddleLeft, colorTexto);

            float anchoBoton = 289f, alto = 95f, y = 105f, gap = 15f;
            float[] xBotones = { 15, 319, 623, 927, 1231 };

            CrearBotonAccion(barra.transform, "BotonGuardarScript", "GUARDAR SCRIPT", ".TXT",
                new Vector2(xBotones[0], y), new Vector2(anchoBoton, alto), colorBoton, OnGuardarScript);
            CrearBotonAccion(barra.transform, "BotonCargarScript", "CARGAR SCRIPT", ".TXT",
                new Vector2(xBotones[1], y), new Vector2(anchoBoton, alto), colorBoton, OnCargarScript);
            CrearBotonAccion(barra.transform, "BotonAbrirTerminal", "ABRIR TERMINAL", "EDITOR DE SCRIPT",
                new Vector2(xBotones[2], y), new Vector2(anchoBoton, alto), colorBotonAccentoRojo, OnAbrirTerminal, colorEsAccento: true);
            CrearBotonAccion(barra.transform, "BotonBorrarScript", "BORRAR SCRIPT", "",
                new Vector2(xBotones[3], y), new Vector2(anchoBoton, alto), colorBoton, OnBorrarScript);
            CrearBotonAccion(barra.transform, "BotonIniciarEjecucion", "INICIAR", "EJECUCIÓN",
                new Vector2(xBotones[4], y), new Vector2(anchoBoton, alto), colorBotonAccentoVerde, OnIniciarEjecucion, colorEsAccento: true);
        }

        // ------------------------------------------------------------------
        // PANEL IZQUIERDO: historial de scripts.
        // ------------------------------------------------------------------

        private void ConstruirPanelHistorial(Transform padre)
        {
            var panel = CrearRect(padre, "PanelHistorial", new Vector2(15, 225), new Vector2(325, 780), colorPanel, spriteMarcoPanel);

            CrearTexto(panel.transform, "TituloHistorial", "HISTORIAL DE SCRIPTS",
                new Vector2(15, 15), new Vector2(295, 30), 20, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            float y = 65f;
            for (int i = 0; i < historialEjemplo.Length; i++)
            {
                string nombre = historialEjemplo[i];
                CrearItemHistorial(panel.transform, $"ItemHistorial_{i}", nombre, new Vector2(15, y));
                y += 62f;
            }

            CrearBotonAccion(panel.transform, "BotonLimpiarHistorial", "LIMPIAR HISTORIAL", "",
                new Vector2(15, 780 - 65), new Vector2(295, 50), colorBoton, OnLimpiarHistorial);
        }

        private void CrearItemHistorial(Transform padre, string nombre, string archivo, Vector2 posicion)
        {
            var fila = CrearRect(padre, nombre, posicion, new Vector2(295, 55), new Color(0, 0, 0, 0));

            CrearRect(fila.transform, "Icono", new Vector2(0, 5), new Vector2(30, 30), colorTextoSecundario,
                spriteIconoHistorial, sliced: false);
            CrearTexto(fila.transform, "NombreArchivo", archivo, new Vector2(42, 0), new Vector2(240, 26),
                16, FontStyle.Normal, TextAnchor.MiddleLeft, colorTexto);
            CrearTexto(fila.transform, "Fecha", "2026-01-01 00:00", new Vector2(42, 26), new Vector2(240, 22),
                12, FontStyle.Normal, TextAnchor.MiddleLeft, colorTextoSecundario);

            var boton = fila.AddComponent<Button>();
            var imagenClic = fila.AddComponent<Image>();
            imagenClic.color = new Color(1, 1, 1, 0f);
            boton.targetGraphic = imagenClic;
            boton.onClick.AddListener(() => OnSeleccionarHistorial(archivo));
        }

        // ------------------------------------------------------------------
        // PANEL CENTRAL: terminal / editor de script.
        // ------------------------------------------------------------------

        private void ConstruirPanelTerminal(Transform padre)
        {
            var panel = CrearRect(padre, "PanelTerminal", new Vector2(350, 225), new Vector2(690, 780), colorPanel, spriteMarcoPanel);

            CrearTexto(panel.transform, "TituloTerminal", "TERMINAL / EDITOR DE SCRIPT",
                new Vector2(15, 15), new Vector2(400, 30), 20, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            var areaCodigo = CrearRect(panel.transform, "AreaCodigo", new Vector2(15, 65), new Vector2(660, 630), colorFondoPantalla);
            CrearTexto(areaCodigo.transform, "TextoCodigoPlaceholder",
                "01  # Programa de ejemplo\n02  INICIO:\n03      MOV(N)\n04      IF RADAR(E) > 0\n05          MISIL\n06      FIN SI\n07  IR A INICIO",
                new Vector2(15, 10), new Vector2(630, 610), 16, FontStyle.Normal, TextAnchor.UpperLeft, colorTexto);

            textoLineaCaracterEstado = CrearTexto(panel.transform, "TextoEstadoTerminal",
                "LÍNEA: 1  |  CARACTER: 1  |  ESTADO: LISTO",
                new Vector2(15, 710), new Vector2(660, 30), 14, FontStyle.Normal, TextAnchor.MiddleLeft, colorTextoSecundario);
        }

        // ------------------------------------------------------------------
        // PANEL DERECHO (arriba): vista previa del tanque.
        // ------------------------------------------------------------------

        private void ConstruirPanelVistaPrevia(Transform padre)
        {
            var panel = CrearRect(padre, "PanelVistaPrevia", new Vector2(1055, 225), new Vector2(466, 320), colorPanel, spriteMarcoPanel);

            CrearTexto(panel.transform, "TituloVistaPrevia", "VISTA PREVIA DEL TANQUE",
                new Vector2(15, 15), new Vector2(400, 30), 18, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            CrearRect(panel.transform, "ImagenVistaPreviaTanque", new Vector2(15, 60), new Vector2(436, 220), colorFondoPantalla,
                spriteVistaPreviaTanque, sliced: false);

            CrearFlecha(panel.transform, "BotonSkinAnterior", "<", new Vector2(25, 150), () => OnCambiarSkin(-1));
            CrearFlecha(panel.transform, "BotonSkinSiguiente", ">", new Vector2(1055 + 436 - 25 - 15, 150), () => OnCambiarSkin(1));
        }

        // ------------------------------------------------------------------
        // PANEL DERECHO (abajo): personalizar skin.
        // ------------------------------------------------------------------

        private void ConstruirPanelPersonalizarSkin(Transform padre)
        {
            var panel = CrearRect(padre, "PanelPersonalizarSkin", new Vector2(1055, 565), new Vector2(466, 440), colorPanel, spriteMarcoPanel);

            CrearTexto(panel.transform, "TituloPersonalizarSkin", "PERSONALIZAR SKIN",
                new Vector2(15, 15), new Vector2(400, 30), 18, FontStyle.Bold, TextAnchor.MiddleLeft, colorBotonAccentoRojo);

            // Fila 1: Patrón (izquierda) + Color principal (derecha).
            CrearTexto(panel.transform, "TituloPatron", "PATRÓN", new Vector2(15, 60), new Vector2(180, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);
            CrearTexto(panel.transform, "TituloColorPrincipal", "COLOR PRINCIPAL", new Vector2(245, 60), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);

            CrearFilaSwatches(panel.transform, "SwatchPatron", 4, new Vector2(15, 90), colorSwatch, i => OnSeleccionarSwatch("Patron", i));
            CrearFilaSwatches(panel.transform, "SwatchColor", 4, new Vector2(245, 90), colorSwatch, i => OnSeleccionarSwatch("Color", i));

            // Fila 2: Calcomanías (izquierda) + Número (derecha).
            CrearTexto(panel.transform, "TituloCalcomanias", "CALCOMANÍAS", new Vector2(15, 165), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);
            CrearTexto(panel.transform, "TituloNumero", "NÚMERO", new Vector2(245, 165), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);

            CrearFilaSwatches(panel.transform, "SwatchCalcomania", 4, new Vector2(15, 195), colorSwatch, i => OnSeleccionarSwatch("Calcomania", i));
            CrearFilaSwatches(panel.transform, "SwatchNumero", 4, new Vector2(245, 195), colorSwatch, i => OnSeleccionarSwatch("Numero", i));

            // Fila 3: Bandera (ancho completo, 5 opciones).
            CrearTexto(panel.transform, "TituloBandera", "BANDERA", new Vector2(15, 270), new Vector2(200, 22),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, colorTextoSecundario);
            CrearFilaSwatches(panel.transform, "SwatchBandera", 5, new Vector2(15, 300), colorSwatch, i => OnSeleccionarSwatch("Bandera", i));
        }

        private void CrearFilaSwatches(Transform padre, string nombreBase, int cantidad, Vector2 posicion, Color color, System.Action<int> alSeleccionar)
        {
            const float tamano = 45f, gap = 10f;
            for (int i = 0; i < cantidad; i++)
            {
                var pos = new Vector2(posicion.x + i * (tamano + gap), posicion.y);
                var swatchGo = CrearRect(padre, $"{nombreBase}_{i}", pos, new Vector2(tamano, tamano), color);
                var boton = swatchGo.AddComponent<Button>();
                boton.targetGraphic = swatchGo.GetComponent<Image>();
                int indice = i;
                boton.onClick.AddListener(() => alSeleccionar(indice));
            }
        }

        // ------------------------------------------------------------------
        // HELPERS DE UI (todo con anclaje arriba-izquierda, para poder usar las mismas
        // coordenadas x/y que verías en un editor de imágenes sobre tu mockup).
        // ------------------------------------------------------------------

        private GameObject CrearRect(Transform padre, string nombre, Vector2 posicion, Vector2 tamano, Color color,
            Sprite sprite = null, bool sliced = true)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var imagen = go.AddComponent<Image>();

            if (sprite != null)
            {
                imagen.sprite = sprite;
                imagen.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                imagen.color = Color.white; // Blanco para no teñir la textura real.
            }
            else
            {
                imagen.color = color;
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = tamano;
            rect.anchoredPosition = new Vector2(posicion.x, -posicion.y);

            return go;
        }

        private Text CrearTexto(Transform padre, string nombre, string contenido, Vector2 posicion, Vector2 tamano,
            int fontSize, FontStyle estilo, TextAnchor alineacion, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var texto = go.AddComponent<Text>();
            texto.text = contenido;
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = fontSize;
            texto.fontStyle = estilo;
            texto.alignment = alineacion;
            texto.color = color;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = tamano;
            rect.anchoredPosition = new Vector2(posicion.x, -posicion.y);

            return texto;
        }

        // Botón con título + subtítulo chico (ej. "GUARDAR SCRIPT" / ".TXT"), como en el mockup.
        // Si 'colorEsAccento' es true (Abrir Terminal / Iniciar Ejecución), se usa el color
        // plano igual aunque haya sprite asignado, para no perder el rojo/verde distintivo
        // a menos que tengas sprites específicos para esos dos botones.
        private void CrearBotonAccion(Transform padre, string nombre, string titulo, string subtitulo,
            Vector2 posicion, Vector2 tamano, Color color, UnityEngine.Events.UnityAction accion, bool colorEsAccento = false)
        {
            var go = colorEsAccento
                ? CrearRect(padre, nombre, posicion, tamano, color)
                : CrearRect(padre, nombre, posicion, tamano, color, spriteFondoBoton);
            var boton = go.AddComponent<Button>();
            boton.targetGraphic = go.GetComponent<Image>();
            boton.onClick.AddListener(accion);

            CrearTexto(go.transform, "Titulo", titulo, new Vector2(15, 10), new Vector2(tamano.x - 30, 30),
                18, FontStyle.Bold, TextAnchor.MiddleLeft, colorTexto);

            if (!string.IsNullOrEmpty(subtitulo))
            {
                CrearTexto(go.transform, "Subtitulo", subtitulo, new Vector2(15, 42), new Vector2(tamano.x - 30, 24),
                    13, FontStyle.Normal, TextAnchor.MiddleLeft, colorTextoSecundario);
            }
        }

        private void CrearFlecha(Transform padre, string nombre, string simbolo, Vector2 posicion, UnityEngine.Events.UnityAction accion)
        {
            var go = CrearRect(padre, nombre, posicion, new Vector2(40, 40), new Color(0, 0, 0, 0));
            var boton = go.AddComponent<Button>();
            boton.targetGraphic = go.GetComponent<Image>();
            boton.onClick.AddListener(accion);

            CrearTexto(go.transform, "Texto", simbolo, Vector2.zero, new Vector2(40, 40),
                26, FontStyle.Bold, TextAnchor.MiddleCenter, colorBotonAccentoRojo);
        }

        // ------------------------------------------------------------------
        // ACCIONES (placeholder: solo imprimen en consola por ahora, para confirmar
        // que cada botón está bien conectado antes de programar la lógica real).
        // ------------------------------------------------------------------

        private void OnGuardarScript() => Debug.Log("Guardar script (placeholder)");
        private void OnCargarScript() => Debug.Log("Cargar script (placeholder)");
        private void OnAbrirTerminal() => Debug.Log("Abrir terminal (placeholder)");
        private void OnBorrarScript() => Debug.Log("Borrar script (placeholder)");
        private void OnIniciarEjecucion() => Debug.Log("Iniciar ejecución (placeholder)");
        private void OnLimpiarHistorial() => Debug.Log("Limpiar historial (placeholder)");
        private void OnSeleccionarHistorial(string archivo) => Debug.Log($"Cargar historial: {archivo}");
        private void OnCambiarSkin(int direccion) => Debug.Log($"Cambiar skin ({(direccion > 0 ? "siguiente" : "anterior")})");
        private void OnSeleccionarSwatch(string grupo, int indice) => Debug.Log($"{grupo} -> opción {indice}");
    }
}