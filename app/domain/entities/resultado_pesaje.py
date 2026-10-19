from dataclasses import dataclass

from app.domain.entities.animal import Animal
from app.domain.entities.evento_pesaje import EventoPesaje
from app.domain.entities.lote import Lote


@dataclass
class ResultadoPesaje:
    evento: EventoPesaje
    animal: Animal
    lote: Lote | None
    peso_previo: EventoPesaje | None
