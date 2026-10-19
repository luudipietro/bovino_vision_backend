from sqlalchemy import case
from sqlalchemy.orm import Session

from app.domain.entities.alerta import Alerta
from app.domain.ports.alerta_repository import AlertaRepository
from app.models.alerta import AlertaModel


class SqlAlchemyAlertaRepository(AlertaRepository):
    def __init__(self, db: Session):
        self._db = db

    def add(self, alerta: Alerta) -> Alerta:
        model = AlertaModel(
            animal_id=alerta.animal_id,
            establecimiento_id=alerta.establecimiento_id,
            evento_pesaje_id=alerta.evento_pesaje_id,
            tipo=alerta.tipo,
            prioridad=alerta.prioridad,
            variacion_pct=alerta.variacion_pct,
            fecha_generacion=alerta.fecha_generacion,
            estado=alerta.estado,
        )
        self._db.add(model)
        self._db.commit()
        self._db.refresh(model)
        return _to_entity(model)

    def get_by_evento(self, evento_pesaje_id: int) -> Alerta | None:
        model = (
            self._db.query(AlertaModel)
            .filter(AlertaModel.evento_pesaje_id == evento_pesaje_id)
            .first()
        )
        return _to_entity(model) if model else None

    def list_by_animal(self, animal_id: int) -> list[Alerta]:
        models = (
            self._db.query(AlertaModel)
            .filter(AlertaModel.animal_id == animal_id)
            .order_by(AlertaModel.fecha_generacion.desc())
            .all()
        )
        return [_to_entity(model) for model in models]

    def list_by_establecimiento(self, establecimiento_id: int) -> list[Alerta]:
        orden_prioridad = case(
            (AlertaModel.prioridad == "alta", 0),
            (AlertaModel.prioridad == "media", 1),
            else_=2,
        )
        models = (
            self._db.query(AlertaModel)
            .filter(AlertaModel.establecimiento_id == establecimiento_id)
            .order_by(orden_prioridad, AlertaModel.fecha_generacion.desc())
            .all()
        )
        return [_to_entity(model) for model in models]


def _to_entity(model: AlertaModel) -> Alerta:
    return Alerta(
        id=model.id,
        animal_id=model.animal_id,
        establecimiento_id=model.establecimiento_id,
        evento_pesaje_id=model.evento_pesaje_id,
        tipo=model.tipo,
        prioridad=model.prioridad,
        variacion_pct=model.variacion_pct,
        fecha_generacion=model.fecha_generacion,
        estado=model.estado,
    )
