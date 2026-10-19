from sqlalchemy.orm import Session

from app.domain.entities.sesion_captura import SesionCaptura
from app.domain.ports.sesion_captura_repository import SesionCapturaRepository
from app.models.sesion_captura import SesionCapturaModel


class SqlAlchemySesionCapturaRepository(SesionCapturaRepository):
    def __init__(self, db: Session):
        self._db = db

    def add(self, sesion: SesionCaptura) -> SesionCaptura:
        model = SesionCapturaModel(
            establecimiento_id=sesion.establecimiento_id,
            fecha_inicio=sesion.fecha_inicio,
        )
        self._db.add(model)
        self._db.commit()
        self._db.refresh(model)
        return _to_entity(model)

    def get(self, sesion_id: int) -> SesionCaptura | None:
        model = self._db.get(SesionCapturaModel, sesion_id)
        return _to_entity(model) if model else None


def _to_entity(model: SesionCapturaModel) -> SesionCaptura:
    return SesionCaptura(
        id=model.id,
        establecimiento_id=model.establecimiento_id,
        fecha_inicio=model.fecha_inicio,
    )
