from datetime import date, timedelta

from sqlalchemy.orm import Session

from app.domain.entities.evento_pesaje import EventoPesaje
from app.domain.ports.evento_pesaje_repository import EventoPesajeRepository
from app.models.animal import AnimalModel
from app.models.evento_pesaje import EventoPesajeModel


class SqlAlchemyEventoPesajeRepository(EventoPesajeRepository):
    def __init__(self, db: Session):
        self._db = db

    def add(self, evento: EventoPesaje) -> EventoPesaje:
        model = EventoPesajeModel(
            animal_id=evento.animal_id,
            captura_id=evento.captura_id,
            estado=evento.estado,
            fecha_pesaje=evento.fecha_pesaje,
            peso_kg=evento.peso_kg,
            version_modelo=evento.version_modelo,
            motivo_error=evento.motivo_error,
        )
        self._db.add(model)
        self._db.commit()
        self._db.refresh(model)
        return _to_entity(model)

    def get(self, evento_id: int) -> EventoPesaje | None:
        model = self._db.get(EventoPesajeModel, evento_id)
        return _to_entity(model) if model else None

    def get_by_captura(self, captura_id: int) -> EventoPesaje | None:
        model = (
            self._db.query(EventoPesajeModel)
            .filter(EventoPesajeModel.captura_id == captura_id)
            .first()
        )
        return _to_entity(model) if model else None

    def list_by_animal(self, animal_id: int) -> list[EventoPesaje]:
        models = (
            self._db.query(EventoPesajeModel)
            .filter(EventoPesajeModel.animal_id == animal_id)
            .order_by(EventoPesajeModel.fecha_pesaje.desc())
            .all()
        )
        return [_to_entity(model) for model in models]

    def list_by_establecimiento(
        self,
        establecimiento_id: int,
        lote_id: int | None = None,
        desde: date | None = None,
        hasta: date | None = None,
    ) -> list[EventoPesaje]:
        query = (
            self._db.query(EventoPesajeModel)
            .join(AnimalModel, EventoPesajeModel.animal_id == AnimalModel.id)
            .filter(AnimalModel.establecimiento_id == establecimiento_id)
        )
        if lote_id is not None:
            query = query.filter(AnimalModel.lote_id == lote_id)
        if desde is not None:
            query = query.filter(EventoPesajeModel.fecha_pesaje >= desde)
        if hasta is not None:
            query = query.filter(EventoPesajeModel.fecha_pesaje < hasta + timedelta(days=1))

        models = query.order_by(EventoPesajeModel.fecha_pesaje).all()
        return [_to_entity(model) for model in models]


def _to_entity(model: EventoPesajeModel) -> EventoPesaje:
    return EventoPesaje(
        id=model.id,
        animal_id=model.animal_id,
        captura_id=model.captura_id,
        estado=model.estado,
        fecha_pesaje=model.fecha_pesaje,
        peso_kg=model.peso_kg,
        version_modelo=model.version_modelo,
        motivo_error=model.motivo_error,
    )
