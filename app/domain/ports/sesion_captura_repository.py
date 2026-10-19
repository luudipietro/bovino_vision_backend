from abc import ABC, abstractmethod

from app.domain.entities.sesion_captura import SesionCaptura


class SesionCapturaRepository(ABC):
    @abstractmethod
    def add(self, sesion: SesionCaptura) -> SesionCaptura: ...

    @abstractmethod
    def get(self, sesion_id: int) -> SesionCaptura | None: ...
