from dataclasses import dataclass
from datetime import datetime
from typing import Literal

EstadoPesaje = Literal["estimado", "error"]


@dataclass
class EventoPesaje:
    id: int | None
    animal_id: int
    captura_id: int
    estado: EstadoPesaje
    fecha_pesaje: datetime
    peso_kg: float | None = None
    version_modelo: str | None = None
    motivo_error: str | None = None
