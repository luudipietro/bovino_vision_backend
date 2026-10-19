from sqlalchemy.orm import Session

from app.domain.entities.lote import Lote
from app.domain.ports.lote_repository import LoteRepository
from app.models.lote import LoteModel


class SqlAlchemyLoteRepository(LoteRepository):
    def __init__(self, db: Session):
        self._db = db

    def add(self, lote: Lote) -> Lote:
        model = LoteModel(codigo=lote.codigo, establecimiento_id=lote.establecimiento_id)
        self._db.add(model)
        self._db.commit()
        self._db.refresh(model)
        return _to_entity(model)

    def list_by_establecimiento(self, establecimiento_id: int) -> list[Lote]:
        models = (
            self._db.query(LoteModel)
            .filter(LoteModel.establecimiento_id == establecimiento_id)
            .order_by(LoteModel.codigo)
            .all()
        )
        return [_to_entity(model) for model in models]

    def get(self, lote_id: int) -> Lote | None:
        model = self._db.get(LoteModel, lote_id)
        return _to_entity(model) if model else None


def _to_entity(model: LoteModel) -> Lote:
    return Lote(
        id=model.id,
        codigo=model.codigo,
        establecimiento_id=model.establecimiento_id,
    )
