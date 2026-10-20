# Pesaje asíncrono y confirmación

El pesaje se solicita sobre una captura con imagen y se procesa en segundo plano. Solo una estimación válida crea un evento; confirmar el resultado registra la revisión, sin cambiar el pesaje.

## Flujo HTTP

1. Crear una sesión con `POST /sesiones-captura` y subir una imagen mediante `POST /capturas` (multipart: `id_sesion`, `id_animal`, `imagen` y `fecha_captura` opcional).
2. Enviar `POST /pesajes` con `{"id_captura":"<UUID>"}`. Responde **202**, sin cuerpo, con `Location: /capturas/{id}`.
3. Consultar `GET /capturas/{id}` hasta que `estado` sea `procesada` o `fallida`. Los estados intermedios son `pendiente` y `procesando`; antes de solicitar, la captura está `registrada`.
4. Si está `procesada`, consultar `GET /pesajes/{id_evento_pesaje}` y, cuando el operador revise el resultado, enviar `POST /capturas/{id}/confirmar` sin cuerpo.

El JSON usa `snake_case` y los identificadores son UUID v7 en texto. La captura expone `intentos`, `motivo_fallo`, `id_evento_pesaje` y `fecha_revision`. Una falla no crea evento ni alerta; requiere registrar otra captura. Una imagen inválida se rechaza con 422 antes de guardar captura o archivo (tipo permitido JPEG, PNG o WebP, contenido no vacío y máximo 10 MiB).

| Ruta | Respuesta y errores |
| --- | --- |
| `POST /pesajes` | 202 al encolar; 404 si la captura no existe; 409 si ya está pendiente, procesándose o procesada; 422 si falló o no tiene imágenes. No duplica eventos. |
| `GET /capturas/{id}` | 200 con el estado y el identificador del evento, o el motivo de falla; 404 si no existe. |
| `GET /pesajes/{id}` | 200 con peso, fecha de captura como `fecha_pesaje`, versión del modelo, identificador del animal, lote histórico, peso previo y alerta; 404 si no existe el evento. |
| `POST /capturas/{id}/confirmar` | 200 con la captura revisada; 404 si no existe; 422 si no está procesada. Repetir la confirmación conserva la primera `fecha_revision`; nunca crea ni modifica eventos. |

Los errores de negocio usan ProblemDetails con el mensaje en `detail`. El historial expone también `fecha_procesamiento`; esta fecha no reemplaza la fecha de captura para calcular la GMD. Ver [analítica y alertas](../Analitica/README.md) para historial, panel, reportes y configuración de reglas.

## Opciones de inferencia

Sección `Inferencia` de configuración (variables de entorno con `__`, por ejemplo `Inferencia__MaximoIntentos=3`):

| Opción | Predeterminado | Uso |
| --- | --- | --- |
| `ProcesarEnSegundoPlano` | `true` | Activa el consumidor hospedado; desactivarlo no impide encolar solicitudes. |
| `MaximoIntentos` | `3` | Límite de reclamos, incluidos los recuperados tras vencer un arrendamiento. |
| `DemoraReintentoSegundos` | `30` | Espera tras indisponibilidad o timeout del estimador. |
| `ArrendamientoSegundos` | `120` | Vigencia del reclamo y timeout de inferencia. |
| `IntervaloSegundos` | `1` | Intervalo de consulta de la cola. |

Los límites e intervalos deben ser positivos. El adaptador actual es simulado: devuelve 400 kg y `version_modelo: simulado-v1`; todavía no conecta un modelo de visión real. Los resultados inválidos fallan sin reintento; los errores de transporte y timeouts se reintentan hasta el límite.

## Persistencia y arranque

La cola usa bloqueos breves y arrendamientos persistidos: no mantiene una transacción abierta durante la inferencia y descarta resultados de intentos vencidos. Cada fecha tiene su columna: `proximo_intento` es el reintento más temprano de una captura `pendiente` y `vence_procesamiento`, el fin del arrendamiento de una `procesando`; la migración `VencimientoProcesamiento` traslada los arrendamientos que estaban en `proximo_intento`. Evento, alerta y transición a procesada se confirman juntos mediante la unidad de trabajo. Se reutilizan las invariantes del agregado `Captura` y la fábrica `EventoPesaje.Registrar`; no hay actualización de eventos.

Con `BaseDeDatos:AplicarMigracionesAlIniciar=true`, la API aplica las migraciones y la semilla antes de arrancar los servicios hospedados. Si se desactiva, la base debe estar migrada antes del arranque. Los tests desactivan el consumidor del host compartido y lo habilitan solo en el host que posee el escenario, para evitar que otro host reclame su trabajo.
