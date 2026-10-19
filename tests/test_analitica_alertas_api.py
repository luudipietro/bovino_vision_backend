from app.dependencies import get_weight_estimator
from app.domain.entities.estimacion import ResultadoEstimacion
from app.main import app
from tests.fakes import FakeWeightEstimatorAdapter


def _fijar_peso(peso_kg):
    app.dependency_overrides[get_weight_estimator] = lambda: FakeWeightEstimatorAdapter(
        ResultadoEstimacion(exitoso=True, peso_kg=peso_kg, version_modelo="fake-v1")
    )


def _crear_animal(client, identificador):
    return client.post("/animales", json={"identificador": identificador}).json()


def _pesar(client, animal_id, peso_kg):
    _fijar_peso(peso_kg)
    sesion = client.post("/sesiones-captura").json()
    captura = client.post(
        "/capturas",
        data={"sesion_id": sesion["id"], "animal_id": animal_id},
        files={"imagen": ("foto.jpg", b"contenido", "image/jpeg")},
    ).json()
    return client.post("/pesajes", json={"captura_id": captura["id"]}).json()


def test_variacion_grande_crea_alerta_visible_por_api(client):
    animal = _crear_animal(client, "AR-ALERTA-01")
    _pesar(client, animal["id"], 300.0)
    segundo_pesaje = _pesar(client, animal["id"], 450.0)

    assert segundo_pesaje["alerta"] is not None
    assert segundo_pesaje["alerta"]["prioridad"] == "alta"

    alertas_animal = client.get(f"/animales/{animal['id']}/alertas").json()
    assert len(alertas_animal) == 1

    alertas_rodeo = client.get("/alertas").json()
    assert any(a["animal_id"] == animal["id"] for a in alertas_rodeo)


def test_variacion_chica_no_crea_alerta(client):
    animal = _crear_animal(client, "AR-ALERTA-02")
    _pesar(client, animal["id"], 300.0)
    segundo_pesaje = _pesar(client, animal["id"], 305.0)

    assert segundo_pesaje["alerta"] is None
    assert client.get(f"/animales/{animal['id']}/alertas").json() == []


def test_historial_incluye_gmd(client):
    animal = _crear_animal(client, "AR-GMD-01")
    _pesar(client, animal["id"], 300.0)
    _pesar(client, animal["id"], 320.0)

    historial = client.get(f"/animales/{animal['id']}/pesajes").json()

    # Ambos pesajes ocurren el mismo día en el test, así que la GMD (que es por día)
    # da None en los dos: en el primero por no tener anterior, en el segundo porque
    # la diferencia de días contra el anterior es 0. La cuenta en sí se prueba de
    # forma aislada (con fechas distintas) en test_analitica_service.py.
    assert historial[0]["gmd_kg_dia"] is None
    assert historial[1]["gmd_kg_dia"] is None


def test_panel_del_rodeo_agrega_datos(client):
    animal1 = _crear_animal(client, "AR-PANEL-01")
    animal2 = _crear_animal(client, "AR-PANEL-02")
    _pesar(client, animal1["id"], 300.0)
    _pesar(client, animal2["id"], 400.0)

    panel = client.get("/panel").json()

    assert panel["cantidad_animales"] == 2
    assert panel["peso_promedio_kg"] == 350.0


def test_reporte_filtra_por_lapso_de_tiempo(client):
    animal = _crear_animal(client, "AR-REPORTE-01")
    _pesar(client, animal["id"], 300.0)

    hoy = client.get("/reportes", params={"desde": "2020-01-01", "hasta": "2020-01-02"}).json()
    assert hoy["cantidad_pesajes"] == 0

    from datetime import date

    reporte_amplio = client.get(
        "/reportes", params={"desde": "2020-01-01", "hasta": date.today().isoformat()}
    ).json()
    assert reporte_amplio["cantidad_pesajes"] >= 1
