import pytest

from app.core.exceptions import (
    AnimalDuplicadoError,
    AnimalNoEncontradoError,
    LoteNoEncontradoError,
)
from app.services.animal_service import AnimalService
from tests.fakes import FakeAnimalRepository, FakeLoteRepository


@pytest.fixture
def service():
    return AnimalService(FakeAnimalRepository(), FakeLoteRepository())


def test_registrar_animal(service):
    animal = service.registrar_animal(establecimiento_id=1, identificador="AR001")

    assert animal.id is not None
    assert animal.identificador == "AR001"


def test_no_permite_identificador_duplicado(service):
    service.registrar_animal(establecimiento_id=1, identificador="AR001")

    with pytest.raises(AnimalDuplicadoError):
        service.registrar_animal(establecimiento_id=1, identificador="AR001")


def test_modificar_animal_inexistente_lanza_error(service):
    with pytest.raises(AnimalNoEncontradoError):
        service.modificar_animal(animal_id=999, raza="Angus")


def test_no_permite_asignar_lote_inexistente(service):
    with pytest.raises(LoteNoEncontradoError):
        service.registrar_animal(establecimiento_id=1, identificador="AR002", lote_id=999)


def test_listar_animales_filtra_por_establecimiento(service):
    service.registrar_animal(establecimiento_id=1, identificador="AR001")
    service.registrar_animal(establecimiento_id=2, identificador="AR002")

    animales = service.listar_animales(establecimiento_id=1)

    assert [a.identificador for a in animales] == ["AR001"]
