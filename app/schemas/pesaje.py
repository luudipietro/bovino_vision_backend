from datetime import datetime
from typing import Literal

from pydantic import BaseModel

from app.schemas.alerta import AlertaRead

EstadoPesaje = Literal["estimado", "error"]


class PesajeCreate(BaseModel):
    captura_id: int


class UltimoPesoRead(BaseModel):
    peso_kg: float
    fecha_pesaje: datetime


class PesajeRead(BaseModel):
    id: int
    animal_id: int
    estado: EstadoPesaje
    peso_kg: float | None
    motivo_error: str | None
    fecha_pesaje: datetime
    version_modelo: str | None
    animal_identificador: str
    lote_codigo: str | None
    ultimo_peso_previo: UltimoPesoRead | None
    alerta: AlertaRead | None = None


class PesajeHistorialRead(BaseModel):
    id: int
    estado: EstadoPesaje
    peso_kg: float | None
    fecha_pesaje: datetime
    version_modelo: str | None
    gmd_kg_dia: float | None
