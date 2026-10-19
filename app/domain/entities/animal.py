from dataclasses import dataclass
from datetime import date


@dataclass
class Animal:
    id: int | None
    identificador: str
    establecimiento_id: int
    fecha_nacimiento: date | None = None
    sexo: str | None = None
    raza: str | None = None
    lote_id: int | None = None
