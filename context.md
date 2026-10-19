# Contexto del backend — BovinoVision

Estado al 19/10/2026, fin de la primera sesión de desarrollo. Esto no reemplaza al
`README.md` (para correrlo o testearlo, ver ahí); es un resumen de decisiones y alcance
para retomar el trabajo sin tener que releer todo el historial de la conversación.

## Qué es esto

API REST (FastAPI) del sistema BovinoVision: estimación de peso de bovinos por visión
artificial a partir de fotos de celular, más trazabilidad y analítica del rodeo. Proyecto
final UTN. El contexto completo del proyecto (acta, requisitos, cronograma, diseño) está
en `../Documentacion/`.

## Alcance acordado para este MVP

- **Release 1 (21/10/2026):** Captura Móvil + Procesamiento e Inferencia de Peso +
  Gestión y Trazabilidad. **Implementado.**
- **Release Final (02/11/2026):** lo anterior + Analítica y Reportes (GMD, alertas,
  panel, reportes). **Implementado.**
- **Pospuesto para después del Release Final (fuera de este MVP):** Gestión de Usuarios
  (login/roles/permisos) y Auditoría. Por eso no hay autenticación todavía: todo opera
  sobre un único `Establecimiento` precargado (`Settings.default_establecimiento_id`,
  hoy = 1).
- **RF-16/RF-17 (modo offline + sincronización de capturas)** tampoco están
  implementados todavía.

## Arquitectura (por qué está así)

Capas separadas por responsabilidad (el README detalla qué hay en cada carpeta):
`domain` (entidades + puertos abstractos) → `services` (casos de uso) → `repositories`
(implementación concreta de los puertos de persistencia, con SQLAlchemy) / `estimators`,
`storage` (implementación concreta de puertos externos) → `routes` (FastAPI,
presentación) → `dependencies.py` (wiring de inyección de dependencias). El objetivo
explícito del usuario fue poder reemplazar piezas sin tocar el resto:

- El estimador de peso real (en desarrollo en otro repo aparte) reemplaza a
  `MockWeightEstimatorAdapter` el día que esté listo, implementando `WeightEstimatorPort`
  y cambiando un único provider (`get_weight_estimator`) en `dependencies.py`.
- El almacenamiento de imágenes (hoy disco local en `uploads/`, vía
  `LocalImageStorageAdapter`) se reemplaza igual de simple por un adaptador de bucket
  cuando se despliegue al VPS, implementando `ImageStoragePort`.

## Simplificaciones deliberadas (y por qué)

- **Un solo establecimiento hardcodeado:** no hay multi-tenant porque no hay módulo de
  Usuarios todavía. `get_establecimiento_id()` en `dependencies.py` es el único lugar a
  tocar cuando eso cambie.
- **Validación de imagen simplificada:** `CapturaService` solo valida tipo de archivo y
  tamaño, no calidad real (nitidez, encuadre, animal detectable) — eso depende del mismo
  servicio de visión artificial que el estimador, todavía no integrado.
- **Sin tabla `Raza`:** es un campo de texto libre en `Animal`, no hay catálogo.
- **Sin endpoint para crear `Establecimiento`:** se precarga por seed en la primera
  migración (`alembic/versions/24f176bca5d6_crear_tablas_establecimientos_lotes_.py`).
- **GMD y alertas por día, no por evento:** si dos pesajes caen el mismo día calendario,
  no se puede calcular GMD entre ellos (coincide con lo que exige el propio documento de
  diseño del proyecto: "sin datos suficientes se indica que no está disponible").
- **Umbral de alerta:** variación ≥15% dispara alerta (prioridad "alta" desde 30%). Es un
  valor que elegí yo, no viene de un requisito numérico — ajustable en
  `app/services/alerta_service.py`.
- **No se implementaron "estado sanitario" ni indicadores de grasa/musculatura/precio**,
  aunque aparecen en algunos mockups: el propio documento de diseño
  (`TP7_Parte_Avance_de_Diseño_V3.pdf`, página 6) aclara explícitamente que esos
  indicadores NO forman parte del alcance de este proyecto.

## Próximos pasos razonables (ninguno arrancado todavía)

1. Dockerizar en serio para el VPS de DonWeb (hoy solo hay un `Dockerfile` básico, sin
   probar en producción, y un `docker-compose.yml` en la raíz que solo levanta Postgres
   para desarrollo local).
2. Reemplazar `MockWeightEstimatorAdapter` por el adaptador real cuando el otro repo del
   estimador esté listo.
3. Módulo de Usuarios (login, roles, permisos) + Auditoría — recién después del Release
   Final, según lo acordado con el usuario.
4. Modo offline (RF-16/RF-17): cola local de capturas pendientes + sincronización.

## Flujo de trabajo con el usuario

Claude no ejecuta `git add` / `commit` / `push` en este repo — el usuario revisa cada
avance y commitea él mismo, a su ritmo, paso a paso.
