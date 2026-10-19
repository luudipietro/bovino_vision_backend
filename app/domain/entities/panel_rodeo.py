from dataclasses import dataclass

from app.domain.entities.punto_evolucion import PuntoEvolucion


@dataclass
class PanelRodeo:
    cantidad_animales: int
    peso_promedio_kg: float | None
    gmd_promedio_kg_dia: float | None
    evolucion_peso_promedio: list[PuntoEvolucion]
