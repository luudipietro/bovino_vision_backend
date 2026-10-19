from datetime import datetime, timedelta

import pytest

from app.domain.entities.evento_pesaje import EventoPesaje
from app.services.analitica_service import AnaliticaService
from tests.fakes import FakeAlertaRepository, FakeEventoPesajeRepository

INICIO = datetime(2026, 1, 1, 10, 0, 0)


@pytest.fixture
def eventos():
    return FakeEventoPesajeRepository()


@pytest.fixture
def service(eventos):
    return AnaliticaService(eventos, FakeAlertaRepository())


def agregar_evento(repo, animal_id, dias_desde_inicio, peso_kg, estado="estimado"):
    return repo.add(
        EventoPesaje(
            id=None,
            animal_id=animal_id,
            captura_id=animal_id * 100 + dias_desde_inicio,
            estado=estado,
            fecha_pesaje=INICIO + timedelta(days=dias_desde_inicio),
            peso_kg=peso_kg,
            version_modelo="fake-v1",
        )
    )


def test_historial_con_gmd_primer_pesaje_sin_gmd(service, eventos):
    agregar_evento(eventos, animal_id=1, dias_desde_inicio=0, peso_kg=300.0)

    historial = service.historial_con_gmd(1)

    assert len(historial) == 1
    assert historial[0].gmd_kg_dia is None


def test_historial_con_gmd_calcula_entre_consecutivos(service, eventos):
    agregar_evento(eventos, animal_id=1, dias_desde_inicio=0, peso_kg=300.0)
    agregar_evento(eventos, animal_id=1, dias_desde_inicio=10, peso_kg=320.0)

    historial = service.historial_con_gmd(1)

    # viene en orden descendente (más reciente primero)
    assert historial[0].gmd_kg_dia == 2.0
    assert historial[1].gmd_kg_dia is None


def test_historial_con_gmd_ignora_pesajes_con_error(service, eventos):
    agregar_evento(eventos, animal_id=1, dias_desde_inicio=0, peso_kg=300.0)
    agregar_evento(eventos, animal_id=1, dias_desde_inicio=5, peso_kg=None, estado="error")
    agregar_evento(eventos, animal_id=1, dias_desde_inicio=10, peso_kg=320.0)

    historial = service.historial_con_gmd(1)

    assert historial[0].gmd_kg_dia is None  # el anterior fue un error, no hay base válida


def test_panel_promedia_el_ultimo_peso_de_cada_animal(service, eventos):
    agregar_evento(eventos, animal_id=1, dias_desde_inicio=0, peso_kg=300.0)
    agregar_evento(eventos, animal_id=1, dias_desde_inicio=10, peso_kg=320.0)
    agregar_evento(eventos, animal_id=2, dias_desde_inicio=0, peso_kg=400.0)

    panel = service.panel_establecimiento(establecimiento_id=1)

    assert panel.cantidad_animales == 2
    assert panel.peso_promedio_kg == 360.0  # promedio de (320, 400)
    assert panel.gmd_promedio_kg_dia == 2.0  # solo el animal 1 tiene 2 pesajes
