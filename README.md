# BovinoVision — Backend

API REST de trazabilidad, pesajes y analítica del rodeo en **C# / .NET 10**, con Clean Architecture y PostgreSQL 17. Proyecto Final de Ingeniería en Sistemas de Información (UTN-FRT).

> Sin configurar `Estimador__UrlBase`, el estimador es **simulado**: siempre devuelve **400 kg**, versión `simulado-v1`. Con esa clave, el backend consume el servicio de estimación del equipo (`servicio-estimacion` del repo `Estimador_Bovino_V2`; ver [Estimador de peso](#estimador-de-peso)). El modelo solo es confiable dentro de su rango de entrenamiento: no desplegar como servicio productivo sin validar su precisión.

## Inicio local

Requisitos: SDK indicado en [`global.json`](global.json) (10.0.402, con `latestFeature`) y Docker activo. Ejecutar desde la raíz del backend, **sobre una base nueva compatible con EF Core**, no sobre la base Python existente.

```bash
docker compose up -d db
dotnet build BovinoVision.slnx
dotnet run --project src/BovinoVision.Api
```

La API escucha en `http://localhost:8000`. El perfil local activa `Development`, usa PostgreSQL en `localhost:5433` salvo que se sobrescriba la conexión y aplica las migraciones al iniciar. En ese entorno están disponibles `/openapi/v1.json` y `/scalar/v1`. `GET /health` devuelve `{"status":"ok"}`; no es una comprobación de conectividad con la base.

**Alternativa con Neon:** la API admite User Secrets con ID `bovinovision-api-neon-local` en Development. Con `ConnectionStrings:BovinoVision` configurada fuera del repositorio, usar una conexión **directa** exclusivamente a `develop` / `neondb`; el arranque aplica las migraciones existentes. Las variables de entorno prevalecen sobre los secretos. User Secrets almacena texto plano, no cifra credenciales. Consultar [la guía local de Neon](docs/neon-desarrollo-local.md); no modifica `production`, `neon_auth` ni los tests con Testcontainers.

Alternativa en contenedores:

```bash
docker compose --profile api up -d --build
```

Compose publica la API en 8000 y PostgreSQL en 5433; conserva datos en `postgres-data` e imágenes en `uploads`. Las credenciales incluidas son solo para desarrollo. El contenedor ejecuta como usuario `app` y Compose habilita explícitamente las migraciones; no habilita la documentación de Development.

**Transición de datos:** la migración EF `Inicial` crea el esquema nuevo; **no convierte datos de Python/Alembic**, ni ids enteros a UUID. Si ya existe un volumen/base anterior, no iniciar la API nueva contra él: respaldarlo y planificar una importación validada hacia una base separada. No borrar volúmenes, bases ni imágenes para completar la migración de fuentes. Véase [el informe de migración](docs/migracion-dotnet.md).

## Inicio de sesión y permisos

Todos los endpoints, salvo `GET /health` y `POST /auth/sesion`, exigen una sesión. El diseño completo está en [docs/autenticacion.md](docs/autenticacion.md).

1. **Primer administrador.** Configurarlo fuera del repositorio (User Secrets en Development o variables de entorno); se crea al arrancar si no existe un usuario con ese correo:

   ```powershell
   dotnet user-secrets set "Autenticacion:AdministradorInicial:Correo" "admin@ejemplo.com" --project src/BovinoVision.Api
   dotnet user-secrets set "Autenticacion:AdministradorInicial:Nombre" "Administrador" --project src/BovinoVision.Api
   dotnet user-secrets set "Autenticacion:AdministradorInicial:Contrasena" "<al menos 8 caracteres>" --project src/BovinoVision.Api
   ```

2. **Inicio de sesión.** `POST /auth/sesion` con `{ "correo", "contrasena" }` devuelve el usuario, sus permisos y `expira_en`, y deja la cookie `bovinovision_sesion` (`HttpOnly`, `Secure`, `SameSite=Strict`). El cliente debe enviar `credentials: "include"`. Respuestas de error: 401 credenciales incorrectas, 429 con `Retry-After` tras 5 intentos fallidos (bloqueo de 5 minutos), 403 cuenta dada de baja.
3. **Sesión.** `GET /auth/sesion` devuelve la sesión actual y `DELETE /auth/sesion` la cierra (204). Sin sesión válida: 401; sin el permiso del endpoint: 403. Los cambios de rol, de permisos, de contraseña y las bajas se aplican en el request siguiente.

| Rol inicial | Permisos |
| --- | --- |
| Administrador | `usuarios_gestionar`, `roles_gestionar`, `auditoria_consultar` |
| Productor Ganadero | `rodeo_gestionar`, `rodeo_consultar`, `analitica_consultar` |
| Operario de Campo | `capturas_registrar`, `rodeo_gestionar`, `rodeo_consultar` |

Usuarios y roles: `GET/POST /usuarios`, `PUT /usuarios/{id}`, `PUT /usuarios/{id}/rol`, `POST /usuarios/{id}/baja` (`usuarios_gestionar`); `GET /roles`, `GET /permisos` (`usuarios_gestionar` o `roles_gestionar`); `PUT /roles/{id}/permisos` (`roles_gestionar`). Ninguna operación puede dejar el sistema sin un usuario activo con `roles_gestionar`.

## Contrato y flujo de pesaje

El JSON usa nombres españoles en `snake_case` y enums como `hembra`, `pendiente` o `procesada`. Los ids son cadenas UUID v7, no enteros. Por ejemplo, el alta mínima sigue siendo:

```bash
curl -X POST http://localhost:8000/animales -H "Content-Type: application/json" -d '{"identificador":"AR001"}'
curl http://localhost:8000/animales
```

Para asociar lote o raza, usar `id_lote` e `id_raza` con los UUID devueltos por `/lotes` y `/razas`; `raza` ya no es una entrada de texto libre.

1. `POST /sesiones-captura` sin cuerpo devuelve 201 y el `id` de la sesión.
2. `POST /capturas`, multipart con `id_sesion`, `id_animal`, `imagen` y opcionalmente `fecha_captura` (fecha ISO 8601 con desplazamiento), devuelve 201 y el `id` de captura. Se aceptan JPEG y PNG por tipo MIME (los formatos que admite el estimador), no vacíos, hasta 10 MiB. Una imagen inválida devuelve 422 y no se guarda. `fecha_captura` no puede superar el momento actual en más de 5 minutos (tolerancia de reloj del dispositivo); si lo hace, devuelve 422.
3. `POST /pesajes` con `{"id_captura":"UUID-DE-LA-CAPTURA"}` devuelve **202 sin cuerpo**, con `Location: /capturas/{id}`. No devuelve el peso en esa respuesta.
4. Consultar periódicamente `GET /capturas/{id}` hasta `procesada` (leer `id_evento_pesaje` y consultar `GET /pesajes/{id_evento_pesaje}`) o `fallida` (mostrar `motivo_fallo`). Mientras esté `pendiente` o `procesando`, seguir esperando con un plazo acotado en el cliente.
5. `DELETE /capturas/{id}` descarta una captura que sigue `registrada` (foto subida pero no enviada a estimar): borra la captura, su imagen y el archivo, y devuelve 204. Devuelve 404 si no existe y 422 si ya está en otro estado.
6. `POST /capturas/{id}/confirmar` registra la revisión de un resultado procesado, sin modificar ni crear pesajes. Repetir la solicitud de pesaje devuelve 409.
7. `GET /imagenes/{id}` devuelve el archivo de la foto con el `Content-Type` con el que se subió y `Cache-Control: private`. Exige sesión y `capturas_registrar` o `rodeo_consultar` (sin sesión, 401; sin ninguno de los dos permisos, 403). Solo sirve fotos de capturas de animales del establecimiento de la sesión: una imagen inexistente, de otro establecimiento o cuyo archivo ya no está devuelve 404 con ProblemDetails. El `id` de la imagen sale de `id_imagen`, campo de `GET /pesajes/{id}` (la imagen utilizada por el evento) y de cada elemento de `GET /sesiones-captura/{id}/capturas` (la primera imagen de la captura, ordenada por id); el historial de pesajes ya trae `id_imagen_utilizada`.
8. `GET /sesiones-captura?limite=N&desplazamiento=M` (`capturas_registrar`) lista las sesiones de captura del usuario de la sesión, de la más nueva a la más vieja: `[{ id, fecha_inicio, fecha_fin, estado, cantidad_capturas, cantidad_procesadas }]`. `limite` va de 1 a 20 (5 por defecto); fuera de ese rango responde 422 con ProblemDetails, y 400 si no es un número. `desplazamiento` (0 por defecto) saltea las M más nuevas para paginar el historial; si es negativo responde 422. `cantidad_procesadas` cuenta las capturas en estado `procesada`. Los filtros opcionales `desde` y `hasta` aceptan instantes ISO 8601 con zona horaria y filtran por `fecha_inicio` **antes** de paginar: `desde` es inclusivo y `hasta` exclusivo (por ejemplo, `desde=2026-10-24T03:00:00Z&hasta=2026-10-27T03:00:00Z` incluye los días 24 al 26 en UTC−3). Se puede enviar un solo extremo; un rango con `desde >= hasta` devuelve 422 y una fecha mal formada, 400. Sin filtros se conserva el comportamiento anterior.

La fecha del pesaje es la de captura, no la de procesamiento. Solo una estimación válida crea un evento inmutable. Las fallas quedan en la captura, sin evento. [Resumen de todas las rutas y cambios de contrato](docs/migracion-dotnet.md#rutas-públicas).

**El frontend requiere adaptación posterior**, fuera de esta migración: UUID, nombres españoles, catálogo de razas y consulta asíncrona. No es compatible sin cambios con el contrato Python.

## Arquitectura y principios de diseño

Dependencias: `Dominio` ← `Aplicacion` ← `Infraestructura` ← `Api`. La API compone los servicios y usa casos de uso de Aplicación; el Dominio no depende de frameworks.

| Capa | Responsabilidad |
| --- | --- |
| `src/BovinoVision.Dominio` | Entidades, invariantes, historial de lotes, transiciones de captura, GMD y reglas de alerta |
| `src/BovinoVision.Aplicacion` | Casos de uso, DTOs y puertos pequeños por módulo; unidad de trabajo |
| `src/BovinoVision.Infraestructura` | EF Core/Npgsql, repositorios, consultas, almacenamiento local y consumidor de inferencia |
| `src/BovinoVision.Api` | Minimal APIs, descubrimiento de `IModulo`, configuración y ProblemDetails (400 por parámetros faltantes o mal formados en todos los entornos; 404, 409 y 422 por errores de negocio) |

**SOLID:** SRP separa casos de uso e invariantes; OCP permite agregar módulos mediante `IModulo`; ISP divide los puertos por responsabilidad; DIP hace que Aplicación defina los puertos y los adaptadores los implementen. LSP exige respetar esos contratos al reemplazar adaptadores; los dobles de tests permiten ejercitar sustituciones, no constituyen una prueba formal del principio.

**Patrones de arquitectura y empresariales:** Clean Architecture, puertos y adaptadores, monolito modular, Repository y Unit of Work (repositorios preparan cambios), CQRS liviano (lecturas proyectadas a DTOs, sin buses separados), agregados de DDD (`Animal`/`HistorialLote`, `Captura`/`Imagen`) y cola persistida en capturas. La publicación del evento y su alerta se confirma en una misma unidad de trabajo; el reclamo de cola usa una transacción corta independiente.

**Patrones de diseño y de framework:** Adapter en los límites de almacenamiento y estimación, Null Object en `EvaluadorAlertasNulo` (reemplazado por el evaluador real al componer Analítica), Options e inyección por constructor. `EventoPesaje.Registrar` es una **fábrica estática**, no Factory Method de GoF; el ciclo de `Captura` usa enum y transiciones validadas, no el patrón State de GoF.

## Configuración

ASP.NET Core lee, en orden de menor a mayor prioridad, `appsettings.json`, el archivo del entorno, User Secrets en Development (ID `bovinovision-api-neon-local`), variables de entorno (`__` separa niveles) y argumentos de línea de comandos. No hay plantilla `.env`: la del backend Python se retiró junto con su código, y **`dotnet run` no carga archivos `.env`**. Exportar las variables al proceso o configurar explícitamente el servicio. No guardar credenciales reales en el repositorio.

Ejemplo sin secretos para PowerShell (variables del proceso actual):

```powershell
$env:Inferencia__MaximoIntentos = "3"
$env:Analitica__ZonaHoraria = "America/Argentina/Buenos_Aires"
dotnet run --project src/BovinoVision.Api
```

| Clave de entorno | Valor predeterminado / efecto |
| --- | --- |
| `ConnectionStrings__BovinoVision` | Vacía en configuración base; Development usa PostgreSQL local en 5433; Compose usa `db:5432` |
| `BaseDeDatos__AplicarMigracionesAlIniciar` | `false`; `true` en Development y Compose |
| `Cors__OrigenesPermitidos__0` | `http://localhost:5173`; Development también permite `http://localhost:5174` (`__1`), el puerto que toma Vite cuando 5173 está ocupado |
| `Almacenamiento__DirectorioImagenes` | `uploads`, relativo a la raíz de contenido de la aplicación; `/app/uploads` en Compose |
| `Inferencia__ProcesarEnSegundoPlano` | `true`; con `false` las solicitudes quedan en cola sin este consumidor |
| `Inferencia__MaximoIntentos` | `3` |
| `Inferencia__DemoraReintentoSegundos` | `30` |
| `Inferencia__ArrendamientoSegundos` | `120` (también limita la espera del estimador) |
| `Inferencia__IntervaloSegundos` | `1` (consulta del consumidor, no instrucción de polling del cliente) |
| `Estimador__UrlBase` | Vacía: estimador simulado. Con valor (por ejemplo `http://localhost:8001`), usa el servicio de estimación |
| `Estimador__Token` | Sin valor; obligatorio si hay `UrlBase`. Es el `TOKEN_SERVICIO` del estimador: guardarlo en User Secrets o en el entorno |
| `Estimador__IntervaloConsultaMilisegundos` | `1000`; espera entre consultas del estado de un trabajo |
| `Analitica__ZonaHoraria` | `America/Argentina/Buenos_Aires` |
| `Autenticacion__DuracionSesionHoras` | `12` |
| `Autenticacion__AdministradorInicial__Correo`, `__Nombre`, `__Contrasena` | Sin valor; si hay correo, crea ese administrador al arrancar cuando no existe. Contraseña de al menos 8 caracteres |
| `Alertas__FraccionEstancamiento` | `0.5` |
| `Alertas__PrioridadPerdida` / `Alertas__PrioridadEstancamiento` | `1` / `2` |

Los límites e intervalos de inferencia deben ser positivos. No se usa ya `DATABASE_URL` ni `DEFAULT_ESTABLECIMIENTO_ID`: el usuario y el establecimiento de cada operación salen de la sesión. El usuario Sistema de la semilla queda inactivo y solo conserva la autoría de los datos anteriores al login.

La GMD se calcula al consultar, por días calendario de la zona configurada; sin intervalo válido es nula. Hay alerta `perdida_peso` si la GMD es negativa; `estancamiento` si queda por debajo de la fracción del `gmd_objetivo` opcional del lote histórico. Ya no se usa el umbral Python de variación porcentual de 15/30%.

`POST /alertas/{id}/resolver` (permiso `rodeo_gestionar`) cierra una alerta: responde 204 sin cuerpo, la alerta pasa de `activa` a `resuelta` y deja de aparecer en `GET /alertas`. Devuelve 404 si no existe o es de otro establecimiento y 409 si ya estaba resuelta. No requiere migración: usa el estado que la alerta ya guardaba.

Campos calculados al consultar (sin migración, nunca se guardan):

- `operario` (texto o `null`) en `GET /pesajes/{id}` y en cada elemento de `GET /animales/{id}/pesajes`: el nombre del usuario dueño de la sesión de captura del pesaje.
- `gmd_kg_dia` (número o `null`) en cada elemento de `GET /animales` y en `GET /animales/{id}`: la GMD entre los dos últimos pesajes del animal, con la misma regla del historial (días calendario de la zona configurada). Es `null` con menos de dos pesajes o si ambos son del mismo día.
- `ultimo_acceso` (fecha-hora o `null`) en los usuarios de `GET /usuarios` y en toda respuesta que devuelva el mismo usuario (alta, `PUT /usuarios/{id}`, `PUT /usuarios/{id}/rol`, baja, `POST` y `GET /auth/sesion`): la creación de su sesión de inicio de sesión más reciente. Es `null` si nunca ingresó.

## Estimador de peso

El adaptador `EstimadorPesoHttp` consume la API asincrónica del servicio de estimación: envía la foto de la captura por multipart a `POST /v1/estimaciones` (con `Authorization: Bearer <token>` e `Idempotency-Key` = id de captura + número de intento) y consulta `GET /v1/estimaciones/{id}` hasta que el trabajo queda `completado` o `fallido`. La concesión de la cola (`Inferencia__ArrendamientoSegundos`) limita la espera total.

| Respuesta del servicio | Resultado en la captura |
| --- | --- |
| `completado` con `confiable: true` | `procesada`, con un evento de pesaje (peso redondeado a 2 decimales y `version_modelo` del servicio) |
| `completado` con `confiable: false` | `fallida`, sin evento; `motivo_fallo` lista los motivos del modelo (sin animal, fuera de rango, etc.) |
| `fallido` | Se reintenta si el servicio lo marca `reintentable`; si no, `fallida` |
| 401, 403, 404, 408, 429, 5xx, red caída o JSON inválido | Se reintenta hasta `Inferencia__MaximoIntentos` |
| Otro 4xx (formato, resolución insuficiente, etc.) | `fallida`, sin reintento |

Para usarlo en local:

1. En `Estimador_Bovino_V2` (rama `main`), descargar los pesos con `python estimador/descargar_modelos.py` y seguir `servicio-estimacion/AGENTS.md` (Python 3.13 y `uv`). Para el rodeo propio conviene el modelo v2 (`CARPETA_MODELOS=../estimador/modelos_v2`, `RESOLUCION_MINIMA_MP=0.5`), cuyos pesos se pasan aparte.
2. Arrancar el servicio en el puerto 8001 con un `TOKEN_SERVICIO` y un `SECRETO_HMAC` propios.
3. Configurar el backend con el mismo token, sin escribirlo en el repositorio:

   ```bash
   dotnet user-secrets set "Estimador:UrlBase" "http://localhost:8001" --project src/BovinoVision.Api
   dotnet user-secrets set "Estimador:Token" "<TOKEN_SERVICIO>" --project src/BovinoVision.Api
   ```

Con `UrlBase` y sin `Token` (o con una URL que no es http/https absoluta), la API no arranca. En Compose, las variables `ESTIMADOR_URL_BASE` y `ESTIMADOR_TOKEN` del entorno alimentan `Estimador__UrlBase` y `Estimador__Token`; desde el contenedor, el servicio del host se alcanza en `http://host.docker.internal:8001`.

## Verificación y migraciones

```bash
dotnet test --solution BovinoVision.slnx
dotnet build BovinoVision.slnx --no-restore
dotnet format BovinoVision.slnx --verify-no-changes --no-restore
docker compose config --quiet
```

Tests con xUnit v3 y Microsoft Testing Platform (no VSTest): Dominio y Aplicación usan dobles en memoria; API e integración levantan PostgreSQL 17 mediante Testcontainers y necesitan Docker activo. No usan la base de desarrollo. Las pruebas de almacenamiento usan directorios temporales. Los warnings de compilación son errores.

Para generar una migración nueva (no convierte datos heredados):

```bash
dotnet tool restore
dotnet ef migrations add NombreDelCambio -p src/BovinoVision.Infraestructura -s src/BovinoVision.Infraestructura -o Persistencia/Migraciones
```

La fábrica de diseño usa solo `ConnectionStrings__BovinoVision` o la conexión local por defecto, no User Secrets: `dotnet ef database update` no usa los secretos de la API. Para aplicar migraciones existentes con User Secrets, usar el arranque en Development descrito en [la guía de Neon](docs/neon-desarrollo-local.md). Revisar toda migración antes de aplicarla; no ejecutar cambios de esquema contra datos ajenos sin un plan de respaldo y transición.

## Alcance pendiente

La validación de calidad de imagen en el backend (la hace el estimador), la auditoría y sincronización offline sin duplicados siguen pendientes. No hay indicadores sanitarios, de grasa, musculatura ni precio. Los módulos .NET están integrados y probados, y el código Python se retiró: sigue disponible en el historial de git (por ejemplo, en la rama `manu`). Ver [contexto del proyecto](context.md), [matriz de los 38 tests y límites de prueba](docs/migracion-dotnet.md#matriz-de-los-38-tests-originales) y [seguimiento ODD](odd/tasks/migracion-dotnet.md).
