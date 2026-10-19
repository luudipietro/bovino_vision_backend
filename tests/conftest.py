import pytest
from fastapi.testclient import TestClient
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker
from sqlalchemy.pool import StaticPool

import app.models
from app.core.database import Base, get_db
from app.dependencies import get_image_storage
from app.main import app
from tests.fakes import FakeImageStorageAdapter


@pytest.fixture
def client():
    # StaticPool: SQLite ":memory:" crea una base nueva por cada conexión, así que sin
    # esto cada sesión vería una base vacía distinta.
    engine = create_engine(
        "sqlite:///:memory:",
        connect_args={"check_same_thread": False},
        poolclass=StaticPool,
    )
    testing_session_local = sessionmaker(bind=engine)
    Base.metadata.create_all(engine)

    def override_get_db():
        db = testing_session_local()
        try:
            yield db
        finally:
            db.close()

    app.dependency_overrides[get_db] = override_get_db
    # Evita que los tests de integración escriban imágenes reales en disco.
    app.dependency_overrides[get_image_storage] = FakeImageStorageAdapter
    yield TestClient(app)
    app.dependency_overrides.clear()
