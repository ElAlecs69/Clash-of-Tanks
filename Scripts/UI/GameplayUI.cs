using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TanksGame.Core;
using TanksGame.Gameplay;

namespace TanksGame.UI
{
    // Genera en tiempo de ejecución una UI mínima: un botón "Siguiente turno", un
    // panel por cada tanque (vida, bombas restantes, escudo y su parte del log) y
    // un panel con el log completo de la última ronda.
    // Es un placeholder funcional; la versión final debería reemplazarse por el panel
    // de instrucciones descrito en DESIGN_GUIDE.md.
    //
    // Uso: agrega este componente a un GameObject vacío y arrastra el GameManager
    // de tu escena al campo "Game Manager".
    public class GameplayUI : MonoBehaviour
    {
        public GameManager gameManager;

        [Header("Paneles de tanques")]
        public Vector2 tamanoPanelTanque = new Vector2(300, 170);
        public float espacioEntrePaneles = 20f;
        public float margenBorde = 20f;

        private readonly List<Text> panelesTanque = new List<Text>();

        [Header("Ejecución automática")]
        [Tooltip("Segundos entre turno y turno a velocidad normal (x1).")]
        public float segundosPorTurno = 1.5f;

        private const int MAX_TANQUES_POR_LADO = 3; // 6 tanques en total como máximo.

        private float multiplicadorVelocidad = 1f;
        private float temporizadorTurno = 0f;
        private bool partidaTerminada = false;
        private Text textoVelocidad;
        private Text textoResultadoPartida;

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

        private void Update()
        {
            if (partidaTerminada || gameManager == null) return;

            temporizadorTurno += Time.deltaTime * multiplicadorVelocidad;
            if (temporizadorTurno >= segundosPorTurno)
            {
                temporizadorTurno = 0f;
                EjecutarTurnoAutomatico();
            }
        }

        private void EjecutarTurnoAutomatico()
        {
            var resultado = gameManager.EjecutarSiguienteTurno();
            ActualizarHud();
            RevisarFinDePartida(resultado);
        }

        // Usa el GameResult real que ya calcula TurnManager (PlayerWins / Draw /
        // en progreso) en vez de aproximarlo contando tanques vivos nosotros mismos
        // — así respeta las reglas de desempate por daño que ya tiene tu lógica de
        // turnos, en vez de reinventarlas acá.
        private void RevisarFinDePartida(GameResult resultado)
        {
            if (resultado == GameResult.PlayerWins)
            {
                var ganador = gameManager.Turno.AliveTanks().First();
                partidaTerminada = true;
                MostrarResultadoPartida($"GANADOR: JUGADOR {ganador.PlayerId}");
            }
            else if (resultado == GameResult.Draw)
            {
                partidaTerminada = true;
                MostrarResultadoPartida("EMPATE");
            }
        }

        private void ConstruirUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("Canvas_TanksGame");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1600, 900);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.transform.SetParent(transform, false);

            // --- Botón de velocidad x1/x2 (abajo, centrado) — reemplaza al viejo
            // "SIGUIENTE TURNO": los turnos ahora se ejecutan solos (ver Update()),
            // este botón solo acelera/desacelera el ritmo. ---
            var botonGo = new GameObject("BotonVelocidad");
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
            textoVelocidad = botonTextoGo.AddComponent<Text>();
            textoVelocidad.text = "VELOCIDAD x1";
            textoVelocidad.alignment = TextAnchor.MiddleCenter;
            textoVelocidad.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoVelocidad.fontSize = 22;
            textoVelocidad.fontStyle = FontStyle.Bold;
            textoVelocidad.color = Color.black;
            var botonTextoRect = botonTextoGo.GetComponent<RectTransform>();
            botonTextoRect.anchorMin = Vector2.zero;
            botonTextoRect.anchorMax = Vector2.one;
            botonTextoRect.offsetMin = Vector2.zero;
            botonTextoRect.offsetMax = Vector2.zero;

            boton.onClick.AddListener(OnCambiarVelocidad);

            // --- Banner de resultado (oculto hasta que termine la partida) ---
            var resultadoGo = new GameObject("ResultadoPartida");
            resultadoGo.transform.SetParent(canvasGo.transform, false);
            textoResultadoPartida = resultadoGo.AddComponent<Text>();
            textoResultadoPartida.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textoResultadoPartida.fontSize = 48;
            textoResultadoPartida.fontStyle = FontStyle.Bold;
            textoResultadoPartida.alignment = TextAnchor.MiddleCenter;
            textoResultadoPartida.color = new Color(0.95f, 0.85f, 0.2f);
            var resultadoRect = resultadoGo.GetComponent<RectTransform>();
            resultadoRect.anchorMin = new Vector2(0.5f, 1f);
            resultadoRect.anchorMax = new Vector2(0.5f, 1f);
            resultadoRect.pivot = new Vector2(0.5f, 1f);
            resultadoRect.sizeDelta = new Vector2(700, 90);
            resultadoRect.anchoredPosition = new Vector2(0, -20);
            resultadoGo.SetActive(false);

            // --- Un panel por cada tanque: mitad en el borde izquierdo, mitad en el derecho ---
            ConstruirPanelesDeTanques(canvasGo.transform);
        }

        private void OnCambiarVelocidad()
        {
            multiplicadorVelocidad = multiplicadorVelocidad >= 2f ? 1f : 2f;
            textoVelocidad.text = multiplicadorVelocidad >= 2f ? "VELOCIDAD x2" : "VELOCIDAD x1";
        }

        private void MostrarResultadoPartida(string mensaje)
        {
            textoResultadoPartida.text = mensaje;
            textoResultadoPartida.gameObject.SetActive(true);
        }

        private void ConstruirPanelesDeTanques(Transform padre)
        {
            panelesTanque.Clear();

            var agentes = gameManager.Agentes;
            if (agentes == null) return;

            // Máximo 3 recuadros por lado (6 tanques en total), aunque el
            // GameManager tenga configurados más — el resto simplemente no muestra
            // recuadro (la cantidad real de tanques la define la programación).
            int total = Mathf.Min(agentes.Count, MAX_TANQUES_POR_LADO * 2);
            int enColumnaIzquierda = Mathf.Min(MAX_TANQUES_POR_LADO, Mathf.CeilToInt(total / 2f));

            for (int i = 0; i < total; i++)
            {
                bool esIzquierda = i < enColumnaIzquierda;
                int indiceEnColumna = esIzquierda ? i : i - enColumnaIzquierda;

                // Ancla y pivote en la esquina superior del borde correspondiente,
                // apilando los paneles hacia abajo según su índice en la columna.
                var anchor = esIzquierda ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                var alineacionTexto = esIzquierda ? TextAnchor.UpperLeft : TextAnchor.UpperRight;

                float offsetY = -margenBorde - indiceEnColumna * (tamanoPanelTanque.y + espacioEntrePaneles);
                float offsetX = esIzquierda ? margenBorde : -margenBorde;

                var panel = CrearPanelDeTexto(padre, $"PanelTanque_{i + 1}",
                    anchor, anchor, new Vector2(offsetX, offsetY),
                    tamanoPanelTanque, alineacionTexto);

                panelesTanque.Add(panel);
            }
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
            texto.fontSize = 16;
            texto.alignment = alineacion;
            texto.color = Color.white;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            texto.supportRichText = true;

            var textoRect = textoGo.GetComponent<RectTransform>();
            textoRect.anchorMin = Vector2.zero;
            textoRect.anchorMax = Vector2.one;
            textoRect.offsetMin = new Vector2(10, 10);
            textoRect.offsetMax = new Vector2(-10, -10);

            return texto;
        }

        private void ActualizarHud()
        {
            var agentes = gameManager.Agentes;
            if (agentes == null) return;

            // ToList() para no re-enumerar el log varias veces por cada panel.
            var log = gameManager.Turno?.LastRoundLog?.ToList() ?? new List<string>();

            for (int i = 0; i < agentes.Count && i < panelesTanque.Count; i++)
            {
                var tanque = agentes[i].Tank;
                panelesTanque[i].text = ArmarTextoTanque(tanque, log);
            }
        }

        // Arma el texto de un recuadro individual: nombre, vida, bombas restantes,
        // escudo, y las últimas líneas del log que correspondan a este tanque
        // (se filtran buscando "Jugador {id}" al inicio de cada línea del log).
        private string ArmarTextoTanque(Tank tanque, List<string> log)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>Jugador {tanque.PlayerId}</b>");

            if (!tanque.IsAlive)
            {
                sb.AppendLine("DESTRUIDO");
                return sb.ToString();
            }

            sb.AppendLine($"Vida: {tanque.Health}%");
            sb.AppendLine($"Bombas: {tanque.Missiles}");
            sb.AppendLine($"Escudo: {(tanque.ShieldActive ? "activo" : "-")}");

            var lineasDelTanque = log
                .Where(l => l.StartsWith($"Jugador {tanque.PlayerId}") && !l.Contains("IF") && !l.Contains("ESPERA"))
                .ToList();

            if (lineasDelTanque.Count > 0)
            {
                int desde = Mathf.Max(0, lineasDelTanque.Count - 3);
                sb.AppendLine("---");
                foreach (var linea in lineasDelTanque.Skip(desde))
                    sb.AppendLine(linea);
            }

            return sb.ToString();
        }
    }
}