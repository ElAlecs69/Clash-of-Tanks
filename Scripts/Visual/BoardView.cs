using System.Collections.Generic;
using UnityEngine;

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

            // Un poco de aleatoriedad fija (misma semilla) para que la silueta no
            // sea perfectamente uniforme, pero sea igual cada vez que se reconstruye.
            var rng = new System.Random(12345);

            for (int anillo = 0; anillo < anillosDeMontanas; anillo++)
            {
                // Sierra cercana para enmarcar el valle, sin cruzar el lado de cámara.
                float distancia = Mathf.Max(mitadAncho, mitadAlto) + tamanoCelda * (2.5f + anillo * 2f);
                int cantidadEnAnillo = 10 + anillo * 4;

                for (int i = 0; i < cantidadEnAnillo; i++)
                {
                    float angulo = (float)i / cantidadEnAnillo * Mathf.PI * 2f;
                    // Un poco de ruido en el ángulo y la distancia para que no quede
                    // un círculo perfecto.
                    angulo += (float)(rng.NextDouble() - 0.5) * 0.15f;
                    float distanciaConRuido = distancia + (float)(rng.NextDouble() - 0.5) * tamanoCelda * 1.5f;

                    float x = centroX + Mathf.Cos(angulo) * distanciaConRuido;
                    float z = centroZ + Mathf.Sin(angulo) * distanciaConRuido;

                    // Deja libre el sector desde el que mira la cámara: las montañas
                    // quedan a los lados y al fondo, nunca entre ella y el tablero.
                    var camara = Camera.main;
                    if (camara != null)
                    {
                        var haciaCamara = camara.transform.position - new Vector3(centroX, 0f, centroZ);
                        haciaCamara.y = 0f;
                        var direccionMontana = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
                        if (haciaCamara.sqrMagnitude > 0.001f &&
                            Vector3.Dot(direccionMontana, haciaCamara.normalized) > 0.15f)
                            continue;
                    }

                    float altura = Mathf.Lerp(alturaMontanaMinMax.x, alturaMontanaMinMax.y, (float)rng.NextDouble());
                    altura = Mathf.Min(altura, 2.6f);
                    // Las más lejanas (anillos de afuera) más altas, para dar sensación
                    // de profundidad/sierra en vez de una pared pareja.
                    altura *= 1f + anillo * 0.2f;

                    float radioBase = altura * Mathf.Lerp(0.5f, 0.9f, (float)rng.NextDouble());

                    var montana = CrearMontanaIrregular(radioBase, altura, i + anillo * 101);
                    montana.name = $"Montana_{anillo}_{i}";
                    montana.transform.SetParent(contenedorMontanas, false);
                    montana.transform.localPosition = new Vector3(x, -0.25f, z);
                    montana.transform.localRotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 6f, 0f, (float)(rng.NextDouble() - 0.5) * 6f);

                    var rendererMontana = montana.GetComponent<Renderer>();
                    if (rendererMontana != null)
                        rendererMontana.material.color = Color.Lerp(colorBase, colorPico, (float)rng.NextDouble() * 0.6f);

                    // Sin collider: son solo decorado de fondo, no deberían bloquear nada del juego.
                    var collider = montana.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);
                }
            }
        }

        // Suelo continuo fuera del tablero: elimina el vacÃ­o gris y crea un valle
        // de tierra donde se apoyan los campamentos y las formaciones rocosas.
        private void ConstruirTerrenoExterior(int ancho, int alto, Color colorRoca)
        {
            contenedorTerreno = new GameObject("TerrenoExterior").transform;
            contenedorTerreno.SetParent(transform, false);

            float lado = Mathf.Max(ancho, alto) * tamanoCelda;
            var centro = new Vector3((ancho - 1) * tamanoCelda * 0.5f, -0.16f,
                (alto - 1) * tamanoCelda * 0.5f);
            CrearPieza(contenedorTerreno, PrimitiveType.Cube, centro,
                new Vector3(lado + 15f, 0.3f, lado + 15f), new Color(0.31f, 0.27f, 0.20f));

            var rng = new System.Random(778);
            for (int i = 0; i < 26; i++)
            {
                float angulo = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distancia = lado * Mathf.Lerp(0.65f, 1.05f, (float)rng.NextDouble());
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
        }

        // Ambientación simple con lo que ya viene con Unity (niebla + color de fondo
        // de cámara), coherente con el bioma elegido — sin necesitar un skybox nuevo.
        private static GameObject CrearMontanaIrregular(float radio, float altura, int semilla)
        {
            const int lados = 9;
            var rng = new System.Random(semilla);
            // Centro de base + anillo bajo + anillo de crestas + cumbre.
            var vertices = new Vector3[lados * 2 + 2];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < lados; i++)
            {
                float angulo = i * Mathf.PI * 2f / lados;
                float r = radio * Mathf.Lerp(0.72f, 1.18f, (float)rng.NextDouble());
                vertices[i + 1] = new Vector3(Mathf.Cos(angulo) * r, 0f, Mathf.Sin(angulo) * r);

                // El anillo medio se alterna para crear aristas, terrazas y sombras.
                float rMedio = r * Mathf.Lerp(0.38f, 0.68f, (float)rng.NextDouble());
                float alturaMedia = altura * Mathf.Lerp(0.38f, 0.68f, (float)rng.NextDouble());
                vertices[lados + 1 + i] = new Vector3(Mathf.Cos(angulo) * rMedio, alturaMedia,
                    Mathf.Sin(angulo) * rMedio);
            }
            int cima = lados * 2 + 1;
            vertices[cima] = new Vector3(radio * 0.18f, altura, -radio * 0.12f);
            var triangulos = new int[lados * 12];
            for (int i = 0; i < lados; i++)
            {
                int actual = i + 1;
                int siguiente = (i + 1) % lados + 1;
                int medioActual = lados + actual;
                int medioSiguiente = lados + siguiente;
                int indice = i * 12;
                triangulos[indice] = 0; triangulos[indice + 1] = siguiente; triangulos[indice + 2] = actual;
                triangulos[indice + 3] = actual; triangulos[indice + 4] = siguiente; triangulos[indice + 5] = medioActual;
                triangulos[indice + 6] = siguiente; triangulos[indice + 7] = medioSiguiente; triangulos[indice + 8] = medioActual;
                triangulos[indice + 9] = medioActual; triangulos[indice + 10] = medioSiguiente; triangulos[indice + 11] = cima;
            }
            var mesh = new Mesh { name = "MontanaIrregular" };
            mesh.vertices = vertices;
            mesh.triangles = triangulos;
            mesh.RecalculateNormals();
            var montana = new GameObject("Montana");
            montana.AddComponent<MeshFilter>().sharedMesh = mesh;
            montana.AddComponent<MeshRenderer>();
            return montana;
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
            var lateral = Vector3.Cross(Vector3.up, haciaCamara).normalized;
            float borde = Mathf.Max(ancho, alto) * tamanoCelda * 0.58f;

            // Tres puestos por lado forman una base extensa en ambos flancos.
            CrearCampamento(centro + haciaCamara * borde + lateral * borde * 0.55f, 1);
            CrearCampamento(centro + haciaCamara * (borde + 0.8f) + lateral * borde * 1.05f, 2);
            CrearCampamento(centro + haciaCamara * (borde - 0.5f) + lateral * borde * 1.15f, 3);
            CrearCampamento(centro + haciaCamara * borde - lateral * borde * 0.55f, 4);
            CrearCampamento(centro + haciaCamara * (borde + 0.8f) - lateral * borde * 1.05f, 5);
            CrearCampamento(centro + haciaCamara * (borde - 0.5f) - lateral * borde * 1.15f, 6);
        }

        private void CrearCampamento(Vector3 centro, int indice)
        {
            var campamento = new GameObject($"Campamento_{indice}").transform;
            campamento.SetParent(contenedorCampamentos, false);
            campamento.localPosition = centro;

            Color lona = indice == 1 ? new Color(0.22f, 0.32f, 0.16f) : new Color(0.33f, 0.25f, 0.14f);
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
        }

        private static void CrearPieza(Transform padre, PrimitiveType tipo, Vector3 posicion,
            Vector3 escala, Color color)
        {
            var pieza = GameObject.CreatePrimitive(tipo);
            pieza.transform.SetParent(padre, false);
            pieza.transform.localPosition = posicion;
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
        public void ActualizarTanques(IEnumerable<(int playerId, Vector2Int posicion, bool vivo)> tanques)
        {
            foreach (var (playerId, posicion, vivo) in tanques)
            {
                if (!tanquesVisuales.TryGetValue(playerId, out var visual))
                {
                    GameObject go;
                    if (tanquePrefab != null)
                    {
                        go = Instantiate(tanquePrefab, contenedorTanques);
                    }
                    else
                    {
                        go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        go.transform.SetParent(contenedorTanques, false);
                        go.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

                        var renderer = go.GetComponent<Renderer>();
                        if (renderer != null)
                            renderer.material.color = playerId == 1 ? colorJugador1 : colorJugador2;
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
