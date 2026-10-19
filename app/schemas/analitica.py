from datetime import date

from pydantic import BaseModel


class PuntoEvolucionRead(BaseModel):
    fecha: date
    peso_promedio_kg: float | None


class PanelRodeoRead(BaseModel):
    cantidad_animales: int
    peso_promedio_kg: float | None
    gmd_promedio_kg_dia: float | None
    evolucion_peso_promedio: list[PuntoEvolucionRead]


class ReporteRead(BaseModel):
    desde: date
    hasta: date
    cantidad_pesajes: int
    peso_promedio_kg: float | None
    gmd_promedio_kg_dia: float | None
