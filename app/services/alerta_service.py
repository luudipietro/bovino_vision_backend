from app.domain.entities.alerta import Alerta
from app.domain.entities.evento_pesaje import EventoPesaje
from app.domain.ports.alerta_repository import AlertaRepository

UMBRAL_VARIACION_PCT = 15.0
UMBRAL_PRIORIDAD_ALTA_PCT = 30.0


class AlertaService:
    """Evalúa si un pesaje recién procesado implica una variación de peso anómala
    respecto al pesaje anterior del mismo animal, y registra una Alerta si corresponde."""

    def __init__(self, alerta_repository: AlertaRepository):
        self._alertas = alerta_repository

    def evaluar_pesaje(
        self,
        evento: EventoPesaje,
        establecimiento_id: int,
        peso_previo_kg: float | None,
    ) -> Alerta | None:
        if peso_previo_kg is None or evento.peso_kg is None:
            return None

        variacion_pct = (evento.peso_kg - peso_previo_kg) / peso_previo_kg * 100
        if abs(variacion_pct) < UMBRAL_VARIACION_PCT:
            return None

        prioridad = "alta" if abs(variacion_pct) >= UMBRAL_PRIORIDAD_ALTA_PCT else "media"
        alerta = Alerta(
            id=None,
            animal_id=evento.animal_id,
            establecimiento_id=establecimiento_id,
            evento_pesaje_id=evento.id,
            tipo="variacion_peso",
            prioridad=prioridad,
            variacion_pct=round(variacion_pct, 1),
            fecha_generacion=evento.fecha_pesaje,
        )
        return self._alertas.add(alerta)
