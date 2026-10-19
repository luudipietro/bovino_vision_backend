from dataclasses import dataclass
from datetime import date


@dataclass
class PuntoEvolucion:
    fecha: date
    peso_promedio_kg: float | None
