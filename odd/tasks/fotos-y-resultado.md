# Feature: fotos-y-resultado

Objetivo: paquetes 1 y 2 del análisis de brechas del MVP. La API sirve las fotos de las capturas, el frontend las achica antes de subirlas y las muestra, y se completan la pantalla de resultado, la ficha y el listado de animales. Ramas `feat/fotos-y-resultado` en los dos repos, desde `manu` (worktrees `../bovino_vision_backend-fotos` y `../bovino_vision_frontend-fotos`); un commit por unidad, sin push, PR ni merge.
Verificación: backend con `dotnet build BovinoVision.slnx`, `dotnet format BovinoVision.slnx --verify-no-changes` y `dotnet test --solution BovinoVision.slnx` (Testcontainers); frontend con `npm run test` (Vitest), `npm run typecheck`, `npm run lint` y `npm run build`. Sin recorridos en el navegador: los hace el usuario.

## Specs

- **S1. Alcance.** Usuario, ante la propuesta "Recomiendo arrancar con los paquetes 1 y 2 juntos": "si, sumalo al paquete" (sobre el achicado de fotos en el frontend). Filas aprobadas, textuales:
  - Paquete 1, "Fotos de las capturas": "La API no tiene ningún endpoint que devuelva las fotos guardadas. Agregar uno, con su permiso, desbloquea tres pantallas: la imagen en el resultado (RF-23), en el historial de pesajes (RF-35) y las miniaturas de la galería al recargar (CU-13)."
  - Paquete 2, "Resultado y ficha completos": "El resultado no muestra fecha y hora, raza, sexo ni edad (RF-24), ni el peso previo con su fecha o el aviso de primer pesaje (RF-26). Falta "Pesar este animal" desde la ficha (CU-21), el filtro "con alertas" en el listado (la API ya lo tiene, RF-33) y los mensajes de listado vacío (CU-19)."
  - Achicado sumado al paquete 1: "Achicar la foto antes de subirla, por ejemplo a 1600 px en el lado mayor y JPEG al 85 %. [...] De paso se corrige la orientación de la foto, que de otra forma puede llegar rotada. No necesita dependencias: alcanza con `createImageBitmap` y un canvas."
  - Fuera de alcance: el redimensionado al tamaño de entrada del modelo (lo hace el servicio de inferencia en Python), el peso en la galería y los avisos fuera de la pantalla (paquete 3), las validaciones de alta del animal (RF-28, decisión pendiente del usuario), paquetes NuGet o npm nuevos.
- **S2. Endpoint de fotos (backend).** `GET /imagenes/{id}` devuelve el archivo de la imagen `id` con su `Content-Type` guardado (`imagenes.tipo_contenido`) y `Cache-Control: private`.
  - Permiso: cualquiera de `capturas_registrar` o `rodeo_consultar` (`RequiereAlgunPermiso`); sin sesión, 401; sin esos permisos, 403.
  - Alcance: solo imágenes de capturas cuyo animal pertenece al establecimiento de la sesión (`IContextoUsuario.IdEstablecimiento`). Imagen inexistente, de otro establecimiento o cuyo archivo ya no está: 404 con ProblemDetails, igual que el resto de la API.
  - El puerto `IAlmacenamientoImagenes` suma la lectura por referencia. El adaptador local nunca resuelve una ruta fuera de su directorio, aunque la referencia guardada fuera manipulada.
- **S3. Ids de imagen en las respuestas (backend).** Campos nuevos y aditivos, sin cambiar los existentes: `id_imagen` en `PesajeRespuesta` (la imagen utilizada por el evento, `GET /pesajes/{id}`) y en cada elemento de `GET /sesiones-captura/{id}/capturas` (la imagen de la captura). El historial de pesajes ya trae `id_imagen_utilizada`.
- **S4. Achicado antes de subir (frontend).** Antes de `POST` de la foto, el frontend la redimensiona para que el lado mayor no supere 1600 px y la codifica como JPEG con calidad 0,85, respetando la orientación de la foto. Una foto JPEG que ya cumple el límite se sube tal cual. Si el navegador no puede decodificarla o redimensionarla, se sube la original. Supuesto a confirmar con el equipo del modelo: 1600 px alcanzan como resolución mínima de entrada; el valor queda en una sola constante.
- **S5. Fotos en pantalla (frontend).** Con `GET /imagenes/{id}`:
  - Resultado del pesaje (RF-23): la foto utilizada.
  - Historial de pesajes en la ficha (RF-35): una miniatura por evento.
  - Galería de la sesión (CU-13): miniatura desde la API para cada captura, también después de recargar; la vista previa local sigue sirviendo mientras la foto se está subiendo.
  - Si la imagen no carga, se muestra un reemplazo neutro sin romper la pantalla.
- **S6. Resultado completo (frontend, RF-23, RF-24, RF-26).** La pantalla de resultado muestra fecha y hora del pesaje (formato argentino de `lib/formato.ts`), raza, sexo y edad del animal cuando existen, y el peso previo con su fecha. Si no hay peso previo, muestra un aviso de primer pesaje (ERS CU-15 4a). El peso previo se muestra también cuando hay alerta.
- **S7. Ficha y listado (frontend, CU-21, RF-33, CU-19, CU-22).**
  - La ficha tiene "Pesar este animal", que abre la captura con ese animal ya identificado (solo con permiso `capturas_registrar`).
  - Con 0 alertas activas, la ficha lo dice.
  - Con historial de pesajes vacío, ofrece "Pesar este animal".
  - El listado de animales suma el filtro "con alertas activas" (`con_alertas_activas=true`).
  - Con listado vacío: si hay filtros, ofrece quitarlos; si no hay animales, ofrece darlos de alta (con permiso `rodeo_gestionar`).

## Tasks

| ID | Specs | Ruta | Estado | Evidencia / commit |
| --- | --- | --- | --- | --- |
| T1 | S2, S3 | delegada (escritor backend) | hecha | `c59b486`. RED/GREEN por regla: 200 con bytes, `Content-Type` y `Cache-Control: private`; 403 sin ambos permisos; 200 solo con `rodeo_consultar`; 404 con `detail` para id inexistente, archivo borrado y otro establecimiento; el adaptador rechaza referencias fuera de su directorio (mutación detectada por 4 tests); `id_imagen` en pesaje y galería. 401 anónimo ya estaba cubierto por la política de respaldo. build 0 warnings, format OK, 253/253. |
| T2 | S4-S7 | delegada (escritor frontend) | pendiente | |
| T3 | S1-S7 | inline + verificador independiente (S2 toca permisos) | pendiente | |

Ruta: T1 y T2 son unidades independientes en repos distintos (el contrato queda fijado en S2 y S3), cada una de varios archivos; se lanzan en paralelo. Además, el contexto del orquestador ya superó el umbral de respaldo.

## Log

- **L1 (usuario, 2026-10-21):** "ahora, qué podemos seguir mejorando del alcance del MVP (Modulo de captura, Modulo de procesamiento y estimacion de peso, Modulo de gestión y trazabilidad de animales) fuera del servicio de inferencia de estimacion de peso, que todavia lo estamos armando y falta integrarlo"
- **L2 (usuario, 2026-10-21):** "se podria hacer que el backend reciba la imagen del animal y la procese a la resolucion que el estimador de peso necesita (que creo que es 264 x 264)? o lo podria hacer el frontend? si lo hace el backend, deberiamos agregar una libreria?" Respuesta: el redimensionado al tamaño del modelo va en el servicio de Python; el backend guarda la original; el frontend solo la achica para subirla.
- **L3 (usuario, 2026-10-21):** "si, sumalo al paquete"
- **L4 (decisión de contrato):** un único endpoint por id de imagen, porque el evento de pesaje referencia la imagen utilizada (`IdImagenUtilizada`) y una captura puede tener varias imágenes. Las pantallas que hoy solo tienen el id de captura reciben `id_imagen` como campo aditivo (S3).
