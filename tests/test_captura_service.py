import pytest

from app.core.exceptions import AnimalNoEncontradoError, SesionCapturaNoEncontradaError
from app.services.animal_service import AnimalService
from app.services.captura_service import CapturaService
from tests.fakes import (
    FakeAnimalRepository,
    FakeCapturaRepository,
    FakeImageStorageAdapter,
    FakeLoteRepository,
    FakeSesionCapturaRepository,
)

IMAGEN_VALIDA = b"contenido-de-prueba"


@pytest.fixture
def animal_repository():
    return FakeAnimalRepository()


@pytest.fixture
def storage():
    return FakeImageStorageAdapter()


@pytest.fixture
def service(animal_repository, storage):
    return CapturaService(
        FakeSesionCapturaRepository(),
        FakeCapturaRepository(),
        animal_repository,
        storage,
    )


@pytest.fixture
def animal_id(animal_repository):
    animal_service = AnimalService(animal_repository, FakeLoteRepository())
    return animal_service.registrar_animal(establecimiento_id=1, identificador="AR001").id


def test_iniciar_sesion(service):
    sesion = service.iniciar_sesion(establecimiento_id=1)

    assert sesion.id is not None
    assert sesion.establecimiento_id == 1


def test_registrar_captura_valida(service, animal_id, storage):
    sesion = service.iniciar_sesion(establecimiento_id=1)

    captura = service.registrar_captura(
        sesion_id=sesion.id,
        animal_id=animal_id,
        nombre_archivo="foto.jpg",
        contenido=IMAGEN_VALIDA,
        content_type="image/jpeg",
    )

    assert captura.validada is True
    assert captura.motivo_invalidez is None
    assert storage.guardados == [IMAGEN_VALIDA]


def test_registrar_captura_con_formato_no_soportado(service, animal_id):
    sesion = service.iniciar_sesion(establecimiento_id=1)

    captura = service.registrar_captura(
        sesion_id=sesion.id,
        animal_id=animal_id,
        nombre_archivo="foto.pdf",
        contenido=IMAGEN_VALIDA,
        content_type="application/pdf",
    )

    assert captura.validada is False
    assert captura.motivo_invalidez == "Formato de imagen no soportado"


def test_registrar_captura_con_sesion_inexistente_lanza_error(service, animal_id):
    with pytest.raises(SesionCapturaNoEncontradaError):
        service.registrar_captura(
            sesion_id=999,
            animal_id=animal_id,
            nombre_archivo="foto.jpg",
            contenido=IMAGEN_VALIDA,
            content_type="image/jpeg",
        )


def test_registrar_captura_con_animal_inexistente_lanza_error(service):
    sesion = service.iniciar_sesion(establecimiento_id=1)

    with pytest.raises(AnimalNoEncontradoError):
        service.registrar_captura(
            sesion_id=sesion.id,
            animal_id=999,
            nombre_archivo="foto.jpg",
            contenido=IMAGEN_VALIDA,
            content_type="image/jpeg",
        )
