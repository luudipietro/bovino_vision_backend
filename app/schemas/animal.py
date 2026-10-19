from datetime import date
from typing import Literal

from pydantic import BaseModel, ConfigDict

Sexo = Literal["macho", "hembra"]


class AnimalCreate(BaseModel):
    identificador: str
    fecha_nacimiento: date | None = None
    sexo: Sexo | None = None
    raza: str | None = None
    lote_id: int | None = None


class AnimalUpdate(BaseModel):
    fecha_nacimiento: date | None = None
    sexo: Sexo | None = None
    raza: str | None = None
    lote_id: int | None = None


class AnimalRead(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: int
    identificador: str
    establecimiento_id: int
    lote_id: int | None
    fecha_nacimiento: date | None
    sexo: str | None
    raza: str | None
