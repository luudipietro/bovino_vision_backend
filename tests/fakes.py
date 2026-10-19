from app.domain.entities.animal import Animal
from app.domain.entities.lote import Lote
from app.domain.ports.animal_repository import AnimalRepository
from app.domain.ports.lote_repository import LoteRepository


class FakeLoteRepository(LoteRepository):
    def __init__(self):
        self._lotes: dict[int, Lote] = {}
        self._next_id = 1

    def add(self, lote: Lote) -> Lote:
        lote.id = self._next_id
        self._lotes[lote.id] = lote
        self._next_id += 1
        return lote

    def list_by_establecimiento(self, establecimiento_id: int) -> list[Lote]:
        return [l for l in self._lotes.values() if l.establecimiento_id == establecimiento_id]

    def get(self, lote_id: int) -> Lote | None:
        return self._lotes.get(lote_id)


class FakeAnimalRepository(AnimalRepository):
    def __init__(self):
        self._animales: dict[int, Animal] = {}
        self._next_id = 1

    def add(self, animal: Animal) -> Animal:
        animal.id = self._next_id
        self._animales[animal.id] = animal
        self._next_id += 1
        return animal

    def get(self, animal_id: int) -> Animal | None:
        return self._animales.get(animal_id)

    def update(self, animal: Animal) -> Animal:
        self._animales[animal.id] = animal
        return animal

    def list_by_establecimiento(
        self,
        establecimiento_id: int,
        lote_id: int | None = None,
        search: str | None = None,
    ) -> list[Animal]:
        resultado = [
            a for a in self._animales.values() if a.establecimiento_id == establecimiento_id
        ]
        if lote_id is not None:
            resultado = [a for a in resultado if a.lote_id == lote_id]
        if search:
            resultado = [a for a in resultado if search.lower() in a.identificador.lower()]
        return resultado

    def exists_identificador(self, establecimiento_id: int, identificador: str) -> bool:
        return any(
            a.establecimiento_id == establecimiento_id and a.identificador == identificador
            for a in self._animales.values()
        )
