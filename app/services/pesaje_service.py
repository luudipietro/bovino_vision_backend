from datetime import UTC, datetime

from app.core.exceptions import (
    CapturaInvalidaError,
    CapturaNoEncontradaError,
    CapturaYaProcesadaError,
    EventoPesajeNoEncontradoError,
)
from app.domain.entities.evento_pesaje import EventoPesaje
from app.domain.entities.resultado_pesaje import ResultadoPesaje
from app.domain.ports.animal_repository import AnimalRepository
from app.domain.ports.captura_repository import CapturaRepository
from app.domain.ports.evento_pesaje_repository import EventoPesajeRepository
from app.domain.ports.lote_repository import LoteRepository
from app.domain.ports.weight_estimator import WeightEstimatorPort
from app.services.alerta_service import AlertaService


class PesajeService:
    def __init__(
        self,
        captura_repository: CapturaRepository,
        evento_repository: EventoPesajeRepository,
        animal_repository: AnimalRepository,
        lote_repository: LoteRepository,
        weight_estimator: WeightEstimatorPort,
        alerta_service: AlertaService,
    ):
        self._capturas = captura_repository
        self._eventos = evento_repository
        self._animales = animal_repository
        self._lotes = lote_repository
        self._estimador = weight_estimator
        self._alerta_service = alerta_service

    def procesar_pesaje(self, captura_id: int) -> EventoPesaje:
        captura = self._capturas.get(captura_id)
        if captura is None:
            raise CapturaNoEncontradaError(captura_id)
        if not captura.validada:
            raise CapturaInvalidaError(captura_id)
        if self._eventos.get_by_captura(captura_id) is not None:
            raise CapturaYaProcesadaError(captura_id)

        animal = self._animales.get(captura.animal_id)
        peso_previo = self._ultimo_peso_estimado(captura.animal_id)
        resultado = self._estimador.estimar_peso(captura.archivo_path)

        evento = EventoPesaje(
            id=None,
            animal_id=captura.animal_id,
            captura_id=captura_id,
            estado="estimado" if resultado.exitoso else "error",
            fecha_pesaje=datetime.now(UTC).replace(tzinfo=None),
            peso_kg=resultado.peso_kg,
            version_modelo=resultado.version_modelo,
            motivo_error=resultado.motivo_error,
        )
        evento = self._eventos.add(evento)

        if evento.estado == "estimado":
            peso_previo_kg = peso_previo.peso_kg if peso_previo else None
            self._alerta_service.evaluar_pesaje(evento, animal.establecimiento_id, peso_previo_kg)

        return evento

    def obtener_resultado(self, evento_id: int) -> ResultadoPesaje:
        evento = self._eventos.get(evento_id)
        if evento is None:
            raise EventoPesajeNoEncontradoError(evento_id)

        animal = self._animales.get(evento.animal_id)
        lote = self._lotes.get(animal.lote_id) if animal and animal.lote_id else None
        peso_previo = self._ultimo_peso_estimado(evento.animal_id, antes_de_id=evento.id)

        return ResultadoPesaje(evento=evento, animal=animal, lote=lote, peso_previo=peso_previo)

    def _ultimo_peso_estimado(
        self, animal_id: int, antes_de_id: int | None = None
    ) -> EventoPesaje | None:
        # Se ordena por id (orden de registro) en vez de fecha_pesaje: todavía no hay
        # forma de cargar un pesaje con fecha pasada, y el id evita ambigüedad cuando
        # dos eventos quedan con el mismo timestamp.
        anteriores = [
            e
            for e in self._eventos.list_by_animal(animal_id)
            if e.estado == "estimado" and (antes_de_id is None or e.id < antes_de_id)
        ]
        if not anteriores:
            return None
        return max(anteriores, key=lambda e: e.id)
