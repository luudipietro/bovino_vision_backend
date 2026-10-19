from abc import ABC, abstractmethod

from app.domain.entities.alerta import Alerta


class AlertaRepository(ABC):
    @abstractmethod
    def add(self, alerta: Alerta) -> Alerta: ...

    @abstractmethod
    def get_by_evento(self, evento_pesaje_id: int) -> Alerta | None: ...

    @abstractmethod
    def list_by_animal(self, animal_id: int) -> list[Alerta]: ...

    @abstractmethod
    def list_by_establecimiento(self, establecimiento_id: int) -> list[Alerta]: ...
