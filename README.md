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

Estas carpetas se van poblando a medida que se implementan los módulos (por ahora solo
existen `core/` y el endpoint `/health` en `app/main.py`).

## Requisitos

- Python 3.14
- [uv](https://docs.astral.sh/uv/)

## Cómo correrlo localmente

```bash
uv sync
uv run uvicorn app.main:app --reload
```

La API queda en `http://localhost:8000`. Podés probar `GET /health`.

## Tests

```bash
uv run pytest
```

## Variables de entorno

Ver `.env.example`. Copiá ese archivo a `.env` y ajustá `DATABASE_URL` si hace falta.
