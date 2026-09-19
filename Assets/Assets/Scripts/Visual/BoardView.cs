using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TanksGame.Core;
using TanksGame.Gameplay;

namespace TanksGame.Visual
{
    // Vista del tablero (Ancho x Alto) más todo el paisaje que lo rodea, generado
    // por procedimiento con primitivas y mallas propias.
    //
    // IDEA CLAVE DE ESTA VERSIÓN: el paisaje es una composición PROPORCIONAL al
    // tablero. Todas las distancias, tamaños y alturas del decorado se miden en
    // una unidad interna (_u) que crece con el lado del tablero, así que un
    // tablero de 20x20 se ve rodeado igual que uno de 8x8 -- la sierra, el
    // bosque y los edificios se quedan alrededor en vez de quedar sueltos a una
    // distancia fija que en tableros grandes se ve rala.
    //
    // Uso: asigna este componente al campo "vistaTablero" del GameManager.
    public class BoardView : MonoBehaviour
    {
        [Header("Prefabs opcionales (si se dejan vacíos, se generan primitivas)")]
        public GameObject celdaPrefab;
        public GameObject tanquePrefab;

        [Header("Tamaño visual")]
        public float tamanoCelda = 1f;
        public float separacion = 0.05f;

        [Header("Colores por jugador (solo aplica a la primitiva por defecto)")]
        public Color colorJugador1 = new Color(0.2f, 0.4f, 0.9f);
        public Color colorJugador2 = new Color(0.9f, 0.25f, 0.2f);

        [Header("Movimiento animado de los tanques")]
        [Tooltip("Segundos que tarda un tanque en desplazarse de una celda a la siguiente.")]
        public float duracionMovimiento = 0.35f;
        [Tooltip("Velocidad de giro del tanque en grados por segundo. Antes el tanque giraba instantáneamente al iniciar cada movimiento (un salto brusco de orientación); con esto gira de forma gradual, como un vehículo real, incluso si eso hace que tarde un poco más en encarar la nueva dirección que en empezar a desplazarse.")]
        public float velocidadGiroTanque = 260f;

        // NOTA: estos campos son nuevos (sustituyen a los márgenes en celdas de
        // la versión anterior). Al cambiar de nombre, Unity los serializa con
        // estos valores por defecto en vez de conservar los viejos de la escena.
        [Header("Paisaje (todo escala solo con el tamaño del tablero)")]
        public bool generarPaisaje = true;
        [Tooltip("Multiplicador global del paisaje sobre el escalado automático. 1 = proporción calculada.")]
        [Range(0.3f, 3f)] public float escalaPaisaje = 1f;
        [Tooltip("Separación entre el borde del tablero y el PIE de la primera montaña. El radio de cada formación se suma aparte.")]
        [Range(0f, 4f)] public float sierraPegadaAlTablero = 0.9f;
        [Tooltip("Altura de la sierra respecto al tamaño del tablero.")]
        [Range(0.3f, 2.5f)] public float alturaSierra = 1f;
        [Tooltip("Cuántos manchones de bosque se plantan alrededor.")]
        [Range(0.3f, 3f)] public float densidadBosque = 1f;
        [Tooltip("Ancho del sector frontal (hacia la cámara) sin montañas, reservado al bosque.")]
        [Range(0.15f, 0.9f)] public float aperturaFrontal = 0.42f;
        [Tooltip("Distancia de los campamentos al borde del tablero (en unidades de paisaje).")]
        [Range(0.5f, 5f)] public float distanciaEdificios = 1.9f;
        [Tooltip("Niebla lejana: separa la sierra del fondo y da profundidad aérea.")]
        public bool nieblaLejana = true;

        [Header("Edificios: prefabs propios (opcional)")]
        [Tooltip("Si asignas prefabs aquí (por ejemplo los de un asset como ithappy Military_Free: tiendas, radios, torres), se usan en cada puesto en vez de las primitivas generadas por código. Se elige uno al azar por puesto. Déjalo vacío para seguir usando el campamento procedural.")]
        public GameObject[] prefabsEdificios;
        [Tooltip("Cuántos edificios de la lista se agrupan por puesto (1 = un edificio por puesto, como una tienda sola; 2-3 = un pequeño grupo).")]
        [Range(1, 4)] public int edificiosPorPuesto = 1;
        [Tooltip("Escala aplicada a cada prefab de edificio. Los assets de este tipo suelen venir modelados a un tamaño real (metros) mucho más grande que las primitivas de placeholder; baja este valor hasta que se vean del tamaño correcto junto al tablero. Prueba con algo como 0.1-0.3 como punto de partida.")]
        public float escalaPrefabsEdificios = 0.2f;
        [Tooltip("Escala adicional SOLO para los prefabs cuyo nombre contiene 'Tower' o 'Torre' (además de la escala general de arriba). Úsalo para bajar el tamaño de las torres sin afectar al resto de edificios.")]
        public float escalaExtraTorres = 0.6f;

        [Header("Avión / helicóptero decorativo")]
        public bool avionDecorativo = true;
        [Tooltip("Si asignas un prefab (por ejemplo un helicóptero), se usa en vez del avión de placeholder generado por código.")]
        public GameObject prefabAvion;
        [Tooltip("Escala del prefab de avión/helicóptero. Ignorado si no hay prefab asignado.")]
        public float escalaPrefabAvion = 1f;
        [Tooltip("Ajuste de rotación si el prefab no mira hacia +Z por defecto: gíralo hasta que el morro apunte en la dirección de vuelo.")]
        public Vector3 rotacionExtraAvion = Vector3.zero;

        [Header("Posición del tablero según su tamaño (interpolada)")]
        [Tooltip("Mueve este mismo GameObject (BoardView), del que cuelgan terreno, tablero y tanques, en vez de tocar la cámara. Se interpola linealmente entre 'Posicion Para Lado Chico' y 'Posicion Para Lado Grande' según el lado más largo del tablero actual (Max(ancho, alto)); fuera de ese rango, extrapola con la misma recta. Calibralo jugando con dos tamaños de tablero bien distintos (por ejemplo 3x3 y 20x20), moviendo este GameObject a mano hasta que cada uno se vea bien, anotando la Position de cada caso y pasándola aquí.")]
        public bool ajustarPosicionTableroSegunTamano = true;
        public int ladoReferenciaChico = 3;
        public Vector3 posicionParaLadoChico = new Vector3(-0.25f, -0.1f, 1f);
        public int ladoReferenciaGrande = 20;
        public Vector3 posicionParaLadoGrande = new Vector3(-0.94f, 0f, 4.25f);

        [Header("Cámara (encuadre automático según el tamaño del tablero)")]
        [Tooltip("Aleja o acerca la cámara según crece el tablero, conservando el mismo ángulo con el que la dejaste colocada en la escena. Se calibra sola la primera vez que se construye un tablero, tomando la posición y rotación que la cámara tenga en ese momento como referencia para 'Tablero Referencia Camara'.")]
        public bool ajustarCamaraAutomaticamente = true;
        [Tooltip("Tamaño de tablero para el que la cámara está colocada manualmente en la escena/prefab (el tamaño con el que se ve bien como en la referencia).")]
        public Vector2Int tableroReferenciaCamara = new Vector2Int(10, 10);
        [Tooltip("Multiplicador extra sobre el alejamiento calculado, por si quieres un poco más o menos margen alrededor del tablero.")]
        [Range(0.5f, 2f)] public float margenExtraCamara = 1f;
        [Tooltip("Ajuste manual, en unidades de mundo, para terminar de centrar el tablero en pantalla si el encuadre automático queda un poco corrido. X mueve el punto de mira a la izquierda/derecha, Z arriba/abajo en pantalla (según el ángulo de la cámara). Su efecto es MÁS FUERTE cuanto más chico es el tablero (a igual valor, en un 3x3 se nota mucho más que en un 20x20), así que ajustalo probando con un tablero chico (por ejemplo 3x3).")]
        public Vector3 correccionCentroCamara = Vector3.zero;
        [Tooltip("Segundo ajuste manual, también en unidades de mundo, pero con efecto CONSTANTE en pantalla sin importar el tamaño del tablero (a diferencia de 'Correccion Centro Camara', que pesa más en tableros chicos). Úsalo para corregir un corrimiento que se nota igual de fuerte en 3x3 y en 20x20 -- por ejemplo si necesitas mover el tablero para un lado en un tamaño chico y para el lado contrario en uno grande: primero ajusta 'Correccion Centro Camara' mirando un tablero chico, y después este otro mirando uno grande, sin que se te desarme el chico.")]
        public Vector3 correccionCentroCamaraProporcional = Vector3.zero;
        [Tooltip("Opcional: si la cámara sigue a un objeto (por ejemplo un 'CameraTarget' del que cuelga un script de seguimiento) en vez de moverse directamente, asigna aquí ese objeto para que el ajuste se aplique a él en lugar de a la cámara.")]
        public Transform objetivoCamaraAlternativo;
        [Tooltip("Cámara real del juego. Solo hace falta si tu cámara NO tiene el tag 'MainCamera' (Camera.main no la encontraría) o si usas varias cámaras y quieres apuntar a una en concreto.")]
        public Camera camaraJuego;
        [Tooltip("Ajusta el campo de visión (zoom) además de la posición. Es el método más fiable: si algo más (un script de seguimiento) reescribe la posición de la cámara cada frame, el ajuste por posición se pierde, pero casi ningún script de seguimiento toca el FOV, así que esto sigue funcionando igual.")]
        public bool ajustarCampoDeVisionTambien = true;
        [Tooltip("Segundos aproximados entre cada pasada del avión por el cielo.")]
        public float intervaloAvion = 22f;
        [Tooltip("Segundos que tarda el avión en cruzar de un extremo al otro.")]
        public float duracionVueloAvion = 13f;

        // --- Biomas: cambia la paleta completa (celda, suelo, roca, cumbre, niebla).
        public enum Bioma { Pradera, Nieve, Arenoso, Selvatico }
        public Bioma biomaActual = Bioma.Pradera;

        private readonly Dictionary<int, Transform> tanquesVisuales = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Coroutine> movimientosEnCurso = new Dictionary<int, Coroutine>();
        private readonly List<Vector3> posicionesCampamentos = new List<Vector3>();
        // Huella de cada formación montañosa: x, z = centro; w = radio ocupado.
        private readonly List<Vector3> huellasMontanas = new List<Vector3>();

        private Transform contenedorCeldas;
        private Transform contenedorTanques;
        private Transform contenedorMontanas;
        private Transform contenedorCampamentos;
        private Transform contenedorTerreno;
        private Transform contenedorAvion;
        private Coroutine rutinaAvion;
        private float _alturaMontanaAprox = 3f;

        // Calibración de la cámara: se toma una sola vez (la primera vez que se
        // construye un tablero) para no pelear con nada que la mueva o la
        // rote manualmente después.
        private bool _camaraCalibrada;
        private Vector3 _offsetCamaraReferencia;
        private Quaternion _rotacionCamaraReferencia;
        private float _ladoReferenciaCamara = 10f;        private float _fovReferenciaCamara = 60f;
        private float _orthoSizeReferencia = 5f;

        // Límites de la cámara libre (paneo + zoom manual del jugador, ver
        // Update()/ActualizarCamaraLibre): se recalculan cada vez que se
        // construye el tablero, en ConfigurarLimitesCamaraLibre().
        private float _panLimiteRadio;
        private float _zoomOrthoMinLimite;
        private float _zoomOrthoMaxLimite;

        // Punto de referencia (posición XZ) contra el que se mide cuánto se
        // alejó el jugador al arrastrar la cámara -- ver el comentario grande
        // en ActualizarCamaraLibre sobre por qué NO puede ser '_centroTablero'.
        private Vector3 _posicionLibreInicial;
        private bool _posicionLibreInicialCapturada;

        // Factor de encuadre (orthographicSize / _orthoSizeReferencia) que
        // AjustarCamaraAlTablero calculó para el tablero actual -- es decir,
        // el zoom EXACTO con el que arranca la cámara para este tablero en
        // particular. ConfigurarLimitesCamaraLibre lo usa como techo del
        // alejamiento manual (ver comentario ahí): no tendría sentido dejar
        // alejarse más allá de "donde empezó la cámara".
        private float _factorEncuadreActual = 1f;

        private float superficieCeldaMundoY;
        private bool superficieCalculada;

        // Unidad de paisaje: crece con el tablero. TODO el decorado se mide con
        // ella, por eso la composición se conserva sea cual sea el tamaño.
        private float _u = 1f;
        private float _escalaObjetos = 1f;
        private Vector3 _centroTablero;
        private float _mitadAncho, _mitadAlto;

        public void SetBioma(Bioma bioma)
        {
            biomaActual = bioma;
        }

        private (Color celda, Color suelo, Color roca, Color cumbre, Color niebla) ObtenerPaletaBioma()
        {
            switch (biomaActual)
            {
                case Bioma.Nieve:
                    return (new Color(0.85f, 0.88f, 0.92f), new Color(0.76f, 0.78f, 0.82f),
                        new Color(0.5f, 0.52f, 0.55f), new Color(0.95f, 0.96f, 0.98f), new Color(0.8f, 0.85f, 0.9f));
                case Bioma.Arenoso:
                    return (new Color(0.82f, 0.68f, 0.42f), new Color(0.72f, 0.6f, 0.38f),
                        new Color(0.6f, 0.45f, 0.25f), new Color(0.78f, 0.66f, 0.44f), new Color(0.85f, 0.75f, 0.55f));
                case Bioma.Selvatico:
                    return (new Color(0.25f, 0.45f, 0.2f), new Color(0.27f, 0.33f, 0.17f),
                        new Color(0.2f, 0.3f, 0.15f), new Color(0.4f, 0.44f, 0.24f), new Color(0.5f, 0.6f, 0.5f));
                default: // Pradera
                    return (new Color(0.36f, 0.47f, 0.26f), new Color(0.47f, 0.43f, 0.29f),
                        new Color(0.45f, 0.42f, 0.36f), new Color(0.72f, 0.7f, 0.68f), new Color(0.66f, 0.72f, 0.79f));
            }
        }

        // ---------------------------------------------------------------------
        // CONSTRUCCIÓN
        // ---------------------------------------------------------------------
        [Header("Efectos de sonido (opcionales)")]
        [Tooltip("Se reproduce cuando un MISIL impacta (al final de su vuelo).")]
        public AudioClip sonidoImpactoMisil;
        [Tooltip("Se reproduce cuando detona una mina.")]
        public AudioClip sonidoExplosionMina;
        [Tooltip("Se reproduce al arrancar cada ráfaga de ametralladora (AMT).")]
        public AudioClip sonidoAmetralladora;
        [Tooltip("Se reproduce cada vez que un tanque se desplaza una celda.")]
        public AudioClip sonidoMovimientoTanque;
        [Tooltip("Se reproduce al iniciar el barrido de un RADAR.")]
        public AudioClip sonidoRadar;
        [Range(0f, 1f)]
        public float volumenEfectos = 0.7f;

        private int _anchoTablero;
        private int _altoTablero;

        private AudioSource audioSourceEfectos;

        private void ReproducirEfecto(AudioClip clip)
        {
            if (clip == null) return;
            if (audioSourceEfectos == null)
            {
                if (FindObjectOfType<AudioListener>() == null)
                    gameObject.AddComponent<AudioListener>();
                audioSourceEfectos = gameObject.AddComponent<AudioSource>();
                audioSourceEfectos.playOnAwake = false;
            }
            audioSourceEfectos.PlayOneShot(clip, volumenEfectos);
        }

        public void Construir(int ancho, int alto)
        {
            Limpiar();

            _anchoTablero = ancho;
            _altoTablero = alto;

            var paleta = ObtenerPaletaBioma();
            CalcularEscala(ancho, alto);

            // Orden importante: primero se fija el terreno (para poder consultar
            // su altura), luego la sierra (que registra su huella) y al final el
            // decorado, que esquiva montañas y campamentos.
            CalcularPosicionesCampamentos(ancho, alto);
            ConstruirSuelo(ancho, alto, paleta.suelo);

            if (generarPaisaje)
            {
                ConstruirMontanas(paleta.roca, paleta.cumbre);
                ConstruirDecorado(paleta.roca);
            }

            ConstruirCeldas(ancho, alto, paleta.celda);
            ConstruirCampamentos();
            AplicarAmbienteBioma(paleta.niebla);
            AjustarPosicionSegunTamano(ancho, alto);
            AjustarCamaraAlTablero(ancho, alto);
            ConfigurarLimitesCamaraLibre();

            if (avionDecorativo)
            {
                ConstruirAvion();
                rutinaAvion = StartCoroutine(RutinaAvion());
            }
        }

        private void CalcularEscala(int ancho, int alto)
        {
            _centroTablero = new Vector3((ancho - 1) * tamanoCelda * 0.5f, 0f, (alto - 1) * tamanoCelda * 0.5f);
            _mitadAncho = ancho * tamanoCelda / 2f;
            _mitadAlto = alto * tamanoCelda / 2f;

            // Tablero de referencia: 10x10. Un 20x20 duplica la unidad, así que
            // montañas, árboles y edificios crecen y se alejan en la misma
            // proporción y la escena se ve idéntica en encuadre.
            float lado = Mathf.Max(ancho, alto) * tamanoCelda;
            float escalaTablero = Mathf.Clamp(lado / (10f * tamanoCelda), 0.65f, 2.6f);

            _escalaObjetos = escalaTablero * escalaPaisaje;
            _u = tamanoCelda * _escalaObjetos;
        }

        // Guarda la posición y rotación actuales de la cámara (o del objeto
        // alternativo) la primera vez que se llama, usándolas como la
        // referencia "correcta" para el tamaño de tablero indicado en
        // 'tableroReferenciaCamara'. Todo ajuste posterior escala esa misma
        // relación en vez de recalcular el encuadre desde cero.
        // Reposiciona este mismo GameObject (con todo lo que cuelga de él:
        // terreno, celdas, tanques) según el tamaño del tablero, interpolando
        // linealmente entre las dos posiciones calibradas a mano
        // ('posicionParaLadoChico' / 'posicionParaLadoGrande'). Usa el lado
        // más largo del tablero (Max(ancho, alto)) como parámetro de la
        // recta, igual que el resto de los cálculos de escala/cámara de este
        // componente. Fuera del rango [ladoReferenciaChico, ladoReferenciaGrande]
        // extrapola con la misma recta en vez de recortar, para que tableros
        // más chicos o más grandes que los dos de referencia sigan
        // corrigiéndose en la misma dirección en vez de quedarse pegados al
        // valor del extremo más cercano.
        private void AjustarPosicionSegunTamano(int ancho, int alto)
        {
            if (!ajustarPosicionTableroSegunTamano) return;

            int ladoActual = Mathf.Max(ancho, alto);
            int rango = ladoReferenciaGrande - ladoReferenciaChico;

            float t = rango != 0
                ? (float)(ladoActual - ladoReferenciaChico) / rango
                : 0f;

            transform.position = Vector3.LerpUnclamped(posicionParaLadoChico, posicionParaLadoGrande, t);
        }

        private void CalibrarCamaraSiHaceFalta(Camera camara, Transform objetivo)
        {
            if (_camaraCalibrada) return;

            var centroReferencia = new Vector3(
                (tableroReferenciaCamara.x - 1) * tamanoCelda * 0.5f, 0f,
                (tableroReferenciaCamara.y - 1) * tamanoCelda * 0.5f);

            _offsetCamaraReferencia = objetivo.position - centroReferencia;
            _rotacionCamaraReferencia = objetivo.rotation;
            _ladoReferenciaCamara = Mathf.Max(
                Mathf.Max(tableroReferenciaCamara.x, tableroReferenciaCamara.y) * tamanoCelda, 0.01f);
            _fovReferenciaCamara = camara != null ? camara.fieldOfView : 60f;
            _orthoSizeReferencia = camara != null ? camara.orthographicSize : 5f;
            _camaraCalibrada = true;
        }

        // Aleja o acerca la cámara para que un tablero grande se vea completo,
        // en la misma proporción que el tablero de referencia.
        //
        // Se hace de DOS formas a la vez porque no sabemos qué controla
        // realmente la cámara en esta escena:
        //  1) Reposicionando el transform (funciona si la cámara es estática o
        //     si 'objetivoCamaraAlternativo' es el objeto correcto a mover).
        //  2) Ajustando el Field of View (funciona incluso si un script de
        //     seguimiento reescribe la posición cada frame, porque ese tipo de
        //     scripts casi nunca tocan el FOV). Este es el método robusto de
        //     verdad: si el tablero no se veía completo pese al ajuste de
        //     posición, es señal de que algo está sobrescribiendo la posición
        //     de la cámara en cada frame -- el FOV no sufre ese problema.
        private void AjustarCamaraAlTablero(int ancho, int alto)
        {
            if (!ajustarCamaraAutomaticamente) return;

            var camara = camaraJuego != null ? camaraJuego : Camera.main;
            var objetivo = objetivoCamaraAlternativo != null ? objetivoCamaraAlternativo
                : (camara != null ? camara.transform : null);

            if (camara == null || objetivo == null)
            {
                Debug.LogWarning("BoardView: no encontré ninguna cámara para ajustar el encuadre. " +
                    "Si tu cámara real no tiene el tag 'MainCamera', asígnala en el campo 'Camara Juego' del Inspector.");
                return;
            }


            CalibrarCamaraSiHaceFalta(camara, objetivo);

            float ladoActual = Mathf.Max(ancho, alto) * tamanoCelda;
            float factor = (ladoActual / _ladoReferenciaCamara) * margenExtraCamara;
            _factorEncuadreActual = factor;

            // 'correccionCentroCamara' es un ajuste manual, en unidades de
            // mundo, para el punto que la cámara usa como centro del
            // tablero. Existe porque la cámara de esta escena no es
            // necesariamente la que se mueve directamente -- 'objetivo' es
            // 'objetivoCamaraAlternativo' (el pivote/rig del que cuelga la
            // Camera real), y forzarle una rotación calculada en código
            // (recalculando un LookAt hacia el centro exacto en cada ajuste)
            // resultó en encuadres erráticos, probablemente porque algún
            // otro componente del rig usa esa rotación para posicionar la
            // Camera real de una forma que no es un simple "mirar para
            // allá". Por eso se volvió a la rotación FIJA calibrada
            // ('_rotacionCamaraReferencia', como estaba en el diseño
            // original) y en cambio se dejan estos dos offsets a mano para
            // terminar de centrar el tablero por prueba y error:
            //
            //  - 'correccionCentroCamara' se suma tal cual, así que su
            //    efecto en PANTALLA es más fuerte cuanto más chico es el
            //    tablero (el mismo desplazamiento en mundo es una fracción
            //    más grande de una vista con 'orthographicSize' chico).
            //  - 'correccionCentroCamaraProporcional' se multiplica por
            //    'factor' (el mismo factor de zoom), así que su efecto en
            //    PANTALLA queda CONSTANTE sin importar el tamaño del
            //    tablero.
            //
            // Con las dos por separado se puede corregir un corrimiento que
            // necesita ir para un lado en un tablero chico y para el lado
            // contrario en uno grande (exactamente lo que pasaba: 3x3 pedía
            // bajar el tablero, 20x20 pedía subirlo y correrlo a la
            // derecha): la primera domina en tableros chicos, la segunda
            // pesa igual en todos.
            var centroConCorreccion = _centroTablero + correccionCentroCamara
                + correccionCentroCamaraProporcional * factor;

            objetivo.position = centroConCorreccion + _offsetCamaraReferencia * factor;
            objetivo.rotation = _rotacionCamaraReferencia;

            // Cámara ORTOGRÁFICA (como la de esta escena): reposicionarla no
            // hace zoom -- en ortográfica el "zoom" lo controla
            // 'orthographicSize' (la mitad de la altura visible, en unidades
            // de mundo), no la distancia ni el fieldOfView. El código de aquí
            // abajo (fieldOfView) es exclusivo de cámaras en perspectiva y
            // antes se saltaba silenciosamente en ortográfica, así que el
            // tablero nunca se ajustaba pese a que la posición sí cambiaba.
            // Achicar/agrandar 'orthographicSize' con el mismo factor
            // proporcional que ya usamos para la posición es exacto (a
            // diferencia de perspectiva, en ortográfica el tamaño visible
            // escala linealmente con el tablero, sin trigonometría de por
            // medio).
            if (camara.orthographic)
            {
                camara.orthographicSize = _orthoSizeReferencia * factor;
                // Sincroniza el objetivo del zoom suavizado con el encuadre recién
                // calculado, para que un tablero nuevo (nueva partida) no herede
                // el objetivo de zoom de la partida anterior y "tironee" la
                // cámara de golpe hacia ese valor viejo en el primer frame.
                _zoomObjetivoActual = camara.orthographicSize;
                _zoomVelocidadActual = 0f;
            }
            else if (ajustarCampoDeVisionTambien)
            {
                // Radio que hay que encuadrar: la diagonal del tablero más un
                // colchón para que no quede pegado al borde de pantalla.
                //
                // El colchón es PROPORCIONAL al tablero (35%), no un valor
                // fijo en unidades de paisaje. Antes era "+2.5f * _u", y _u
                // deja de achicarse por debajo de cierto tamaño de tablero
                // (ver el Clamp de 'escalaTablero' en CalcularEscala, para
                // que árboles/montañas no se vuelvan microscópicos). En un
                // tablero chico (3x3) ese colchón fijo terminaba siendo
                // enorme comparado con el radio real del tablero, así que la
                // fórmula pedía un FOV mucho más ancho del necesario: la
                // cámara se acercaba, pero al abrir tanto el campo de visión
                // terminaba encuadrando un montón de paisaje alrededor y el
                // tablero se veía chico y lejano. Con un colchón proporcional
                // el encuadre queda igual de ajustado sea cual sea el tamaño
                // del tablero (a 10x10, el tablero de referencia, el 35% da
                // prácticamente el mismo resultado que el valor fijo de
                // antes, así que el encuadre de referencia no cambia).
                float radioTablero = Mathf.Sqrt(_mitadAncho * _mitadAncho + _mitadAlto * _mitadAlto) * 1.35f;
                float distanciaCamara = Vector3.Distance(objetivo.position, centroConCorreccion);
                float fovNecesario = 2f * Mathf.Atan(
                    (radioTablero * margenExtraCamara) / Mathf.Max(distanciaCamara, 0.01f)) * Mathf.Rad2Deg;

                // Nunca se cierra por debajo del FOV con el que quedó
                // configurada la cámara para el tablero de referencia: solo
                // abre más cuando el tablero real es más grande que esa
                // referencia.
                camara.fieldOfView = Mathf.Clamp(fovNecesario, _fovReferenciaCamara, 100f);
            }
        }

        // Recalcula, cada vez que se construye/reconstruye el tablero, hasta
        // dónde puede moverse la cámara libre del jugador (ver
        // ActualizarCamaraLibre): un radio de paneo alrededor del centro del
        // tablero que se queda antes del anillo de árboles/montañas (que
        // arranca en ~1.35x la diagonal del tablero, ver 'radioTablero' más
        // arriba), y los mismos dos extremos de zoom (orthographicSize) que ya
        // usa el encuadre automático para tableros de 3x3 (más cerca) y 20x20
        // (más lejos), para no inventar límites nuevos.
        private void ConfigurarLimitesCamaraLibre()
        {
            _panLimiteRadio = Mathf.Sqrt(_mitadAncho * _mitadAncho + _mitadAlto * _mitadAlto) * 1.1f;

            float factorCercano = (3f * tamanoCelda / _ladoReferenciaCamara) * margenExtraCamara;
            _zoomOrthoMinLimite = _orthoSizeReferencia * factorCercano;

            // ANTES: el techo de alejamiento (_zoomOrthoMaxLimite) se calculaba
            // con un "factorLejano" FIJO, como si el tablero siempre fuera de
            // 20x20 -- para cualquier tablero más chico que ese, ese techo
            // quedaba por ENCIMA del encuadre inicial real (AjustarCamaraAlTablero,
            // que ya guardó su resultado en '_factorEncuadreActual'), así que la
            // rueda del mouse dejaba seguir alejando la cámara más allá de "donde
            // arrancó" -- de ahí que se viera el tablero cada vez más chico sin
            // tope real y, al arrastrar con el cursor en ese estado, el radio de
            // paneo efectivo (que escala con el zoom, ver ActualizarCamaraLibre)
            // quedara desproporcionado y la cámara se saliera del encuadre
            // calibrado ("se buguea").
            //
            // Ahora el techo es dinámico y coincide EXACTAMENTE con el zoom
            // inicial de este tablero (el mismo factor que ya usó
            // AjustarCamaraAlTablero): no se puede alejar más de donde la
            // cámara ya empezó.
            _zoomOrthoMaxLimite = _orthoSizeReferencia * _factorEncuadreActual;

            // Nuevo tablero -> la próxima vez que ActualizarCamaraLibre corra
            // tiene que volver a capturar la posición inicial real de ESTA
            // cámara (ver comentario en ActualizarCamaraLibre), no seguir
            // usando la del tablero anterior.
            _posicionLibreInicialCapturada = false;
        }

        // Paneo (arrastrar con el botón central del mouse) + zoom (rueda) de la
        // cámara del jugador. El encuadre automático (AjustarCamaraAlTablero)
        // sigue fijando la posición/zoom INICIAL cada vez que se arma el
        // tablero; esto solo se agrega encima para poder moverse libremente
        // durante la partida, sin salirse del anillo de árboles/montañas
        // (_panLimiteRadio) ni de los extremos de zoom ya validados
        // (_zoomOrthoMinLimite/_zoomOrthoMaxLimite).
        private void Update()
        {
            ActualizarCamaraLibre();
        }

        private void ActualizarCamaraLibre()
        {
            if (!ajustarCamaraAutomaticamente) return;

            var camara = camaraJuego != null ? camaraJuego : Camera.main;
            var objetivo = objetivoCamaraAlternativo != null ? objetivoCamaraAlternativo
                : (camara != null ? camara.transform : null);
            if (camara == null || objetivo == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // Primera vez que corre tras un tablero nuevo: guarda la posición
            // XZ real con la que AjustarCamaraAlTablero dejó a la cámara
            // (que NO está cerca de '_centroTablero' -- está a la distancia
            // de encuadre isométrico, offset*factor, calculada ahí --, así
            // que el radio de paneo se mide desde ACÁ, no desde el centro del
            // tablero).
            //
            // ESTE ERA EL BUG DE FONDO ("se mueve para arriba y no deja
            // bajar"): antes, ClamparDentroDeLimitesDePaneo medía la
            // distancia de la cámara a '_centroTablero' directamente. Pero la
            // cámara, para verse isométrica, arranca calibrada a bastante más
            // distancia del centro del tablero (offset fijo, ver
            // CalibrarCamaraSiHaceFalta) que '_panLimiteRadio' (que es chico
            // a propósito, ~1.1x la diagonal del tablero). Entonces, apenas
            // el jugador arrastraba UNA SOLA VEZ, el clamp veía "distancia >
            // radioEfectivo" (porque la distancia INICIAL ya era mayor) y de
            // golpe arrastraba la posición X/Z de la cámara hasta pegarla a
            // ese radio chico, sin tocar ni la altura (Y) ni la rotación fija
            // -- el resultado es la cámara mirando desde muy cerca del centro
            // pero con el mismo ángulo/altura calibrados para verla desde
            // lejos: exactamente el acercamiento brusco a las montañas que se
            // ve en pantalla. Y como cada arrastre posterior volvía a quedar
            // pegado a ese mismo radio chico, no había forma de "volver".
            //
            // Midiendo en cambio desde la posición inicial real de la cámara,
            // el paneo empieza en distancia 0 (sin clamp) y solo se limita
            // cuánto te alejás DESDE ahí, que es lo que se quiso hacer
            // siempre.
            if (!_posicionLibreInicialCapturada)
            {
                _posicionLibreInicial = objetivo.position;
                _posicionLibreInicialCapturada = true;
            }

            // El objetivo de zoom arranca en el tamaño ortográfico actual (el
            // que ya fijó AjustarCamaraAlTablero), para no "saltar" apenas
            // empieza la partida, antes de que el jugador toque la rueda.
            if (_zoomObjetivoActual < 0f)
                _zoomObjetivoActual = camara.orthographicSize;

            if (camara.orthographic)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.001f)
                {
                    // Subido de 0.045 a 0.07: el zoom seguía sintiéndose lento
                    // porque, además, dependía de Time.deltaTime (ver abajo).
                    float objetivoZoom = Mathf.Clamp(
                        camara.orthographicSize - scroll * camara.orthographicSize * 0.07f,
                        _zoomOrthoMinLimite, _zoomOrthoMaxLimite);
                    _zoomObjetivoActual = objetivoZoom;
                }
            }

            if (Mathf.Abs(camara.orthographicSize - _zoomObjetivoActual) > 0.0001f)
            {
                // Time.deltaTime se congela si el juego está en pausa
                // (Time.timeScale = 0, ej. el botón de pausa de la partida) --
                // con eso el SmoothDamp anterior se quedaba "trabado" sin
                // aplicar el zoom aunque el valor objetivo sí cambiara, dando
                // la sensación de que el zoom seguía lento o no respondía del
                // todo. Con el tiempo NO escalado, la cámara sigue
                // respondiendo esté pausado el juego o no.
                camara.orthographicSize = Mathf.SmoothDamp(
                    camara.orthographicSize, _zoomObjetivoActual, ref _zoomVelocidadActual, 0.08f,
                    Mathf.Infinity, Time.unscaledDeltaTime);
            }

            // Paneo libre: arrastrar con el click IZQUIERDO (como en Clash of
            // Clans) o con el botón central (para quien ya se acostumbró a ese).
            bool arrastrando = mouse.leftButton.isPressed || mouse.middleButton.isPressed;
            if (arrastrando)
            {
                var delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.0001f)
                {
                    Vector3 derecha = objetivo.right; derecha.y = 0f; derecha.Normalize();
                    Vector3 adelante = objetivo.forward; adelante.y = 0f; adelante.Normalize();

                    float velocidad = camara.orthographicSize * 0.006f;
                    Vector3 desplazamiento = (-derecha * delta.x - adelante * delta.y) * velocidad;

                    Vector3 nuevaPosicion = objetivo.position + desplazamiento;

                    // ANTES (dos bugs encadenados):
                    // 1) El radio de paneo permitido escalaba con
                    //    'orthographicSize / _orthoSizeReferencia', pero
                    //    '_orthoSizeReferencia' es el tamaño ortográfico del
                    //    tablero de CALIBRACIÓN (10x10, fijo), no el de este
                    //    tablero. Para cualquier tablero más grande que 10x10
                    //    (como el 10x12 de esta escena), el zoom INICIAL ya
                    //    arrancaba con esa razón por encima de 1 -- es decir,
                    //    el radio quedaba "inflado" desde el primer frame,
                    //    sin que el jugador tocara la rueda del mouse para
                    //    nada.
                    // 2) Ese radio inflado crecía todavía más si el jugador
                    //    alejaba el zoom, así que arrastrar con el cursor
                    //    (sobre todo cerca del borde) sacaba fácilmente al
                    //    pivote de la cámara hasta pegarlo contra -- o
                    //    directamente dentro de -- el anillo de montañas: con
                    //    la cámara en un ángulo bajo (~26°), eso se ve como un
                    //    acercamiento brusco a picos de montaña llenando toda
                    //    la pantalla, sin poder "bajar" de ahí (la vista
                    //    correcta del tablero) porque el borde permitido
                    //    seguía siendo mayor de lo que debía.
                    //
                    // Ahora se divide por '_zoomOrthoMaxLimite', que es
                    // justamente el tamaño ortográfico INICIAL calculado para
                    // ESTE tablero (ver AjustarCamaraAlTablero /
                    // ConfigurarLimitesCamaraLibre) -- y, con el techo de zoom
                    // ya limitado a ese mismo valor (no se puede alejar más
                    // allá de donde arrancó la cámara), esta razón nunca pasa
                    // de 1. El radio efectivo queda entonces CONSTANTE en
                    // '_panLimiteRadio' durante toda la partida, sin importar
                    // el zoom.
                    float radioEfectivo = _panLimiteRadio *
                        Mathf.Max(1f, camara.orthographicSize / Mathf.Max(_zoomOrthoMaxLimite, 0.01f));

                    ClamparDentroDeLimitesDePaneo(ref nuevaPosicion, radioEfectivo);
                    objetivo.position = nuevaPosicion;
                }
            }
        }

        // Estado del suavizado de zoom (SmoothDamp necesita "recordar" la
        // velocidad actual entre frames y a dónde se está dirigiendo el valor).
        private float _zoomObjetivoActual = -1f;
        private float _zoomVelocidadActual;

        private void ClamparDentroDeLimitesDePaneo(ref Vector3 posicion, float radioEfectivo)
        {
            // Medido desde '_posicionLibreInicial' (la posición XZ real con
            // la que arrancó la cámara para este tablero), NO desde
            // '_centroTablero' -- ver el comentario grande en
            // ActualizarCamaraLibre. El tablero puede estar a más de
            // 'radioEfectivo' de distancia de la cámara (así se ve
            // isométrica), así que anclar el clamp al centro del tablero
            // sacaba a la cámara de su posición calibrada apenas se
            // arrastraba una vez.
            float dx = posicion.x - _posicionLibreInicial.x;
            float dz = posicion.z - _posicionLibreInicial.z;
            float distancia = Mathf.Sqrt(dx * dx + dz * dz);
            if (distancia > radioEfectivo && distancia > 0.0001f)
            {
                float escala = radioEfectivo / distancia;
                posicion.x = _posicionLibreInicial.x + dx * escala;
                posicion.z = _posicionLibreInicial.z + dz * escala;
            }
        }

        private void ConstruirCeldas(int ancho, int alto, Color colorCelda)
        {
            contenedorCeldas = new GameObject("Celdas").transform;
            contenedorCeldas.SetParent(transform, false);

            contenedorTanques = new GameObject("Tanques").transform;
            contenedorTanques.SetParent(transform, false);

            superficieCalculada = false;

            for (int x = 0; x < ancho; x++)
            {
                for (int y = 0; y < alto; y++)
                {
                    GameObject celda;
                    if (celdaPrefab != null)
                    {
                        celda = Instantiate(celdaPrefab, contenedorCeldas);
                    }
                    else
                    {
                        celda = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        celda.transform.SetParent(contenedorCeldas, false);
                        celda.transform.localScale =
                            new Vector3(tamanoCelda - separacion, 0.1f, tamanoCelda - separacion);

                        var rendererCelda = celda.GetComponent<Renderer>();
                        if (rendererCelda != null)
                        {
                            rendererCelda.material.color = Color.white;
                            rendererCelda.material.mainTexture = GenerarTexturaCelda(colorCelda);
                        }
                    }

                    celda.name = $"Celda_{x}_{y}";
                    celda.transform.localPosition = CeldaAPosicionMundo(x, y);

                    if (!superficieCalculada)
                    {
                        superficieCeldaMundoY = ObtenerAlturaSuperior(celda.transform, contenedorCeldas.position.y);
                        superficieCalculada = true;
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // COLOCACIÓN
        // ---------------------------------------------------------------------
        private float UmbralFrente => 1f - aperturaFrontal * 2f;

        private Vector3 DireccionHaciaCamara()
        {
            var camara = Camera.main;
            var direccion = camara != null ? camara.transform.position - _centroTablero : new Vector3(-1f, 0f, -1f);
            direccion.y = 0f;
            if (direccion.sqrMagnitude < 0.0001f) direccion = new Vector3(-1f, 0f, -1f);
            return direccion.normalized;
        }

        // Distancia del centro al borde RECTANGULAR real del tablero en esa
        // dirección. Todo el decorado se mide desde aquí y no desde el centro:
        // en un tablero alargado, un radio fijo caería dentro del tablero.
        private static float DistanciaCentroABorde(Vector3 direccionNormalizada, float mitadAncho, float mitadAlto)
        {
            float porX = Mathf.Abs(direccionNormalizada.x) > 0.0001f ? mitadAncho / Mathf.Abs(direccionNormalizada.x) : float.MaxValue;
            float porZ = Mathf.Abs(direccionNormalizada.z) > 0.0001f ? mitadAlto / Mathf.Abs(direccionNormalizada.z) : float.MaxValue;
            return Mathf.Min(porX, porZ);
        }

        // Punto a una distancia del borde expresada en unidades de paisaje, con
        // el ángulo restringido por su orientación respecto a la cámara.
        private bool PuntoAlrededorDelTablero(Vector3 haciaCamara, float dotMinimo, float dotMaximo,
            float offsetMin, float offsetMax, System.Random rng, out float px, out float pz)
        {
            for (int intento = 0; intento < 40; intento++)
            {
                float angulo = (float)rng.NextDouble() * Mathf.PI * 2f;
                var direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));

                float dot = Vector3.Dot(direccion, haciaCamara);
                if (dot < dotMinimo || dot > dotMaximo) continue;

                float distancia = DistanciaCentroABorde(direccion, _mitadAncho, _mitadAlto)
                                  + _u * Mathf.Lerp(offsetMin, offsetMax, (float)rng.NextDouble());

                px = _centroTablero.x + direccion.x * distancia;
                pz = _centroTablero.z + direccion.z * distancia;
                return true;
            }

            px = 0f;
            pz = 0f;
            return false;
        }

        private bool EstaCercaDeCampamento(float x, float z, float distanciaMinima)
        {
            float minimoCuadrado = distanciaMinima * distanciaMinima;
            foreach (var campamento in posicionesCampamentos)
            {
                float dx = x - campamento.x;
                float dz = z - campamento.z;
                if (dx * dx + dz * dz < minimoCuadrado) return true;
            }
            return false;
        }

        // Permite plantar árboles y rocas hasta el pie mismo de la sierra sin
        // que se metan dentro de una montaña.
        private bool EstaDentroDeMontana(float x, float z, float margenExtra)
        {
            foreach (var huella in huellasMontanas)
            {
                float dx = x - huella.x;
                float dz = z - huella.z;
                float radio = huella.y + margenExtra;
                if (dx * dx + dz * dz < radio * radio) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------
        // SIERRA
        // ---------------------------------------------------------------------
        private void ConstruirMontanas(Color colorRoca, Color colorCumbre)
        {
            contenedorMontanas = new GameObject("Montañas").transform;
            contenedorMontanas.SetParent(transform, false);

            var haciaCamara = DireccionHaciaCamara();
            var rng = new System.Random(12345);

            var texturaMontana = GenerarTexturaMontana(colorRoca, colorCumbre);
            var normalMontana = GenerarNormalMontana();
            var materialMontana = new Material(ObtenerShaderEstandar()) { mainTexture = texturaMontana };
            AsignarNormal(materialMontana, normalMontana, 1.1f);
            AjustarBrillo(materialMontana, 0.08f);

            Color colorDerrubio = Color.Lerp(colorRoca, new Color(0.82f, 0.79f, 0.72f), 0.7f);

            const int anillos = 3;
            for (int anillo = 0; anillo < anillos; anillo++)
            {
                float progreso = (float)anillo / (anillos - 1);
                float margenAnillo = _u * (sierraPegadaAlTablero + anillo * 1.9f);

                // Altura y radio típicos del anillo: con ellos se calcula cuántas
                // formaciones caben pegadas unas a otras. Así la sierra siempre
                // forma un muro continuo, sea el tablero cuadrado o alargado, y
                // no un collar de conos sueltos con huecos.
                float alturaTipica = _u * 2.5f * alturaSierra * (1f + anillo * 0.38f);
                float radioTipico = alturaTipica * 0.58f;
                if (anillo == anillos - 1) _alturaMontanaAprox = alturaTipica * 1.3f;
                float perimetroAnillo = 4f * (_mitadAncho + _mitadAlto)
                                        + 2f * Mathf.PI * (margenAnillo + radioTipico);
                int cantidad = Mathf.Clamp(Mathf.RoundToInt(perimetroAnillo / (radioTipico * 1.05f)), 12, 46);

                for (int i = 0; i < cantidad; i++)
                {
                    float angulo = (float)i / cantidad * Mathf.PI * 2f;
                    angulo += (float)(rng.NextDouble() - 0.5) * 0.18f;
                    var direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));

                    float dotCamara = Vector3.Dot(direccion, haciaCamara);
                    if (dotCamara > UmbralFrente) continue; // sector del bosque

                    float altura = alturaTipica * Mathf.Lerp(0.78f, 1.3f, (float)rng.NextDouble());
                    float radioBase = altura * Mathf.Lerp(0.45f, 0.7f, (float)rng.NextDouble());

                    // La distancia incluye la huella real de la formación (pico +
                    // agujas + derrubios), por eso ninguna puede pisar el tablero
                    // por grande que sea.
                    float huella = radioBase * 1.6f;
                    float extraFondo = _u * 0.7f * Mathf.Clamp01(-dotCamara);
                    float distancia = DistanciaCentroABorde(direccion, _mitadAncho, _mitadAlto)
                                      + margenAnillo + huella + extraFondo
                                      + (float)rng.NextDouble() * _u * 1.1f;

                    float x = _centroTablero.x + direccion.x * distancia;
                    float z = _centroTablero.z + direccion.z * distancia;

                    float tinte = 0.88f + (float)rng.NextDouble() * 0.22f;
                    var color = Color.Lerp(new Color(tinte, tinte, tinte),
                        new Color(0.8f, 0.85f, 0.95f), progreso * 0.4f);

                    var posicion = new Vector3(x, AlturaSueloMundo(x, z) - 0.3f * _escalaObjetos, z);
                    CrearFormacionMontanosa(posicion, radioBase, altura, materialMontana, color,
                        colorDerrubio, $"Montana_{anillo}_{i}", i + anillo * 101, rng);

                    huellasMontanas.Add(new Vector3(x, radioBase * 1.35f, z));
                }
            }
        }

        private void CrearFormacionMontanosa(Vector3 posicionBase, float radioBase, float altura,
            Material materialCompartido, Color tinte, Color colorDerrubio,
            string nombre, int semilla, System.Random rng)
        {
            var grupo = new GameObject(nombre).transform;
            grupo.SetParent(contenedorMontanas, false);
            grupo.localPosition = posicionBase;
            grupo.localRotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 5f,
                (float)(rng.NextDouble() * 360.0), (float)(rng.NextDouble() - 0.5) * 5f);

            // Falda de derrubios: la pedrera clara que se acumula al pie de las
            // paredes de roca. Es la transición que evita que la montaña parezca
            // clavada en el suelo como un cono suelto.
            var derrubio = CrearMontanaIrregular(radioBase * 1.4f, altura * 0.17f, semilla + 4321,
                altura * 0.17f, 18, false);
            derrubio.name = "Derrubios";
            derrubio.transform.SetParent(grupo, false);
            derrubio.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            PintarYLimpiar(derrubio, colorDerrubio * tinte);

            CrearPicoMontana(radioBase, altura, semilla, Vector3.zero, grupo, materialCompartido, tinte, rng);

            int satelites = rng.NextDouble() < 0.85 ? (rng.NextDouble() < 0.5 ? 2 : 1) : 0;
            for (int s = 0; s < satelites; s++)
            {
                float alturaSat = altura * Mathf.Lerp(0.45f, 0.85f, (float)rng.NextDouble());
                float radioSat = radioBase * Mathf.Lerp(0.32f, 0.58f, (float)rng.NextDouble());
                float anguloSat = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distSat = radioBase * Mathf.Lerp(0.45f, 0.8f, (float)rng.NextDouble());
                var offset = new Vector3(Mathf.Cos(anguloSat) * distSat, 0f, Mathf.Sin(anguloSat) * distSat);

                CrearPicoMontana(radioSat, alturaSat, semilla + 777 + s * 131, offset, grupo,
                    materialCompartido, tinte, rng);
            }
        }

        private void CrearPicoMontana(float radio, float altura, int semilla, Vector3 offsetLocal,
            Transform padre, Material materialCompartido, Color tinte, System.Random rng)
        {
            // El rango se calcula con la altura máxima posible de la sierra para
            // que las bandas de la textura (pasto / pedrera / roca / nieve) caigan
            // siempre a la misma cota real: solo los picos altos salen nevados.
            float rangoMaximo = _u * 2.5f * alturaSierra * 1.76f * 1.3f;
            var montana = CrearMontanaIrregular(radio, altura, semilla, rangoMaximo, 28);
            montana.transform.SetParent(padre, false);
            montana.transform.localPosition = offsetLocal;

            var renderer = montana.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = materialCompartido;
                renderer.material.color = tinte;
            }

            var collider = montana.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        // ---------------------------------------------------------------------
        // SUELO
        // ---------------------------------------------------------------------
        private void ConstruirSuelo(int ancho, int alto, Color colorSuelo)
        {
            contenedorTerreno = new GameObject("TerrenoExterior").transform;
            contenedorTerreno.SetParent(transform, false);

            var centro = new Vector3(_centroTablero.x, -0.16f, _centroTablero.z);
            float lado = Mathf.Max(ancho, alto) * tamanoCelda;
            float extensionTotal = lado + 52f * _escalaObjetos;

            _terrenoCentro = centro;
            _terrenoMitadAncho = _mitadAncho;
            _terrenoMitadAlto = _mitadAlto;
            _terrenoMargenPlano = tamanoCelda * 1.4f;
            _terrenoEscala = _escalaObjetos;
            _terrenoRadioCuenco = (_mitadAncho + _mitadAlto) * 0.5f + 12f * _escalaObjetos;
            _terrenoAlturaCuenco = 2.4f * _escalaObjetos;

            var sueloGo = new GameObject("SueloAccidentado");
            sueloGo.transform.SetParent(contenedorTerreno, false);
            sueloGo.transform.localPosition = centro;

            // Más subdivisión que antes: el relieve fino se pierde si la malla no
            // tiene vértices suficientes, por muy buena que sea la textura.
            int resolucion = Mathf.Clamp(Mathf.RoundToInt(extensionTotal / 0.9f), 48, 150);
            var mesh = CrearMallaTerrenoAccidentado(extensionTotal, resolucion,
                _mitadAncho, _mitadAlto, _terrenoMargenPlano, _terrenoEscala,
                _terrenoRadioCuenco, _terrenoAlturaCuenco);
            sueloGo.AddComponent<MeshFilter>().sharedMesh = mesh;

            var rendererTerreno = sueloGo.AddComponent<MeshRenderer>();
            var material = new Material(ObtenerShaderEstandar()) { color = Color.white };
            material.mainTexture = GenerarTexturaTerreno(colorSuelo);
            float repeticiones = Mathf.Max(3f, extensionTotal / (12f * _escalaObjetos));
            material.mainTextureScale = new Vector2(repeticiones, repeticiones);

            // Mapa de normales derivado del mismo ruido que pinta la textura: es
            // lo que da relieve de grava y hierba a distancia corta, algo que una
            // textura plana no puede conseguir por muchos colores que tenga.
            AsignarNormal(material, GenerarNormalTerreno(), 0.9f);
            AjustarBrillo(material, 0.04f);
            rendererTerreno.material = material;
        }

        private void ConstruirDecorado(Color colorRoca)
        {
            var rng = new System.Random(778);
            var haciaCamara = DireccionHaciaCamara();

            ConstruirRocas(haciaCamara, colorRoca, rng);
            ConstruirBosque(haciaCamara, rng);
            ConstruirPasto(haciaCamara, rng);
            ConstruirDecoradoBelico(haciaCamara, rng);
        }

        private void ConstruirRocas(Vector3 haciaCamara, Color colorRoca, System.Random rng)
        {
            var contenedor = new GameObject("Rocas").transform;
            contenedor.SetParent(contenedorTerreno, false);

            int cantidad = Mathf.RoundToInt(70f * Mathf.Clamp(_escalaObjetos, 0.8f, 1.8f));
            for (int i = 0; i < cantidad; i++)
            {
                if (!PuntoAlrededorDelTablero(haciaCamara, -1f, 1f, 1.2f, 11f, rng, out float px, out float pz))
                    continue;
                if (EstaDentroDeMontana(px, pz, 0.1f)) continue;
                if (EstaCercaDeCampamento(px, pz, 1.6f * _escalaObjetos)) continue;

                float radio = Mathf.Lerp(0.14f, 0.45f, (float)rng.NextDouble()) * _escalaObjetos;
                float altura = Mathf.Lerp(0.12f, 0.55f, (float)rng.NextDouble()) * _escalaObjetos;
                var roca = CrearMontanaIrregular(radio, altura, i + 500, altura, 12);
                roca.name = "Roca";
                roca.transform.SetParent(contenedor, false);
                roca.transform.localPosition = new Vector3(px, AlturaSueloMundo(px, pz), pz);
                roca.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                PintarYLimpiar(roca, Color.Lerp(colorRoca, new Color(0.3f, 0.27f, 0.22f), (float)rng.NextDouble() * 0.65f));
            }
        }

        // Bosque denso: empieza justo por detrás de la línea de campamentos y se
        // extiende hasta el pie de la sierra, rellenando el frente y los flancos.
        private void ConstruirBosque(Vector3 haciaCamara, System.Random rng)
        {
            var contenedor = new GameObject("Bosque").transform;
            contenedor.SetParent(contenedorTerreno, false);

            Color tronco = new Color(0.21f, 0.14f, 0.08f);
            Color follajeOscuro = new Color(0.07f, 0.16f, 0.08f);
            Color follajeClaro = new Color(0.22f, 0.34f, 0.14f);
            Color follajeOtono = new Color(0.45f, 0.36f, 0.13f);

            // Densidad más alta y sector un poco más ancho que antes: quedaban
            // huecos vacíos y visibles cerca del pie de la sierra en los
            // costados y en la esquina más alejada de los campamentos, porque el
            // sector frontal era muy estrecho y las distancias de exclusión
            // (montaña/campamento) dejaban demasiado colchón sin árboles.
            // La cantidad de manchones ahora escala con el perímetro del
            // tablero (como ya hacían las montañas y los campamentos), en vez
            // de un número fijo. Con un número fijo, un tablero chico (8x8)
            // tenía la MISMA cantidad de intentos que un tablero de referencia
            // 10x10 mientras la franja de bosque disponible es más corta, así
            // que cada intento descartado (por caer cerca de un campamento o
            // dentro de una montaña) pesaba mucho más y dejaba huecos visibles
            // en un flanco. Con esta fórmula un tablero chico simplemente pide
            // menos manchones en vez de perder más de los que pide.
            float celdasAncho = 2f * _mitadAncho / Mathf.Max(tamanoCelda, 0.01f);
            float celdasAlto = 2f * _mitadAlto / Mathf.Max(tamanoCelda, 0.01f);
            int manchones = Mathf.Clamp(
                Mathf.RoundToInt((celdasAncho + celdasAlto) * 5f * densidadBosque), 40, 220);
            const int maximoArboles = 700;
            const int reintentosPorManchon = 6;
            int indice = 0;

            float dotMinimo = UmbralFrente - 0.18f;
            float inicioBosque = distanciaEdificios + 0.3f;
            // Ningún árbol (ni el centro del manchón ni una bala perdida del
            // borde del manchón) puede caer más cerca del tablero que esto:
            // es la misma franja despejada que separa el tablero de los
            // campamentos. Antes solo el CENTRO del manchón respetaba
            // 'inicioBosque'; el radio del manchón (hasta 3.8) podía superar
            // ese margen (2.3 con los valores por defecto) y tirar árboles
            // sueltos dentro de la franja despejada -- el árbol "de más" que
            // aparece pegado a los campamentos en tableros chicos.
            float distanciaMinimaAlBorde = _u * (distanciaEdificios + 0.1f);

            for (int m = 0; m < manchones && indice < maximoArboles; m++)
            {
                float cx = 0f, cz = 0f;
                bool centroValido = false;

                for (int intento = 0; intento < reintentosPorManchon; intento++)
                {
                    if (!PuntoAlrededorDelTablero(haciaCamara, dotMinimo, 1f, inicioBosque, inicioBosque + 11f,
                            rng, out cx, out cz))
                        continue;
                    if (EstaDentroDeMontana(cx, cz, 0.35f * _escalaObjetos)) continue;
                    if (EstaCercaDeCampamento(cx, cz, 1.5f * _escalaObjetos)) continue;

                    centroValido = true;
                    break;
                }

                if (!centroValido) continue;

                // Manchones más grandes y más poblados, y con menos separación
                // entre sí (el rango de offset de arriba es más corto): así las
                // copas se solapan entre manchones vecinos y se lee como un
                // bosque compacto en vez de islas de árboles.
                int arbolesEnManchon = 9 + rng.Next(8);
                float radioManchon = Mathf.Lerp(2f, 3.8f, (float)rng.NextDouble()) * _escalaObjetos;
                float tonoManchon = (float)rng.NextDouble();

                for (int a = 0; a < arbolesEnManchon && indice < maximoArboles; a++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist = radioManchon * Mathf.Sqrt((float)rng.NextDouble());
                    float px = cx + Mathf.Cos(ang) * dist;
                    float pz = cz + Mathf.Sin(ang) * dist;

                    if (EstaDentroDeMontana(px, pz, 0.15f * _escalaObjetos)) continue;
                    if (EstaCercaDeCampamento(px, pz, 1.5f * _escalaObjetos)) continue;

                    var direccionPunto = new Vector3(px, 0f, pz) - _centroTablero;
                    if (direccionPunto.sqrMagnitude > 0.0001f)
                    {
                        float bordePunto = DistanciaCentroABorde(direccionPunto.normalized, _mitadAncho, _mitadAlto);
                        if (direccionPunto.magnitude - bordePunto < distanciaMinimaAlBorde) continue;
                    }

                    var follaje = Color.Lerp(follajeOscuro, follajeClaro, (float)rng.NextDouble());
                    if (tonoManchon > 0.8f) follaje = Color.Lerp(follaje, follajeOtono, 0.5f);

                    CrearArbol(contenedor, px, pz, indice++, tronco, follaje, rng);
                }
            }
        }

        private void CrearArbol(Transform contenedor, float px, float pz, int indice,
            Color tronco, Color follaje, System.Random rng)
        {
            var arbol = new GameObject("Arbol").transform;
            arbol.SetParent(contenedor, false);
            arbol.localPosition = new Vector3(px, AlturaSueloMundo(px, pz), pz);
            arbol.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.75f, (float)rng.NextDouble()) * _escalaObjetos;
            arbol.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            float alturaTronco = Mathf.Lerp(0.35f, 0.6f, (float)rng.NextDouble());
            CrearPieza(arbol, PrimitiveType.Cylinder, new Vector3(0f, alturaTronco * 0.5f, 0f),
                new Vector3(0.05f, alturaTronco * 0.5f, 0.05f), tronco);

            float radioCopa = Mathf.Lerp(0.3f, 0.46f, (float)rng.NextDouble());
            float alturaCapa = Mathf.Lerp(0.48f, 0.7f, (float)rng.NextDouble());

            for (int capa = 0; capa < 3; capa++)
            {
                float factor = 1f - capa * 0.27f;
                var copa = CrearMontanaIrregular(radioCopa * factor, alturaCapa * (1f - capa * 0.18f),
                    indice * 11 + 1000 + capa, alturaCapa, 8, false);
                copa.transform.SetParent(arbol, false);
                copa.transform.localPosition = new Vector3(0f, alturaTronco * 0.6f + alturaCapa * 0.42f * capa, 0f);
                PintarYLimpiar(copa, follaje * (1f - capa * 0.07f));
            }
        }

        private void ConstruirPasto(Vector3 haciaCamara, System.Random rng)
        {
            var contenedor = new GameObject("Pasto").transform;
            contenedor.SetParent(contenedorTerreno, false);

            Color verdeOscuro = new Color(0.2f, 0.3f, 0.12f);
            Color verdeClaro = new Color(0.38f, 0.44f, 0.19f);

            int manojos = Mathf.RoundToInt(80f * Mathf.Clamp(_escalaObjetos, 0.8f, 1.8f));
            for (int i = 0; i < manojos; i++)
            {
                if (!PuntoAlrededorDelTablero(haciaCamara, -1f, 1f, 1f, 12f, rng, out float px, out float pz))
                    continue;
                if (EstaDentroDeMontana(px, pz, 0f)) continue;

                var baseManojo = new Vector3(px, AlturaSueloMundo(px, pz) + 0.02f, pz);
                int hojas = 3 + rng.Next(4);
                for (int h = 0; h < hojas; h++)
                {
                    float ex = (float)(rng.NextDouble() - 0.5) * 0.45f * _escalaObjetos;
                    float ez = (float)(rng.NextDouble() - 0.5) * 0.45f * _escalaObjetos;
                    float alturaHoja = Mathf.Lerp(0.15f, 0.32f, (float)rng.NextDouble()) * _escalaObjetos;
                    var color = Color.Lerp(verdeOscuro, verdeClaro, (float)rng.NextDouble());

                    CrearPieza(contenedor, PrimitiveType.Cube,
                        baseManojo + new Vector3(ex, alturaHoja * 0.5f, ez),
                        new Vector3(0.035f * _escalaObjetos, alturaHoja, 0.035f * _escalaObjetos), color,
                        Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 20f, (float)rng.NextDouble() * 360f,
                            (float)(rng.NextDouble() - 0.5) * 20f));
                }
            }
        }

        private void ConstruirDecoradoBelico(Vector3 haciaCamara, System.Random rng)
        {
            var contenedor = new GameObject("DecoradoBelico").transform;
            contenedor.SetParent(contenedorTerreno, false);

            Color tierraQuemada = new Color(0.14f, 0.11f, 0.08f);
            float dotMinimo = UmbralFrente - 0.3f;

            int crateres = Mathf.RoundToInt(14f * Mathf.Clamp(_escalaObjetos, 0.8f, 1.8f));
            for (int i = 0; i < crateres; i++)
            {
                if (!PuntoAlrededorDelTablero(haciaCamara, dotMinimo, 1f, 1.4f, 9f, rng, out float px, out float pz))
                    continue;
                if (EstaDentroDeMontana(px, pz, 0f)) continue;
                if (EstaCercaDeCampamento(px, pz, 2.4f * _escalaObjetos)) continue;

                var posicion = new Vector3(px, AlturaSueloMundo(px, pz) + 0.015f, pz);
                float radio = Mathf.Lerp(0.5f, 1.3f, (float)rng.NextDouble()) * _escalaObjetos;

                CrearPieza(contenedor, PrimitiveType.Cylinder, posicion,
                    new Vector3(radio, 0.02f, radio), tierraQuemada);
                CrearPieza(contenedor, PrimitiveType.Sphere, posicion + Vector3.down * radio * 0.35f,
                    new Vector3(radio * 0.85f, radio * 0.5f, radio * 0.85f),
                    Color.Lerp(tierraQuemada, Color.black, 0.3f));
            }

            int trincheras = Mathf.RoundToInt(5f * Mathf.Clamp(_escalaObjetos, 0.8f, 1.6f));
            for (int i = 0; i < trincheras; i++)
            {
                if (!PuntoAlrededorDelTablero(haciaCamara, dotMinimo, 1f, 2f, 7f, rng, out float px, out float pz))
                    continue;
                if (EstaDentroDeMontana(px, pz, 0f)) continue;
                if (EstaCercaDeCampamento(px, pz, 3.2f * _escalaObjetos)) continue;

                var trinchera = new GameObject("Trinchera").transform;
                trinchera.SetParent(contenedor, false);
                trinchera.localPosition = new Vector3(px, AlturaSueloMundo(px, pz) + 0.05f, pz);
                trinchera.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                trinchera.localScale = Vector3.one * _escalaObjetos;

                CrearPieza(trinchera, PrimitiveType.Cube, Vector3.zero, new Vector3(2.4f, 0.05f, 0.7f), tierraQuemada);
                for (int lado = -1; lado <= 1; lado += 2)
                    for (int s = 0; s < 4; s++)
                        CrearPieza(trinchera, PrimitiveType.Capsule,
                            new Vector3(-1.0f + s * 0.65f, 0.1f, lado * 0.42f),
                            new Vector3(0.28f, 0.14f, 0.28f), new Color(0.4f, 0.34f, 0.24f));
            }
        }

        // ---------------------------------------------------------------------
        // TERRENO (malla + consulta de altura)
        // ---------------------------------------------------------------------
        private const float OffsetRuidoTerrenoX = 137.2f;
        private const float OffsetRuidoTerrenoZ = 84.9f;

        private Vector3 _terrenoCentro;
        private float _terrenoMitadAncho, _terrenoMitadAlto, _terrenoMargenPlano;
        private float _terrenoEscala = 1f, _terrenoRadioCuenco = 20f, _terrenoAlturaCuenco = 2.4f;

        // Relieve del valle. Además del ruido (ahora escalado con el tablero para
        // que las lomas crezcan con él), el terreno sube suavemente al alejarse:
        // el campo de batalla queda en el fondo de un cuenco y la sierra apoyada
        // sobre las laderas, en vez de todo sobre una mesa plana.
        private static float AlturaTerrenoLocalEn(float px, float pz, float mitadAncho, float mitadAlto,
            float margenPlano, float escala, float radioCuenco, float alturaCuenco)
        {
            float fueraX = Mathf.Max(0f, Mathf.Abs(px) - (mitadAncho + margenPlano));
            float fueraZ = Mathf.Max(0f, Mathf.Abs(pz) - (mitadAlto + margenPlano));
            float fuera = Mathf.Sqrt(fueraX * fueraX + fueraZ * fueraZ);
            float influencia = Mathf.Clamp01(fuera / (3f * escala));

            float nx = (px + OffsetRuidoTerrenoX) / escala;
            float nz = (pz + OffsetRuidoTerrenoZ) / escala;

            float amplio = Mathf.PerlinNoise(nx * 0.035f, nz * 0.035f) - 0.5f;
            float medio = Mathf.PerlinNoise(nx * 0.09f, nz * 0.09f) - 0.5f;
            float grueso = Mathf.PerlinNoise(nx * 0.22f, nz * 0.22f) - 0.5f;
            float fino = Mathf.PerlinNoise(nx * 0.55f, nz * 0.55f) - 0.5f;

            float relieve = (amplio * 2.8f + medio * 1.5f + grueso * 0.6f + fino * 0.22f) * escala;
            float cuenco = Mathf.Pow(Mathf.Clamp01(fuera / Mathf.Max(radioCuenco, 0.01f)), 1.7f) * alturaCuenco;

            return (relieve + cuenco) * influencia;
        }

        private float AlturaSueloMundo(float x, float z)
        {
            float px = x - _terrenoCentro.x;
            float pz = z - _terrenoCentro.z;
            return _terrenoCentro.y + AlturaTerrenoLocalEn(px, pz, _terrenoMitadAncho, _terrenoMitadAlto,
                _terrenoMargenPlano, _terrenoEscala, _terrenoRadioCuenco, _terrenoAlturaCuenco);
        }

        private static Mesh CrearMallaTerrenoAccidentado(float extension, int resolucion,
            float mitadAncho, float mitadAlto, float margenPlano, float escala,
            float radioCuenco, float alturaCuenco)
        {
            int verticesPorLado = resolucion + 1;
            var vertices = new Vector3[verticesPorLado * verticesPorLado];
            var uvs = new Vector2[vertices.Length];
            float paso = extension / resolucion;
            float mitadExtension = extension / 2f;

            for (int z = 0; z <= resolucion; z++)
            {
                for (int x = 0; x <= resolucion; x++)
                {
                    float px = -mitadExtension + x * paso;
                    float pz = -mitadExtension + z * paso;
                    float altura = AlturaTerrenoLocalEn(px, pz, mitadAncho, mitadAlto, margenPlano,
                        escala, radioCuenco, alturaCuenco);

                    int indice = z * verticesPorLado + x;
                    vertices[indice] = new Vector3(px, altura, pz);
                    uvs[indice] = new Vector2((float)x / resolucion, (float)z / resolucion);
                }
            }

            var triangulos = new int[resolucion * resolucion * 6];
            int t = 0;
            for (int z = 0; z < resolucion; z++)
            {
                for (int x = 0; x < resolucion; x++)
                {
                    int i0 = z * verticesPorLado + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + verticesPorLado;
                    int i3 = i2 + 1;
                    triangulos[t++] = i0; triangulos[t++] = i2; triangulos[t++] = i1;
                    triangulos[t++] = i1; triangulos[t++] = i2; triangulos[t++] = i3;
                }
            }

            var mesh = new Mesh { name = "SueloAccidentado" };
            if (vertices.Length > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangulos;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents(); // necesario para que el normal map funcione
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------------
        // GEOMETRÍA DE MONTAÑA
        //
        // El ruido angular es COMPARTIDO por todos los anillos, con amplitud
        // creciente hacia la cumbre: eso genera aristas y canaletas continuas de
        // la cima al pie, que es lo que hace leer una pared de roca. La malla va
        // con sombreado plano (vértices sin compartir) para que cada faceta
        // atrape la luz por separado.
        // ---------------------------------------------------------------------
        private static GameObject CrearMontanaIrregular(float radio, float altura, int semilla,
            float rangoMaxAltura, int lados = 24, bool agujasRocosas = true)
        {
            lados = Mathf.Max(6, lados);
            var rng = new System.Random(semilla);

            var perfilAngular = new float[lados];
            var perfilAgujas = new float[lados];
            float fase1 = (float)rng.NextDouble() * 10f;
            float fase2 = (float)rng.NextDouble() * 10f;
            int armonico = 3 + rng.Next(3);

            for (int i = 0; i < lados; i++)
            {
                float a = (float)i / lados * Mathf.PI * 2f;
                float ondaGruesa = Mathf.Sin(a * armonico + fase1);
                float ondaFina = Mathf.Sin(a * (armonico * 2 + 1) + fase2) * 0.45f;
                float aleatorio = (float)rng.NextDouble() - 0.5f;
                perfilAngular[i] = Mathf.Clamp(ondaGruesa * 0.6f + ondaFina + aleatorio * 0.55f, -1f, 1f);
                perfilAgujas[i] = (float)rng.NextDouble();
            }

            // (altura relativa, radio relativo, amplitud del perfil angular).
            Vector3[] anillos = agujasRocosas
                ? new[]
                {
                    new Vector3(0f, 1f, 0.12f),
                    new Vector3(0.14f, 0.82f, 0.2f),
                    new Vector3(0.33f, 0.62f, 0.3f),
                    new Vector3(0.55f, 0.5f, 0.38f),
                    new Vector3(0.73f, 0.37f, 0.46f),
                    new Vector3(0.87f, 0.2f, 0.54f)
                }
                : new[]
                {
                    new Vector3(0f, 1f, 0.16f),
                    new Vector3(0.32f, 0.72f, 0.22f),
                    new Vector3(0.64f, 0.42f, 0.26f),
                    new Vector3(0.87f, 0.18f, 0.3f)
                };

            Vector3 PuntoAnillo(int anillo, int indice)
            {
                var datos = anillos[anillo];
                float ang = (float)indice / lados * Mathf.PI * 2f;
                float r = Mathf.Max(radio * 0.04f, radio * datos.y * (1f + perfilAngular[indice] * datos.z));
                float y = altura * datos.x;
                if (agujasRocosas && anillo >= anillos.Length - 2)
                    y += altura * (perfilAgujas[indice] - 0.35f) * 0.18f;
                return new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
            }

            float anguloCima = (float)rng.NextDouble() * Mathf.PI * 2f;
            float offsetCima = radio * 0.2f * (float)rng.NextDouble();
            var cima = new Vector3(Mathf.Cos(anguloCima) * offsetCima, altura, Mathf.Sin(anguloCima) * offsetCima);

            var vertices = new List<Vector3>(lados * anillos.Length * 6);
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangulos = new List<int>(vertices.Capacity);
            float rango = Mathf.Max(rangoMaxAltura, 0.01f);

            void Cara(Vector3 a, Vector3 b, Vector3 c, float ua, float ub, float uc)
            {
                int baseIdx = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uvs.Add(new Vector2(ua, Mathf.Clamp01(a.y / rango)));
                uvs.Add(new Vector2(ub, Mathf.Clamp01(b.y / rango)));
                uvs.Add(new Vector2(uc, Mathf.Clamp01(c.y / rango)));
                triangulos.Add(baseIdx); triangulos.Add(baseIdx + 1); triangulos.Add(baseIdx + 2);
            }

            for (int i = 0; i < lados; i++)
            {
                int sig = (i + 1) % lados;
                float u0 = (float)i / lados;
                float u1 = (float)(i + 1) / lados;

                Cara(Vector3.zero, PuntoAnillo(0, i), PuntoAnillo(0, sig), 0f, u0, u1);

                for (int anillo = 0; anillo < anillos.Length - 1; anillo++)
                {
                    var abajoI = PuntoAnillo(anillo, i);
                    var abajoSig = PuntoAnillo(anillo, sig);
                    var arribaI = PuntoAnillo(anillo + 1, i);
                    var arribaSig = PuntoAnillo(anillo + 1, sig);

                    Cara(abajoI, arribaI, abajoSig, u0, u0, u1);
                    Cara(abajoSig, arribaI, arribaSig, u1, u0, u1);
                }

                var crestaI = PuntoAnillo(anillos.Length - 1, i);
                var crestaSig = PuntoAnillo(anillos.Length - 1, sig);
                Cara(crestaI, cima, crestaSig, u0, (u0 + u1) * 0.5f, u1);
            }

            var mesh = new Mesh { name = "MontanaIrregular" };
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangulos, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var montana = new GameObject("Montana");
            montana.AddComponent<MeshFilter>().sharedMesh = mesh;
            // Un MeshRenderer creado a mano queda sin material (a diferencia de
            // CreatePrimitive) y bajo URP eso se ve plano y deslavado.
            var renderer = montana.AddComponent<MeshRenderer>();
            var material = new Material(ObtenerShaderEstandar());
            AjustarBrillo(material, 0.06f);
            renderer.material = material;
            return montana;
        }

        // ---------------------------------------------------------------------
        // MATERIALES Y TEXTURAS
        // ---------------------------------------------------------------------
        private static void AjustarBrillo(Material material, float brillo)
        {
            if (material == null) return;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", brillo);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", brillo);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        }

        private static void AsignarNormal(Material material, Texture2D normal, float fuerza)
        {
            if (material == null || normal == null) return;
            if (!material.HasProperty("_BumpMap")) return;

            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", fuerza);
        }

        // Ruido sin costura en X (la textura de montaña da la vuelta al cono).
        private static float RuidoTileableU(float x, float y, float frecX, float frecY, int ancho)
        {
            float a = Mathf.PerlinNoise(x * frecX, y * frecY);
            float b = Mathf.PerlinNoise((x - ancho) * frecX, y * frecY);
            return Mathf.Lerp(a, b, x / ancho);
        }

        // Ruido sin costura en ambos ejes (el suelo se repite muchas veces).
        private static float RuidoTileable(float x, float y, float frec, int tam)
        {
            float fx = x * frec, fy = y * frec;
            float periodo = tam * frec;
            float a = Mathf.PerlinNoise(fx, fy);
            float b = Mathf.PerlinNoise(fx - periodo, fy);
            float c = Mathf.PerlinNoise(fx, fy - periodo);
            float d = Mathf.PerlinNoise(fx - periodo, fy - periodo);
            float u = x / tam, v = y / tam;
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // Convierte un campo de alturas en un mapa de normales tangente. Se
        // empaqueta con alfa = 1 para que funcione tanto con el camino RGB como
        // con el DXT5nm que usa Unity al desempaquetar.
        private static Texture2D NormalDesdeAlturas(float[] alturas, int tam, float fuerza, string nombre)
        {
            var textura = new Texture2D(tam, tam, TextureFormat.RGBA32, false, true)
            {
                name = nombre,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < tam; y++)
            {
                for (int x = 0; x < tam; x++)
                {
                    int xi = (x - 1 + tam) % tam, xd = (x + 1) % tam;
                    int ya = (y - 1 + tam) % tam, yb = (y + 1) % tam;

                    float dx = alturas[y * tam + xi] - alturas[y * tam + xd];
                    float dy = alturas[ya * tam + x] - alturas[yb * tam + x];

                    var n = new Vector3(dx * fuerza, dy * fuerza, 1f).normalized;
                    textura.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            }

            textura.Apply();
            return textura;
        }

        private static Texture2D _normalTerrenoCache;

        private static Texture2D GenerarNormalTerreno()
        {
            if (_normalTerrenoCache != null) return _normalTerrenoCache;

            const int tam = 256;
            var alturas = new float[tam * tam];
            for (int y = 0; y < tam; y++)
                for (int x = 0; x < tam; x++)
                    alturas[y * tam + x] = RuidoTileable(x, y, 0.05f, tam) * 0.55f
                                           + RuidoTileable(x, y, 0.16f, tam) * 0.3f
                                           + RuidoTileable(x, y, 0.42f, tam) * 0.15f;

            _normalTerrenoCache = NormalDesdeAlturas(alturas, tam, 14f, "NormalTerreno");
            return _normalTerrenoCache;
        }

        private static Texture2D _normalMontanaCache;

        private static Texture2D GenerarNormalMontana()
        {
            if (_normalMontanaCache != null) return _normalMontanaCache;

            const int tam = 256;
            var alturas = new float[tam * tam];
            for (int y = 0; y < tam; y++)
                for (int x = 0; x < tam; x++)
                    // Mismo patrón que la textura: canaletas verticales marcadas
                    // (frecuencia alta en X, baja en Y) más grano de roca.
                    alturas[y * tam + x] = RuidoTileableU(x, y, 0.05f, 0.008f, tam) * 0.6f
                                           + RuidoTileableU(x, y, 0.2f, 0.03f, tam) * 0.28f
                                           + RuidoTileableU(x, y, 0.45f, 0.45f, tam) * 0.12f;

            _normalMontanaCache = NormalDesdeAlturas(alturas, tam, 22f, "NormalMontana");
            return _normalMontanaCache;
        }

        private static Texture2D _texturaMontanaCache;
        private static Color _texturaMontanaRoca, _texturaMontanaCumbre;

        // Bandas por cota real (UV.v = altura mundial normalizada): pasto al pie,
        // pedrera, pared de roca cálida, roca gris y cumbre clara, con estrías
        // verticales encima. Solo los picos altos llegan a la banda clara.
        private static Texture2D GenerarTexturaMontana(Color colorRoca, Color colorCumbre)
        {
            if (_texturaMontanaCache != null && _texturaMontanaRoca == colorRoca && _texturaMontanaCumbre == colorCumbre)
                return _texturaMontanaCache;

            const int ancho = 256;
            const int alto = 512;
            var textura = new Texture2D(ancho, alto, TextureFormat.RGB24, false)
            {
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color pradera = Color.Lerp(colorRoca, new Color(0.26f, 0.34f, 0.16f), 0.65f);
            Color pedrera = Color.Lerp(colorRoca, new Color(0.76f, 0.72f, 0.63f), 0.6f);
            Color rocaCalida = Color.Lerp(colorRoca, new Color(0.64f, 0.52f, 0.36f), 0.5f);
            Color rocaClara = Color.Lerp(colorRoca, new Color(0.8f, 0.78f, 0.74f), 0.6f);

            for (int y = 0; y < alto; y++)
            {
                float t = (float)y / (alto - 1);

                Color franja;
                if (t < 0.1f) franja = Color.Lerp(pradera, pedrera, t / 0.1f);
                else if (t < 0.28f) franja = Color.Lerp(pedrera, rocaCalida, (t - 0.1f) / 0.18f);
                else if (t < 0.62f) franja = Color.Lerp(rocaCalida, rocaClara, (t - 0.28f) / 0.34f);
                else franja = Color.Lerp(rocaClara, colorCumbre, (t - 0.62f) / 0.38f);

                for (int x = 0; x < ancho; x++)
                {
                    float canaletaGruesa = RuidoTileableU(x, y, 0.05f, 0.008f, ancho) - 0.5f;
                    float canaletaFina = RuidoTileableU(x, y, 0.2f, 0.03f, ancho) - 0.5f;
                    float estrato = Mathf.PerlinNoise(x * 0.01f, y * 0.18f) - 0.5f;
                    float grano = RuidoTileableU(x, y, 0.45f, 0.45f, ancho) - 0.5f;

                    float sombreado = canaletaGruesa * 0.45f + canaletaFina * 0.22f
                                      + estrato * 0.12f + grano * 0.09f;
                    sombreado *= Mathf.Lerp(0.4f, 1.2f, Mathf.Clamp01((t - 0.08f) / 0.5f));

                    var color = franja * (1f + sombreado);
                    color.r = Mathf.Clamp01(color.r);
                    color.g = Mathf.Clamp01(color.g);
                    color.b = Mathf.Clamp01(color.b);
                    textura.SetPixel(x, y, color);
                }
            }

            textura.Apply();
            _texturaMontanaCache = textura;
            _texturaMontanaRoca = colorRoca;
            _texturaMontanaCumbre = colorCumbre;
            return textura;
        }

        private static Texture2D _texturaCeldaCache;
        private static Color _texturaCeldaColor;

        // Textura de cada celda del tablero: la misma familia de ruido que el
        // suelo exterior (para que el césped del campo de juego se sienta del
        // mismo material que el valle) más un borde oscuro marcado en los
        // cuatro lados, que es lo que da la cuadrícula contrastada. Antes la
        // celda era un color plano y el "grid" solo se notaba por el hueco de
        // separación entre cubos, muy sutil a distancia.
        private static Texture2D GenerarTexturaCelda(Color colorCelda)
        {
            if (_texturaCeldaCache != null && _texturaCeldaColor == colorCelda)
                return _texturaCeldaCache;

            const int tam = 128;
            var textura = new Texture2D(tam, tam, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color pastoClaro = Color.Lerp(colorCelda, new Color(0.55f, 0.62f, 0.32f), 0.35f);
            Color pastoOscuro = Color.Lerp(colorCelda, new Color(0.18f, 0.26f, 0.11f), 0.4f);
            Color borde = colorCelda * 0.45f;

            const int grosorBorde = 6;

            for (int y = 0; y < tam; y++)
            {
                for (int x = 0; x < tam; x++)
                {
                    float macro = RuidoTileable(x, y, 0.03f, tam);
                    float fino = RuidoTileable(x, y, 0.12f, tam);

                    var color = Color.Lerp(pastoOscuro, pastoClaro, Mathf.SmoothStep(0.3f, 0.7f, macro));
                    color *= 1f + (fino - 0.5f) * 0.15f;

                    int distanciaBorde = Mathf.Min(Mathf.Min(x, tam - 1 - x), Mathf.Min(y, tam - 1 - y));
                    if (distanciaBorde < grosorBorde)
                    {
                        float t = 1f - (float)distanciaBorde / grosorBorde;
                        color = Color.Lerp(color, borde, t * t);
                    }

                    color.r = Mathf.Clamp01(color.r);
                    color.g = Mathf.Clamp01(color.g);
                    color.b = Mathf.Clamp01(color.b);
                    textura.SetPixel(x, y, color);
                }
            }

            textura.Apply();
            _texturaCeldaCache = textura;
            _texturaCeldaColor = colorCelda;
            return textura;
        }

        private static Texture2D _texturaTerrenoCache;
        private static Color _texturaTerrenoColor;

        // Suelo del valle: pasto verde, pasto seco, calvas de tierra y gravilla,
        // mezclados con ruido en cuatro escalas y sin costura al repetirse.
        private static Texture2D GenerarTexturaTerreno(Color colorSuelo)
        {
            if (_texturaTerrenoCache != null && _texturaTerrenoColor == colorSuelo)
                return _texturaTerrenoCache;

            const int tam = 256;
            var textura = new Texture2D(tam, tam, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color pastoVivo = Color.Lerp(colorSuelo, new Color(0.26f, 0.38f, 0.15f), 0.6f);
            Color pastoSeco = Color.Lerp(colorSuelo, new Color(0.58f, 0.52f, 0.28f), 0.55f);
            Color tierra = Color.Lerp(colorSuelo, new Color(0.31f, 0.24f, 0.16f), 0.6f);
            Color gravilla = Color.Lerp(colorSuelo, new Color(0.62f, 0.6f, 0.55f), 0.7f);

            for (int y = 0; y < tam; y++)
            {
                for (int x = 0; x < tam; x++)
                {
                    float macro = RuidoTileable(x, y, 0.008f, tam);
                    float medio = RuidoTileable(x, y, 0.035f, tam);
                    float fino = RuidoTileable(x, y, 0.13f, tam);
                    float micro = RuidoTileable(x, y, 0.4f, tam);

                    var color = Color.Lerp(pastoVivo, pastoSeco,
                        Mathf.SmoothStep(0.32f, 0.7f, macro + (medio - 0.5f) * 0.35f));
                    color = Color.Lerp(color, tierra, Mathf.SmoothStep(0.58f, 0.86f, medio));
                    color = Color.Lerp(color, gravilla, Mathf.SmoothStep(0.72f, 0.94f, fino) * 0.6f);

                    // El micro-ruido hace de sombra de las piedrecillas y le da
                    // grano; combinado con el normal map se lee como suelo real.
                    float sombreado = (fino - 0.5f) * 0.2f + (micro - 0.5f) * 0.26f;
                    color *= (1f + sombreado);
                    color.r = Mathf.Clamp01(color.r);
                    color.g = Mathf.Clamp01(color.g);
                    color.b = Mathf.Clamp01(color.b);
                    textura.SetPixel(x, y, color);
                }
            }

            textura.Apply();
            _texturaTerrenoCache = textura;
            _texturaTerrenoColor = colorSuelo;
            return textura;
        }

        private static Shader _shaderEstandarCache;

        private static Shader ObtenerShaderEstandar()
        {
            if (_shaderEstandarCache != null) return _shaderEstandarCache;

            _shaderEstandarCache = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Diffuse");
            return _shaderEstandarCache;
        }

        // Asigna una textura al slot "base" correcto según el shader real que
        // devolvió ObtenerShaderEstandar(). El causante de que las banderas se
        // vieran GRISES (planas, sin dibujo) era justamente esto: en este
        // proyecto (URP) ese shader es "Universal Render Pipeline/Lit", que
        // usa la propiedad "_BaseMap" -- no "_MainTex". El setter
        // Material.mainTexture (usado antes acá) sólo escribe en "_MainTex",
        // así que en un shader URP no hacía nada: la textura de la bandera
        // nunca llegaba a aplicarse y quedaba el "_BaseColor" gris por
        // defecto del material nuevo. Con SetTexture("_BaseMap", ...) (y de
        // paso "_BaseColor" a blanco para no teñir la textura) se pinta
        // correctamente en URP; para Standard/Diffuse (fallback si el
        // proyecto no tuviera URP) sí existe "_MainTex", así que ese caso
        // sigue andando con mainTexture como antes.
        private static void AplicarTexturaPrincipal(Material material, Texture2D textura)
        {
            if (material == null || textura == null) return;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", textura);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            }
            else if (material.HasProperty("_MainTex"))
            {
                material.mainTexture = textura;
                if (material.HasProperty("_Color")) material.color = Color.white;
            }
        }

        // ---------------------------------------------------------------------
        // CAMPAMENTOS
        // ---------------------------------------------------------------------
        private void CalcularPosicionesCampamentos(int ancho, int alto)
        {
            posicionesCampamentos.Clear();

            var haciaCamara = DireccionHaciaCamara();
            float margen = _u * distanciaEdificios;
            float distanciaAncla = DistanciaCentroABorde(haciaCamara, _mitadAncho, _mitadAlto) + margen;

            float espaciado = _u * 1.85f;
            float anguloBase = Mathf.Atan2(haciaCamara.z, haciaCamara.x);
            float anguloPaso = espaciado / Mathf.Max(distanciaAncla, 0.01f);

            // El número de puestos sigue el perímetro frontal real, así que un
            // tablero grande tiene más base militar en vez de los mismos tres.
            // Antes el multiplicador (0.16) dejaba huecos vacíos en los flancos
            // de tableros medianos; con 0.24 y un tope más alto se llena mejor
            // la franja lateral que antes quedaba solo con árboles.
            int puestosPorFlanco = Mathf.Clamp(Mathf.RoundToInt((ancho + alto) * 0.24f), 2, 10);

            for (int lado = 0; lado < 2; lado++)
            {
                float signo = lado == 0 ? 1f : -1f;
                for (int i = 0; i < puestosPorFlanco; i++)
                {
                    float angulo = anguloBase + signo * (i + 0.6f) * anguloPaso;
                    var direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
                    float distancia = DistanciaCentroABorde(direccion, _mitadAncho, _mitadAlto) + margen;
                    posicionesCampamentos.Add(_centroTablero + direccion * distancia);
                }
            }
        }

        private void ConstruirCampamentos()
        {
            contenedorCampamentos = new GameObject("CampamentosMilitares").transform;
            contenedorCampamentos.SetParent(transform, false);

            // Se resetea aquí (no dentro de CrearCampamento) porque el objetivo
            // es que NINGÚN puesto repita el tipo de edificio de su vecino
            // inmediato en la lista, no solo dentro de sí mismo.
            _ultimoIndicePrefabEdificio = -1;

            for (int i = 0; i < posicionesCampamentos.Count; i++)
                CrearCampamento(posicionesCampamentos[i], i + 1);
        }

        // Índice del prefab de edificio usado en el puesto anterior, para que
        // ConstruirCampamentos() pueda pedirle al siguiente que no se repita.
        private int _ultimoIndicePrefabEdificio = -1;

        private void CrearCampamento(Vector3 centro, int indice)
        {
            var campamento = new GameObject($"Campamento_{indice}").transform;
            campamento.SetParent(contenedorCampamentos, false);
            campamento.localPosition = new Vector3(centro.x, AlturaSueloMundo(centro.x, centro.z), centro.z);

            var haciaCentro = _centroTablero - centro;
            haciaCentro.y = 0f;
            if (haciaCentro.sqrMagnitude > 0.0001f)
                campamento.localRotation = Quaternion.LookRotation(haciaCentro.normalized, Vector3.up);

            // Si el usuario asignó prefabs propios (por ejemplo los de un asset
            // como ithappy Military_Free), se usan esos en vez de las primitivas
            // generadas por código: se elige uno por puesto (y, si
            // 'edificiosPorPuesto' es mayor a 1, se agrupan varios alrededor del
            // mismo punto para formar un mini-campamento con piezas reales).
            if (prefabsEdificios != null && prefabsEdificios.Length > 0)
            {
                campamento.localScale = Vector3.one; // la escala la trae cada prefab
                var rngPuesto = new System.Random(indice * 7919 + 13);

                for (int e = 0; e < edificiosPorPuesto; e++)
                {
                    int indicePrefab;
                    if (e == 0 && prefabsEdificios.Length > 1)
                    {
                        // "Salteados, no consecutivos": si hay más de un tipo
                        // disponible, se descarta el mismo índice que usó el
                        // puesto inmediatamente anterior antes de sortear.
                        do { indicePrefab = rngPuesto.Next(prefabsEdificios.Length); }
                        while (indicePrefab == _ultimoIndicePrefabEdificio);
                        _ultimoIndicePrefabEdificio = indicePrefab;
                    }
                    else
                    {
                        indicePrefab = rngPuesto.Next(prefabsEdificios.Length);
                    }

                    var prefab = prefabsEdificios[indicePrefab];
                    if (prefab == null) continue;

                    var instancia = Instantiate(prefab, campamento);

                    // Las torres suelen venir modeladas mucho más altas que el
                    // resto de piezas del set; se detectan por nombre y reciben
                    // una escala extra multiplicativa aparte, sin tocar el
                    // tamaño del resto de edificios.
                    bool esTorre = prefab.name.IndexOf("Tower", System.StringComparison.OrdinalIgnoreCase) >= 0
                                   || prefab.name.IndexOf("Torre", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    float escalaFinal = escalaPrefabsEdificios * _escalaObjetos * (esTorre ? escalaExtraTorres : 1f);
                    instancia.transform.localScale = Vector3.one * escalaFinal;

                    // El primero queda centrado en el punto del puesto; el resto
                    // se reparte alrededor para no superponerse.
                    if (e == 0)
                    {
                        instancia.transform.localPosition = Vector3.zero;
                    }
                    else
                    {
                        float angulo = (float)rngPuesto.NextDouble() * Mathf.PI * 2f;
                        float distancia = Mathf.Lerp(1.2f, 2.2f, (float)rngPuesto.NextDouble()) * _escalaObjetos;
                        instancia.transform.localPosition =
                            new Vector3(Mathf.Cos(angulo) * distancia, 0f, Mathf.Sin(angulo) * distancia);
                    }
                    instancia.transform.localRotation = Quaternion.Euler(0f, (float)rngPuesto.NextDouble() * 360f, 0f);
                }
                return;
            }

            CrearCampamentoProcedural(campamento, indice);
        }

        // Versión anterior, íntegra: primitivas generadas por código. Se usa
        // como respaldo cuando no se asignan prefabs propios en el Inspector.
        private void CrearCampamentoProcedural(Transform campamento, int indice)
        {
            campamento.localScale = Vector3.one * 0.72f * _escalaObjetos;

            Color lona = indice % 3 == 1 ? new Color(0.22f, 0.32f, 0.16f) : new Color(0.33f, 0.25f, 0.14f);
            Color madera = new Color(0.22f, 0.15f, 0.08f);
            Color saco = new Color(0.42f, 0.36f, 0.25f);

            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(-0.55f, 0.18f, 0.1f), new Vector3(1.3f, 0.35f, 0.8f), lona);
            CrearPieza(campamento, PrimitiveType.Capsule, new Vector3(0.55f, 0.55f, 0.1f), new Vector3(0.18f, 0.55f, 0.18f), madera);
            CrearPieza(campamento, PrimitiveType.Cylinder, new Vector3(0.55f, 1.05f, 0.1f), new Vector3(0.08f, 0.45f, 0.08f), madera);

            // Tiendas secundarias, almacenes y suministros.
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(-0.25f, 0.14f, 0.95f), new Vector3(0.8f, 0.28f, 0.55f), lona);
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(0.95f, 0.16f, 0.75f), new Vector3(0.55f, 0.32f, 0.55f), madera);
            for (int i = 0; i < 4; i++)
                CrearPieza(campamento, PrimitiveType.Cube,
                    new Vector3(0.75f + (i % 2) * 0.26f, 0.12f + (i / 2) * 0.2f, -0.6f),
                    new Vector3(0.23f, 0.2f, 0.23f), madera);
            for (int i = 0; i < 3; i++)
                CrearPieza(campamento, PrimitiveType.Cylinder, new Vector3(-0.85f + i * 0.28f, 0.16f, 0.72f),
                    new Vector3(0.13f, 0.16f, 0.13f), saco);

            for (int i = 0; i < 5; i++)
                CrearPieza(campamento, PrimitiveType.Capsule, new Vector3(-0.8f + i * 0.35f, 0.12f, -0.55f),
                    new Vector3(0.2f, 0.12f, 0.2f), saco);

            // Techo triangular sobre la tienda principal (dos tapas inclinadas).
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(-0.55f, 0.4f, 0.1f),
                new Vector3(1.15f, 0.28f, 0.7f), lona, Quaternion.Euler(0f, 0f, 20f));
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(-0.55f, 0.4f, 0.1f),
                new Vector3(1.15f, 0.28f, 0.7f), lona, Quaternion.Euler(0f, 0f, -20f));

            // Torre de vigilancia sobre pilotes.
            float torreX = 1.5f;
            for (int i = 0; i < 4; i++)
                CrearPieza(campamento, PrimitiveType.Cylinder,
                    new Vector3(torreX + ((i % 2) - 0.5f) * 0.5f, 0.55f, 0.1f + ((i / 2) - 0.5f) * 0.5f),
                    new Vector3(0.06f, 0.55f, 0.06f), madera);
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(torreX, 1.12f, 0.1f), new Vector3(0.85f, 0.08f, 0.85f), madera);
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(torreX, 1.4f, 0.1f), new Vector3(0.65f, 0.5f, 0.05f), lona);
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(torreX, 1.68f, 0.1f), new Vector3(0.75f, 0.06f, 0.75f),
                madera, Quaternion.Euler(15f, 0f, 0f));

            // Mástil con bandera.
            CrearPieza(campamento, PrimitiveType.Cylinder, new Vector3(0f, 1.0f, -1.1f), new Vector3(0.04f, 1.0f, 0.04f), madera);
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(0.28f, 1.75f, -1.1f), new Vector3(0.5f, 0.3f, 0.02f), lona);

            // Cerca perimetral baja de estacas.
            for (int i = 0; i < 10; i++)
            {
                float angulo = i / 10f * Mathf.PI * 1.3f + Mathf.PI * 0.15f;
                CrearPieza(campamento, PrimitiveType.Cylinder,
                    new Vector3(Mathf.Cos(angulo) * 1.9f, 0.18f, Mathf.Sin(angulo) * 1.9f - 0.3f),
                    new Vector3(0.04f, 0.18f, 0.04f), madera);
            }
        }

        private static void CrearPieza(Transform padre, PrimitiveType tipo, Vector3 posicion,
            Vector3 escala, Color color, Quaternion? rotacionLocal = null)
        {
            var pieza = GameObject.CreatePrimitive(tipo);
            pieza.transform.SetParent(padre, false);
            pieza.transform.localPosition = posicion;
            pieza.transform.localRotation = rotacionLocal ?? Quaternion.identity;
            pieza.transform.localScale = escala;
            var renderer = pieza.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                AjustarBrillo(renderer.material, 0.05f);
            }
            var collider = pieza.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private static void PintarYLimpiar(GameObject objetivo, Color color)
        {
            var renderer = objetivo.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
            var collider = objetivo.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        // La niebla arranca más allá del paisaje cercano, así que solo lava la
        // sierra del fondo y no el campo de juego.
        private void AplicarAmbienteBioma(Color colorNiebla)
        {
            float lado = Mathf.Max(_mitadAncho, _mitadAlto) * 2f;

            RenderSettings.fog = nieblaLejana;
            RenderSettings.fogColor = colorNiebla;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Mathf.Max(lado * 1.7f, 26f * _escalaObjetos);
            RenderSettings.fogEndDistance = Mathf.Max(lado * 4.5f, 90f * _escalaObjetos);

            var camara = Camera.main;
            if (camara != null) camara.backgroundColor = colorNiebla;
        }

        // ---------------------------------------------------------------------
        // TANQUES
        // ---------------------------------------------------------------------
        // Versión original: sigue funcionando exactamente igual que antes (así
        // el código que ya la llama no se rompe). Internamente delega en la
        // versión nueva asumiendo vida al 100%.
        public void ActualizarTanques(IEnumerable<(int playerId, Vector2Int posicion, bool vivo, TanqueSkinDatos skin)> tanques)
        {
            var conVida = new List<(int, Vector2Int, bool, int, TanqueSkinDatos)>();
            foreach (var t in tanques) conVida.Add((t.playerId, t.posicion, t.vivo, 100, t.skin));
            ActualizarTanques(conVida);
        }

        // Versión nueva: igual que la anterior, pero además recibe el
        // porcentaje de vida (0-100) de cada tanque para mostrar su estado --
        // dañado a partir de 50% (chapa oscurecida y humo) y completamente
        // incinerado y volcado a 0%. Úsala desde el GameManager pasando la
        // vida real de cada tanque en vez de la sobrecarga de arriba.
        public void ActualizarTanques(
            IEnumerable<(int playerId, Vector2Int posicion, bool vivo, int vidaPorcentaje, TanqueSkinDatos skin)> tanques,
            bool animarMovimiento = true)
        {
            foreach (var (playerId, posicion, vivo, vidaPorcentaje, skin) in tanques)
            {
                if (!tanquesVisuales.TryGetValue(playerId, out var visual))
                {
                    GameObject go;
                    if (tanquePrefab != null)
                    {
                        go = Instantiate(tanquePrefab, contenedorTanques);
                        AplicarSkinAMateriales(go, skin);
                    }
                    else
                    {
                        go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        go.transform.SetParent(contenedorTanques, false);
                        go.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

                        var renderer = go.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            var colorBase = PaletaSkins.ObtenerColorPrincipal(skin.Color);
                            AplicarTexturaPrincipal(renderer.material, PaletaSkins.GenerarTexturaPatron(skin.Patron, colorBase));
                        }

                        // La cápsula de respaldo usa el tamaño de referencia
                        // (0.6 x 1 x 0.6) para el que están pensadas las medidas
                        // fijas de ObtenerBoundsVisual, así que factor sale en 1.
                        CrearEtiquetaFlotante(go.transform, skin, new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 1f, 0.6f)));
                    }

                    go.name = $"Tanque_Jugador{playerId}";
                    visual = go.transform;
                    tanquesVisuales[playerId] = visual;

                    visual.localPosition = CeldaAPosicionMundo(posicion.x, posicion.y);
                    AsentarSobreCelda(visual);
                }

                // Un tanque destruido ya NO se oculta (SetActive(false)): queda su
                // chatarra visible en el tablero (volcada y ennegrecida, ver
                // AplicarEstadoDeDano con vidaPorcentaje 0), igual que ocupa la
                // celda como obstáculo en la lógica de juego.
                visual.gameObject.SetActive(true);
                AplicarEstadoDeDano(visual, vidaPorcentaje);
                if (!vivo) continue; // no se anima más movimiento sobre una chatarra

                var destino = CeldaAPosicionMundo(posicion.x, posicion.y);
                if (Vector3.Distance(new Vector3(visual.localPosition.x, 0f, visual.localPosition.z),
                        new Vector3(destino.x, 0f, destino.z)) < 0.001f)
                    continue;

                if (!animarMovimiento) continue; // lo animará ReproducirRondaSecuencial, en orden

                if (movimientosEnCurso.TryGetValue(playerId, out var enCurso) && enCurso != null)
                    StopCoroutine(enCurso);

                movimientosEnCurso[playerId] = StartCoroutine(MoverTanqueSuave(visual, destino));
            }
        }

        // Oscurece la chapa y agrega humo a partir de 50% de vida, y a 0% deja
        // el tanque volcado y ennegrecido con más humo. Es puramente visual:
        // no toca vida ni lógica de juego. El estado se recuerda por tanque
        // (en el humo/inclinación ya aplicados) para no recrear el efecto en
        // cada frame si la vida no cambió.
        private readonly Dictionary<int, int> _ultimaVidaVisual = new Dictionary<int, int>();

        private void AplicarEstadoDeDano(Transform visual, int vidaPorcentaje)
{
    // Obtiene un código de hash entero único a partir de la estructura EntityId
    int idVisual = visual.GetEntityId().GetHashCode(); 

    if (_ultimaVidaVisual.TryGetValue(idVisual, out int vidaAnterior) && vidaAnterior == vidaPorcentaje)
        return;
    _ultimaVidaVisual[idVisual] = vidaPorcentaje;

    var humoExistente = visual.Find("HumoDano");
    if (humoExistente != null) Destroy(humoExistente.gameObject);

    float dano = 1f - Mathf.Clamp01(vidaPorcentaje / 100f);

    var propBlock = new MaterialPropertyBlock();

    foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
    {
        if (renderer.gameObject.name == "HumoDano") continue;
        // La bandera y el asta NO deben "quemarse"/oscurecerse con el daño
        // del tanque como el resto de la chapa -- son un elemento de
        // identificación (país/equipo) que tiene que seguir siendo legible
        // sin importar la vida del tanque. Antes este mismo bucle las trataba
        // como una placa más: apenas el tanque perdía vida, la bandera se
        // iba tiñendo hacia negro/gris junto con el casco.
        if (renderer.gameObject.name == "Bandera" || renderer.gameObject.name == "AstaBandera") continue;

        renderer.GetPropertyBlock(propBlock);

        Color colorBase = renderer.sharedMaterial.HasProperty("_BaseColor") 
            ? renderer.sharedMaterial.GetColor("_BaseColor") 
            : renderer.sharedMaterial.color;

        var colorQuemado = Color.Lerp(colorBase, new Color(0.05f, 0.05f, 0.05f), dano * 0.85f);

        propBlock.SetColor("_Color", colorQuemado);      
        propBlock.SetColor("_BaseColor", colorQuemado);  

        renderer.SetPropertyBlock(propBlock);
    }

    if (vidaPorcentaje <= 0)
    {
        visual.localRotation *= Quaternion.Euler(0f, 0f, 80f);
        CrearHumoDano(visual, intensidadAlta: true);
    }
    else if (vidaPorcentaje <= 50)
    {
        CrearHumoDano(visual, intensidadAlta: false);
    }
}

        private void CrearHumoDano(Transform visual, bool intensidadAlta)
        {
            var humoGo = new GameObject("HumoDano");
            humoGo.transform.SetParent(visual, false);
            humoGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            var sistema = humoGo.AddComponent<ParticleSystem>();
            var principal = sistema.main;
            principal.startColor = intensidadAlta
                ? new Color(0.1f, 0.1f, 0.1f, 0.85f)
                : new Color(0.35f, 0.35f, 0.35f, 0.5f);
            principal.startSize = intensidadAlta ? 0.35f : 0.22f;
            principal.startSpeed = intensidadAlta ? 1.2f : 0.6f;
            principal.startLifetime = intensidadAlta ? 1.4f : 1.0f;
            principal.simulationSpace = ParticleSystemSimulationSpace.World;

            var emision = sistema.emission;
            emision.rateOverTime = intensidadAlta ? 14f : 6f;

            var forma = sistema.shape;
            forma.shapeType = ParticleSystemShapeType.Cone;
            forma.angle = 12f;
            forma.radius = 0.1f;

            var renderer = humoGo.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
                renderer.material = new Material(ObtenerShaderEstandar());
        }

        private void AplicarSkinAMateriales(GameObject raiz, TanqueSkinDatos skin)
        {
            var colorBase = PaletaSkins.ObtenerColorPrincipal(skin.Color);
            var textura = PaletaSkins.GenerarTexturaPatron(skin.Patron, colorBase);

            foreach (var renderer in raiz.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    AplicarTexturaPrincipal(material, textura);
                }
            }

            // El asta/bandera y el número se dimensionaban antes con números fijos
            // pensados para un tanque de ~1 unidad de alto. El modelo real
            // (tanquePrefab, ej. el pack Military_Free) puede ser bastante más
            // grande o más chico que eso, así que con tamaño fijo terminaban
            // invisibles o enterrados dentro del propio modelo. Ahora se calculan
            // a partir del tamaño real del modelo, medido en su propio espacio
            // LOCAL (ver ObtenerBoundsLocal) -- si se midiera en espacio mundo
            // (Renderer.bounds tal cual) y el contenedor tuviera alguna escala,
            // las unidades no coincidirían con las de localPosition/localScale de
            // abajo y el asta terminaría mal ubicada de nuevo.
            var bounds = ObtenerBoundsLocal(raiz);
            CrearEtiquetaFlotante(raiz.transform, skin, bounds);
        }

        // Calcula los bounds del modelo en el espacio LOCAL de "raiz" (no en
        // espacio mundo), combinando los bounds de cada malla ya transformados
        // por su matriz local respecto a raiz. Así el resultado se puede usar
        // directamente para posicionar/escalar objetos hijos de raiz.transform,
        // sin importar la escala que tenga raiz o sus padres en la jerarquía.
        private Bounds ObtenerBoundsLocal(GameObject raiz)
        {
            var filtros = raiz.GetComponentsInChildren<MeshFilter>();
            bool huboAlguno = false;
            Bounds resultado = new Bounds(Vector3.zero, Vector3.zero);

            foreach (var filtro in filtros)
            {
                if (filtro.sharedMesh == null) continue;
                var mb = filtro.sharedMesh.bounds;
                Vector3 c = mb.center, e = mb.extents;

                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 esquinaMundo = filtro.transform.TransformPoint(c + new Vector3(sx * e.x, sy * e.y, sz * e.z));
                    Vector3 esquinaLocal = raiz.transform.InverseTransformPoint(esquinaMundo);
                    if (!huboAlguno) { resultado = new Bounds(esquinaLocal, Vector3.zero); huboAlguno = true; }
                    else resultado.Encapsulate(esquinaLocal);
                }
            }

            return huboAlguno ? resultado : new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 1f, 0.6f));
        }

        private void CrearEtiquetaFlotante(Transform padreTanque, TanqueSkinDatos skin, Bounds bounds)
        {
            // "factor" reescala todas las medidas fijas de abajo (pensadas para un
            // tanque de referencia de 0.6 unidades de ancho) al tamaño real del
            // modelo. "alturaTope" es la altura local (relativa al pivote del
            // tanque) del punto más alto del modelo -- ya no se asume que el
            // pivote está en la base: si estuviera centrado, bounds.max.y ya lo
            // resuelve solo.
            float factor = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) / 0.6f, 0.5f, 6f);
            float alturaTope = bounds.max.y;

            var etiquetaGo = new GameObject("Etiqueta");
            etiquetaGo.transform.SetParent(padreTanque, false);
            etiquetaGo.transform.localPosition = new Vector3(0f, alturaTope + 0.3f * factor, 0f);

            // Número (texto blanco) flotando sobre el tanque, con billboard
            // (MiraSiempreACamara) para quedar siempre legible sin importar
            // cómo gire la cámara (OrbitZoomCamera). Antes tenía una placa
            // oscura de fondo para dar contraste; se quitó a pedido porque
            // se confundía con un intento fallido de bandera.

            // El número ya no se elige como skin: es el número de orden en que se
            // programó el tanque (1, 2, 3...), pintado aquí para poder identificar
            // cada tanque en el tablero durante la partida.
            var textoGo = new GameObject("TextoNumero");
            textoGo.transform.SetParent(etiquetaGo.transform, false);
            textoGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var textMesh = textoGo.AddComponent<TextMesh>();
            textMesh.text = $"{PaletaSkins.ObtenerSimboloCalcomania(skin.Calcomania)} {skin.Numero:D2}";
            textMesh.characterSize = 0.14f * factor;
            textMesh.fontSize = 64;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
            var textoRenderer = textoGo.GetComponent<MeshRenderer>();
            textoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            textoGo.AddComponent<MiraSiempreACamara>();

            CrearAstaConBandera(padreTanque, skin.Bandera, factor, alturaTope);
        }

        // Gira el objeto para que siempre mire hacia la cámara principal (billboard),
        // así la placa del número y la bandera se leen bien sin importar el ángulo
        // desde el que OrbitZoomCamera esté mirando el tablero.
        private class MiraSiempreACamara : MonoBehaviour
        {
            private void LateUpdate()
            {
                var camara = Camera.main;
                if (camara == null) return;
                transform.rotation = Quaternion.LookRotation(transform.position - camara.transform.position);
            }
        }

        // Asta con la bandera real (dibujada por código, ver PaletaSkins) del país
        // elegido para el tanque. La bandera es una tira de malla subdividida cuyos
        // vértices se animan en BanderaOndeante para que "hondee" en el aire.
        // "factor" y "alturaTope" vienen de CrearEtiquetaFlotante y escalan/ubican
        // el asta según el tamaño real del modelo (ver comentario ahí).
        private void CrearAstaConBandera(Transform padreTanque, int indiceBandera, float factor, float alturaTope)
        {
            var astaGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            astaGo.name = "AstaBandera";
            astaGo.transform.SetParent(padreTanque, false);
            astaGo.transform.localPosition = new Vector3(0.32f * factor, alturaTope * 0.55f, 0f);
            astaGo.transform.localScale = new Vector3(0.025f * factor, 0.28f * factor, 0.025f * factor);
            var astaRenderer = astaGo.GetComponent<Renderer>();
            if (astaRenderer != null)
            {
                astaRenderer.material = new Material(ObtenerShaderEstandar()) { color = new Color(0.15f, 0.15f, 0.15f) };
            }
            var astaCollider = astaGo.GetComponent<Collider>();
            if (astaCollider != null) Destroy(astaCollider);

            var banderaGo = new GameObject("Bandera");
            banderaGo.transform.SetParent(astaGo.transform, false);
            // Contrarresta la escala no-uniforme del cilindro padre para que la
            // bandera mantenga su proporción real.
            banderaGo.transform.localScale = new Vector3(1f / (0.025f * factor), 1f / (0.28f * factor), 1f / (0.025f * factor));
            banderaGo.transform.localPosition = new Vector3(0.55f, 0.75f, 0f);

            const int segmentos = 8;
            // El material ya se comprobó correcto (ver [DIAG BANDERA]): el
            // problema no era la textura, era el tamaño. 0.36x0.22 unidades de
            // mundo es más chico que una rueda del tanque -- a la distancia de
            // cámara normal del juego, las franjas de color se promedian
            // visualmente y se leen como un manchón gris en vez de una
            // bandera reconocible. Se agranda ~2.5x para que sea legible.
            const float anchoBandera = 3.6f, altoBandera = 2.2f;
            var malla = new Mesh { name = "MallaBandera" };
            var vertices = new Vector3[(segmentos + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangulos = new int[segmentos * 6];

            for (int i = 0; i <= segmentos; i++)
            {
                float t = i / (float)segmentos;
                vertices[i * 2] = new Vector3(t * anchoBandera, altoBandera * 0.5f, 0f);
                vertices[i * 2 + 1] = new Vector3(t * anchoBandera, -altoBandera * 0.5f, 0f);
                uvs[i * 2] = new Vector2(t, 1f);
                uvs[i * 2 + 1] = new Vector2(t, 0f);
            }
            for (int i = 0; i < segmentos; i++)
            {
                int vi = i * 2, ti = i * 6;
                triangulos[ti] = vi; triangulos[ti + 1] = vi + 1; triangulos[ti + 2] = vi + 2;
                triangulos[ti + 3] = vi + 1; triangulos[ti + 4] = vi + 3; triangulos[ti + 5] = vi + 2;
            }
            malla.vertices = vertices;
            malla.uv = uvs;
            malla.triangles = triangulos;
            malla.RecalculateNormals();
            malla.RecalculateBounds();

            var filtro = banderaGo.AddComponent<MeshFilter>();
            filtro.mesh = malla;
            var banderaRenderer = banderaGo.AddComponent<MeshRenderer>();
            var materialBandera = new Material(ObtenerShaderEstandar());
            AplicarTexturaPrincipal(materialBandera, PaletaSkins.GenerarTexturaBandera(indiceBandera));
            // Cull Off (doble cara): por defecto el shader Lit descarta la
            // cara trasera (Cull Back). La bandera es una sola lámina de
            // malla, no un objeto cerrado, así que desde el lado "de atrás"
            // (según hacia dónde termine rotado el tanque) esa cara trasera
            // simplemente no se dibujaba -- se veía lo que hay detrás (la
            // montaña gris/beige del fondo), y de ahí la sensación de
            // "bandera gris". Con Cull Off la textura se ve desde cualquier
            // ángulo.
            if (materialBandera.HasProperty("_Cull"))
                materialBandera.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            banderaRenderer.material = materialBandera;
            banderaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var ondeante = banderaGo.AddComponent<BanderaOndeante>();
            ondeante.Inicializar(malla, vertices);

            // IMPORTANTE: un billboard que solo gira en YAW no sirve con una
            // cámara casi cenital/top-down como la de este juego -- un plano
            // vertical, gires lo que gires en YAW, se sigue viendo de canto
            // (un hilo invisible) si la cámara mira casi derecho hacia abajo,
            // porque el YAW no lo inclina hacia arriba, solo lo rota sobre su
            // propio eje vertical. Por eso antes "no se veía por nada del
            // mundo". Hace falta un billboard COMPLETO (mirar de frente a la
            // cámara en todos los ejes, igual que ya hace el número con
            // MiraSiempreACamara), para que la cara con el dibujo siempre
            // esté de frente sin importar el ángulo de la cámara.
            //
            // Un billboard completo rota en pitch, no solo en yaw. Si esa
            // rotación se aplicara directo sobre "banderaGo" mientras sigue
            // siendo hijo de "astaGo" (que tiene escala NO uniforme:
            // 0.025/0.28/0.025), el resultado se vería deformado (shear) en
            // cuanto la rotación dejara de ser puramente vertical -- girar en
            // pitch dentro de un padre con escala no uniforme distorsiona la
            // malla. Por eso primero se calcula todo (posición/escala) como
            // hijo de "astaGo" -para que el tamaño en mundo salga correcto-
            // y AHORA se reengancha como hijo directo del tanque
            // (worldPositionStays: true conserva su posición/escala actuales
            // tal cual, ya sin depender de la escala del asta), donde sí es
            // seguro rotarlo libremente en cualquier eje sin deformarse.
            banderaGo.transform.SetParent(padreTanque, true);
            banderaGo.AddComponent<MiraSiempreACamara>();
        }

        // Anima, cuadro a cuadro, los vértices de la malla de una bandera con una
        // onda senoidal que crece desde el asta (vértices en x=0, fijos) hacia la
        // punta -- efecto simple de "bandera ondeando al viento", sin física de tela.
        // El desplazamiento se aplica en Y (no en Z, "de profundidad"): la bandera
        // ahora usa billboard completo (MiraSiempreACamara), así que su cara
        // siempre queda perpendicular a la cámara -- un ondeo en profundidad
        // (Z) quedaría alineado con la línea de visión y no se notaría. Ondear
        // en Y sí se ve, porque ese eje queda "dentro" de la cara visible sin
        // importar el ángulo desde el que se mire.
        private class BanderaOndeante : MonoBehaviour
        {
            private Mesh malla;
            private Vector3[] verticesBase;
            private Vector3[] verticesAnimados;
            private float faseAleatoria;

            public void Inicializar(Mesh mallaObjetivo, Vector3[] verticesOriginales)
            {
                malla = mallaObjetivo;
                verticesBase = verticesOriginales;
                verticesAnimados = new Vector3[verticesOriginales.Length];
                faseAleatoria = Random.Range(0f, Mathf.PI * 2f);
            }

            private void Update()
            {
                if (malla == null || verticesBase == null) return;

                float t = Time.time * 4.5f + faseAleatoria;
                for (int i = 0; i < verticesBase.Length; i++)
                {
                    var v = verticesBase[i];
                    // La amplitud crece con la distancia al asta (v.x) para que el
                    // borde pegado al asta quede quieto y la punta ondee más.
                    float amplitud = v.x * 0.09f;
                    float desplazamiento = Mathf.Sin(t + v.x * 9f) * amplitud;
                    verticesAnimados[i] = new Vector3(v.x, v.y + desplazamiento, 0f);
                }
                malla.vertices = verticesAnimados;
                malla.RecalculateNormals();
            }
        }

        private System.Collections.IEnumerator MoverTanqueSuave(Transform visual, Vector3 destinoLocal)
        {
            Vector3 origen = visual.localPosition;
            Vector3 direccionMovimiento = destinoLocal - origen;

            ReproducirEfecto(sonidoMovimientoTanque);

            // Antes la rotación se fijaba de golpe (LookRotation) en el primer
            // frame del movimiento: un giro instantáneo de cualquier ángulo, por
            // eso se veía brusco al cambiar de dirección. Ahora se gira de forma
            // gradual con RotateTowards a una velocidad angular fija, en
            // paralelo al desplazamiento -- si el giro necesario es grande, el
            // tanque avanza mientras todavía está terminando de orientarse, tal
            // como haría un vehículo real con inercia de giro.
            Quaternion rotacionObjetivo = visual.localRotation;
            if (direccionMovimiento.sqrMagnitude > 0.0001f)
                rotacionObjetivo = Quaternion.LookRotation(direccionMovimiento.normalized, Vector3.up);

            float tiempo = 0f;
            while (tiempo < duracionMovimiento)
            {
                tiempo += Time.deltaTime;
                float t = Mathf.Clamp01(tiempo / duracionMovimiento);
                float tSuave = t * t * (3f - 2f * t);
                visual.localPosition = Vector3.Lerp(origen, destinoLocal, tSuave);
                visual.localRotation = Quaternion.RotateTowards(visual.localRotation, rotacionObjetivo,
                    velocidadGiroTanque * Time.deltaTime);
                AsentarSobreCelda(visual);
                yield return null;
            }

            visual.localPosition = destinoLocal;

            // Si el giro no llegó a completarse durante el desplazamiento (por
            // ejemplo, un giro muy cerrado con velocidad de giro baja), se
            // termina de orientar aparte para no dejarlo "torcido" al llegar.
            float tiempoGiroExtra = 0f;
            while (Quaternion.Angle(visual.localRotation, rotacionObjetivo) > 0.5f && tiempoGiroExtra < 2f)
            {
                tiempoGiroExtra += Time.deltaTime;
                visual.localRotation = Quaternion.RotateTowards(visual.localRotation, rotacionObjetivo,
                    velocidadGiroTanque * Time.deltaTime);
                yield return null;
            }

            AsentarSobreCelda(visual);
        }

        private void AsentarSobreCelda(Transform visual)
        {
            float baseMundoY = ObtenerAlturaInferior(visual);
            float ajuste = superficieCeldaMundoY - baseMundoY;

            if (Mathf.Abs(ajuste) > 0.0001f)
            {
                var pos = visual.position;
                pos.y += ajuste;
                visual.position = pos;
            }
        }

        private float ObtenerAlturaInferior(Transform objetivo)
        {
            var renderers = objetivo.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return objetivo.position.y;

            float minY = float.MaxValue;
            foreach (var r in renderers) minY = Mathf.Min(minY, r.bounds.min.y);
            return minY;
        }

        private float ObtenerAlturaSuperior(Transform objetivo, float alturaMinimaSiNoHayRenderer)
        {
            var renderers = objetivo.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return alturaMinimaSiNoHayRenderer;

            float maxY = float.MinValue;
            foreach (var r in renderers) maxY = Mathf.Max(maxY, r.bounds.max.y);
            return maxY;
        }

        private Vector3 CeldaAPosicionMundo(int x, int y)
        {
            return new Vector3(x * tamanoCelda, 0f, y * tamanoCelda);
        }

        // Convierte una celda de la grilla a un punto de MUNDO real, listo
        // para usar en efectos que no cuelgan de "contenedorTanques" (como
        // el proyectil o la explosión, que se sueltan sobre "transform" para
        // no interferir con las corutinas de movimiento de los tanques).
        //
        // Antes esos efectos usaban CeldaAPosicionMundo(...) directamente
        // como si ya fuera una posición de mundo, pero esa función solo da
        // coordenadas LOCALES relativas a "contenedorTanques" (el mismo
        // truco que usan los tanques, con localPosition). Si "contenedorTanques"
        // no está exactamente en el origen del mundo, o el suelo real no
        // está en Y=0 (el terreno tiene su propia altura, "superficieCeldaMundoY"),
        // el resultado queda desplazado -- que es justo lo que se veía como
        // "el misil sale una casilla abajo del tanque".
        private Vector3 PuntoDeFuegoEnCelda(int x, int y, float alturaSobreCelda)
        {
            var local = CeldaAPosicionMundo(x, y);
            var mundo = contenedorTanques.TransformPoint(new Vector3(local.x, 0f, local.z));
            mundo.y = superficieCeldaMundoY + alturaSobreCelda;
            return mundo;
        }

        // ---------------------------------------------------------------------
        // MINAS: marcador visible en el tablero mientras la mina siga ahí
        // (armada o recién colocada) y se quita solo cuando GridBoard ya no
        // la reporta (porque detonó o porque, en el futuro, se retire por
        // otro motivo).
        // ---------------------------------------------------------------------
        private readonly Dictionary<Vector2Int, GameObject> minasVisuales = new Dictionary<Vector2Int, GameObject>();

        public void ActualizarMinas(IEnumerable<Vector2Int> celdasConMina)
        {
            var vigentes = new HashSet<Vector2Int>(celdasConMina);

            foreach (var celda in vigentes)
            {
                if (minasVisuales.ContainsKey(celda)) continue;

                var raiz = new GameObject("Mina").transform;
                raiz.SetParent(contenedorTanques, true);
                raiz.position = PuntoDeFuegoEnCelda(celda.x, celda.y, 0.03f);

                CrearPieza(raiz, PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(0.22f, 0.02f, 0.22f), new Color(0.12f, 0.12f, 0.1f));
                CrearPieza(raiz, PrimitiveType.Sphere, Vector3.up * 0.03f,
                    new Vector3(0.07f, 0.07f, 0.07f), new Color(0.8f, 0.1f, 0.05f));

                minasVisuales[celda] = raiz.gameObject;
                StartCoroutine(ParpadeoMina(raiz));
            }

            var aQuitar = new List<Vector2Int>();
            foreach (var kv in minasVisuales)
                if (!vigentes.Contains(kv.Key)) aQuitar.Add(kv.Key);

            foreach (var celda in aQuitar)
            {
                if (minasVisuales[celda] != null) Destroy(minasVisuales[celda]);
                minasVisuales.Remove(celda);
            }
        }

        private System.Collections.IEnumerator ParpadeoMina(Transform raiz)
        {
            var luz = raiz.GetChild(1); // la esferita roja
            while (raiz != null)
            {
                float t = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
                luz.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.09f, t);
                yield return null;
            }
        }

        // ---------------------------------------------------------------------
        // DISPAROS: lanzamiento, trayectoria y explosión de AMT/MISIL. Se
        // llama una vez por ronda con todos los disparos que ocurrieron en
        // ella (GameManager pasa turnManager.LastRoundShots).
        //
        // Antes se lanzaba una corutina independiente POR disparo, así que si
        // dos tanques disparaban en la misma ronda, sus misiles volaban a la
        // vez -- se veía como si dispararan "en paralelo". La lógica del
        // juego (TurnManager.ExecuteRound) ya procesa a los tanques uno por
        // uno, en orden; ahora la reproducción visual respeta ese mismo
        // orden: se espera a que termine el giro+disparo+explosión de un
        // tanque antes de arrancar el siguiente.
        // ---------------------------------------------------------------------
        public void ReproducirDisparos(IEnumerable<ShotEvent> disparos)
        {
            StartCoroutine(ReproducirDisparosEnOrden(disparos));
        }

        private System.Collections.IEnumerator ReproducirDisparosEnOrden(IEnumerable<ShotEvent> disparos)
        {
            foreach (var disparo in disparos)
                yield return ReproducirUnDisparo(disparo);
        }

        // --------------------------------------------------------------
        // RONDA COMPLETA EN ORDEN: mueve y dispara tanque por tanque, en el
        // mismo orden en que TurnManager.ExecuteRound() los procesó. Antes,
        // ActualizarTanques() arrancaba una corrutina de movimiento POR
        // tanque en el mismo frame (todas a la vez, en paralelo) y los
        // disparos se reproducían aparte, también en paralelo respecto a
        // los movimientos. Ahora todo pasa por una única corrutina: no
        // arranca el paso del siguiente tanque hasta que el anterior
        // termina de moverse Y de disparar.
        // --------------------------------------------------------------
        public class PasoRonda
        {
            public int PlayerId;
            public bool SeMovio;
            public Vector2Int Destino;
            public ShotEvent Disparo; // null si ese tanque no disparó esta ronda
            public RadarEvent Radar;  // null si ese tanque no usó RADAR esta ronda
        }

        // Espejo visual de Gameplay.DamageEvent: mina, choque o desgaste.
        public class EventoDanoVisual
        {
            public DamageEventType Tipo;
            public Vector2Int Celda;
            public Vector2Int? CeldaB;
            public List<int> TargetIds;
            public Dictionary<int, int> VidaPorcentajeDespuesPorId;
            public HashSet<int> DestruidosIds;
        }

        private bool _rondaEnAnimacion;
        public bool RondaEnAnimacion => _rondaEnAnimacion;

        public void ReproducirRondaSecuencial(List<PasoRonda> pasos, List<EventoDanoVisual> eventos = null)
        {
            StartCoroutine(ReproducirRondaSecuencialCoroutine(pasos, eventos));
        }

        private System.Collections.IEnumerator ReproducirRondaSecuencialCoroutine(
            List<PasoRonda> pasos, List<EventoDanoVisual> eventos)
        {
            _rondaEnAnimacion = true;
            foreach (var paso in pasos)
            {
                if (paso.SeMovio && tanquesVisuales.TryGetValue(paso.PlayerId, out var visual) && visual != null)
                {
                    var destinoMundo = CeldaAPosicionMundo(paso.Destino.x, paso.Destino.y);
                    if (movimientosEnCurso.TryGetValue(paso.PlayerId, out var enCurso) && enCurso != null)
                        StopCoroutine(enCurso);
                    yield return MoverTanqueSuave(visual, destinoMundo);
                }

                if (paso.Disparo != null)
                    yield return ReproducirUnDisparo(paso.Disparo);

                if (paso.Radar != null)
                    yield return ReproducirRadar(paso.Radar);
            }

            if (eventos != null)
                foreach (var evento in eventos)
                    yield return ReproducirEventoDeDano(evento);

            _rondaEnAnimacion = false;
        }

        // Reproduce la detonación de una mina, un choque entre tanques o la
        // sacudida de desgaste, y RECIÉN AHÍ aplica la apariencia dañada de
        // cada tanque afectado -- misma idea que con los disparos: nunca se
        // ve "ya dañado" antes de que el evento pase en pantalla.
        private System.Collections.IEnumerator ReproducirEventoDeDano(EventoDanoVisual evento)
        {
            float alturaExplosion = 0.4f * _escalaObjetos;
            // Choque entre dos tanques: la explosión va justo a mitad de
            // camino entre las dos celdas (en espacio de mundo, no de
            // grilla, para que quede centrada incluso si el tablero no es
            // cuadrado). El resto de los casos (obstáculo, mina, desgaste)
            // no tiene CeldaB y usa la celda única de siempre.
            Vector3 puntoMundo = evento.CeldaB.HasValue
                ? Vector3.Lerp(
                    PuntoDeFuegoEnCelda(evento.Celda.x, evento.Celda.y, alturaExplosion),
                    PuntoDeFuegoEnCelda(evento.CeldaB.Value.x, evento.CeldaB.Value.y, alturaExplosion),
                    0.5f)
                : PuntoDeFuegoEnCelda(evento.Celda.x, evento.Celda.y, alturaExplosion);

            switch (evento.Tipo)
            {
                case DamageEventType.Mina:
                    ReproducirEfecto(sonidoExplosionMina);
                    yield return Explosion(puntoMundo, 0.9f);
                    break;
                case DamageEventType.Choque:
                {
                    // El "rebote" es el mismo golpecito que usa Desgaste
                    // (SacudirTanque), pero aquí se dispara para CADA tanque
                    // involucrado -- uno solo si chocó contra un obstáculo,
                    // los dos si chocaron entre sí -- y en paralelo con la
                    // explosión, para que se sienta como un impacto real y
                    // no como dos animaciones sueltas.
                    var rutinasRebote = new List<Coroutine>();
                    foreach (var id in evento.TargetIds)
                        if (tanquesVisuales.TryGetValue(id, out var visualChoque) && visualChoque != null)
                            rutinasRebote.Add(StartCoroutine(SacudirTanque(visualChoque)));

                    yield return Explosion(puntoMundo, 1.0f);

                    foreach (var rutina in rutinasRebote)
                        yield return rutina;
                    break;
                }
                case DamageEventType.Desgaste:
                    // Sin explosión (no hay proyectil ni detonación real):
                    // una sacudida breve del propio tanque contra el
                    // obstáculo/borde con el que chocó.
                    foreach (var id in evento.TargetIds)
                        if (tanquesVisuales.TryGetValue(id, out var visualSacudida) && visualSacudida != null)
                            yield return SacudirTanque(visualSacudida);
                    break;
            }

            foreach (var id in evento.TargetIds)
            {
                if (!tanquesVisuales.TryGetValue(id, out var visualObjetivo) || visualObjetivo == null) continue;

                if (evento.DestruidosIds.Contains(id))
                    AplicarEstadoDeDano(visualObjetivo, 0); // queda la chatarra, no se oculta
                else if (evento.VidaPorcentajeDespuesPorId.TryGetValue(id, out var vidaDespues))
                    AplicarEstadoDeDano(visualObjetivo, vidaDespues);
            }
        }

        // Pequeño golpe/vibración para el desgaste por estancamiento: no hay
        // explosión ni proyectil, así que un choque brusco corto contra la
        // dirección en la que el tanque venía intentando moverse comunica
        // mejor "chocaste contra algo" que un simple cambio de color.
        private System.Collections.IEnumerator SacudirTanque(Transform visual)
        {
            Vector3 posOriginal = visual.localPosition;
            Vector3 direccionSacudida = visual.forward * (0.12f * _escalaObjetos);

            float duracion = 0.18f;
            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Time.deltaTime;
                float t = tiempo / duracion;
                float onda = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t);
                visual.localPosition = posOriginal + direccionSacudida * onda;
                yield return null;
            }

            visual.localPosition = posOriginal;
        }

        private System.Collections.IEnumerator ReproducirUnDisparo(ShotEvent disparo)
        {
            float alturaCanon = 0.55f * _escalaObjetos;
            // Prioriza la posición REAL del tanque que dispara (si su visual
            // ya existe): así el fogonazo sale exactamente del cañón que se
            // ve en pantalla, incluso si ese tanque está a mitad de un
            // desplazamiento animado en este mismo instante.
            Vector3 origen = tanquesVisuales.TryGetValue(disparo.ShooterId, out var visualOrigen) && visualOrigen != null
                ? visualOrigen.position + Vector3.up * alturaCanon
                : PuntoDeFuegoEnCelda(disparo.Origin.x, disparo.Origin.y, alturaCanon);

            Vector3 destino = PuntoDeFuegoEnCelda(disparo.ImpactCell.x, disparo.ImpactCell.y, alturaCanon);

            // El tanque gira para encarar hacia donde va a disparar ANTES de
            // que salga el fogonazo. Antes, disparar sin moverse dejaba al
            // tanque mirando para donde quedó la última vez que se desplazó
            // (la rotación solo se actualizaba en MoverTanqueSuave, que solo
            // corre cuando cambia de casilla) aunque tank.Facing internamente
            // ya apuntara hacia el disparo.
            if (visualOrigen != null)
                yield return GirarTanqueHaciaDisparo(visualOrigen, destino);

            // Fogonazo de boca de cañón: una esfera que se infla y se apaga
            // rápido, justo donde está el tanque que dispara.
            yield return DestelloFogonazo(origen);

            // Proyectil. El MISIL vuela como un cohete: una sola cápsula
            // grande y lenta con estela. El AMT ya NO usa el mismo modelo a
            // menor escala (antes se veía como "un mini misil"); dispara una
            // ráfaga real de varias balas pequeñas y rápidas en sucesión,
            // como una ametralladora.
            if (disparo.EsMisil)
            {
                yield return VueloDeMisil(origen, destino);
                ReproducirEfecto(sonidoImpactoMisil);
                yield return Explosion(destino, 1.3f);
            }
            else
            {
                // El AMT no termina en una explosión grande: cada bala ya
                // deja su propia chispa de impacto (ver VolarBala). Poner
                // además la Explosion() grande aquí es lo que hacía que la
                // ráfaga se confundiera visualmente con el misil.
                yield return RafagaDeAmt(origen, destino);
            }

            // Recién ahora, con la explosión ya en pantalla, se actualiza la
            // apariencia del tanque golpeado (chapa quemada/humo, o volcado
            // y ocultamiento si quedó destruido). Antes esto se aplicaba de
            // golpe al principio de la ronda -- se veía dañado antes de que
            // el disparo siquiera saliera.
            if (disparo.TargetId != -1 && tanquesVisuales.TryGetValue(disparo.TargetId, out var visualObjetivo)
                && visualObjetivo != null)
            {
                if (disparo.TargetDestruido)
                    AplicarEstadoDeDano(visualObjetivo, 0); // queda la chatarra, no se oculta
                else
                    AplicarEstadoDeDano(visualObjetivo, disparo.TargetVidaPorcentajeDespues);
            }
        }

        // ---------------------------------------------------------------------
        // RADAR: barrido visual en la dirección consultada, desde el tanque
        // hasta el borde del tablero en esa línea, con un sonido de escaneo.
        // No afecta al resultado del RADAR (ya calculado en TurnManager);
        // es puramente la animación de "algo escaneando hacia allá".
        // ---------------------------------------------------------------------
        private System.Collections.IEnumerator ReproducirRadar(RadarEvent radar)
        {
            float alturaHaz = 0.5f * _escalaObjetos;
            Vector3 origen = tanquesVisuales.TryGetValue(radar.ShooterId, out var visualOrigen) && visualOrigen != null
                ? visualOrigen.position + Vector3.up * alturaHaz
                : PuntoDeFuegoEnCelda(radar.Origin.x, radar.Origin.y, alturaHaz);

            var celdaBorde = CeldaHastaElBorde(radar.Origin, radar.Dir);
            Vector3 destino = PuntoDeFuegoEnCelda(celdaBorde.x, celdaBorde.y, alturaHaz);

            // El tanque gira para encarar la dirección del escaneo antes de
            // que salga el haz, igual que con los disparos.
            if (visualOrigen != null)
                yield return GirarTanqueHaciaDisparo(visualOrigen, destino);

            ReproducirEfecto(sonidoRadar);
            yield return EscaneoDeRadar(origen, destino);
        }

        // Última celda DENTRO del tablero en línea recta desde 'origen' hacia
        // 'dir', para que el barrido llegue justo hasta el borde del tablero.
        private Vector2Int CeldaHastaElBorde(Vector2Int origen, Direction dir)
        {
            var offset = dir.ToOffset();
            var celda = origen;
            while (true)
            {
                var siguiente = celda + offset;
                if (siguiente.x < 0 || siguiente.x >= _anchoTablero || siguiente.y < 0 || siguiente.y >= _altoTablero)
                    break;
                celda = siguiente;
            }
            return celda;
        }

        // Haz delgado que "crece" desde el tanque hasta el borde del tablero
        // (el barrido en sí) y luego se desvanece rápido -- como un pulso de
        // radar viajando en la dirección consultada.
        private System.Collections.IEnumerator EscaneoDeRadar(Vector3 origen, Vector3 destino)
        {
            const float duracionBarrido = 0.35f;
            const float duracionDesvanecido = 0.18f;
            float grosor = 0.1f * _escalaObjetos;
            var colorRadar = new Color(0.25f, 1f, 0.55f);

            var direccion = destino - origen;
            float distanciaTotal = direccion.magnitude;
            if (distanciaTotal < 0.0001f) yield break;
            var direccionNormalizada = direccion / distanciaTotal;

            var hazGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hazGo.transform.SetParent(transform, true);
            var colliderHaz = hazGo.GetComponent<Collider>();
            if (colliderHaz != null) Destroy(colliderHaz);
            var rendererHaz = hazGo.GetComponent<Renderer>();
            var materialHaz = new Material(ObtenerShaderEstandar()) { color = colorRadar };
            if (rendererHaz != null) rendererHaz.material = materialHaz;
            hazGo.transform.rotation = Quaternion.LookRotation(direccionNormalizada, Vector3.up);

            // El cubo crece a lo largo de su eje hacia adelante desde 0 hasta
            // la distancia total; como el pivote queda en su centro, hay que
            // reubicarlo cada frame a mitad de su largo actual para que el
            // extremo trasero se quede clavado en el origen (el barrido
            // "avanza" en vez de estirarse desde el medio para los dos lados).
            float tiempo = 0f;
            while (tiempo < duracionBarrido)
            {
                tiempo += Time.deltaTime;
                float t = Mathf.Clamp01(tiempo / duracionBarrido);
                float largoActual = distanciaTotal * t;
                hazGo.transform.localScale = new Vector3(grosor, grosor, largoActual);
                hazGo.transform.position = origen + direccionNormalizada * (largoActual * 0.5f);
                yield return null;
            }

            hazGo.transform.localScale = new Vector3(grosor, grosor, distanciaTotal);
            hazGo.transform.position = origen + direccionNormalizada * (distanciaTotal * 0.5f);

            float tiempoDesvanecido = 0f;
            while (tiempoDesvanecido < duracionDesvanecido)
            {
                tiempoDesvanecido += Time.deltaTime;
                float alfa = 1f - Mathf.Clamp01(tiempoDesvanecido / duracionDesvanecido);
                materialHaz.color = new Color(colorRadar.r, colorRadar.g, colorRadar.b, alfa);
                yield return null;
            }

            Destroy(hazGo);
        }

        // Vuelo del MISIL: una sola cápsula grande, lenta, con estela larga
        // -- como un cohete. Esto es exactamente lo que antes hacía también
        // el AMT (a menor escala); ahora es exclusivo del MISIL.
        private System.Collections.IEnumerator VueloDeMisil(Vector3 origen, Vector3 destino)
        {
            const float duracionVuelo = 0.45f;
            const float grosor = 0.09f;
            var colorProyectil = new Color(1f, 0.55f, 0.1f);

            var proyectilGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            proyectilGo.transform.SetParent(transform, true);
            proyectilGo.transform.localScale = new Vector3(grosor, grosor * 3f, grosor);
            var colliderProyectil = proyectilGo.GetComponent<Collider>();
            if (colliderProyectil != null) Destroy(colliderProyectil);
            var rendererProyectil = proyectilGo.GetComponent<Renderer>();
            if (rendererProyectil != null) rendererProyectil.material.color = colorProyectil;

            var direccionVuelo = (destino - origen);
            var rotacionVuelo = direccionVuelo.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direccionVuelo.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.identity;
            proyectilGo.transform.rotation = rotacionVuelo;

            var rastro = proyectilGo.AddComponent<TrailRenderer>();
            rastro.time = 0.35f;
            rastro.startWidth = grosor * 1.4f;
            rastro.endWidth = 0f;
            rastro.material = new Material(ObtenerShaderEstandar());
            rastro.startColor = colorProyectil;
            rastro.endColor = new Color(colorProyectil.r, colorProyectil.g, colorProyectil.b, 0f);

            float tiempo = 0f;
            while (tiempo < duracionVuelo)
            {
                tiempo += Time.deltaTime;
                float t = Mathf.Clamp01(tiempo / duracionVuelo);
                proyectilGo.transform.position = Vector3.Lerp(origen, destino, t);
                yield return null;
            }

            Destroy(proyectilGo, rastro.time);
        }

        // Ráfaga del AMT: varias balas pequeñas y muy rápidas, disparadas en
        // sucesión (no una sola a la vez), como una ametralladora de
        // verdad -- ya no comparte el modelo "cohete" del MISIL.
        private System.Collections.IEnumerator RafagaDeAmt(Vector3 origen, Vector3 destino)
        {
            const int numeroDeBalas = 5;
            const float duracionPorBala = 0.08f;
            const float espacioEntreBalas = 0.035f;

            ReproducirEfecto(sonidoAmetralladora);

            // Dispersión: sin esto todas las balas viajan exactamente por la
            // misma línea y se ven como una sola raya continua (fácil de
            // confundir con el misil). Cada bala se desvía un poco al azar,
            // en el plano perpendicular a la trayectoria, tanto al salir del
            // cañón como al llegar al blanco -- como una ráfaga real.
            const float dispersionSalida = 0.06f;
            const float dispersionLlegada = 0.16f;

            var direccionVuelo = (destino - origen);
            var direccionNormalizada = direccionVuelo.sqrMagnitude > 0.0001f
                ? direccionVuelo.normalized
                : Vector3.forward;
            var rotacionVuelo = Quaternion.LookRotation(direccionNormalizada, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

            // Base perpendicular a la dirección de disparo (horizontal y
            // vertical relativas a esa dirección), para dispersar las balas
            // en un pequeño cono en vez de en línea recta.
            var lateral = Vector3.Cross(Vector3.up, direccionNormalizada);
            if (lateral.sqrMagnitude < 0.0001f) lateral = Vector3.right;
            lateral.Normalize();
            var vertical = Vector3.Cross(direccionNormalizada, lateral).normalized;

            for (int i = 0; i < numeroDeBalas; i++)
            {
                Vector2 desvioSalida = UnityEngine.Random.insideUnitCircle * dispersionSalida;
                Vector2 desvioLlegada = UnityEngine.Random.insideUnitCircle * dispersionLlegada;

                var origenBala = origen + lateral * desvioSalida.x + vertical * desvioSalida.y;
                var destinoBala = destino + lateral * desvioLlegada.x + vertical * desvioLlegada.y;

                var direccionBala = destinoBala - origenBala;
                var rotacionBala = direccionBala.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(direccionBala.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f)
                    : rotacionVuelo;

                StartCoroutine(VolarBala(origenBala, destinoBala, rotacionBala, duracionPorBala));
                yield return new WaitForSeconds(espacioEntreBalas);
            }

            // Espera a que la última bala termine de volar antes de que
            // ReproducirUnDisparo siga con el siguiente paso.
            yield return new WaitForSeconds(duracionPorBala);
        }

        // Una sola bala de la ráfaga de AMT: mucho más chica y rápida que el
        // MISIL, con una estela cortita y una chispa de impacto pequeña.
        // Cada bala recibe su propio origen/destino, ligeramente distintos
        // entre sí (ver RafagaDeAmt), para que la ráfaga se vea dispersa en
        // vez de como una sola línea recta.
        private System.Collections.IEnumerator VolarBala(Vector3 origen, Vector3 destino, Quaternion rotacion, float duracion)
        {
            const float grosor = 0.028f;
            var colorBala = new Color(1f, 0.9f, 0.4f);

            var balaGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            balaGo.transform.SetParent(transform, true);
            balaGo.transform.localScale = new Vector3(grosor, grosor * 2.2f, grosor);
            var colliderBala = balaGo.GetComponent<Collider>();
            if (colliderBala != null) Destroy(colliderBala);
            var rendererBala = balaGo.GetComponent<Renderer>();
            if (rendererBala != null) rendererBala.material.color = colorBala;
            balaGo.transform.rotation = rotacion;

            var rastro = balaGo.AddComponent<TrailRenderer>();
            rastro.time = 0.08f;
            rastro.startWidth = grosor * 1.2f;
            rastro.endWidth = 0f;
            rastro.material = new Material(ObtenerShaderEstandar());
            rastro.startColor = colorBala;
            rastro.endColor = new Color(colorBala.r, colorBala.g, colorBala.b, 0f);

            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Time.deltaTime;
                float t = Mathf.Clamp01(tiempo / duracion);
                balaGo.transform.position = Vector3.Lerp(origen, destino, t);
                yield return null;
            }

            Destroy(balaGo, rastro.time);

            // Chispa breve de impacto de esta bala en particular.
            var chispaGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chispaGo.transform.SetParent(transform, true);
            chispaGo.transform.position = destino;
            chispaGo.transform.localScale = Vector3.one * (grosor * 4f);
            var colliderChispa = chispaGo.GetComponent<Collider>();
            if (colliderChispa != null) Destroy(colliderChispa);
            var rendererChispa = chispaGo.GetComponent<Renderer>();
            if (rendererChispa != null) rendererChispa.material.color = colorBala;
            Destroy(chispaGo, 0.08f);
        }

        // Gira el tanque visual (con la misma velocidad angular que usa para
        // moverse, "velocidadGiroTanque") hasta encarar el punto de impacto,
        // ANTES de que salga el disparo. Si el tanque ya estaba mirando para
        // ese lado, esto termina casi de inmediato.
        private System.Collections.IEnumerator GirarTanqueHaciaDisparo(Transform visual, Vector3 puntoDeImpacto)
        {
            var direccion = puntoDeImpacto - visual.position;
            direccion.y = 0f;
            if (direccion.sqrMagnitude < 0.0001f) yield break;

            var rotacionObjetivo = Quaternion.LookRotation(direccion.normalized, Vector3.up);

            float tiempoLimite = 0f;
            while (Quaternion.Angle(visual.rotation, rotacionObjetivo) > 0.5f && tiempoLimite < 2f)
            {
                tiempoLimite += Time.deltaTime;
                visual.rotation = Quaternion.RotateTowards(visual.rotation, rotacionObjetivo,
                    velocidadGiroTanque * Time.deltaTime);
                yield return null;
            }

            visual.rotation = rotacionObjetivo;
        }

        private System.Collections.IEnumerator DestelloFogonazo(Vector3 posicion)
        {
            var flashGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flashGo.transform.SetParent(transform, true);
            flashGo.transform.position = posicion;
            var colliderFlash = flashGo.GetComponent<Collider>();
            if (colliderFlash != null) Destroy(colliderFlash);
            var rendererFlash = flashGo.GetComponent<Renderer>();
            if (rendererFlash != null)
            {
                rendererFlash.material.color = new Color(1f, 0.85f, 0.4f);
                AjustarBrillo(rendererFlash.material, 1.5f);
            }

            float duracion = 0.12f;
            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Time.deltaTime;
                float t = tiempo / duracion;
                flashGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.4f, t) * (1f - t * 0.3f);
                yield return null;
            }

            Destroy(flashGo);
        }

        // Explosión: una bola de luz que se infla y se apaga rápido, más una
        // ráfaga de partículas (humo/chispas) que se disuelve sola. Sin
        // impacto en la lógica del juego: puramente decorativo.
        private System.Collections.IEnumerator Explosion(Vector3 posicion, float escala)
        {
            var bolaGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolaGo.transform.SetParent(transform, true);
            bolaGo.transform.position = posicion;
            var colliderBola = bolaGo.GetComponent<Collider>();
            if (colliderBola != null) Destroy(colliderBola);
            var rendererBola = bolaGo.GetComponent<Renderer>();
            if (rendererBola != null)
            {
                rendererBola.material.color = new Color(1f, 0.5f, 0.05f);
                AjustarBrillo(rendererBola.material, 2f);
            }

            var particulasGo = new GameObject("ExplosionParticulas");
            particulasGo.transform.SetParent(transform, true);
            particulasGo.transform.position = posicion;
            var sistema = particulasGo.AddComponent<ParticleSystem>();

            // AddComponent<ParticleSystem>() lo deja reproduciéndose desde
            // ya (playOnAwake=true por defecto), así que cambiar 'duration'
            // o 'loop' en 'main' un momento después -- con el sistema ya en
            // marcha -- no está soportado y tiraba el warning en consola
            // ("Setting the duration while system is still playing"). Se
            // detiene primero, se configura todo, y recién al final se llama
            // Play() una sola vez.
            sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var principal = sistema.main;
            principal.startColor = new Color(0.25f, 0.22f, 0.2f, 0.9f);
            principal.startSize = 0.3f * escala;
            principal.startSpeed = 2.5f * escala;
            principal.startLifetime = 0.6f;
            principal.duration = 0.25f;
            principal.loop = false;
            var emision = sistema.emission;
            emision.rateOverTime = 0f;
            emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(14 * escala)) });
            var forma = sistema.shape;
            forma.shapeType = ParticleSystemShapeType.Sphere;
            forma.radius = 0.1f;
            var rendererParticulas = particulasGo.GetComponent<ParticleSystemRenderer>();
            if (rendererParticulas != null) rendererParticulas.material = new Material(ObtenerShaderEstandar());
            sistema.Play();

            float duracion = 0.35f;
            float tiempo = 0f;
            while (tiempo < duracion)
            {
                tiempo += Time.deltaTime;
                float t = tiempo / duracion;
                bolaGo.transform.localScale = Vector3.one * escala * Mathf.Lerp(0.15f, 1.1f, Mathf.Sqrt(t));
                if (rendererBola != null)
                {
                    var c = rendererBola.material.color;
                    c.a = 1f - t;
                    rendererBola.material.color = c;
                }
                yield return null;
            }

            Destroy(bolaGo);
            Destroy(particulasGo, 1.5f);
        }

        private void Limpiar()
        {
            foreach (var corutina in movimientosEnCurso.Values)
                if (corutina != null) StopCoroutine(corutina);
            movimientosEnCurso.Clear();

            if (rutinaAvion != null) StopCoroutine(rutinaAvion);
            rutinaAvion = null;

            tanquesVisuales.Clear();
            _ultimaVidaVisual.Clear();
            posicionesCampamentos.Clear();
            huellasMontanas.Clear();

            if (contenedorCeldas != null) Destroy(contenedorCeldas.gameObject);
            if (contenedorTanques != null) Destroy(contenedorTanques.gameObject);
            if (contenedorMontanas != null) Destroy(contenedorMontanas.gameObject);
            if (contenedorCampamentos != null) Destroy(contenedorCampamentos.gameObject);
            if (contenedorTerreno != null) Destroy(contenedorTerreno.gameObject);
            if (contenedorAvion != null) Destroy(contenedorAvion.gameObject);
        }

        // ---------------------------------------------------------------------
        // AVIÓN DECORATIVO
        //
        // Cruza el cielo por encima de la sierra siguiendo la diagonal que va
        // desde el flanco cercano a la cámara hacia el flanco opuesto y más
        // alto -- la misma trayectoria que se marcó a mano sobre la captura de
        // pantalla. Es puramente decorativo: sin collider y sin efecto en la
        // lógica del juego.
        // ---------------------------------------------------------------------
        private void ConstruirAvion()
        {
            contenedorAvion = new GameObject("Avion").transform;
            contenedorAvion.SetParent(transform, false);
            contenedorAvion.gameObject.SetActive(false); // se activa solo durante el vuelo

            GameObject avionGo;
            if (prefabAvion != null)
            {
                // Con un prefab propio (por ejemplo un helicóptero) no hace
                // falta construir piezas: se instancia tal cual y se aplica la
                // escala y el ajuste de rotación que el usuario configure en el
                // Inspector, por si el modelo no mira hacia +Z de origen.
                avionGo = Instantiate(prefabAvion);
                avionGo.transform.localScale = Vector3.one * escalaPrefabAvion * _escalaObjetos;
            }
            else
            {
                avionGo = new GameObject("AvionPlaceholder");
                var avionPieza = avionGo.transform;
                avionPieza.localScale = Vector3.one * _escalaObjetos * 1.4f;

                Color pintura = new Color(0.55f, 0.58f, 0.6f);
                Color cabina = new Color(0.15f, 0.18f, 0.2f);

                // El fuselaje va a lo largo del eje LOCAL +Z (morro hacia
                // adelante) y las alas a lo largo de X: VueloAvion() orienta el
                // objeto con Quaternion.LookRotation(dirección), que alinea el
                // eje +Z con la dirección de vuelo.
                CrearPieza(avionPieza, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.18f, 1.1f, 0.18f),
                    pintura, Quaternion.Euler(90f, 0f, 0f));
                CrearPieza(avionPieza, PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.7f),
                    new Vector3(0.22f, 0.22f, 0.22f), cabina);
                CrearPieza(avionPieza, PrimitiveType.Cube, Vector3.zero, new Vector3(2.6f, 0.04f, 0.25f), pintura);
                CrearPieza(avionPieza, PrimitiveType.Cube, new Vector3(0f, 0.25f, -1.0f),
                    new Vector3(0.06f, 0.5f, 0.12f), pintura);
                CrearPieza(avionPieza, PrimitiveType.Cube, new Vector3(0f, 0.05f, -1.0f),
                    new Vector3(0.9f, 0.04f, 0.1f), pintura);
            }

            avionGo.name = "AvionDecorativo";
            avionGo.transform.SetParent(contenedorAvion, false);
        }

        private System.Collections.IEnumerator RutinaAvion()
        {
            var rng = new System.Random(System.Guid.NewGuid().GetHashCode());
            while (true)
            {
                float espera = intervaloAvion * (0.7f + (float)rng.NextDouble() * 0.7f);
                yield return new WaitForSeconds(espera);
                yield return VueloAvion(rng.NextDouble() < 0.5);
            }
        }

        private System.Collections.IEnumerator VueloAvion(bool deIdaYVuelta)
        {
            if (contenedorAvion == null || contenedorAvion.childCount == 0) yield break;
            var avion = contenedorAvion.GetChild(0);

            var haciaCamara = DireccionHaciaCamara();
            var lateral = Vector3.Cross(Vector3.up, haciaCamara).normalized;

            float alcance = (_mitadAncho + _mitadAlto) * 0.5f + 45f * _escalaObjetos;
            float alturaBaja = _alturaMontanaAprox * 0.95f;
            float alturaAlta = _alturaMontanaAprox * 1.7f;

            // Diagonal: de un flanco bajo y cercano al opuesto alto y lejano,
            // igual que la línea marcada en la captura.
            var puntoA = _centroTablero - lateral * alcance * 0.95f - haciaCamara * alcance * 0.25f
                         + Vector3.up * alturaBaja;
            var puntoB = _centroTablero + lateral * alcance * 0.95f + haciaCamara * alcance * 0.55f
                         + Vector3.up * alturaAlta;

            var origen = deIdaYVuelta ? puntoA : puntoB;
            var destino = deIdaYVuelta ? puntoB : puntoA;

            avion.position = origen;
            avion.rotation = Quaternion.LookRotation((destino - origen).normalized, Vector3.up)
                             * Quaternion.Euler(rotacionExtraAvion);
            contenedorAvion.gameObject.SetActive(true);

            float tiempo = 0f;
            while (tiempo < duracionVueloAvion)
            {
                tiempo += Time.deltaTime;
                float t = Mathf.Clamp01(tiempo / duracionVueloAvion);
                avion.position = Vector3.Lerp(origen, destino, t);
                yield return null;
            }

            contenedorAvion.gameObject.SetActive(false);
        }
    }
}