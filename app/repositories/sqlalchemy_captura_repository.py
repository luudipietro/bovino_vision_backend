from sqlalchemy.orm import Session

from app.domain.entities.captura import Captura
from app.domain.ports.captura_repository import CapturaRepository
from app.models.captura import CapturaModel


class SqlAlchemyCapturaRepository(CapturaRepository):
    def __init__(self, db: Session):
        self._db = db

    def add(self, captura: Captura) -> Captura:
        model = CapturaModel(
            sesion_id=captura.sesion_id,
            animal_id=captura.animal_id,
            archivo_path=captura.archivo_path,
            validada=captura.validada,
            motivo_invalidez=captura.motivo_invalidez,
        )
        self._db.add(model)
        self._db.commit()
        self._db.refresh(model)
        return _to_entity(model)

    def get(self, captura_id: int) -> Captura | None:
        model = self._db.get(CapturaModel, captura_id)
        return _to_entity(model) if model else None

    def list_by_sesion(self, sesion_id: int) -> list[Captura]:
        models = (
            self._db.query(CapturaModel)
            .filter(CapturaModel.sesion_id == sesion_id)
            .order_by(CapturaModel.id)
            .all()
        )
        return [_to_entity(model) for model in models]


def _to_entity(model: CapturaModel) -> Captura:
    return Captura(
        id=model.id,
        sesion_id=model.sesion_id,
        animal_id=model.animal_id,
        archivo_path=model.archivo_path,
        validada=model.validada,
        motivo_invalidez=model.motivo_invalidez,
    )
