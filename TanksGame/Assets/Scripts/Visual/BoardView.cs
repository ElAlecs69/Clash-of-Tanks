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

        private readonly Dictionary<int, Transform> tanquesVisuales = new Dictionary<int, Transform>();
        private Transform contenedorCeldas;
        private Transform contenedorTanques;

        // Crea (o recrea) la grilla visual con el tamaño indicado.
        public void Construir(int ancho, int alto)
        {
            Limpiar();

            contenedorCeldas = new GameObject("Celdas").transform;
            contenedorCeldas.SetParent(transform, false);

            contenedorTanques = new GameObject("Tanques").transform;
            contenedorTanques.SetParent(transform, false);

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
                    }

                    celda.name = $"Celda_{x}_{y}";
                    celda.transform.localPosition = CeldaAPosicionMundo(x, y);
                }
            }
        }

        // Mueve (o crea si no existen aún) los marcadores visuales de cada tanque
        // a su posición actual en el tablero lógico.
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
                }

                visual.gameObject.SetActive(vivo);
                if (vivo)
                {
                    var destino = CeldaAPosicionMundo(posicion.x, posicion.y);
                    destino.y += 0.4f;
                    visual.localPosition = destino;
                }
            }
        }

        private Vector3 CeldaAPosicionMundo(int x, int y)
        {
            return new Vector3(x * tamanoCelda, 0f, y * tamanoCelda);
        }

        private void Limpiar()
        {
            tanquesVisuales.Clear();
            if (contenedorCeldas != null) Destroy(contenedorCeldas.gameObject);
            if (contenedorTanques != null) Destroy(contenedorTanques.gameObject);
        }
    }
}
