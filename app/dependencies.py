from pathlib import Path

from fastapi import Depends
from sqlalchemy.orm import Session

from app.core.config import get_settings
from app.core.database import get_db
from app.domain.ports.image_storage import ImageStoragePort
from app.domain.ports.weight_estimator import WeightEstimatorPort
from app.estimators.mock_weight_estimator import MockWeightEstimatorAdapter
from app.repositories.sqlalchemy_alerta_repository import SqlAlchemyAlertaRepository
from app.repositories.sqlalchemy_animal_repository import SqlAlchemyAnimalRepository
from app.repositories.sqlalchemy_captura_repository import SqlAlchemyCapturaRepository
from app.repositories.sqlalchemy_evento_pesaje_repository import (
    SqlAlchemyEventoPesajeRepository,
)
from app.repositories.sqlalchemy_lote_repository import SqlAlchemyLoteRepository
from app.repositories.sqlalchemy_sesion_captura_repository import (
    SqlAlchemySesionCapturaRepository,
)
from app.services.alerta_service import AlertaService
from app.services.analitica_service import AnaliticaService
from app.services.animal_service import AnimalService
from app.services.captura_service import CapturaService
from app.services.lote_service import LoteService
from app.services.pesaje_service import PesajeService
from app.storage.local_image_storage import LocalImageStorageAdapter


def get_establecimiento_id() -> int:
    return get_settings().default_establecimiento_id


def get_lote_repository(db: Session = Depends(get_db)) -> SqlAlchemyLoteRepository:
    return SqlAlchemyLoteRepository(db)


def get_animal_repository(db: Session = Depends(get_db)) -> SqlAlchemyAnimalRepository:
    return SqlAlchemyAnimalRepository(db)


def get_lote_service(
    lote_repository: SqlAlchemyLoteRepository = Depends(get_lote_repository),
) -> LoteService:
    return LoteService(lote_repository)


def get_animal_service(
    animal_repository: SqlAlchemyAnimalRepository = Depends(get_animal_repository),
    lote_repository: SqlAlchemyLoteRepository = Depends(get_lote_repository),
) -> AnimalService:
    return AnimalService(animal_repository, lote_repository)


def get_sesion_captura_repository(
    db: Session = Depends(get_db),
) -> SqlAlchemySesionCapturaRepository:
    return SqlAlchemySesionCapturaRepository(db)


def get_captura_repository(db: Session = Depends(get_db)) -> SqlAlchemyCapturaRepository:
    return SqlAlchemyCapturaRepository(db)


def get_evento_pesaje_repository(
    db: Session = Depends(get_db),
) -> SqlAlchemyEventoPesajeRepository:
    return SqlAlchemyEventoPesajeRepository(db)


def get_image_storage() -> ImageStoragePort:
    return LocalImageStorageAdapter(Path(get_settings().uploads_dir))


def get_weight_estimator() -> WeightEstimatorPort:
    return MockWeightEstimatorAdapter()


def get_captura_service(
    sesion_repository: SqlAlchemySesionCapturaRepository = Depends(get_sesion_captura_repository),
    captura_repository: SqlAlchemyCapturaRepository = Depends(get_captura_repository),
    animal_repository: SqlAlchemyAnimalRepository = Depends(get_animal_repository),
    image_storage: ImageStoragePort = Depends(get_image_storage),
) -> CapturaService:
    return CapturaService(sesion_repository, captura_repository, animal_repository, image_storage)


def get_alerta_repository(db: Session = Depends(get_db)) -> SqlAlchemyAlertaRepository:
    return SqlAlchemyAlertaRepository(db)


def get_alerta_service(
    alerta_repository: SqlAlchemyAlertaRepository = Depends(get_alerta_repository),
) -> AlertaService:
    return AlertaService(alerta_repository)


def get_analitica_service(
    evento_repository: SqlAlchemyEventoPesajeRepository = Depends(get_evento_pesaje_repository),
    alerta_repository: SqlAlchemyAlertaRepository = Depends(get_alerta_repository),
) -> AnaliticaService:
    return AnaliticaService(evento_repository, alerta_repository)


def get_pesaje_service(
    captura_repository: SqlAlchemyCapturaRepository = Depends(get_captura_repository),
    evento_repository: SqlAlchemyEventoPesajeRepository = Depends(get_evento_pesaje_repository),
    animal_repository: SqlAlchemyAnimalRepository = Depends(get_animal_repository),
    lote_repository: SqlAlchemyLoteRepository = Depends(get_lote_repository),
    weight_estimator: WeightEstimatorPort = Depends(get_weight_estimator),
    alerta_service: AlertaService = Depends(get_alerta_service),
) -> PesajeService:
    return PesajeService(
        captura_repository,
        evento_repository,
        animal_repository,
        lote_repository,
        weight_estimator,
        alerta_service,
    )
