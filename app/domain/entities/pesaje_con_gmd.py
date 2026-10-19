from dataclasses import dataclass

from app.domain.entities.evento_pesaje import EventoPesaje


@dataclass
class PesajeConGmd:
    evento: EventoPesaje
    gmd_kg_dia: float | None
