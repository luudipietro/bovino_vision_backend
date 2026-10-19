# BovinoVision — Backend

API del sistema BovinoVision (estimación de peso de bovinos por visión artificial y
trazabilidad del rodeo), construida con **Python + FastAPI**.

## Arquitectura

El código vive en `app/`, separado por capas:

- `core/` — configuración (`config.py`) y acceso a base de datos (`database.py`).
- `domain/` — entidades de negocio puras e interfaces (puertos) que la capa de dominio
  necesita del exterior (p. ej. `WeightEstimatorPort`).
- `models/` — modelos ORM (SQLAlchemy) que mapean el dominio a tablas.
- `schemas/` — DTOs Pydantic de entrada/salida de la API.
- `repositories/` — implementaciones concretas de los repositorios del dominio.
- `estimators/` — implementaciones del `WeightEstimatorPort` (hoy, un mock; a futuro, el
  adaptador que llama al servicio real de estimación de peso, desarrollado en otro repo).
- `storage/` — implementaciones del `ImageStoragePort` (hoy, disco local en `uploads/`; a
  futuro, un bucket de almacenamiento en el VPS de producción).
- `services/` — casos de uso que orquestan dominio, repositorios y puertos.
- `routes/` — routers de FastAPI, uno por submódulo.
- `dependencies.py` — wiring de inyección de dependencias (qué implementación concreta se
  inyecta en cada puerto).

Estas carpetas se van poblando a medida que se implementan los módulos. Por ahora están
implementados:

- `Lote` y `Animal` (alta, modificación, listado con filtros y ficha de detalle) sobre un
  único `Establecimiento` precargado.
- Captura y pesaje: iniciar sesión de captura, subir una imagen (validación simplificada de
  tipo/tamaño, no de calidad real), procesar el pesaje contra el `WeightEstimatorPort`
  (mock por ahora) y consultar el resultado y el historial de un animal.
- Analítica y Alertas: GMD (ganancia media diaria) en el historial de cada animal, alertas
  automáticas cuando un pesaje varía más de un 15% respecto al anterior (prioridad "alta"
  desde 30%), panel agregado del rodeo (peso y GMD promedio, evolución del peso promedio)
  y reportes por lapso de tiempo.

Todavía no existe el módulo de Usuarios, así que no hay multi-establecimiento ni
autenticación; tampoco existe el modo offline (RF-16/RF-17).

## Requisitos

- Python 3.14
- [uv](https://docs.astral.sh/uv/)
- Docker (para levantar Postgres localmente)

## Cómo correrlo localmente

```bash
# desde la raíz de BovinoVison/, levanta Postgres en el puerto 5433
docker compose up -d db

cd backend
uv sync
uv run alembic upgrade head   # crea las tablas y precarga el establecimiento
uv run uvicorn app.main:app --reload
```

La API queda en `http://localhost:8000`. Podés probar `GET /health`, o:

```bash
curl -X POST http://localhost:8000/lotes -H "Content-Type: application/json" -d '{"codigo":"L1"}'
curl -X POST http://localhost:8000/animales -H "Content-Type: application/json" -d '{"identificador":"AR001","lote_id":1}'
curl http://localhost:8000/animales
```

Flujo de captura y pesaje:

```bash
SESION_ID=$(curl -s -X POST http://localhost:8000/sesiones-captura | python -c "import sys,json;print(json.load(sys.stdin)['id'])")
curl -X POST http://localhost:8000/capturas -F "sesion_id=$SESION_ID" -F "animal_id=1" -F "imagen=@foto.jpg;type=image/jpeg"
curl -X POST http://localhost:8000/pesajes -H "Content-Type: application/json" -d '{"captura_id":1}'
curl http://localhost:8000/animales/1/pesajes
```

Analítica y alertas:

```bash
curl http://localhost:8000/alertas
curl http://localhost:8000/animales/1/alertas
curl http://localhost:8000/panel
curl "http://localhost:8000/reportes?desde=2026-10-01&hasta=2026-10-31"
```

## Tests

```bash
uv run pytest
```

Los tests no necesitan Postgres: los de servicios usan repositorios y adaptadores fake en
memoria, y los de la API usan SQLite en memoria y no escriben imágenes reales en disco.

## Lint

```bash
uv run ruff check .
```

`alembic/versions/` queda excluido (son migraciones generadas); `Depends(...)` como valor
por default de parámetro está permitido explícitamente (es el patrón estándar de FastAPI),
ver `[tool.ruff.lint.flake8-bugbear]` en `pyproject.toml`.

## Migraciones (Alembic)

```bash
uv run alembic revision --autogenerate -m "descripción del cambio"
uv run alembic upgrade head
```

## Variables de entorno

Ver `.env.example`. Copiá ese archivo a `.env` y ajustá `DATABASE_URL` si hace falta.
