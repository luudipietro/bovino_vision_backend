from fastapi import Depends
from sqlalchemy.orm import Session

from app.core.config import get_settings
from app.core.database import get_db
from app.repositories.sqlalchemy_animal_repository import SqlAlchemyAnimalRepository
from app.repositories.sqlalchemy_lote_repository import SqlAlchemyLoteRepository
from app.services.animal_service import AnimalService
from app.services.lote_service import LoteService


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
