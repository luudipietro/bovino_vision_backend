# Contexto del backend — BovinoVision

El backend activo de la rama `feat/migracion-dotnet` está implementado en **C# / .NET 10**, con Clean Architecture, EF Core y PostgreSQL 17. Las instrucciones operativas están en el [README](README.md); las decisiones y la trazabilidad del cierre, en [el informe de migración](docs/migracion-dotnet.md) y [ODD](odd/tasks/migracion-dotnet.md).

## Alcance real

- Trazabilidad: animales, lotes, catálogo de siete razas e historial de pertenencia a lotes.
- Capturas: sesiones, carga de imágenes, galería, cierre de sesión y revisión de resultados.
- Pesajes: cola asíncrona persistida con reclamos exclusivos, arrendamiento, recuperación y reintentos. Eventos inmutables y alerta en la misma unidad de trabajo de publicación.
- Analítica: historial con GMD calculada, alertas, panel y reportes con filtro temporal y lote histórico.

**No confundir implementación del flujo con disponibilidad de ML:** el adaptador `EstimadorPesoSimulado` devuelve siempre 400 kg (`simulado-v1`). La validación de foto comprueba tipo MIME, contenido no vacío y tamaño (10 MiB), no calidad visual ni presencia de un bovino.

## Decisiones que deben preservarse

1. Dependencias hacia el dominio: `Dominio` ← `Aplicacion` ← `Infraestructura` ← `Api`. Los puertos son pequeños y se definen en Aplicación; `IModulo` permite registrar funcionalidades sin modificar `Program.cs`.
2. UUID v7, vocabulario español y JSON/columnas en `snake_case`. Raza se referencia por `id_raza`, no por texto libre. El cliente necesita una adaptación posterior fuera de esta feature.
3. `POST /pesajes` devuelve 202 sin cuerpo; el cliente consulta `/capturas/{id}` hasta `procesada` o `fallida`. Una falla no genera un evento de pesaje. Una imagen inválida se rechaza con 422 sin guardarse.
4. La fecha de pesaje es la de captura. Cada evento referencia la asignación de lote vigente en esa fecha. Confirmar solo registra la revisión en la captura; nunca modifica eventos.
5. GMD por días calendario en `America/Argentina/Buenos_Aires` por defecto, sin persistirla. Dos pesajes del mismo día no producen GMD. Las alertas usan pérdida de peso (GMD negativa) y estancamiento (por debajo de 0,5 del objetivo del lote, configurable); no porcentajes Python de 15/30%.
6. La semilla crea establecimiento, rol, usuario Sistema y razas. `ContextoUsuarioPorDefecto` selecciona ese contexto: no equivale a autenticación ni a aislamiento multiusuario completo.

Los [patrones del README](README.md#arquitectura-y-principios-de-diseño) se separan entre arquitectura/empresariales y diseño/framework. La fábrica estática de eventos no es Factory Method de GoF; las transiciones de captura no implementan State de GoF.

## Datos y ejecución

La migración EF inicial es un esquema nuevo, **no un importador de datos Python**. Mantener respaldos y bases separadas hasta diseñar y validar una conversión de ids, razas, imágenes, historial y fallas. No borrar bases, volúmenes ni `uploads` como parte del retiro de fuentes. La evidencia de tests no certifica una transición de datos reales ni un despliegue productivo.

Docker Compose ofrece PostgreSQL en 5433 y API opcional con perfil `api` en 8000. El perfil local de `dotnet run` activa Development y migraciones al iniciar; el valor base es `false`. Las opciones y sus valores efectivos están en el README. `.env` no se carga automáticamente con `dotnet run`.

## Trabajo siguiente

- Adaptar el frontend React/Vite del repositorio hermano al contrato nuevo y al polling.
- Integrar estimador y control de calidad reales a través de los puertos existentes.
- Diseñar migración de datos existentes si deben conservarse en el sistema nuevo.
- Implementar usuarios, roles y permisos, bloqueo de login y auditoría.
- Implementar sincronización offline sin duplicados; todavía no se acepta un id de captura generado por el cliente.
- Trasladar al TP7 los cambios enumerados en el informe. Resolver la discrepancia de MAPE (<20% en ERS frente a <15% en TP2); no afirmar precisión del simulador.

Estado del retiro de fuentes y correspondencia de los 38 tests originales: ver [informe de migración](docs/migracion-dotnet.md#matriz-de-los-38-tests-originales). Hay 38 nombres mapeados, con cambios deliberados de B y límites de prueba explícitos; 168 casos .NET verdes no demuestran por sí solos paridad literal. La verificación independiente confirmó suite, compilación, formato y configuración de Compose, pero no estableció el diferencial HTTP contra la versión .NET anterior. El cierre y el retiro de Python quedan pendientes de una decisión explícita sobre esa prueba faltante. `.env.example` sigue heredado y sin cambios por un bloqueo de seguridad; usar las claves y el ejemplo sin secretos del README, no prometer una plantilla .NET migrada.

## Acuerdos de trabajo

Documentación en español; comentarios de código y mensajes de commit en inglés. SOLID y patrones se documentan solo cuando existen en el código. Sin paquetes ni herramientas nuevos sin aprobación. RDD desactivado por decisión del usuario. El orquestador realiza los commits por unidad coherente (Conventional Commits, sin atribución de IA); push, PR y merge los decide el usuario. Los escritores no realizan acciones de git de entrega.
