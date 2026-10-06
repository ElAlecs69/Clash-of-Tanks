using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TanksGame.UI
{
    // Pantalla de arranque: fondo negro, el logo del equipo y su nombre aparecen
    // con un degradado de opacidad, se sostienen un momento y vuelven a
    // desaparecer. Después el negro se disipa y entra la pantalla de inicio.
    //
    // Solo se muestra UNA vez por ejecución del juego (YaMostrado es estático y
    // sobrevive a los cambios de escena), así que al volver al menú desde una
    // partida no se repite.
    //
    // El jugador puede saltarla pulsando cualquier tecla o haciendo clic.
    public class SplashInicial : MonoBehaviour
    {
        public static bool YaMostrado { get; private set; }

        // Tiempos en segundos (edítalos a gusto).
        private const float EsperaInicial = 0.4f;    // negro puro antes de que aparezca el logo
        private const float Aparicion = 1.4f;        // fundido de entrada del logo y el nombre
        private const float Sostenido = 1.8f;        // tiempo con el logo completamente visible
        private const float Desaparicion = 1.1f;     // fundido de salida
        private const float PausaNegra = 0.3f;       // negro puro antes de entrar al menú
        private const float RevelarMenu = 0.8f;      // el negro se disipa y se ve el menú

        private GameObject raiz;
        private CanvasGroup grupoRaiz;
        private CanvasGroup grupoContenido;
        private Action alRevelarMenu;
        private Action alTerminar;
        private bool saltar;
        private bool aceptaSalto = true;

        // alRevelarMenu: se llama justo cuando el negro empieza a disiparse (aquí el
        // menú arranca su música y la animación del título).
        // alTerminar: se llama cuando el splash ya desapareció por completo.
        public static SplashInicial Crear(Transform canvas, GameObject anfitrion, Sprite logo,
            string nombreEquipo, Action alRevelarMenu, Action alTerminar)
        {
            var splash = anfitrion.AddComponent<SplashInicial>();
            splash.alRevelarMenu = alRevelarMenu;
            splash.alTerminar = alTerminar;
            splash.Construir(canvas, logo, nombreEquipo);
            YaMostrado = true;
            splash.StartCoroutine(splash.Secuencia());
            return splash;
        }

        private void Construir(Transform canvas, Sprite logo, string nombreEquipo)
        {
            raiz = new GameObject("Splash", typeof(RectTransform));
            raiz.transform.SetParent(canvas, false);
            raiz.transform.SetAsLastSibling();
            Estirar(raiz.GetComponent<RectTransform>());

            var fondo = raiz.AddComponent<Image>();
            fondo.color = Color.black;           // también bloquea los clics al menú de abajo
            grupoRaiz = raiz.AddComponent<CanvasGroup>();

            var contenido = new GameObject("Contenido", typeof(RectTransform));
            contenido.transform.SetParent(raiz.transform, false);
            Estirar(contenido.GetComponent<RectTransform>());
            grupoContenido = contenido.AddComponent<CanvasGroup>();
            grupoContenido.alpha = 0f;

            if (logo != null)
            {
                var logoGo = new GameObject("Logo", typeof(RectTransform));
                logoGo.transform.SetParent(contenido.transform, false);
                var img = logoGo.AddComponent<Image>();
                img.sprite = logo;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var r = logoGo.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(280f, 280f);
                r.anchoredPosition = new Vector2(0f, 50f);
            }

            var textoGo = new GameObject("Nombre", typeof(RectTransform));
            textoGo.transform.SetParent(contenido.transform, false);
            var texto = textoGo.AddComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.text = nombreEquipo;
            texto.fontSize = 44;
            texto.fontStyle = FontStyle.Bold;
            texto.alignment = TextAnchor.MiddleCenter;
            texto.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            texto.raycastTarget = false;
            var tr = textoGo.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(900f, 80f);
            tr.anchoredPosition = new Vector2(0f, logo != null ? -150f : 0f);
        }

        private void Update()
        {
            if (!aceptaSalto) return;
            bool tecla = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool clic = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (tecla || clic) saltar = true;
        }

        private IEnumerator Secuencia()
        {
            yield return Esperar(EsperaInicial);
            yield return Fundir(grupoContenido, 0f, 1f, Aparicion);
            yield return Esperar(Sostenido);
            yield return Fundir(grupoContenido, 1f, 0f, Desaparicion);
            yield return Esperar(PausaNegra);

            // A partir de aquí el salto ya no aplica: el menú se revela con suavidad.
            aceptaSalto = false;
            saltar = false;
            if (alRevelarMenu != null) alRevelarMenu();
            yield return Fundir(grupoRaiz, 1f, 0f, RevelarMenu);

            Destroy(raiz);
            if (alTerminar != null) alTerminar();
            Destroy(this);
        }

        // Tiempo sin escalar; se corta en cuanto el jugador salta.
        private IEnumerator Esperar(float segundos)
        {
            float t = 0f;
            while (t < segundos && !saltar)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator Fundir(CanvasGroup grupo, float desde, float hasta, float duracion)
        {
            float t = 0f;
            grupo.alpha = desde;
            while (t < duracion && !saltar)
            {
                t += Time.unscaledDeltaTime;
                grupo.alpha = Mathf.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracion)));
                yield return null;
            }
            grupo.alpha = hasta;
        }

        private static void Estirar(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }
    }
}
