from dataclasses import dataclass
from datetime import date


@dataclass
class Reporte:
    desde: date
    hasta: date
    cantidad_pesajes: int
    peso_promedio_kg: float | None
    gmd_promedio_kg_dia: float | None
