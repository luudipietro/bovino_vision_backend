from datetime import datetime

from pydantic import BaseModel, ConfigDict


class SesionCapturaRead(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: int
    establecimiento_id: int
    fecha_inicio: datetime


class CapturaRead(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: int
    sesion_id: int
    animal_id: int
    validada: bool
    motivo_invalidez: str | None
