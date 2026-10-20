# Migración a .NET: contrato, trazabilidad y cambios para TP7

El backend activo es C#/.NET 10 con Clean Architecture, EF Core y PostgreSQL 17. Este informe documenta S1, S3, S4, S8 y S9 de [la feature ODD](../odd/tasks/migracion-dotnet.md). No certifica una importación de datos ni un modelo de visión productivo. Para ejecutar y configurar, consultar el [README](../README.md).

## Estado y límites del cierre documental

- Suite integrada observada antes de este cierre exclusivamente documental: **168/168**, sin errores ni omitidos. No se deduce paridad del total: la matriz de abajo identifica los **38 tests Python**, incluidos los casos sustituidos por B.
- El estimador registrado devuelve **400 kg**, versión `simulado-v1`; no es ML de producción. Solo se validan MIME/tamaño/contenido no vacío, no calidad visual.
- El frontend debe adaptarse a UUID, nombres españoles, catálogo de razas y polling. Esa adaptación queda fuera de esta feature.
- **Retiro de fuentes pendiente del padre:** esta entrega no elimina Python, migraciones Alembic, tests ni manifiestos. El runtime .NET no los usa. Su presencia no significa que existan dos runtimes soportados.
- **`.env.example` queda sin cambios, heredado:** su escritura fue denegada por la herramienta de seguridad. No utilizarlo como plantilla actualizada de .NET. Las claves vigentes están en el README; `dotnet run` no carga `.env` automáticamente.
- No se modifican documentos externos del TP7, datos, volúmenes ni uploads. El padre conserva el seguimiento ODD y los commits de cierre.

## Contrato y transición de datos

S4 conserva el propósito de las 16 rutas de negocio originales; S7 y el alcance B prevalecen sobre la compatibilidad literal de JSON. Hay además `/health` y seis operaciones nuevas: catálogo de razas, modificación de lote, historial de lotes, cierre de sesión, consulta y confirmación de captura.

| Antes (Python) | Ahora (.NET) |
| --- | --- |
| Ids enteros; `animal_id`, `lote_id`, `sesion_id`, `captura_id` | UUID v7 en texto; `id_animal`, `id_lote`, `id_sesion`, `id_captura` |
| Raza libre en `Animal` | Catálogo `Raza`; entrada `id_raza` |
| `POST /pesajes` devuelve 201 con resultado síncrono | Devuelve 202 sin cuerpo, `Location: /capturas/{id}`; consultar hasta `procesada` o `fallida` |
| Imagen inválida registrada con `validada=false` | 422 sin guardar captura, imagen ni archivo |
| Fallo de estimación genera evento con estado error | Fallo en captura con `motivo_fallo`, sin evento de pesaje ni alerta |
| Fecha de procesamiento usada como fecha de pesaje | `fecha_pesaje = fecha_captura`; procesamiento separado |
| Lote actual y campo directo en animal | Asignación histórica con intervalo; evento vinculado al lote vigente en fecha de captura |
| Alertas por variación porcentual de 15/30% | Pérdida por GMD negativa; estancamiento bajo fracción del objetivo del lote |
| Commits de repositorios separados | Unidad de trabajo por caso de uso; publicación de evento y alerta atómica |

No se guarda GMD: se calcula con fechas calendario de la zona configurada (Buenos Aires por defecto). Sin días de diferencia no hay GMD. La confirmación registra `fecha_revision` en la captura y deja el evento inmutable. El resultado conserva `estado: estimado` para eventos válidos; los estados de cola pertenecen a la captura.

**La migración EF `Inicial` crea un esquema nuevo, no transforma una base Alembic.** No hay conversión automática de ids ni importación de datos/imágenes anteriores. Antes de usar datos existentes se necesita respaldo, base de destino separada y un plan validado de correspondencias para ids, razas, asignaciones de lote y fallas. No reutilizar a ciegas un volumen anterior ni ejecutar limpieza de bases como parte del retiro de fuentes.

## Rutas públicas

Las rutas de negocio se definen en `src/BovinoVision.Api/Modulos/{Trazabilidad,Capturas,Pesajes,Analitica}/Modulo*.cs`; `/health`, en `Program.cs`. Los parámetros `{id}` son UUID. Los errores de dominio usan ProblemDetails con mensaje en `detail`: 404 para referencias ausentes, 409 para conflictos y 422 para operaciones inválidas. La validación del transporte puede devolver 400; un segmento que no cumple `{id:guid}` no selecciona la ruta.

| Método y ruta | Entrada / resultado principal |
| --- | --- |
| `GET /health` | 200, `{"status":"ok"}`; no comprueba la base |
| `GET /razas` | Catálogo precargado ordenado por nombre |
| `GET /lotes` | Lotes con cantidad de animales vigentes |
| `POST /lotes` | `codigo` obligatorio; `nombre` y `gmd_objetivo` opcionales; 201 |
| `PUT /lotes/{id}` | Cambios de nombre/objetivo; 200 |
| `GET /animales` | Filtros `id_lote`, `q`, `con_alertas_activas`; listado con último peso |
| `POST /animales` | `identificador`; opcionales `id_raza`, `sexo`, `fecha_nacimiento`, `id_lote`; 201 |
| `GET /animales/{id}` | Ficha, último peso y cantidad de alertas activas |
| `PUT /animales/{id}` | Datos y reasignación de lote; conserva valores no informados |
| `GET /animales/{id}/historial-lotes` | Asignaciones desde la más reciente |
| `POST /sesiones-captura` | Sin cuerpo; sesión activa, 201 |
| `POST /sesiones-captura/{id}/finalizar` | Sin cuerpo; sesión finalizada, 200 |
| `GET /sesiones-captura/{id}/capturas` | Galería por fecha de captura |
| `POST /capturas` | Multipart `id_sesion`, `id_animal`, `imagen`, `fecha_captura` opcional; 201 |
| `GET /capturas/{id}` | Estado, intentos, motivo de fallo, revisión e id de evento |
| `POST /capturas/{id}/confirmar` | Sin cuerpo; revisión de captura procesada, 200; repetir conserva primera fecha |
| `POST /pesajes` | JSON `id_captura`; 202 sin cuerpo; repetir una solicitud pendiente/procesando/procesada da 409 |
| `GET /pesajes/{id}` | Evento con peso, versión, animal, lote histórico, peso previo y alerta |
| `GET /animales/{id}/pesajes` | Historial descendente con GMD, imagen y fecha de procesamiento |
| `GET /animales/{id}/alertas` | Alertas del animal desde la más reciente |
| `GET /alertas` | Alertas activas ordenadas por prioridad y fecha |
| `GET /panel` | Agregados y evolución; filtro opcional `id_lote` histórico |
| `GET /reportes` | `desde`, `hasta` obligatorios (fechas inclusivas), `id_lote` opcional; rango invertido da 422 |

No hay `GET /lotes/{id}` ni `GET /sesiones-captura/{id}`: no asumir una ruta de lectura porque un alta devuelva esa URI en `Location`. Documentación OpenAPI/Scalar solo en Development. Ver [flujo detallado de pesaje](../src/BovinoVision.Api/Modulos/Pesajes/README.md) y [analítica](../src/BovinoVision.Api/Modulos/Analitica/README.md).

## Matriz de los 38 tests originales

Se leyeron las rutas y los ocho archivos `tests/test_*.py` antes del retiro. Los nombres originales se conservan exactamente. Los destinos son métodos de test existentes sobre interfaces públicas HTTP o casos de uso; no son nombres de pruebas propuestas. **C** conserva la regla de negocio (con ids/campos nuevos); **B** sustituye una expectativa por el alcance B/S7. Una fila puede necesitar varias pruebas .NET, no hay correspondencia uno a uno ni equivalencia literal de todas las aserciones internas.

Clases de destino y ubicación (el nombre de método de cada fila se añade a esta clase):

| Abreviatura | Clase / archivo, relativo a `tests/` |
| --- | --- |
| AA | `BovinoVision.Api.Tests/Trazabilidad/AnimalesApiTests.cs` (`AnimalesApiTests`) |
| AU | `BovinoVision.Aplicacion.Tests/Trazabilidad/AnimalesTests.cs` (`AnimalesTests`) |
| CA | `BovinoVision.Api.Tests/Capturas/CapturasApiTests.cs` (`CapturasApiTests`) |
| CU | `BovinoVision.Aplicacion.Tests/Capturas/RegistrarCapturaTests.cs` (`RegistrarCapturaTests`) |
| PA | `BovinoVision.Api.Tests/Pesajes/PesajesApiTests.cs` (`PesajesApiTests`) |
| AN | `BovinoVision.Api.Tests/Analitica/AnaliticaEndpointsTests.cs` (`AnaliticaEndpointsTests`) |
| E2E | `BovinoVision.Api.Tests/Integracion/FlujoPesajeIntegracionTests.cs` (`FlujoPesajeIntegracionTests`) |
| SA | `BovinoVision.Api.Tests/SaludTests.cs` (`SaludTests`) |

### `tests/test_animal_service.py` — 5

| Test original | Tipo | Prueba .NET / alcance |
| --- | --- | --- |
| `test_registrar_animal` | C | AU.`Registrar_animal_lo_guarda_y_devuelve_su_ficha` |
| `test_no_permite_identificador_duplicado` | C | AU.`No_permite_identificador_duplicado_en_el_establecimiento` |
| `test_modificar_animal_inexistente_lanza_error` | C | AU.`Modificar_animal_inexistente_lanza_no_encontrado` |
| `test_no_permite_asignar_lote_inexistente` | C | AU.`No_permite_asignar_lote_inexistente` |
| `test_listar_animales_filtra_por_establecimiento` | C | AU.`Listar_animales_filtra_por_establecimiento`; prueba de caso de uso, no de login HTTP multiusuario |

### `tests/test_animales_api.py` — 5

| Test original | Tipo | Prueba .NET |
| --- | --- | --- |
| `test_crear_y_listar_animal` | C | AA.`Crear_y_listar_animal` |
| `test_identificador_duplicado_devuelve_409` | C | AA.`Identificador_duplicado_devuelve_409` |
| `test_ficha_de_animal_inexistente_devuelve_404` | C | AA.`Ficha_de_animal_inexistente_devuelve_404` (UUID ausente en vez de entero 999) |
| `test_crear_lote_y_asignar_a_animal` | C | AA.`Crear_lote_y_asignar_a_animal` y `Crear_animal_devuelve_la_ficha_completa_en_snake_case` |
| `test_asignar_lote_inexistente_devuelve_404` | C | AA.`Asignar_lote_inexistente_devuelve_404_y_no_guarda_el_animal` |

### `tests/test_captura_service.py` — 5

| Test original | Tipo | Prueba .NET / cambio |
| --- | --- | --- |
| `test_iniciar_sesion` | B | CU.`Iniciar_sesion_la_crea_activa_para_el_usuario_actual`; sesión asociada al usuario, no el antiguo campo directo de establecimiento. SA.`Las_migraciones_cargan_la_semilla_del_establecimiento_usuario_y_razas` verifica la semilla |
| `test_registrar_captura_valida` | B | CU.`Registrar_captura_valida_guarda_la_imagen_con_su_huella_en_una_transaccion`; estado `registrada` e imagen propia sustituyen `validada=true` |
| `test_registrar_captura_con_formato_no_soportado` | B | CU.`Una_imagen_invalida_se_rechaza_sin_guardar_nada`; ahora excepción y nada guardado, no captura con `validada=false` |
| `test_registrar_captura_con_sesion_inexistente_lanza_error` | C | CU.`Registrar_captura_con_sesion_inexistente_lanza_no_encontrado` |
| `test_registrar_captura_con_animal_inexistente_lanza_error` | C | CU.`Registrar_captura_con_animal_inexistente_lanza_no_encontrado` |

### `tests/test_capturas_pesajes_api.py` — 4

| Test original | Tipo | Prueba .NET / cambio |
| --- | --- | --- |
| `test_flujo_completo_captura_y_pesaje` | B | CA.`Subir_una_captura_valida_la_registra_con_su_imagen_y_huella`; PA.`Solicitar_pesaje_responde_202_y_permite_consultar_la_cola`; PA.`Procesar_concurrentemente_crea_un_evento_fechado_en_la_captura_y_confirmar_no_lo_modifica`; E2E.`El_consumidor_real_publica_pesajes_alertas_y_analitica_sin_duplicar_ni_mutar`. Cubre alta, resultado e historial con consumidor real; no el viejo 201 síncrono |
| `test_captura_con_formato_invalido_no_se_puede_procesar` | B | CA.`Una_subida_invalida_responde_422_con_el_motivo_y_no_guarda_nada`; ya no existe una captura inválida almacenada para enviarla a pesaje |
| `test_no_permite_procesar_la_misma_captura_dos_veces` | C | PA.`Procesar_concurrentemente_crea_un_evento_fechado_en_la_captura_y_confirmar_no_lo_modifica` y E2E.`El_consumidor_real_publica_pesajes_alertas_y_analitica_sin_duplicar_ni_mutar` |
| `test_segundo_pesaje_incluye_peso_previo` | C | PA.`El_resultado_incluye_el_lote_historico_y_el_peso_previo_por_fecha_de_captura` |

### `tests/test_pesaje_service.py` — 8

| Test original | Tipo | Prueba .NET / cambio |
| --- | --- | --- |
| `test_procesar_pesaje_exitoso` | B | PA.`Procesar_concurrentemente_crea_un_evento_fechado_en_la_captura_y_confirmar_no_lo_modifica`; solicitud/procesamiento separados, resultado de 400 kg y versión verificados |
| `test_procesar_pesaje_con_estimacion_fallida` | B | PA.`Un_resultado_invalido_o_agotar_reintentos_falla_sin_evento`; motivo en captura, no evento error; E2E.`El_consumidor_real_publica_pesajes_alertas_y_analitica_sin_duplicar_ni_mutar` comprueba historial/analítica sin alteración |
| `test_no_permite_procesar_captura_invalida` | B | CA.`Una_subida_invalida_responde_422_con_el_motivo_y_no_guarda_nada`; la validación ocurre antes de crear la captura |
| `test_no_permite_procesar_captura_inexistente` | C | PA.`Las_referencias_inexistentes_devuelven_404` |
| `test_no_permite_procesar_la_misma_captura_dos_veces` | C | PA.`Procesar_concurrentemente_crea_un_evento_fechado_en_la_captura_y_confirmar_no_lo_modifica` |
| `test_obtener_resultado_incluye_lote_y_peso_previo` | B | PA.`El_resultado_incluye_el_lote_historico_y_el_peso_previo_por_fecha_de_captura`; verifica código de lote, peso y fecha previos. **No porta la aserción interna de id del evento previo**; véase límite de prueba abajo |
| `test_variacion_grande_de_peso_genera_alerta` | B | AN.`Una_perdida_de_peso_crea_una_alerta_visible_en_el_animal_y_en_el_rodeo` y E2E.`El_consumidor_real_publica_pesajes_alertas_y_analitica_sin_duplicar_ni_mutar`; la subida 300→450 ya no es regla de alerta |
| `test_variacion_chica_de_peso_no_genera_alerta` | C | AN.`Un_aumento_normal_no_crea_alerta`; conserva ausencia para un aumento sin objetivo de lote, no una regla general porcentual |

### `tests/test_analitica_service.py` — 4

| Test original | Tipo | Prueba .NET / cambio |
| --- | --- | --- |
| `test_historial_con_gmd_primer_pesaje_sin_gmd` | C | AN.`El_historial_trae_los_pesajes_del_mas_reciente_al_mas_antiguo_con_gmd_y_lote_historico` y E2E.`El_consumidor_real_publica_pesajes_alertas_y_analitica_sin_duplicar_ni_mutar`; primer pesaje sin GMD |
| `test_historial_con_gmd_calcula_entre_consecutivos` | C | AN.`El_historial_trae_los_pesajes_del_mas_reciente_al_mas_antiguo_con_gmd_y_lote_historico`; 300→320 en diez días = 2 kg/día |
| `test_historial_con_gmd_ignora_pesajes_con_error` | B | E2E.`El_consumidor_real_publica_pesajes_alertas_y_analitica_sin_duplicar_ni_mutar`; fallo sin evento deja historial y GMD intactos. Ya no existe el evento error intermedio que anulaba el intervalo en Python |
| `test_panel_promedia_el_ultimo_peso_de_cada_animal` | C | AN.`El_panel_agrega_el_ultimo_peso_y_la_gmd_de_cada_animal`; pesos 320/400, promedio 360 y GMD 2 |

### `tests/test_analitica_alertas_api.py` — 6

| Test original | Tipo | Prueba .NET / cambio |
| --- | --- | --- |
| `test_listado_y_ficha_incluyen_ultimo_peso` | C | AA.`Listado_y_ficha_incluyen_el_ultimo_peso_por_fecha_de_pesaje` |
| `test_variacion_grande_crea_alerta_visible_por_api` | B | AN.`Una_perdida_de_peso_crea_una_alerta_visible_en_el_animal_y_en_el_rodeo`; prioridad numérica 1 y tipo `perdida_peso`, no prioridad textual `alta` por aumento |
| `test_variacion_chica_no_crea_alerta` | C | AN.`Un_aumento_normal_no_crea_alerta` |
| `test_historial_incluye_gmd` | C | AN.`El_historial_no_tiene_gmd_para_pesajes_del_mismo_dia` |
| `test_panel_del_rodeo_agrega_datos` | C | AN.`El_panel_agrega_el_ultimo_peso_y_la_gmd_de_cada_animal`; además de los agregados, evolución inicial de 350 kg |
| `test_reporte_filtra_por_lapso_de_tiempo` | C | AN.`El_reporte_filtra_por_periodo_inclusivo` |

### `tests/test_health.py` — 1

| Test original | Tipo | Prueba .NET |
| --- | --- | --- |
| `test_health_check_returns_ok` | C | SA.`Health_devuelve_ok`; E2E.`El_arranque_con_consumidor_activo_migra_y_precarga_antes_de_consultar_la_cola` también comprueba el cuerpo exacto `{"status":"ok"}` |

### Balance y límites de la prueba

**38/38 nombres mapeados: 26 C y 12 B.** No se encontró en esta lectura una regla pública Python conservada por B sin destino identificado. Eso no equivale a 38 ports literales ni certifica todos los casos posibles del sistema.

**Prueba original no trasladada literalmente:** `test_obtener_resultado_incluye_lote_y_peso_previo` comprobaba `resultado.peso_previo.id == primer_evento.id` en el servicio Python. El DTO .NET `PesoPrevioRespuesta` expone solo peso y fecha, igual que el JSON público Python (`UltimoPesoRead` en `app/routes/pesajes.py`), por lo que no hay prueba pública equivalente de ese id. La consulta actual ordena por fecha de pesaje y luego UUID (`Infraestructura/Pesajes/ConsultasPesajes.cs`), mientras el test HTTP usa fechas diferentes. **No hay prueba identificada del desempate entre eventos con idéntica fecha de captura**: la suite no afirma cubrirlo. La verificación independiente no lo consideró un requisito adicional ni un bloqueo funcional; no se agregó código ni se reinterpretó como requisito nuevo.

Los tests de analítica preparan algunos eventos por fixtures y luego consultan por HTTP; el E2E citado sí usa carga HTTP, almacenamiento local temporal, consumidor y evaluador reales con estimador controlado. No prueba precisión ML ni la conversión de una base real. Los casos sustituidos de imagen inválida, fallo y alertas porcentuales no deben restaurarse como expectativas antiguas incompatibles con S4/S7.

## Cambios para trasladar al TP7

Esta es una lista de cambios de diseño respaldados por código, no una afirmación de que los documentos externos ya se hayan modificado. Corresponde al padre registrar el cierre en `## Log` de ODD (S3). Las rutas de evidencia siguientes son relativas a `src/`.

| Artefacto a actualizar | Cambio propuesto y evidencia |
| --- | --- |
| Vista lógica y dependencias | Sustituir FastAPI/SQLAlchemy por .NET 10: Dominio sin frameworks, Aplicación con puertos, Infraestructura con EF/Npgsql y API como composición. Evidencia: proyectos `BovinoVision.*.csproj`, `Api/Program.cs`, `Api/Modulos/IModulo.cs` |
| Vista de módulos y comunicación | Registrar casos de uso y endpoints mediante `IModulo`, descubierto por reflexión; agregar módulo no requiere editar Program. Evidencia: `Api/Modulos/IModulo.cs` y los cuatro módulos implementados |
| Modelo ER: identidad y raza | UUID v7, tablas/columnas en español y snake_case; catálogo `Raza` en lugar de texto libre. Evidencia: `Dominio/Comun/Entidad.cs`, `Dominio/Trazabilidad/Raza.cs`, `Infraestructura/Persistencia/ConvencionesDeNombres.cs` y migración `Inicial` |
| Modelo ER: pertenencia a lotes | `Animal` posee `HistorialLote`; cada asignación tiene vigencia y usuario. Una única asignación abierta por animal, con índice único parcial. Evento referencia la asignación histórica, no el lote actual. Evidencia: `Dominio/Trazabilidad/Animal.cs`, `HistorialLote.cs`, `Infraestructura/Persistencia/Configuraciones/ConfiguracionesTrazabilidad.cs` |
| Modelo ER: capturas e imágenes | Agregado `Captura` con colección `Imagen`, referencia de archivo, MIME y huella SHA-256; evento referencia la imagen utilizada. Evidencia: `Dominio/Capturas/{Captura,Imagen}.cs`, `Aplicacion/Capturas/RegistrarCaptura.cs` |
| CU-12/CU-13: sesión y validación | Sesión del usuario actual, cierre y galería; rechazo 422 sin almacenamiento para foto inválida. MIME/tamaño no implican calidad visual. Evidencia: `Aplicacion/Capturas/` y `Api/Modulos/Capturas/ModuloCapturas.cs` |
| CU-14: secuencia asíncrona | Alta de captura `registrada`; solicitud explícita → `pendiente` → `procesando` → `procesada` o `fallida`. Agregar polling y motivo de fallo, intentos, reintentos y arrendamiento. Evidencia: `Dominio/Capturas/Captura.cs`, `Aplicacion/Pesajes/ProcesarSiguienteCaptura.cs`, `Infraestructura/Pesajes/{ColaInferencia,ServicioInferencia}.cs` |
| CU-14: transacciones | Reclamo corto independiente; no mantener transacción durante inferencia. Evento, alerta y transición final se confirman juntos, descartando resultados de reclamos vencidos. Evidencia: `Infraestructura/Pesajes/ColaInferencia.cs` y `Aplicacion/Pesajes/ProcesarSiguienteCaptura.cs` |
| CU-14/CU-15: evento y revisión | Solo estimación válida genera evento inmutable con fecha de captura y fecha de procesamiento separadas; confirmar solo revisa captura. Evidencia: `Dominio/Pesajes/EventoPesaje.cs`, `Aplicacion/Capturas/ConfirmarCaptura.cs` |
| Analítica/alertas y mockups | GMD calculada por fecha calendario; pérdida si GMD < 0, estancamiento bajo fracción del `gmd_objetivo` opcional. Actualizar prioridades numéricas y quitar umbral porcentual heredado. Evidencia: `Dominio/Analitica/{CalculadoraGmd,ReglasAlerta,ParametrosReglasAlerta}.cs` |
| Despliegue y transición | API .NET y PostgreSQL 17, almacenamiento local como adaptador; migración EF crea esquema nuevo, sin importación Alembic. No presentar retiro de fuentes como migración de datos. Evidencia: `Dockerfile`, `docker-compose.yml` (raíz), `Infraestructura/Persistencia/Migraciones/` |
| Usuarios y límites del prototipo | Semilla de establecimiento, rol, usuario Sistema y siete razas; no login real, auditoría ni sincronización offline. Estimador simulado no demuestra objetivo MAPE. Evidencia: `Infraestructura/Persistencia/DatosSemilla.cs`, `Infraestructura/Seguridad/ContextoUsuarioPorDefecto.cs`, `Infraestructura/Pesajes/ExtensionesPesajes.cs` |

### Patrones y principios: vocabulario para los diagramas

- **Arquitectura y patrones empresariales:** Clean Architecture, puertos y adaptadores, monolito modular, Repository/Unit of Work, CQRS liviano (lecturas proyectadas, sin infraestructura separada), agregados de DDD y cola persistida en capturas. No es una arquitectura de microservicios ni un bus de eventos.
- **Diseño y framework:** Adapter en almacenamiento/estimador, Null Object (`EvaluadorAlertasNulo`, reemplazado por el evaluador real de Analítica), Options e inyección por constructor. `EventoPesaje.Registrar` es fábrica estática, **no Factory Method de GoF**; el enum con transiciones validadas de captura **no es State de GoF**. Corrige la denominación histórica del Log L17 sin editar ese registro en esta entrega.
- **SOLID:** SRP separa invariantes y casos de uso; OCP permite módulos nuevos por `IModulo`; ISP mantiene puertos específicos; DIP hace depender adaptadores de puertos de Aplicación. LSP exige conservar contratos e invariantes al sustituir adaptadores/dobles: no basta con declarar interfaces. Ejemplos concretos en el [README](../README.md#arquitectura-y-principios-de-diseño) y `CLAUDE.md` local.

## Verificación y siguiente unidad

La entrega inicial observó salida 0 en `dotnet test --solution BovinoVision.slnx` (168/168), `dotnet build BovinoVision.slnx --no-restore` (0 errores/advertencias), `dotnet format BovinoVision.slnx --verify-no-changes --no-restore`, `docker compose config --quiet` y `git diff --check` (solo avisos LF→CRLF). La continuación modifica exclusivamente documentación: no se repiten suite/build/formato ni se hace un nuevo smoke de Docker.

La verificación independiente posterior confirmó 168/168 tests y salida 0 en compilación, formato y configuración de Compose, sin reproducir bloqueadores funcionales. No logró establecer la comparación diferencial HTTP contra la versión .NET anterior `4931e52`; ese resultado no demuestra ni regresión ni equivalencia. Esa comparación no aplica (ver el cierre más abajo). Los tres diagnósticos LSP que agotaron su tiempo siguen sin confirmación, aunque la compilación sí está confirmada.

## Cierre de la migración

La comparación diferencial sirve para detectar cambios no pedidos en comportamiento que ya existía. No aplica a estos módulos: en la base anterior (`5019548`) la API solo exponía `/health`, `4931e52` era un commit parcial de trabajo y no un contrato publicado, y las diferencias con el contrato Python son deliberadas (S7) y están mapeadas en la matriz de los 38 tests. Con la suite integrada en 168/168 se retiraron solo los archivos Python seguidos por git (`app/`, `alembic/`, `alembic.ini`, `pyproject.toml`, `uv.lock`, `.python-version`, `.env.example` y `tests/*.py`), más el bytecode generado que quedaba en esas carpetas. No se tocaron bases, volúmenes, uploads ni `.gitignore`. El código Python sigue disponible en el historial de git (rama `manu`).
