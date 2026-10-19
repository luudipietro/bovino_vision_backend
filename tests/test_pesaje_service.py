import pytest

from app.core.exceptions import (
    CapturaInvalidaError,
    CapturaNoEncontradaError,
    CapturaYaProcesadaError,
)
from app.domain.entities.captura import Captura
from app.domain.entities.estimacion import ResultadoEstimacion
from app.domain.entities.lote import Lote
from app.services.alerta_service import AlertaService
from app.services.animal_service import AnimalService
from app.services.pesaje_service import PesajeService
from tests.fakes import (
    FakeAlertaRepository,
    FakeAnimalRepository,
    FakeCapturaRepository,
    FakeEventoPesajeRepository,
    FakeLoteRepository,
    FakeWeightEstimatorAdapter,
)


@pytest.fixture
def repos():
    return {
        "capturas": FakeCapturaRepository(),
        "eventos": FakeEventoPesajeRepository(),
        "animales": FakeAnimalRepository(),
        "lotes": FakeLoteRepository(),
        "alertas": FakeAlertaRepository(),
    }


def make_service(repos, estimador=None):
    return PesajeService(
        repos["capturas"],
        repos["eventos"],
        repos["animales"],
        repos["lotes"],
        estimador or FakeWeightEstimatorAdapter(),
        AlertaService(repos["alertas"]),
    )


@pytest.fixture
def animal_id(repos):
    animal_service = AnimalService(repos["animales"], repos["lotes"])
    return animal_service.registrar_animal(establecimiento_id=1, identificador="AR001").id


def crear_captura_validada(repos, animal_id, validada=True):
    return repos["capturas"].add(
        Captura(
            id=None,
            sesion_id=1,
            animal_id=animal_id,
            archivo_path="fake://foto.jpg",
            validada=validada,
        )
    )


def test_procesar_pesaje_exitoso(repos, animal_id):
    captura = crear_captura_validada(repos, animal_id)
    service = make_service(repos, FakeWeightEstimatorAdapter(
        ResultadoEstimacion(exitoso=True, peso_kg=400.0, version_modelo="fake-v1")
    ))

    evento = service.procesar_pesaje(captura.id)

    assert evento.estado == "estimado"
    assert evento.peso_kg == 400.0


def test_procesar_pesaje_con_estimacion_fallida(repos, animal_id):
    captura = crear_captura_validada(repos, animal_id)
    service = make_service(repos, FakeWeightEstimatorAdapter(
        ResultadoEstimacion(exitoso=False, motivo_error="No se detectó el animal")
    ))

    evento = service.procesar_pesaje(captura.id)

    assert evento.estado == "error"
    assert evento.peso_kg is None
    assert evento.motivo_error == "No se detectó el animal"


def test_no_permite_procesar_captura_invalida(repos, animal_id):
    captura = crear_captura_validada(repos, animal_id, validada=False)
    service = make_service(repos)

    with pytest.raises(CapturaInvalidaError):
        service.procesar_pesaje(captura.id)


def test_no_permite_procesar_captura_inexistente(repos):
    service = make_service(repos)

    with pytest.raises(CapturaNoEncontradaError):
        service.procesar_pesaje(999)


def test_no_permite_procesar_la_misma_captura_dos_veces(repos, animal_id):
    captura = crear_captura_validada(repos, animal_id)
    service = make_service(repos)
    service.procesar_pesaje(captura.id)

    with pytest.raises(CapturaYaProcesadaError):
        service.procesar_pesaje(captura.id)


def test_obtener_resultado_incluye_lote_y_peso_previo(repos, animal_id):
    lote = repos["lotes"].add(Lote(id=None, codigo="L1", establecimiento_id=1))
    animal = repos["animales"].get(animal_id)
    animal.lote_id = lote.id
    repos["animales"].update(animal)

    service = make_service(repos, FakeWeightEstimatorAdapter(
        ResultadoEstimacion(exitoso=True, peso_kg=300.0, version_modelo="fake-v1")
    ))
    primera_captura = crear_captura_validada(repos, animal_id)
    primer_evento = service.procesar_pesaje(primera_captura.id)

    segunda_captura = crear_captura_validada(repos, animal_id)
    segundo_evento = service.procesar_pesaje(segunda_captura.id)

    resultado = service.obtener_resultado(segundo_evento.id)

    assert resultado.lote.codigo == "L1"
    assert resultado.peso_previo.id == primer_evento.id
    assert resultado.peso_previo.peso_kg == 300.0


def test_variacion_grande_de_peso_genera_alerta(repos, animal_id):
    service = make_service(repos, FakeWeightEstimatorAdapter(
        ResultadoEstimacion(exitoso=True, peso_kg=300.0, version_modelo="fake-v1")
    ))
    primera_captura = crear_captura_validada(repos, animal_id)
    service.procesar_pesaje(primera_captura.id)

    service_segundo_pesaje = make_service(repos, FakeWeightEstimatorAdapter(
        ResultadoEstimacion(exitoso=True, peso_kg=450.0, version_modelo="fake-v1")
    ))
    segunda_captura = crear_captura_validada(repos, animal_id)
    segundo_evento = service_segundo_pesaje.procesar_pesaje(segunda_captura.id)

    alertas = repos["alertas"].list_by_animal(animal_id)
    assert len(alertas) == 1
    assert alertas[0].evento_pesaje_id == segundo_evento.id
    assert alertas[0].prioridad == "alta"


def test_variacion_chica_de_peso_no_genera_alerta(repos, animal_id):
    service = make_service(repos, FakeWeightEstimatorAdapter(
        ResultadoEstimacion(exitoso=True, peso_kg=300.0, version_modelo="fake-v1")
    ))
    primera_captura = crear_captura_validada(repos, animal_id)
    service.procesar_pesaje(primera_captura.id)

    service_segundo_pesaje = make_service(repos, FakeWeightEstimatorAdapter(
        ResultadoEstimacion(exitoso=True, peso_kg=310.0, version_modelo="fake-v1")
    ))
    segunda_captura = crear_captura_validada(repos, animal_id)
    service_segundo_pesaje.procesar_pesaje(segunda_captura.id)

    assert repos["alertas"].list_by_animal(animal_id) == []
