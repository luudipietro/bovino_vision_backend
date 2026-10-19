def _crear_animal(client, identificador="AR001"):
    return client.post("/animales", json={"identificador": identificador}).json()


def _iniciar_sesion(client):
    return client.post("/sesiones-captura").json()


def _subir_captura(client, sesion_id, animal_id, content_type="image/jpeg"):
    return client.post(
        "/capturas",
        data={"sesion_id": sesion_id, "animal_id": animal_id},
        files={"imagen": ("foto.jpg", b"contenido-de-prueba", content_type)},
    )


def test_flujo_completo_captura_y_pesaje(client):
    animal = _crear_animal(client)
    sesion = _iniciar_sesion(client)

    captura = _subir_captura(client, sesion["id"], animal["id"]).json()
    assert captura["validada"] is True

    response = client.post("/pesajes", json={"captura_id": captura["id"]})
    assert response.status_code == 201

    pesaje = response.json()
    assert pesaje["estado"] == "estimado"
    assert pesaje["peso_kg"] is not None
    assert pesaje["animal_identificador"] == "AR001"
    assert pesaje["ultimo_peso_previo"] is None

    historial = client.get(f"/animales/{animal['id']}/pesajes").json()
    assert len(historial) == 1
    assert historial[0]["id"] == pesaje["id"]


def test_captura_con_formato_invalido_no_se_puede_procesar(client):
    animal = _crear_animal(client)
    sesion = _iniciar_sesion(client)

    captura = _subir_captura(client, sesion["id"], animal["id"], content_type="application/pdf").json()
    assert captura["validada"] is False

    response = client.post("/pesajes", json={"captura_id": captura["id"]})
    assert response.status_code == 422


def test_no_permite_procesar_la_misma_captura_dos_veces(client):
    animal = _crear_animal(client)
    sesion = _iniciar_sesion(client)
    captura = _subir_captura(client, sesion["id"], animal["id"]).json()

    client.post("/pesajes", json={"captura_id": captura["id"]})
    response = client.post("/pesajes", json={"captura_id": captura["id"]})

    assert response.status_code == 409


def test_segundo_pesaje_incluye_peso_previo(client):
    animal = _crear_animal(client)
    sesion = _iniciar_sesion(client)

    primera_captura = _subir_captura(client, sesion["id"], animal["id"]).json()
    client.post("/pesajes", json={"captura_id": primera_captura["id"]})

    segunda_captura = _subir_captura(client, sesion["id"], animal["id"]).json()
    segundo_pesaje = client.post("/pesajes", json={"captura_id": segunda_captura["id"]}).json()

    assert segundo_pesaje["ultimo_peso_previo"] is not None
