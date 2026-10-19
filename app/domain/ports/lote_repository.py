from abc import ABC, abstractmethod

from app.domain.entities.lote import Lote


class LoteRepository(ABC):
    @abstractmethod
    def add(self, lote: Lote) -> Lote: ...

    @abstractmethod
    def list_by_establecimiento(self, establecimiento_id: int) -> list[Lote]: ...

    @abstractmethod
    def get(self, lote_id: int) -> Lote | None: ...
