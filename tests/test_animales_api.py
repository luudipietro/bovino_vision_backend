def test_crear_y_listar_animal(client):
    response = client.post("/animales", json={"identificador": "AR001"})
    assert response.status_code == 201

    response = client.get("/animales")
    assert response.status_code == 200
    assert [a["identificador"] for a in response.json()] == ["AR001"]


def test_identificador_duplicado_devuelve_409(client):
    client.post("/animales", json={"identificador": "AR001"})

    response = client.post("/animales", json={"identificador": "AR001"})

    assert response.status_code == 409


def test_ficha_de_animal_inexistente_devuelve_404(client):
    response = client.get("/animales/999")

    assert response.status_code == 404


def test_crear_lote_y_asignar_a_animal(client):
    lote = client.post("/lotes", json={"codigo": "L1"}).json()

    response = client.post("/animales", json={"identificador": "AR002", "lote_id": lote["id"]})

    assert response.status_code == 201
    assert response.json()["lote_id"] == lote["id"]


def test_asignar_lote_inexistente_devuelve_404(client):
    response = client.post("/animales", json={"identificador": "AR003", "lote_id": 999})

    assert response.status_code == 404
