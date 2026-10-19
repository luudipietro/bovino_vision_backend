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
  adaptador que llama al servicio real de estimación de peso).
- `services/` — casos de uso que orquestan dominio, repositorios y puertos.
- `routes/` — routers de FastAPI, uno por submódulo.
- `dependencies.py` — wiring de inyección de dependencias (qué implementación concreta se
  inyecta en cada puerto).

Estas carpetas se van poblando a medida que se implementan los módulos. Por ahora están
implementados `Lote` y `Animal` (alta, modificación, listado con filtros y ficha de
detalle) sobre un único `Establecimiento` precargado — todavía no existe el módulo de
Usuarios, así que no hay multi-establecimiento ni autenticación.

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

## Tests

```bash
uv run pytest
```

Los tests no necesitan Postgres: los de servicios (`test_animal_service.py`) usan
repositorios fake en memoria, y los de la API (`test_animales_api.py`) usan SQLite en
memoria.

## Migraciones (Alembic)

```bash
uv run alembic revision --autogenerate -m "descripción del cambio"
uv run alembic upgrade head
```

## Variables de entorno

Ver `.env.example`. Copiá ese archivo a `.env` y ajustá `DATABASE_URL` si hace falta.
