# Juego de Tanques Programables — Base de proyecto Unity

Este paquete contiene la **lógica de juego completa** (tablero, tanques, mini-lenguaje de
programación, combate y turnos) descrita en tu documento de diseño, lista para pegarse en
un proyecto de Unity. La parte visual (modelos 3D, UI final) queda como siguiente paso —
ver `DESIGN_GUIDE.md` para la dirección de arte sugerida (inspirada en el estilo que
mandaste, sin copiar assets de Clash of Clans, que son propiedad de Supercell).

## 1. Instalación
1. Crea un proyecto nuevo en Unity (recomendado: **Unity 2021 LTS o superior**, plantilla 3D
   o 3D URP). El código usa `switch` expressions de C# 8, soportado desde Unity 2020.2+.
2. Copia la carpeta `Assets/Scripts` de este paquete dentro del `Assets` de tu proyecto
   (puedes arrastrarla completa en el panel Project de Unity).
3. Crea un GameObject vacío llamado `GameManager` y agrégale el componente
   `TanksGame.Gameplay.GameManager`.
4. Crea un GameObject `MainCamera` con el componente
   `TanksGame.CameraControl.OrbitZoomCamera` (o agrégalo a tu cámara existente).
5. Dale Play. En la consola verás el log de cada ronda si llamas a
   `EjecutarSiguienteTurno()` desde un botón de UI (o desde el Inspector con un evento).

## 2. Estructura del código
```
Assets/Scripts/
  Core/
    Direction.cs        -> enum N/S/E/O + offset en el tablero
    BoardCell.cs         -> tipos de celda: Free, Obstacle, Mine, Hospital
    GridBoard.cs         -> tablero 8x8, colocación/consumo de minas, obstáculos
    Tank.cs              -> estado de un tanque (vida, misiles, escudo, posición)
  Language/
    Instruction.cs       -> MOV, AMT, MINA, MISIL, RADAR, ESCUDO, ESPERAR, REPARAR
    Condition.cs         -> condiciones IF (VIDA, MISILES, RADAR + operador + valor)
    ProgramStep.cs        -> una línea de programa (instrucción simple o IF+instrucción)
    TankProgramParser.cs -> convierte texto del mini-lenguaje en una lista de ProgramStep
  Gameplay/
    TankAgent.cs          -> un Tank + su programa + puntero de ejecución
    CombatResolver.cs     -> raycasting en el grid para AMT / MISIL / RADAR
    TurnManager.cs        -> ejecuta una ronda completa (orden de la sección 22 del doc)
    GameManager.cs        -> ejemplo de wiring: crea tablero + 2 tanques + corre turnos
  CameraControl/
    OrbitZoomCamera.cs    -> cámara isométrica con órbita libre (botón derecho) y zoom (scroll)
```

## 3. Sintaxis del mini-lenguaje soportada
```
MOV(N)      MOV(S)      MOV(E)      MOV(O)
AMT(N)      AMT(S)      AMT(E)      AMT(O)
RADAR(N)    RADAR(S)    RADAR(E)    RADAR(O)
MINA
MISIL
ESCUDO
ESPERAR
REPARAR

IF VIDA < 40
REPARAR

IF RADAR(N) > 0
MISIL

IF MISILES > 0
AMT(N)
```
Operadores soportados en el IF: `>`, `<`, `>=`, `<=`, `==`.
Cada IF debe ir seguido, en la siguiente línea, de la instrucción que se ejecuta si la
condición es verdadera. Si es falsa, el tanque espera ese turno (sigue avanzando el
programa en la siguiente ronda).

## 4. Decisiones de diseño e implementación (léelo antes de extender el sistema)
- **El programa se repite en bucle**: al llegar a la última línea, `TankAgent` vuelve a la
  primera. Si prefieres que el tanque se detenga (ESPERAR indefinido) al terminar el
  programa, es un cambio de una línea en `TankAgent.GetNextStep()`.
- **Orden de ejecución por ronda**: se procesa un jugador a la vez, en el orden en que
  aparecen en la lista `Agents`. Esto significa que dentro de la misma ronda un jugador
  puede reaccionar a algo que el otro ya hizo (por ejemplo, moverse antes de que el
  segundo dispare). Si quieres turnos verdaderamente simultáneos, hay que separar
  "recolectar instrucciones" de "aplicar efectos" en dos fases.
- **Escudo**: protege solo durante la ronda en la que se activa (se resetea al inicio de
  cada ronda para todos los tanques). No protege retroactivamente contra daño ya aplicado
  antes en la misma ronda.
- **Colisión (DAÑAR)**: se revisa una sola vez al final de la ronda, después de que todos
  los tanques ya se movieron.
- **Tanque destruido → obstáculo**: se aplica automáticamente vía `Board.SetObstacle()`
  en `TurnManager.HandleIfDestroyed()`, tal como pide la sección 4 del documento.

## 5. Tablero configurable (NxM) y tablero visual
El tablero ya no está fijo en 8x8: `GridBoard` recibe `width` y `height` en su constructor,
y `GameManager` expone `anchoTablero` / `altoTablero` en el Inspector para configurarlo sin
tocar código.

Para ver el tablero en pantalla (con cubos/cápsulas como placeholder, sin arte final):
1. Crea un GameObject vacío llamado `BoardView` en la Hierarchy.
2. Agrégale el componente `TanksGame.Visual.BoardView`.
3. Selecciona el objeto `GameManager` y arrastra el objeto `BoardView` al campo
   "Vista Tablero" en el Inspector.
4. Dale Play. Vas a ver una grilla de cubos según `anchoTablero x altoTablero`, y dos
   cápsulas de color (azul = Jugador 1, rojo = Jugador 2) en sus posiciones iniciales.
   Cada vez que llames a `EjecutarSiguienteTurno()`, las cápsulas se mueven a su nueva
   posición.
5. Cuando tengas modelos propios, arrastra tus prefabs a `celdaPrefab` / `tanquePrefab`
   en `BoardView` y se usarán en vez de las primitivas.

Nota: `BoardView` es intencionalmente simple (sin animación de movimiento, salto directo
a la nueva posición) para que sirva como verificación visual rápida. La sección 3 de
`DESIGN_GUIDE.md` describe cómo debería verse la versión final.

## 6. Botón de UI real y HUD ("Siguiente turno")
`GameplayUI.cs` genera por código un Canvas con:
- Un botón **SIGUIENTE TURNO** abajo al centro.
- Un panel arriba a la izquierda con vida, misiles y estado del escudo de cada tanque.
- Un panel arriba a la derecha con el log de la última ronda (últimas 8 líneas).

Para usarlo:
1. Crea un GameObject vacío llamado `GameplayUI` en la Hierarchy.
2. Agrégale el componente `TanksGame.UI.GameplayUI`.
3. Arrastra el objeto `Game Manager` de tu escena al campo "Game Manager" del Inspector.
4. Dale Play. Ya no necesitas llamar a `EjecutarSiguienteTurno()` desde el Inspector:
   hazlo con el botón en pantalla.

No requiere TextMeshPro ni ningún paquete adicional: usa `UnityEngine.UI` (el sistema de
UI clásico), que viene incluido por defecto en cualquier proyecto de Unity.

## 7. Siguientes pasos sugeridos
1. Reemplazar este HUD de texto plano por el panel de "programa" descrito en la sección 4
   de `DESIGN_GUIDE.md` (arrastrar instrucciones para armar la secuencia del tanque, en
   vez de programas fijos en el Inspector).
2. Animaciones de movimiento/disparo/explosión y barra de vida flotante (Canvas en
   World Space sobre cada tanque), en vez del salto directo de `BoardView`.
3. Modo IA: reemplazar el programa de un jugador por lógica generada en runtime en vez
   de un script fijo.
4. Soporte para más de 2 jugadores (el código ya está escrito de forma genérica con
   `List<TankAgent>`, así que agregar un tercer/cuarto tanque no requiere cambios en
   `TurnManager`).
