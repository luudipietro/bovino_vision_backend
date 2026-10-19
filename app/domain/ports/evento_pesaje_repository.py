from abc import ABC, abstractmethod
from datetime import date

from app.domain.entities.evento_pesaje import EventoPesaje


class EventoPesajeRepository(ABC):
    @abstractmethod
    def add(self, evento: EventoPesaje) -> EventoPesaje: ...

    @abstractmethod
    def get(self, evento_id: int) -> EventoPesaje | None: ...

    @abstractmethod
    def get_by_captura(self, captura_id: int) -> EventoPesaje | None: ...

    @abstractmethod
    def list_by_animal(self, animal_id: int) -> list[EventoPesaje]: ...

    @abstractmethod
    def list_by_establecimiento(
        self,
        establecimiento_id: int,
        lote_id: int | None = None,
        desde: date | None = None,
        hasta: date | None = None,
    ) -> list[EventoPesaje]: ...
