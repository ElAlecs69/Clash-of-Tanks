# Guía de arte y UI — Juego de Tanques Programables

Nota importante: las capturas que compartiste son de **Clash of Clans** (propiedad de
Supercell). No se pueden usar ni reproducir sus assets, iconos o arte. Lo que sí se puede
hacer es tomar el **lenguaje visual general** (paneles de madera, colores cálidos, iconos
tipo gema, tipografía redondeada) y aplicarlo a un juego completamente distinto y original.
Todo lo descrito abajo se debe producir con arte propio.

## 1. Paleta y ambientación
- Paleta cálida: verdes de césped, marrones de madera/tierra, dorado para acentos y HUD.
- Battlefield tipo diorama: tablero 8x8 elevado sobre una base de "campo de batalla"
  (tierra, pasto, cráteres), con bordes decorados (rocas, cajas de munición, banderas).
- Los tanques pueden estilizarse como máquinas de guerra "de juguete"/steampunk-fantástico
  en vez de tanques militares realistas, para mantener un tono amigable y evitar cualquier
  referencia a marcas reales.

## 2. Tablero (vista isométrica, cámara libre)
- Grid 8x8 con celdas ligeramente biseladas, líneas doradas tenues entre casillas.
- Obstáculos: rocas / restos de tanques destruidos con textura oscura y grietas.
- Minas: icono parpadeante rojo/naranja apenas visible en el suelo (visible solo para su
  dueño, opcional).
- Hospital: casilla con una cruz o icono de botiquín, con un brillo verde suave.
- Cada tanque lleva una barra de vida flotante (verde→amarillo→rojo) y, si tiene escudo
  activo, un aro/burbuja azul translúcido alrededor.

## 3. HUD superior (inspirado en el estilo de recursos tipo gema de la referencia)
- Barra superior con: retrato/nombre del jugador, contador de vida en % grande,
  contador de misiles restantes (icono de misil + número), y turno actual.
- Iconos de recursos con forma de "gema"/medallón y borde dorado, como en juegos
  móviles de estrategia — pero con iconografía propia (misil, escudo, mina, botiquín).

## 4. Panel de "programa" (equivalente al panel de ejército de tu referencia)
En vez de una pantalla de tropas, se usa un panel de **instrucciones** con la misma
lógica de slots con contador que viste en "Mi ejército":
- Fila de iconos de instrucciones disponibles: MOV, AMT, MINA, MISIL, RADAR, ESCUDO,
  ESPERAR, REPARAR — cada uno en un slot cuadrado con borde dorado tipo marco de madera.
- El jugador arrastra o toca instrucciones para construir su secuencia de turnos
  (lista vertical numerada a la derecha, como una "cola de programa").
- Bajo cada slot de instrucción con dirección (MOV/AMT/RADAR) aparecen 4 flechas
  (N/S/E/O) para elegir dirección, igual que se eligen objetivos en juegos de estrategia.
- Botón principal grande "ATACAR" / "EJECUTAR TURNO" abajo a la derecha, en el mismo
  estilo de botón grande con relieve y texto en mayúsculas que viste en la referencia.

## 5. Tipografía y botones
- Fuente redondeada y gruesa (tipo "cartoon bold") para títulos y botones.
- Botones con relieve 3D (sombra inferior oscura, brillo superior), como en la mayoría
  de juegos móviles de estrategia — este estilo de botón es genérico y no es IP de nadie.

## 6. Feedback de combate
- Números flotantes de daño ("-25%", "-20%") que suben y se desvanecen sobre el tanque.
- Destello breve de pantalla al impactar un misil; sonido/animación de explosión simple.
- Al destruirse un tanque, animación de colapso y aparición del modelo de "obstáculo"
  (chatarra) en su celda.

## 7. Siguientes pasos de arte
1. Diseñar 1 modelo de tanque low-poly por bando (o reskins de color) + animaciones
   idle/movimiento/disparo/destrucción.
2. Diseñar el set de iconos de instrucciones (8 iconos + 4 flechas de dirección).
3. Diseñar el frame de madera/piedra reutilizable para paneles y slots.
4. Definir 1-2 variantes de tablero (día/noche, desierto/pasto) para rejugabilidad visual.
