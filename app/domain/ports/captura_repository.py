from abc import ABC, abstractmethod

from app.domain.entities.captura import Captura


class CapturaRepository(ABC):
    @abstractmethod
    def add(self, captura: Captura) -> Captura: ...

    @abstractmethod
    def get(self, captura_id: int) -> Captura | None: ...

    @abstractmethod
    def list_by_sesion(self, sesion_id: int) -> list[Captura]: ...
