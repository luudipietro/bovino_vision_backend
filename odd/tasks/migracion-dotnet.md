# Feature: migracion-dotnet

Objective: migrate the BovinoVision backend from Python/FastAPI to C#/.NET with Clean Architecture, scope B (parity plus design improvements).
Branch: `feat/migracion-dotnet` (from `manu`). Delivery: single feature branch, one commit per work unit; the user reviews before merging (`single-pr`).
Test runner: `dotnet test` (xUnit). Parallel writers run in isolated git worktrees; the parent integrates. Forecast: about 4,000 authored lines.

## Specs

- **S1. Migration target.** "migrar este backend de Python a C#", with "una Clean Arquitecture", "principios SOLID y patrones de arquitectura, fundamentales para un buen diseño del sistema". Layers: `BovinoVision.Dominio` → `BovinoVision.Aplicacion` → `BovinoVision.Infraestructura` → `BovinoVision.Api`, with `Aplicacion` organized by module. Runtime: .NET 10 LTS (assumption pending the SDK install approval; .NET 9 support ends 2026-11-10).
- **S2. Naming.** "los nombres van en español". Classes, methods, properties, tables and columns use Spanish domain vocabulary (tables and columns in snake_case following the ER, e.g. `eventos_pesaje.id_animal`).
- **S3. Design documents follow the code.** "los documentos de analisis y diseño los estamos refinando todavia, por lo que no es necesario hacer que el codigo coincida con lo que ya está hecho. si recomendas hacer algun cambio en el diseño para que el codigo sea mejor, lo hacemos." Every design deviation is recorded in `## Log` as a change to carry into the TP7 documents.
- **S4. Scope B.** "alcance B", defined as "A, más ids UUID v7, historial de lotes, catálogo de razas, tabla de imágenes, fecha de pesaje igual a la de captura, fallas que no generan evento, inferencia asíncrona y una transacción por caso de uso", where A is "Las mismas 16 rutas y el mismo JSON, con los 38 tests portados". Where B changes behavior, B wins over A (see L6).
- **S5. Accepted design changes** (recommended in the session; accepted by S3):
  1. Captures act as the inference work queue: `capturas.estado` (`pendiente` → `procesando` → `procesada` | `fallida`), `motivo_fallo`, `intentos`.
  2. Expected GMD: `lotes.gmd_objetivo` (kg/day, optional). Alert `perdida_peso` when the interval GMD is negative; alert `estancamiento` when the lot has `gmd_objetivo` and the interval GMD is below a configurable fraction of it. Assumption: default fraction 0.5, priority 1 for loss and 2 for stagnation, both in configuration.
  3. GMD is computed on read from weighing events (never stored).
  4. A default user and establecimiento are seeded so the ER foreign keys hold until the Users module exists.
- **S6. Parallel work and commits.** "en paralelo con worktrees y con tus commits en esa rama". "cambia la regla esa de que yo tengo que hacer los commits, para que siempre los hagas vos automaticamente."
- **S7. API contract.** JSON stays snake_case. Field names follow the Spanish properties (`id_animal`, `id_lote`, ...). Ids are UUID v7 strings. Weighing is asynchronous: `POST /pesajes {id_captura}` answers 202 and the client polls `GET /capturas/{id}` until `procesada` (with `id_evento_pesaje`) or `fallida` (with `motivo_fallo`). An invalid upload answers 422 and stores nothing. The frontend needs a follow-up adaptation (ids, field names, `id_raza`, async weighing); it is not part of this feature.

## Tasks

| ID | Specs | Route | Status | Evidence |
|---|---|---|---|---|
| T0 | S6 | inline | done | backend `a59d922`, frontend `70eacaa` |
| T1 | S1, S2, S4, S5 | inline | blocked: SDK and package approval | Base: solution, `Dominio` for all modules (entities, GMD calculator, alert rules, errors), unit of work, `DbContext` with per-module configurations, API skeleton (ProblemDetails, snake_case, CORS, `/health`), test harness |
| T2 | S2, S4, S7 | delegated (worktree, parallel) | pending | Trazabilidad: lotes (with `gmd_objetivo`, animal count), animales, `historial_lotes` (lot change closes and opens in one transaction), razas catalog |
| T3 | S2, S4, S5.1, S7 | delegated (worktree, parallel) | pending | Captura e Inferencia: sesiones, capturas with `fecha_captura`, imagenes with hash, local storage, mock estimator, background worker with retries, no event on failure |
| T4 | S2, S4, S5.2, S5.3, S7 | delegated (worktree, parallel) | pending | Analítica y Alertas: history with GMD, last weight, panel, reports, alert rules, prioritized alerts (no N+1 queries) |
| T5 | S1, S4, S5.4 | inline | pending | Integration: merge T2-T4, single EF migration with seed, module wiring in `Program`, full suite, end-to-end smoke test against PostgreSQL |
| T6 | S1, S3 | inline | pending | Dockerfile, README, CLAUDE.md, context.md, design-change list for TP7, remove the Python code |

Writer trigger for T2-T4: three independent units with disjoint edit surfaces, each several hundred lines. Worktrees instead of a shared tree because parallel `dotnet build` runs collide on the shared projects' `obj/` and `bin/` on Windows, and EF migrations share one snapshot file. High-risk items: 1 (new persisted schema and migration) and 3 (API contract consumed by the frontend), so an independent verifier runs once at the end of the feature.

## Log

- **L1 (2026-10-19, user):** "La idea es trabajar con el stack tecnologico de C# y .NET en el backend, y Typescript, React y Tailwind en el frontend. Además, en el backend quiero implementar una Clean Arquitecture, que creo que es lo mejor para C#, como asi tambien trabajar con principios SOLID y patrones de arquitectura, fundamentales para un buen diseño del sistema. [...] El primer paso que tengo pensado por ahora es migrar este backend de Python a C#, con la arquitectura y estructura mencionada."
- **L2 (2026-10-19, user):** "los nombres van en español. cuanto esfuerzo lleva hacer la migracion ahora? ademas, acordate que los documentos de analisis y diseño los estamos refinando todavia, por lo que no es necesario hacer que el codigo coincida con lo que ya está hecho. si recomendas hacer algun cambio en el diseño para que el codigo sea mejor, lo hacemos."
- **L3 (2026-10-19, user):** "y si hacemos trabajo en paralelo, liberando subagentes o worktrees aislados, se puede hacer mas rapido? ademas, podes usar ODD de gentle-ai?"
- **L4 (2026-10-19, user):** "si, alcance B, en paralelo con worktrees y con tus commits en esa rama. despues cambia la regla esa de que yo tengo que hacer los commits, para que siempre los hagas vos automaticamente."
- **L5 (2026-10-19):** T0 done. The commit rule was changed in both repos' `context.md` and in `CLAUDE.md`. The user's uncommitted `.gitignore` change (adds `CLAUDE.md`) stays out of Claude's commits.
- **L6 (2026-10-19):** Contract decision. Asynchronous inference, UUID ids, Spanish field names and the razas catalog make "el mismo JSON" from A impossible for the weighing and animal endpoints, so S7 records the new contract and the frontend gets a follow-up task. The 38 Python tests are ported as behavior specs, adapted where B changes the rule (invalid uploads, failed estimations, alert rule).
- **L7 (2026-10-19):** Environment. Only the .NET 9.0.304 SDK is installed (winget offers `Microsoft.DotNet.SDK.10` 10.0.402). The Docker daemon is not running. `uv` is not installed, so the Python suite cannot be run as a baseline; its tests are read as the specification. Next step: user approval for the .NET 10 SDK and the NuGet packages, then T1.
