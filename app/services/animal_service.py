from datetime import date

from app.core.exceptions import (
    AnimalDuplicadoError,
    AnimalNoEncontradoError,
    LoteNoEncontradoError,
)
from app.domain.entities.animal import Animal
from app.domain.ports.animal_repository import AnimalRepository
from app.domain.ports.lote_repository import LoteRepository


class AnimalService:
    def __init__(self, animal_repository: AnimalRepository, lote_repository: LoteRepository):
        self._animales = animal_repository
        self._lotes = lote_repository

    def registrar_animal(
        self,
        establecimiento_id: int,
        identificador: str,
        fecha_nacimiento: date | None = None,
        sexo: str | None = None,
        raza: str | None = None,
        lote_id: int | None = None,
    ) -> Animal:
        if self._animales.exists_identificador(establecimiento_id, identificador):
            raise AnimalDuplicadoError(identificador)

        self._validar_lote(lote_id)

        animal = Animal(
            id=None,
            identificador=identificador,
            establecimiento_id=establecimiento_id,
            fecha_nacimiento=fecha_nacimiento,
            sexo=sexo,
            raza=raza,
            lote_id=lote_id,
        )
        return self._animales.add(animal)

    def modificar_animal(
        self,
        animal_id: int,
        fecha_nacimiento: date | None = None,
        sexo: str | None = None,
        raza: str | None = None,
        lote_id: int | None = None,
    ) -> Animal:
        animal = self._obtener_o_fallar(animal_id)

        if lote_id is not None:
            self._validar_lote(lote_id)
            animal.lote_id = lote_id
        if fecha_nacimiento is not None:
            animal.fecha_nacimiento = fecha_nacimiento
        if sexo is not None:
            animal.sexo = sexo
        if raza is not None:
            animal.raza = raza

        return self._animales.update(animal)

    def obtener_ficha(self, animal_id: int) -> Animal:
        return self._obtener_o_fallar(animal_id)

    def listar_animales(
        self,
        establecimiento_id: int,
        lote_id: int | None = None,
        search: str | None = None,
    ) -> list[Animal]:
        return self._animales.list_by_establecimiento(establecimiento_id, lote_id, search)

    def _obtener_o_fallar(self, animal_id: int) -> Animal:
        animal = self._animales.get(animal_id)
        if animal is None:
            raise AnimalNoEncontradoError(animal_id)
        return animal

    def _validar_lote(self, lote_id: int | None) -> None:
        if lote_id is not None and self._lotes.get(lote_id) is None:
            raise LoteNoEncontradoError(lote_id)
