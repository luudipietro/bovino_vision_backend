# BovinoVision — Backend

API REST de trazabilidad, pesajes y analítica del rodeo en **C# / .NET 10**, con Clean Architecture y PostgreSQL 17. Proyecto Final de Ingeniería en Sistemas de Información (UTN-FRT).

> El estimador actual es **simulado**: siempre devuelve **400 kg**, versión `simulado-v1`. No hay un modelo de visión artificial de producción integrado ni validación real de calidad de las fotos. Tampoco hay autenticación. No desplegar como servicio productivo sin resolver estas limitaciones.

## Inicio local

Requisitos: SDK indicado en [`global.json`](global.json) (10.0.402, con `latestFeature`) y Docker activo. Ejecutar desde la raíz del backend, **sobre una base nueva compatible con EF Core**, no sobre la base Python existente.

```bash
docker compose up -d db
dotnet build BovinoVision.slnx
dotnet run --project src/BovinoVision.Api
```

La API escucha en `http://localhost:8000`. El perfil local activa `Development`, usa PostgreSQL en `localhost:5433` y aplica las migraciones al iniciar. En ese entorno están disponibles `/openapi/v1.json` y `/scalar/v1`. `GET /health` devuelve `{"status":"ok"}`; no es una comprobación de conectividad con la base.

Alternativa en contenedores:

```bash
docker compose --profile api up -d --build
```

Compose publica la API en 8000 y PostgreSQL en 5433; conserva datos en `postgres-data` e imágenes en `uploads`. Las credenciales incluidas son solo para desarrollo. El contenedor ejecuta como usuario `app` y Compose habilita explícitamente las migraciones; no habilita la documentación de Development.

**Transición de datos:** la migración EF `Inicial` crea el esquema nuevo; **no convierte datos de Python/Alembic**, ni ids enteros a UUID. Si ya existe un volumen/base anterior, no iniciar la API nueva contra él: respaldarlo y planificar una importación validada hacia una base separada. No borrar volúmenes, bases ni imágenes para completar la migración de fuentes. Véase [el informe de migración](docs/migracion-dotnet.md).

## Contrato y flujo de pesaje

El JSON usa nombres españoles en `snake_case` y enums como `hembra`, `pendiente` o `procesada`. Los ids son cadenas UUID v7, no enteros. Por ejemplo, el alta mínima sigue siendo:

```bash
curl -X POST http://localhost:8000/animales -H "Content-Type: application/json" -d '{"identificador":"AR001"}'
curl http://localhost:8000/animales
```

Para asociar lote o raza, usar `id_lote` e `id_raza` con los UUID devueltos por `/lotes` y `/razas`; `raza` ya no es una entrada de texto libre.

1. `POST /sesiones-captura` sin cuerpo devuelve 201 y el `id` de la sesión.
2. `POST /capturas`, multipart con `id_sesion`, `id_animal`, `imagen` y opcionalmente `fecha_captura` (fecha ISO 8601 con desplazamiento), devuelve 201 y el `id` de captura. Se aceptan JPEG, PNG y WebP por tipo MIME, no vacíos, hasta 10 MiB. Una imagen inválida devuelve 422 y no se guarda.
3. `POST /pesajes` con `{"id_captura":"UUID-DE-LA-CAPTURA"}` devuelve **202 sin cuerpo**, con `Location: /capturas/{id}`. No devuelve el peso en esa respuesta.
4. Consultar periódicamente `GET /capturas/{id}` hasta `procesada` (leer `id_evento_pesaje` y consultar `GET /pesajes/{id_evento_pesaje}`) o `fallida` (mostrar `motivo_fallo`). Mientras esté `pendiente` o `procesando`, seguir esperando con un plazo acotado en el cliente.
5. `POST /capturas/{id}/confirmar` registra la revisión de un resultado procesado, sin modificar ni crear pesajes. Repetir la solicitud de pesaje devuelve 409.

La fecha del pesaje es la de captura, no la de procesamiento. Solo una estimación válida crea un evento inmutable. Las fallas quedan en la captura, sin evento. [Resumen de todas las rutas y cambios de contrato](docs/migracion-dotnet.md#rutas-públicas).

**El frontend requiere adaptación posterior**, fuera de esta migración: UUID, nombres españoles, catálogo de razas y consulta asíncrona. No es compatible sin cambios con el contrato Python.

## Arquitectura y principios de diseño

Dependencias: `Dominio` ← `Aplicacion` ← `Infraestructura` ← `Api`. La API compone los servicios y usa casos de uso de Aplicación; el Dominio no depende de frameworks.

| Capa | Responsabilidad |
| --- | --- |
| `src/BovinoVision.Dominio` | Entidades, invariantes, historial de lotes, transiciones de captura, GMD y reglas de alerta |
| `src/BovinoVision.Aplicacion` | Casos de uso, DTOs y puertos pequeños por módulo; unidad de trabajo |
| `src/BovinoVision.Infraestructura` | EF Core/Npgsql, repositorios, consultas, almacenamiento local y consumidor de inferencia |
| `src/BovinoVision.Api` | Minimal APIs, descubrimiento de `IModulo`, configuración y ProblemDetails |

**SOLID:** SRP separa casos de uso e invariantes; OCP permite agregar módulos mediante `IModulo`; ISP divide los puertos por responsabilidad; DIP hace que Aplicación defina los puertos y los adaptadores los implementen. LSP exige respetar esos contratos al reemplazar adaptadores; los dobles de tests permiten ejercitar sustituciones, no constituyen una prueba formal del principio.

**Patrones de arquitectura y empresariales:** Clean Architecture, puertos y adaptadores, monolito modular, Repository y Unit of Work (repositorios preparan cambios), CQRS liviano (lecturas proyectadas a DTOs, sin buses separados), agregados de DDD (`Animal`/`HistorialLote`, `Captura`/`Imagen`) y cola persistida en capturas. La publicación del evento y su alerta se confirma en una misma unidad de trabajo; el reclamo de cola usa una transacción corta independiente.

**Patrones de diseño y de framework:** Adapter en los límites de almacenamiento y estimación, Null Object en `EvaluadorAlertasNulo` (reemplazado por el evaluador real al componer Analítica), Options e inyección por constructor. `EventoPesaje.Registrar` es una **fábrica estática**, no Factory Method de GoF; el ciclo de `Captura` usa enum y transiciones validadas, no el patrón State de GoF.

## Configuración

ASP.NET Core lee `appsettings.json`, el archivo del entorno y variables de entorno (`__` separa niveles). **`.env.example` permanece heredado y sin cambios; no es una plantilla migrada a .NET.** Su actualización quedó diferida por el bloqueo de seguridad de la herramienta. Usar las claves de esta sección, no las instrucciones Python de esa plantilla. **`dotnet run` no carga `.env` automáticamente** y Compose no inyecta todas sus claves por tener ese archivo. Exportar las variables al proceso o configurar explícitamente el servicio. No guardar credenciales reales en el repositorio.

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
| `Cors__OrigenesPermitidos__0` | `http://localhost:5173` |
| `Almacenamiento__DirectorioImagenes` | `uploads`, relativo a la raíz de contenido de la aplicación; `/app/uploads` en Compose |
| `Inferencia__ProcesarEnSegundoPlano` | `true`; con `false` las solicitudes quedan en cola sin este consumidor |
| `Inferencia__MaximoIntentos` | `3` |
| `Inferencia__DemoraReintentoSegundos` | `30` |
| `Inferencia__ArrendamientoSegundos` | `120` (también limita la espera del estimador) |
| `Inferencia__IntervaloSegundos` | `1` (consulta del consumidor, no instrucción de polling del cliente) |
| `Analitica__ZonaHoraria` | `America/Argentina/Buenos_Aires` |
| `Alertas__FraccionEstancamiento` | `0.5` |
| `Alertas__PrioridadPerdida` / `Alertas__PrioridadEstancamiento` | `1` / `2` |

Los límites e intervalos de inferencia deben ser positivos. No se usa ya `DATABASE_URL` ni `DEFAULT_ESTABLECIMIENTO_ID`: `ContextoUsuarioPorDefecto` selecciona el establecimiento y usuario Sistema de la semilla, sin login real.

La GMD se calcula al consultar, por días calendario de la zona configurada; sin intervalo válido es nula. Hay alerta `perdida_peso` si la GMD es negativa; `estancamiento` si queda por debajo de la fracción del `gmd_objetivo` opcional del lote histórico. Ya no se usa el umbral Python de variación porcentual de 15/30%.

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

La fábrica de diseño usa `ConnectionStrings__BovinoVision` o la conexión local por defecto. Revisar toda migración antes de aplicarla; no ejecutar cambios de esquema contra datos ajenos sin un plan de respaldo y transición.

## Alcance pendiente

Estimador real, calidad de imagen, autenticación/roles, auditoría y sincronización offline sin duplicados siguen pendientes. No hay indicadores sanitarios, de grasa, musculatura ni precio. Los módulos .NET están integrados y probados. El cierre de la migración y el retiro de fuentes Python quedan pendientes de una decisión sobre la comparación HTTP contra la versión anterior, que no se logró establecer; la actualización de la plantilla protegida también sigue pendiente. Ver [contexto del proyecto](context.md), [matriz de los 38 tests y límites de prueba](docs/migracion-dotnet.md#matriz-de-los-38-tests-originales) y [seguimiento ODD](odd/tasks/migracion-dotnet.md).
