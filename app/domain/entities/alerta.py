from dataclasses import dataclass
from datetime import datetime


@dataclass
class Alerta:
    id: int | None
    animal_id: int
    establecimiento_id: int
    evento_pesaje_id: int
    tipo: str
    prioridad: str
    variacion_pct: float
    fecha_generacion: datetime
    estado: str = "activa"
