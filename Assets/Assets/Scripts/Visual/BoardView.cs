using System.Collections.Generic;
using UnityEngine;
using TanksGame.Core;

namespace TanksGame.Visual
{
    // Genera una representación visual simple del tablero (Ancho x Alto) usando primitivas
    // de Unity, y coloca un marcador por cada tanque. Es un placeholder gráfico para probar
    // la lógica de juego antes de reemplazarlo por arte final (ver DESIGN_GUIDE.md).
    //
    // Uso: asigna este componente al campo "vistaTablero" del GameManager.
    // Si dejas celdaPrefab / tanquePrefab vacíos, se generan cubos y cápsulas automáticamente.
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

        [Header("Montañas alrededor del tablero (placeholder de arte)")]
        public bool generarMontanas = true;
        [Tooltip("Cuántos 'anillos' de montañas rodean el tablero.")]
        [Min(1)] public int anillosDeMontanas = 2;
        public Vector2 alturaMontanaMinMax = new Vector2(1.2f, 2.6f);

        // --- Biomas: solo cambia colores/paleta por ahora (sin texturas todavía).
        // El selector de paisaje antes de la partida puede llamar a SetBioma() con
        // uno de estos valores antes de Construir().
        public enum Bioma { Pradera, Nieve, Arenoso, Selvatico }
        public Bioma biomaActual = Bioma.Pradera;

        private readonly Dictionary<int, Transform> tanquesVisuales = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Coroutine> movimientosEnCurso = new Dictionary<int, Coroutine>();
        private Transform contenedorCeldas;
        private Transform contenedorTanques;
        private Transform contenedorMontanas;
        private Transform contenedorCampamentos;
        private Transform contenedorTerreno;

        // Altura mundial (Y) de la superficie de arriba de la celda, calculada a partir
        // de los bounds reales de la primera celda instanciada. Sirve de referencia para
        // saber dónde "apoyar" cada tanque, sin importar el prefab que uses.
        private float superficieCeldaMundoY;
        private bool superficieCalculada;

        // Llamalo ANTES de Construir() (por ejemplo, desde una pantalla de selección
        // de paisaje) para que el tablero y las montañas salgan con esa paleta.
        public void SetBioma(Bioma bioma)
        {
            biomaActual = bioma;
        }

        private (Color celda, Color montanaBase, Color montanaPico, Color niebla) ObtenerPaletaBioma()
        {
            switch (biomaActual)
            {
                case Bioma.Nieve:
                    return (new Color(0.85f, 0.88f, 0.92f), new Color(0.5f, 0.52f, 0.55f), new Color(0.95f, 0.96f, 0.98f), new Color(0.8f, 0.85f, 0.9f));
                case Bioma.Arenoso:
                    return (new Color(0.82f, 0.68f, 0.42f), new Color(0.6f, 0.45f, 0.25f), new Color(0.75f, 0.6f, 0.35f), new Color(0.85f, 0.75f, 0.55f));
                case Bioma.Selvatico:
                    return (new Color(0.25f, 0.45f, 0.2f), new Color(0.2f, 0.3f, 0.15f), new Color(0.35f, 0.4f, 0.2f), new Color(0.5f, 0.6f, 0.5f));
                default: // Pradera
                    return (new Color(0.35f, 0.45f, 0.25f), new Color(0.4f, 0.38f, 0.32f), new Color(0.55f, 0.53f, 0.5f), new Color(0.6f, 0.65f, 0.7f));
            }
        }

        // Crea (o recrea) la grilla visual con el tamaño indicado.
        public void Construir(int ancho, int alto)
        {
            Limpiar();

            var paleta = ObtenerPaletaBioma();

            ConstruirTerrenoExterior(ancho, alto, paleta.montanaBase);

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
                            rendererCelda.material.color = paleta.celda;
                    }

                    celda.name = $"Celda_{x}_{y}";
                    celda.transform.localPosition = CeldaAPosicionMundo(x, y);

                    // Con la primera celda instanciada medimos su altura real (bounds),
                    // en vez de asumir un valor fijo. Así sirve tanto para la primitiva
                    // por defecto como para cualquier prefab de celda que asignes.
                    if (!superficieCalculada)
                    {
                        superficieCeldaMundoY = ObtenerAlturaSuperior(celda.transform, contenedorCeldas.position.y);
                        superficieCalculada = true;
                    }
                }
            }

            if (generarMontanas)
                ConstruirMontanas(ancho, alto, paleta.montanaBase, paleta.montanaPico);

            ConstruirCampamentos(ancho, alto);

            AplicarAmbienteBioma(paleta.niebla);
        }

        // Anillo de "montañas" (conos escalonados, placeholder) rodeando el tablero,
        // como si el campo de batalla fuera un valle dentro de una sierra. Es
        // deliberadamente procedural (primitivas + variación aleatoria de altura),
        // pensado para reemplazarse por terreno esculpido / arte final más adelante.
        private void ConstruirMontanas(int ancho, int alto, Color colorBase, Color colorPico)
        {
            contenedorMontanas = new GameObject("Montañas").transform;
            contenedorMontanas.SetParent(transform, false);

            float mitadAncho = ancho * tamanoCelda / 2f;
            float mitadAlto = alto * tamanoCelda / 2f;
            float centroX = mitadAncho - tamanoCelda / 2f;
            float centroZ = mitadAlto - tamanoCelda / 2f;
            var centro = new Vector3(centroX, 0f, centroZ);

            // Un poco de aleatoriedad fija (misma semilla) para que la silueta no
            // sea perfectamente uniforme, pero sea igual cada vez que se reconstruye.
            var rng = new System.Random(12345);

            // Cuántas montañas caben "cómodamente" alrededor del perímetro real del
            // tablero: escala con (ancho + alto) en vez de un número fijo, así un
            // tablero grande no se queda con una sierra rala ni uno chico saturado.
            int densidadBase = Mathf.Max(10, Mathf.RoundToInt((ancho + alto) * 0.9f));

            for (int anillo = 0; anillo < anillosDeMontanas; anillo++)
            {
                // Separación fija respecto al borde real del tablero (no un círculo
                // basado en el lado mayor), para que en tableros rectangulares la
                // sierra abrace todo el contorno en vez de alejarse en el lado corto.
                float margenAnillo = tamanoCelda * (2.2f + anillo * 2f);
                int cantidadEnAnillo = densidadBase + anillo * 5;

                for (int i = 0; i < cantidadEnAnillo; i++)
                {
                    float angulo = (float)i / cantidadEnAnillo * Mathf.PI * 2f;
                    // Un poco de ruido en el ángulo para que no quede un anillo
                    // perfectamente uniforme.
                    angulo += (float)(rng.NextDouble() - 0.5) * 0.2f;
                    var direccionMontana = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));

                    // Deja libre el sector desde el que mira la cámara: las montañas
                    // quedan a los lados y al fondo, nunca entre ella y el tablero.
                    var camara = Camera.main;
                    if (camara != null)
                    {
                        var haciaCamara = camara.transform.position - centro;
                        haciaCamara.y = 0f;
                        if (haciaCamara.sqrMagnitude > 0.001f &&
                            Vector3.Dot(direccionMontana, haciaCamara.normalized) > 0.15f)
                            continue;
                    }

                    float distanciaBorde = DistanciaCentroABorde(direccionMontana, mitadAncho, mitadAlto);
                    float distanciaConRuido = distanciaBorde + margenAnillo + (float)(rng.NextDouble() - 0.5) * tamanoCelda * 1.5f;

                    float x = centroX + direccionMontana.x * distanciaConRuido;
                    float z = centroZ + direccionMontana.z * distanciaConRuido;

                    float altura = Mathf.Lerp(alturaMontanaMinMax.x, alturaMontanaMinMax.y, (float)rng.NextDouble());
                    altura = Mathf.Min(altura, 2.6f);
                    // Las más lejanas (anillos de afuera) más altas, para dar sensación
                    // de profundidad/sierra en vez de una pared pareja.
                    altura *= 1f + anillo * 0.2f;

                    float radioBase = altura * Mathf.Lerp(0.55f, 0.95f, (float)rng.NextDouble());

                    var picoLocal = new Vector3(x, -0.25f, z);
                    CrearFormacionMontanosa(picoLocal, radioBase, altura, colorBase, colorPico,
                        $"Montana_{anillo}_{i}", i + anillo * 101, rng);
                }
            }
        }

        // Distancia desde el centro del tablero hasta su borde rectangular real,
        // siguiendo la dirección indicada (intersección rayo-caja en 2D). A
        // diferencia de un radio fijo, esto hace que el anillo de montañas (y
        // los campamentos) se ajusten al ancho y al alto del tablero por
        // separado: en un tablero muy angosto no se alejan de más en ese eje.
        private static float DistanciaCentroABorde(Vector3 direccionNormalizada, float mitadAncho, float mitadAlto)
        {
            float porX = Mathf.Abs(direccionNormalizada.x) > 0.0001f ? mitadAncho / Mathf.Abs(direccionNormalizada.x) : float.MaxValue;
            float porZ = Mathf.Abs(direccionNormalizada.z) > 0.0001f ? mitadAlto / Mathf.Abs(direccionNormalizada.z) : float.MaxValue;
            return Mathf.Min(porX, porZ);
        }

        // Una "formación" es un pico principal más 1-2 picos satélite pequeños
        // pegados a su base: rompe el patrón de conos idénticos y equiespaciados
        // dando una silueta de sierra más orgánica, con cumbres nevadas/claras
        // en los puntos más altos.
        private void CrearFormacionMontanosa(Vector3 posicionBase, float radioBase, float altura,
            Color colorBase, Color colorPico, string nombre, int semilla, System.Random rng)
        {
            var grupo = new GameObject(nombre).transform;
            grupo.SetParent(contenedorMontanas, false);
            grupo.localPosition = posicionBase;
            grupo.localRotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 6f, (float)(rng.NextDouble() * 360.0), (float)(rng.NextDouble() - 0.5) * 6f);

            PintarMontana(CrearPicoMontana(radioBase, altura, semilla, Vector3.zero, grupo),
                colorBase, colorPico, altura, alturaMontanaMinMax.y);

            // ~55% de las formaciones ganan un pico satélite más bajo, para que la
            // sierra se vea como un conjunto de cumbres y no una fila de conos.
            if (rng.NextDouble() < 0.55)
            {
                float alturaSat = altura * Mathf.Lerp(0.35f, 0.6f, (float)rng.NextDouble());
                float radioSat = radioBase * Mathf.Lerp(0.45f, 0.7f, (float)rng.NextDouble());
                float anguloSat = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distSat = radioBase * Mathf.Lerp(0.5f, 0.8f, (float)rng.NextDouble());
                var offsetSat = new Vector3(Mathf.Cos(anguloSat) * distSat, 0f, Mathf.Sin(anguloSat) * distSat);

                PintarMontana(CrearPicoMontana(radioSat, alturaSat, semilla + 777, offsetSat, grupo),
                    colorBase, colorPico, alturaSat, alturaMontanaMinMax.y);
            }
        }

        private GameObject CrearPicoMontana(float radio, float altura, int semilla, Vector3 offsetLocal, Transform padre)
        {
            var montana = CrearMontanaIrregular(radio, altura, semilla);
            montana.transform.SetParent(padre, false);
            montana.transform.localPosition = offsetLocal;

            // Sin collider: son solo decorado de fondo, no deberían bloquear nada del juego.
            var collider = montana.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            return montana;
        }

        // Colorea con degradado base->pico y, para las cumbres más altas del
        // rango configurado, agrega un remate claro tipo nieve/roca expuesta
        // (mezclando hacia blanco) para dar sensación de profundidad e hitos
        // visuales distintos en vez de un mismo tono plano en toda la sierra.
        private static void PintarMontana(GameObject montana, Color colorBase, Color colorPico, float altura, float alturaMaxRango)
        {
            var renderer = montana.GetComponent<Renderer>();
            if (renderer == null) return;

            float proporcionAltura = alturaMaxRango > 0f ? Mathf.Clamp01(altura / alturaMaxRango) : 0f;
            Color color = Color.Lerp(colorBase, colorPico, Mathf.Clamp01(proporcionAltura * 0.7f + 0.1f));

            // Solo las cumbres realmente más altas del rango se aclaran, y menos
            // intensamente que antes -- si no, toda la sierra se ve pálida y plana.
            if (proporcionAltura > 0.92f)
                color = Color.Lerp(color, Color.white, (proporcionAltura - 0.92f) / 0.08f * 0.3f);

            renderer.material.color = color;
        }

        // Suelo continuo fuera del tablero: elimina el vacÃ­o gris y crea un valle
        // de tierra donde se apoyan los campamentos y las formaciones rocosas.
        // Suelo con relieve real (ruido) en vez de un cubo plano: queda
        // perfectamente llano justo debajo y alrededor del tablero (para no
        // interferir con las celdas) y se vuelve accidentado -- lomas, hondonadas
        // -- a partir de ahí, como el terreno irregular de una zona de guerra.
        private void ConstruirTerrenoExterior(int ancho, int alto, Color colorRoca)
        {
            contenedorTerreno = new GameObject("TerrenoExterior").transform;
            contenedorTerreno.SetParent(transform, false);

            float mitadAncho = ancho * tamanoCelda / 2f;
            float mitadAlto = alto * tamanoCelda / 2f;
            var centro = new Vector3((ancho - 1) * tamanoCelda * 0.5f, -0.16f,
                (alto - 1) * tamanoCelda * 0.5f);

            float lado = Mathf.Max(ancho, alto) * tamanoCelda;
            float extensionTotal = lado + 34f;

            var sueloGo = new GameObject("SueloAccidentado");
            sueloGo.transform.SetParent(contenedorTerreno, false);
            sueloGo.transform.localPosition = centro;

            // Más subdivisiones en tableros grandes (hasta un tope) para que el
            // relieve no se vea "en bloques" incluso cuando la extensión total
            // del suelo crece mucho.
            int resolucion = Mathf.Clamp(Mathf.RoundToInt(extensionTotal / 1.4f), 26, 70);
            var mesh = CrearMallaTerrenoAccidentado(extensionTotal, resolucion, mitadAncho, mitadAlto, tamanoCelda * 1.4f);
            sueloGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var rendererTerreno = sueloGo.AddComponent<MeshRenderer>();
            rendererTerreno.material = new Material(ObtenerShaderEstandar());
            rendererTerreno.material.color = new Color(0.31f, 0.27f, 0.20f);

            var rng = new System.Random(778);

            // Cantidad de rocas, cráteres y trincheras escalada con el perímetro
            // del tablero: en un tablero de 20x20 hay mucho más terreno alrededor
            // que llenar que en uno de 8x8, así que la densidad de decorado
            // acompaña ese crecimiento en vez de quedarse en un número fijo.
            int cantidadRocas = Mathf.Clamp(Mathf.RoundToInt((ancho + alto) * 1.7f), 26, 110);
            for (int i = 0; i < cantidadRocas; i++)
            {
                float angulo = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distancia = lado * Mathf.Lerp(0.62f, 1.15f, (float)rng.NextDouble());
                var posicion = centro + new Vector3(Mathf.Cos(angulo), 0.02f, Mathf.Sin(angulo)) * distancia;
                var roca = CrearMontanaIrregular(Mathf.Lerp(0.12f, 0.38f, (float)rng.NextDouble()),
                    Mathf.Lerp(0.12f, 0.5f, (float)rng.NextDouble()), i + 500);
                roca.name = "RocaDecorativa";
                roca.transform.SetParent(contenedorTerreno, false);
                roca.transform.localPosition = posicion;
                var renderer = roca.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = Color.Lerp(colorRoca, new Color(0.16f, 0.13f, 0.1f), (float)rng.NextDouble() * 0.5f);
            }

            ConstruirDecoradoBelico(centro, lado, ancho, alto, rng);
        }

        // Cráteres, trincheras y estacas con alambre de espino esparcidos entre el
        // tablero y las montañas -- rellenan el vacío en tableros grandes y
        // refuerzan la sensación de campo de batalla, no solo de valle vacío.
        private void ConstruirDecoradoBelico(Vector3 centro, float lado, int ancho, int alto, System.Random rng)
        {
            var contenedor = new GameObject("DecoradoBelico").transform;
            contenedor.SetParent(contenedorTerreno, false);

            Color tierraQuemada = new Color(0.14f, 0.11f, 0.08f);
            Color maderaVieja = new Color(0.2f, 0.14f, 0.08f);

            int cantidadCrateres = Mathf.Clamp(Mathf.RoundToInt((ancho + alto) * 0.45f), 6, 26);
            for (int i = 0; i < cantidadCrateres; i++)
            {
                float angulo = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distancia = lado * Mathf.Lerp(0.55f, 1.1f, (float)rng.NextDouble());
                var posicion = centro + new Vector3(Mathf.Cos(angulo), 0.015f, Mathf.Sin(angulo)) * distancia;
                float radioCrater = Mathf.Lerp(0.5f, 1.3f, (float)rng.NextDouble());

                // Anillo oscuro (tierra removida) + fondo hundido: un cráter de
                // impacto simple pero reconocible desde arriba.
                CrearPieza(contenedor, PrimitiveType.Cylinder, posicion,
                    new Vector3(radioCrater, 0.02f, radioCrater), tierraQuemada);
                CrearPieza(contenedor, PrimitiveType.Sphere, posicion + Vector3.down * radioCrater * 0.35f,
                    new Vector3(radioCrater * 0.85f, radioCrater * 0.5f, radioCrater * 0.85f), Color.Lerp(tierraQuemada, Color.black, 0.3f));
            }

            int cantidadTrincheras = Mathf.Clamp(Mathf.RoundToInt((ancho + alto) * 0.25f), 3, 12);
            for (int i = 0; i < cantidadTrincheras; i++)
            {
                float angulo = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distancia = lado * Mathf.Lerp(0.6f, 1.0f, (float)rng.NextDouble());
                var posicion = centro + new Vector3(Mathf.Cos(angulo), 0.05f, Mathf.Sin(angulo)) * distancia;
                var trinchera = new GameObject("Trinchera").transform;
                trinchera.SetParent(contenedor, false);
                trinchera.localPosition = posicion;
                trinchera.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                CrearPieza(trinchera, PrimitiveType.Cube, Vector3.zero,
                    new Vector3(2.4f, 0.05f, 0.7f), tierraQuemada);
                // Bordes de sacos de tierra a ambos lados de la zanja.
                for (int lado2 = -1; lado2 <= 1; lado2 += 2)
                {
                    for (int s = 0; s < 4; s++)
                        CrearPieza(trinchera, PrimitiveType.Capsule,
                            new Vector3(-1.0f + s * 0.65f, 0.1f, lado2 * 0.42f),
                            new Vector3(0.28f, 0.14f, 0.28f), new Color(0.4f, 0.34f, 0.24f));
                }
            }

            // Filas de estacas con "alambre" (cilindros finos) marcando el límite
            // del terreno defendido, cerca del borde del tablero.
            int cantidadAlambradas = Mathf.Clamp(Mathf.RoundToInt((ancho + alto) * 0.3f), 4, 16);
            float radioAlambrada = lado * 0.56f;
            for (int i = 0; i < cantidadAlambradas; i++)
            {
                float angulo = (float)i / cantidadAlambradas * Mathf.PI * 2f + (float)(rng.NextDouble() - 0.5) * 0.1f;
                var posicion = centro + new Vector3(Mathf.Cos(angulo), 0.15f, Mathf.Sin(angulo)) * radioAlambrada;
                CrearPieza(contenedor, PrimitiveType.Cylinder, posicion,
                    new Vector3(0.035f, 0.18f, 0.035f), maderaVieja, Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 25f));
            }
        }

        // Genera una grilla subdividida y le aplica ruido Perlin como altura,
        // pero con una "rampa" que la mantiene perfectamente plana (altura 0)
        // dentro del rectángulo del tablero + margen, para no dejar huecos ni
        // protuberancias debajo de las celdas jugables.
        private static Mesh CrearMallaTerrenoAccidentado(float extension, int resolucion,
            float mitadAnchoTablero, float mitadAltoTablero, float margenPlano)
        {
            int verticesPorLado = resolucion + 1;
            var vertices = new Vector3[verticesPorLado * verticesPorLado];
            var uvs = new Vector2[vertices.Length];
            float paso = extension / resolucion;
            float mitadExtension = extension / 2f;

            const float offsetRuidoX = 137.2f;
            const float offsetRuidoZ = 84.9f;

            for (int z = 0; z <= resolucion; z++)
            {
                for (int x = 0; x <= resolucion; x++)
                {
                    float px = -mitadExtension + x * paso;
                    float pz = -mitadExtension + z * paso;

                    float distanciaFueraX = Mathf.Max(0f, Mathf.Abs(px) - (mitadAnchoTablero + margenPlano));
                    float distanciaFueraZ = Mathf.Max(0f, Mathf.Abs(pz) - (mitadAltoTablero + margenPlano));
                    float distanciaFuera = Mathf.Sqrt(distanciaFueraX * distanciaFueraX + distanciaFueraZ * distanciaFueraZ);
                    // Rampa suave de 3 unidades: pasa de plano a accidentado sin
                    // un escalón brusco en el borde.
                    float influencia = Mathf.Clamp01(distanciaFuera / 3f);

                    float ruidoGrueso = Mathf.PerlinNoise((px + offsetRuidoX) * 0.12f, (pz + offsetRuidoZ) * 0.12f) - 0.5f;
                    float ruidoFino = Mathf.PerlinNoise((px + offsetRuidoX) * 0.45f, (pz + offsetRuidoZ) * 0.45f) - 0.5f;
                    float altura = (ruidoGrueso * 1.3f + ruidoFino * 0.35f) * influencia;

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
            mesh.RecalculateBounds();
            return mesh;
        }

        // Ambientación simple con lo que ya viene con Unity (niebla + color de fondo
        // de cámara), coherente con el bioma elegido — sin necesitar un skybox nuevo.
        // Un cono de N lados es la forma más simple que sigue leyéndose como
        // "montaña" desde cualquier ángulo: base circular cerrada (para que no
        // se vea hueca por debajo) y una única cumbre centrada. El ruido es
        // moderado y afecta solo el radio de la base -- versiones anteriores
        // variaban radio, anillo medio y cumbre por separado y de forma
        // descentrada, lo que producía siluetas finas tipo "aleta".
        private static GameObject CrearMontanaIrregular(float radio, float altura, int semilla)
        {
            const int lados = 12;
            var rng = new System.Random(semilla);

            var factores = new float[lados];
            for (int i = 0; i < lados; i++)
                factores[i] = (float)rng.NextDouble();

            // vértices: 0 = centro de la base, 1..lados = anillo de la base,
            // lados+1 = centro de la tapa inferior duplicado (para poder cerrar
            // la base con normales hacia abajo), lados+2 = cumbre.
            var baseAnillo = new Vector3[lados];
            for (int i = 0; i < lados; i++)
            {
                // Promedia con los vecinos para que el contorno de la base sea
                // una silueta suave (una colina real no tiene picos aislados en
                // la base), pero conserva algo de variación entre formaciones.
                float anterior = factores[(i - 1 + lados) % lados];
                float siguiente = factores[(i + 1) % lados];
                float factorSuave = (factores[i] * 2f + anterior + siguiente) / 4f;

                float angulo = i * Mathf.PI * 2f / lados;
                float r = radio * Mathf.Lerp(0.85f, 1.15f, factorSuave);
                baseAnillo[i] = new Vector3(Mathf.Cos(angulo) * r, 0f, Mathf.Sin(angulo) * r);
            }

            var vertices = new Vector3[lados * 2 + 2];
            vertices[0] = Vector3.zero; // Centro base (cara superior de la base, mirando abajo -- tapa).
            for (int i = 0; i < lados; i++)
            {
                vertices[1 + i] = baseAnillo[i];
                vertices[1 + lados + i] = baseAnillo[i]; // Copia para las caras laterales (normales distintas a la tapa).
            }
            int cima = lados * 2 + 1;
            vertices[cima] = new Vector3(0f, altura, 0f); // Cumbre perfectamente centrada.

            var triangulos = new int[lados * 6];
            for (int i = 0; i < lados; i++)
            {
                int siguiente = (i + 1) % lados;

                // Tapa inferior (para que no se vea hueca desde abajo/lados bajos).
                int b0 = 1 + i, b1 = 1 + siguiente;
                int t = i * 6;
                triangulos[t] = 0; triangulos[t + 1] = b0; triangulos[t + 2] = b1;

                // Cara lateral, usando la copia del anillo para no compartir
                // normales con la tapa.
                int l0 = 1 + lados + i, l1 = 1 + lados + siguiente;
                triangulos[t + 3] = l0; triangulos[t + 4] = l1; triangulos[t + 5] = cima;
            }

            var mesh = new Mesh { name = "MontanaCono" };
            mesh.vertices = vertices;
            mesh.triangles = triangulos;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var montana = new GameObject("Montana");
            montana.AddComponent<MeshFilter>().sharedMesh = mesh;
            // IMPORTANTE: a diferencia de GameObject.CreatePrimitive() (que asigna
            // automáticamente el material correcto para el render pipeline
            // activo), un MeshRenderer creado a mano queda sin material -- accede
            // a .material recién cuando alguien lo pide y ahí Unity le pone un
            // material por defecto que bajo URP se ve plano y deslavado (por eso
            // las montañas se veían mal sin importar cuánto se ajustara la forma).
            // Le asignamos el shader Lit de URP explícitamente.
            montana.AddComponent<MeshRenderer>().material = new Material(ObtenerShaderEstandar());
            return montana;
        }

        private static Shader _shaderEstandarCache;

        // Busca el shader Lit de URP una sola vez (con caídas de respaldo por si
        // el proyecto usara Simple Lit o el pipeline integrado). El resultado se
        // cachea porque Shader.Find no es gratis.
        private static Shader ObtenerShaderEstandar()
        {
            if (_shaderEstandarCache != null) return _shaderEstandarCache;

            _shaderEstandarCache = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Diffuse");
            return _shaderEstandarCache;
        }

        // Dos puestos militares decorativos en la entrada del valle, uno por lado.
        // No tienen collider y por tanto no afectan la lÃ³gica del tablero.
        private void ConstruirCampamentos(int ancho, int alto)
        {
            contenedorCampamentos = new GameObject("CampamentosMilitares").transform;
            contenedorCampamentos.SetParent(transform, false);

            var centro = new Vector3((ancho - 1) * tamanoCelda * 0.5f, 0f,
                (alto - 1) * tamanoCelda * 0.5f);
            var haciaCamara = Camera.main != null ? Camera.main.transform.position - centro : new Vector3(-1f, 0f, -1f);
            haciaCamara.y = 0f;
            haciaCamara.Normalize();

            float mitadAncho = ancho * tamanoCelda / 2f;
            float mitadAlto = alto * tamanoCelda / 2f;
            // Espacio libre entre el borde del tablero y la base: fijo en celdas,
            // apenas el necesario para que no se pise con el tablero pero sin
            // alejarse de la frontera.
            float margen = tamanoCelda * 2f;

            float distanciaAncla = DistanciaCentroABorde(haciaCamara, mitadAncho, mitadAlto) + margen;

            // En vez de alinear los 3 puestos en una recta perpendicular a la
            // cámara (eso los hacía "hundirse" hacia el tablero en un extremo y
            // alejarse de más en el otro, porque la esquina real del tablero es
            // angulosa), cada puesto gira un poco más de ángulo respecto al
            // centro y recalcula SU PROPIA distancia al borde. Así el grupo
            // sigue el contorno real del rectángulo, abrazando la esquina de
            // forma pareja.
            float espaciadoDeseado = tamanoCelda * 2.4f;
            float anguloBase = Mathf.Atan2(haciaCamara.z, haciaCamara.x);
            float anguloPaso = espaciadoDeseado / Mathf.Max(distanciaAncla, 0.01f);

            // Más puestos por flanco en tableros grandes: en 8x8 alcanza con 3,
            // pero en 20x20 el borde es mucho más largo y con solo 3 la base se
            // ve como un puntito perdido en medio de tanto espacio vacío.
            int puestosPorFlanco = Mathf.Clamp(Mathf.RoundToInt((ancho + alto) * 0.18f) + 2, 3, 8);

            for (int lado = 0; lado < 2; lado++)
            {
                float signo = lado == 0 ? 1f : -1f;
                for (int i = 0; i < puestosPorFlanco; i++)
                {
                    float angulo = anguloBase + signo * (i + 1) * anguloPaso;
                    var direccion = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
                    float distancia = DistanciaCentroABorde(direccion, mitadAncho, mitadAlto) + margen;
                    var posicion = centro + direccion * distancia;

                    CrearCampamento(posicion, lado * puestosPorFlanco + i + 1, centro);
                }
            }
        }

        private void CrearCampamento(Vector3 centro, int indice, Vector3 centroTablero)
        {
            var campamento = new GameObject($"Campamento_{indice}").transform;
            campamento.SetParent(contenedorCampamentos, false);
            campamento.localPosition = centro;
            // Reduce la huella real del campamento (antes ~3.8 de ancho por la
            // cerca perimetral) para poder agrupar los 3 puestos de cada flanco
            // sin que se toquen y sin tener que alejarlos demasiado del tablero.
            campamento.localScale = Vector3.one * 0.72f;

            // La base mira hacia el centro del tablero, como un puesto avanzado
            // vigilando el campo de batalla, en vez de tener una orientación fija.
            var haciaCentro = centroTablero - centro;
            haciaCentro.y = 0f;
            if (haciaCentro.sqrMagnitude > 0.0001f)
                campamento.localRotation = Quaternion.LookRotation(haciaCentro.normalized, Vector3.up);

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
                CrearPieza(campamento, PrimitiveType.Cube, new Vector3(0.75f + (i % 2) * 0.26f, 0.12f + (i / 2) * 0.2f, -0.6f),
                    new Vector3(0.23f, 0.2f, 0.23f), madera);
            for (int i = 0; i < 3; i++)
                CrearPieza(campamento, PrimitiveType.Cylinder, new Vector3(-0.85f + i * 0.28f, 0.16f, 0.72f),
                    new Vector3(0.13f, 0.16f, 0.13f), saco);

            for (int i = 0; i < 5; i++)
            {
                float x = -0.8f + i * 0.35f;
                CrearPieza(campamento, PrimitiveType.Capsule, new Vector3(x, 0.12f, -0.55f),
                    new Vector3(0.2f, 0.12f, 0.2f), saco);
            }

            // Techo triangular sobre la tienda principal (dos tapas inclinadas)
            // en vez de una caja plana, para que se lea como una carpa de verdad.
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(-0.55f, 0.4f, 0.1f),
                new Vector3(1.15f, 0.28f, 0.7f), lona, Quaternion.Euler(0f, 0f, 20f));
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(-0.55f, 0.4f, 0.1f),
                new Vector3(1.15f, 0.28f, 0.7f), lona, Quaternion.Euler(0f, 0f, -20f));

            // Torre de vigilancia sobre pilotes, con plataforma y techo -- da altura
            // al conjunto y una silueta reconocible desde lejos.
            float torreX = 1.5f;
            for (int i = 0; i < 4; i++)
            {
                float px = torreX + ((i % 2) - 0.5f) * 0.5f;
                float pz = 0.1f + ((i / 2) - 0.5f) * 0.5f;
                CrearPieza(campamento, PrimitiveType.Cylinder, new Vector3(px, 0.55f, pz),
                    new Vector3(0.06f, 0.55f, 0.06f), madera);
            }
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(torreX, 1.12f, 0.1f),
                new Vector3(0.85f, 0.08f, 0.85f), madera);
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(torreX, 1.4f, 0.1f),
                new Vector3(0.65f, 0.5f, 0.05f), lona);
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(torreX, 1.68f, 0.1f),
                new Vector3(0.75f, 0.06f, 0.75f), madera, Quaternion.Euler(15f, 0f, 0f));

            // Mástil con bandera propia (distinta del banderín del tanque) marcando
            // el territorio de la base.
            CrearPieza(campamento, PrimitiveType.Cylinder, new Vector3(0f, 1.0f, -1.1f),
                new Vector3(0.04f, 1.0f, 0.04f), madera);
            CrearPieza(campamento, PrimitiveType.Cube, new Vector3(0.28f, 1.75f, -1.1f),
                new Vector3(0.5f, 0.3f, 0.02f), lona);

            // Cerca perimetral baja de estacas, para delimitar visualmente el
            // puesto sin bloquear la vista del tablero.
            for (int i = 0; i < 10; i++)
            {
                float angulo = i / 10f * Mathf.PI * 1.3f + Mathf.PI * 0.15f;
                float ex = Mathf.Cos(angulo) * 1.9f;
                float ez = Mathf.Sin(angulo) * 1.9f - 0.3f;
                CrearPieza(campamento, PrimitiveType.Cylinder, new Vector3(ex, 0.18f, ez),
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
            if (renderer != null) renderer.material.color = color;
            var collider = pieza.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void AplicarAmbienteBioma(Color colorNiebla)
        {
            RenderSettings.fog = false;
            RenderSettings.fogColor = colorNiebla;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Mathf.Max(tamanoCelda * 6f, 6f);
            RenderSettings.fogEndDistance = Mathf.Max(tamanoCelda * 24f, 24f);

            var camara = Camera.main;
            if (camara != null)
                camara.backgroundColor = colorNiebla;
        }

        // Mueve (o crea si no existen aún) los marcadores visuales de cada tanque
        // a su posición actual en el tablero lógico, con una animación de
        // desplazamiento en vez de saltar instantáneamente (movimiento "realista").
        public void ActualizarTanques(IEnumerable<(int playerId, Vector2Int posicion, bool vivo, TanqueSkinDatos skin)> tanques)
        {
            foreach (var (playerId, posicion, vivo, skin) in tanques)
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
                            // Antes acá se pintaba con un color fijo por jugador
                            // (colorJugador1/colorJugador2, y CUALQUIER jugador 3+
                            // quedaba con colorJugador2) -- ahora usa el
                            // patrón+color que se eligió en la pantalla de
                            // programación, igual que en la vista previa de ahí.
                            var colorBase = PaletaSkins.ObtenerColorPrincipal(skin.Color);
                            renderer.material.mainTexture = PaletaSkins.GenerarTexturaPatron(skin.Patron, colorBase);
                            renderer.material.color = Color.white;
                        }

                        CrearEtiquetaFlotante(go.transform, skin);
                    }

                    go.name = $"Tanque_Jugador{playerId}";
                    visual = go.transform;
                    tanquesVisuales[playerId] = visual;

                    // La primera vez no hay "desde dónde" animar: se coloca directo.
                    visual.localPosition = CeldaAPosicionMundo(posicion.x, posicion.y);
                    AsentarSobreCelda(visual);
                }

                visual.gameObject.SetActive(vivo);
                if (!vivo) continue;

                var destino = CeldaAPosicionMundo(posicion.x, posicion.y);
                if (Vector3.Distance(new Vector3(visual.localPosition.x, 0f, visual.localPosition.z),
                        new Vector3(destino.x, 0f, destino.z)) < 0.001f)
                    continue; // ya está ahí, no hace falta animar.

                if (movimientosEnCurso.TryGetValue(playerId, out var enCurso) && enCurso != null)
                    StopCoroutine(enCurso);

                movimientosEnCurso[playerId] = StartCoroutine(MoverTanqueSuave(visual, destino));
            }
        }

        // Pinta TODOS los materiales del prefab real con el patrón+color elegido
        // (igual que la vista previa 3D de la pantalla de programación) y le agrega
        // la etiqueta flotante con calcomanía/número/bandera.
        private void AplicarSkinAMateriales(GameObject raiz, TanqueSkinDatos skin)
        {
            var colorBase = PaletaSkins.ObtenerColorPrincipal(skin.Color);
            var textura = PaletaSkins.GenerarTexturaPatron(skin.Patron, colorBase);

            foreach (var renderer in raiz.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    material.mainTexture = textura;
                    material.color = Color.white;
                }
            }

            CrearEtiquetaFlotante(raiz.transform, skin);
        }

        // Texto flotante arriba del tanque (calcomanía + número) más un mini
        // "banderín" de color -- así la personalización elegida en la pantalla de
        // programación también se nota en el campo de batalla, no solo en la vista
        // previa de esa pantalla.
        private void CrearEtiquetaFlotante(Transform padreTanque, TanqueSkinDatos skin)
        {
            var etiquetaGo = new GameObject("Etiqueta");
            etiquetaGo.transform.SetParent(padreTanque, false);
            etiquetaGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            var textMesh = etiquetaGo.AddComponent<TextMesh>();
            textMesh.text = $"{PaletaSkins.ObtenerSimboloCalcomania(skin.Calcomania)} {PaletaSkins.ObtenerNumero(skin.Numero)}";
            textMesh.characterSize = 0.15f;
            textMesh.fontSize = 48;
            textMesh.anchor = TextAnchor.LowerCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            var banderaGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banderaGo.transform.SetParent(padreTanque, false);
            banderaGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            banderaGo.transform.localScale = new Vector3(0.18f, 0.18f, 0.03f);
            var banderaRenderer = banderaGo.GetComponent<Renderer>();
            if (banderaRenderer != null) banderaRenderer.material.color = PaletaSkins.ObtenerColorBandera(skin.Bandera);
            var banderaCollider = banderaGo.GetComponent<Collider>();
            if (banderaCollider != null) Destroy(banderaCollider);
        }

        private System.Collections.IEnumerator MoverTanqueSuave(Transform visual, Vector3 destinoLocal)
        {
            Vector3 origen = visual.localPosition;
            Vector3 direccionMovimiento = destinoLocal - origen;

            // Gira el tanque hacia donde se está moviendo (además de desplazarlo),
            // para que el movimiento se sienta menos "flotante".
            if (direccionMovimiento.sqrMagnitude > 0.0001f)
                visual.localRotation = Quaternion.LookRotation(direccionMovimiento.normalized, Vector3.up);

            float tiempo = 0f;
            while (tiempo < duracionMovimiento)
            {
                tiempo += Time.deltaTime;
                float t = Mathf.Clamp01(tiempo / duracionMovimiento);
                // Suavizado (ease-in-out) en vez de velocidad constante.
                float tSuave = t * t * (3f - 2f * t);
                visual.localPosition = Vector3.Lerp(origen, destinoLocal, tSuave);
                AsentarSobreCelda(visual);
                yield return null;
            }

            visual.localPosition = destinoLocal;
            AsentarSobreCelda(visual);
        }

        // Calcula la altura del punto más bajo del modelo (usando los Renderer reales,
        // sin importar dónde esté el pivote) y desplaza el objeto en Y hasta que esa
        // base quede exactamente sobre la superficie de la celda.
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

        // Punto más bajo (mundo) de todos los Renderer del objeto y sus hijos.
        private float ObtenerAlturaInferior(Transform objetivo)
        {
            var renderers = objetivo.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return objetivo.position.y;

            float minY = float.MaxValue;
            foreach (var r in renderers)
                minY = Mathf.Min(minY, r.bounds.min.y);

            return minY;
        }

        // Punto más alto (mundo) de todos los Renderer del objeto y sus hijos.
        // "alturaMinimaSiNoHayRenderer" se usa como respaldo (ej. si el prefab de celda
        // no tiene ningún Renderer, algo inusual pero posible).
        private float ObtenerAlturaSuperior(Transform objetivo, float alturaMinimaSiNoHayRenderer)
        {
            var renderers = objetivo.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return alturaMinimaSiNoHayRenderer;

            float maxY = float.MinValue;
            foreach (var r in renderers)
                maxY = Mathf.Max(maxY, r.bounds.max.y);

            return maxY;
        }

        private Vector3 CeldaAPosicionMundo(int x, int y)
        {
            return new Vector3(x * tamanoCelda, 0f, y * tamanoCelda);
        }

        private void Limpiar()
        {
            foreach (var corutina in movimientosEnCurso.Values)
                if (corutina != null) StopCoroutine(corutina);
            movimientosEnCurso.Clear();

            tanquesVisuales.Clear();
            if (contenedorCeldas != null) Destroy(contenedorCeldas.gameObject);
            if (contenedorTanques != null) Destroy(contenedorTanques.gameObject);
            if (contenedorMontanas != null) Destroy(contenedorMontanas.gameObject);
            if (contenedorCampamentos != null) Destroy(contenedorCampamentos.gameObject);
            if (contenedorTerreno != null) Destroy(contenedorTerreno.gameObject);
        }
    }
}