from abc import ABC, abstractmethod

from app.domain.entities.animal import Animal


class AnimalRepository(ABC):
    @abstractmethod
    def add(self, animal: Animal) -> Animal: ...

    @abstractmethod
    def get(self, animal_id: int) -> Animal | None: ...

    @abstractmethod
    def update(self, animal: Animal) -> Animal: ...

    @abstractmethod
    def list_by_establecimiento(
        self,
        establecimiento_id: int,
        lote_id: int | None = None,
        search: str | None = None,
    ) -> list[Animal]: ...

    @abstractmethod
    def exists_identificador(self, establecimiento_id: int, identificador: str) -> bool: ...
