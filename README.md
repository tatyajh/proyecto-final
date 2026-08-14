# Proyecto final

Proyecto creado con **Unity 6.3 LTS** (`6000.3.16f1`).

## Abrir el proyecto

1. Instala Unity Hub y el Editor `6000.3.16f1`.
2. En Unity Hub, selecciona **Add** y elige esta carpeta.
3. Abre el proyecto desde Unity Hub.

La versión se fija en `ProjectSettings/ProjectVersion.txt` para que todo el equipo use el mismo Editor.

## Escena inicial y flujo

El punto de partida configurado para las compilaciones es
`Assets/Scenes/Menus/Type Ypur Name.unity`. Después de guardar el nombre del
jugador, el juego carga `Assets/Scenes/Menus/Main Menu.unity`.

Desde el menú principal se puede acceder a las opciones, al menú multijugador
y a la selección del modo historia. Los cuatro niveles de historia están
incluidos en la configuración de compilación.

Las escenas habilitadas para compilar se encuentran en
`ProjectSettings/EditorBuildSettings.asset`.
