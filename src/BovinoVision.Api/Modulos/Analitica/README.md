# Analítica y alertas

Consultas de solo lectura del establecimiento del usuario actual. Respuestas JSON en `snake_case`, con identificadores UUID. No se persiste la GMD.

| GET | Parámetros | Resultado |
| --- | --- | --- |
| `/animales/{id}/pesajes` | `id`: UUID del animal | Historial descendente por fecha de captura, GMD contra el pesaje anterior y código del lote histórico. |
| `/animales/{id}/alertas` | `id`: UUID del animal | Todas sus alertas, de la más reciente a la más antigua. |
| `/alertas` | Ninguno | Alertas activas, por prioridad ascendente y luego fecha descendente. |
| `/panel` | `id_lote`: UUID opcional | Último peso por animal, GMD del último intervalo, cantidad de animales con GMD y evolución del peso promedio por día. |
| `/reportes` | `desde`, `hasta`: fechas `AAAA-MM-DD` obligatorias; `id_lote`: UUID opcional | Cantidades de pesajes y animales, promedio de pesos y promedio de GMD entre pesajes consecutivos dentro del período. |

Las rutas de animal devuelven 404 si no existe en el establecimiento. El reporte devuelve 400 si faltan fechas o no se pueden interpretar y 422 si `desde` es posterior a `hasta` (`detail`: `La fecha 'desde' no puede ser posterior a 'hasta'`). Los días extremos se incluyen completos en la zona horaria configurada.

El filtro `id_lote` usa la asignación histórica referenciada por cada evento, no la pertenencia actual. Solo cuentan animales con pesajes dentro del filtro. Los promedios sin datos son `null`; los pares del mismo día no tienen GMD. El panel no reutiliza una GMD antigua si el último intervalo corresponde al mismo día. El historial agrega `fecha_procesamiento`, `id_imagen_utilizada` y `lote_codigo`; no hay eventos fallidos (la falla pertenece a la captura). Las alertas exponen `id_evento`, `id_animal`, `variacion` en kg/día, `gmd_esperada` y prioridad numérica, no la variación porcentual ni las prioridades textuales del contrato Python anterior.

## Configuración y arquitectura

- `Analitica:ZonaHoraria`: `America/Argentina/Buenos_Aires` por defecto; GMD por días calendario locales y fecha de captura, no por tiempo de procesamiento.
- `Alertas:FraccionEstancamiento`: `0.5`; `Alertas:PrioridadPerdida`: `1`; `Alertas:PrioridadEstancamiento`: `2`.
- Pérdida: GMD negativa; estancamiento: GMD menor que la fracción del objetivo del lote histórico. La pérdida prevalece si ambas reglas aplican.
- Arquitectura: CQRS liviano y puertos/adaptadores; cada caso de uso depende de `IConsultasAnalitica`. El adaptador EF proyecta cada lote de resultados en una consulta, sin lecturas por animal. La existencia de un animal requiere una consulta adicional.
- Diseño: Adapter e inyección por constructor. Se conserva `IEvaluadorAlertas`, invocado por el procesamiento de pesajes dentro de su propia unidad de trabajo; las consultas no guardan cambios.
