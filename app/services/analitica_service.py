from datetime import date
from itertools import pairwise

from app.domain.entities.alerta import Alerta
from app.domain.entities.evento_pesaje import EventoPesaje
from app.domain.entities.panel_rodeo import PanelRodeo
from app.domain.entities.pesaje_con_gmd import PesajeConGmd
from app.domain.entities.punto_evolucion import PuntoEvolucion
from app.domain.entities.reporte import Reporte
from app.domain.ports.alerta_repository import AlertaRepository
from app.domain.ports.evento_pesaje_repository import EventoPesajeRepository


class AnaliticaService:
    """Lado de lectura del módulo de Analítica y Reportes: GMD, evolución de peso,
    panel del rodeo y alertas. No escribe nada (eso lo hace AlertaService como
    efecto colateral de PesajeService al procesar un pesaje)."""

    def __init__(
        self,
        evento_repository: EventoPesajeRepository,
        alerta_repository: AlertaRepository,
    ):
        self._eventos = evento_repository
        self._alertas = alerta_repository

    def historial_con_gmd(self, animal_id: int) -> list[PesajeConGmd]:
        eventos = sorted(self._eventos.list_by_animal(animal_id), key=lambda e: e.fecha_pesaje)

        resultado = []
        anterior = None
        for evento in eventos:
            gmd = None
            if anterior is not None and anterior.estado == "estimado" and evento.estado == "estimado":
                gmd = _calcular_gmd(anterior, evento)
            resultado.append(PesajeConGmd(evento=evento, gmd_kg_dia=gmd))
            anterior = evento

        resultado.reverse()
        return resultado

    def alertas_de_animal(self, animal_id: int) -> list[Alerta]:
        return self._alertas.list_by_animal(animal_id)

    def alertas_priorizadas(self, establecimiento_id: int) -> list[Alerta]:
        return self._alertas.list_by_establecimiento(establecimiento_id)

    def alerta_de_evento(self, evento_pesaje_id: int) -> Alerta | None:
        return self._alertas.get_by_evento(evento_pesaje_id)

    def panel_establecimiento(
        self, establecimiento_id: int, lote_id: int | None = None
    ) -> PanelRodeo:
        eventos = [
            e
            for e in self._eventos.list_by_establecimiento(establecimiento_id, lote_id=lote_id)
            if e.estado == "estimado"
        ]
        por_animal = _agrupar_por_animal(eventos)

        ultimos = [eventos_animal[-1] for eventos_animal in por_animal.values()]
        gmds = _gmd_mas_reciente_por_animal(por_animal)

        return PanelRodeo(
            cantidad_animales=len(por_animal),
            peso_promedio_kg=_promedio([e.peso_kg for e in ultimos]),
            gmd_promedio_kg_dia=_promedio(gmds),
            evolucion_peso_promedio=_evolucion_por_fecha(eventos),
        )

    def reporte(
        self,
        establecimiento_id: int,
        desde: date,
        hasta: date,
        lote_id: int | None = None,
    ) -> Reporte:
        eventos = [
            e
            for e in self._eventos.list_by_establecimiento(
                establecimiento_id, lote_id=lote_id, desde=desde, hasta=hasta
            )
            if e.estado == "estimado"
        ]
        por_animal = _agrupar_por_animal(eventos)

        return Reporte(
            desde=desde,
            hasta=hasta,
            cantidad_pesajes=len(eventos),
            peso_promedio_kg=_promedio([e.peso_kg for e in eventos]),
            gmd_promedio_kg_dia=_promedio(_gmd_entre_consecutivos_por_animal(por_animal)),
        )


def _agrupar_por_animal(eventos: list[EventoPesaje]) -> dict[int, list[EventoPesaje]]:
    por_animal: dict[int, list[EventoPesaje]] = {}
    for evento in eventos:
        por_animal.setdefault(evento.animal_id, []).append(evento)
    for eventos_animal in por_animal.values():
        eventos_animal.sort(key=lambda e: e.fecha_pesaje)
    return por_animal


def _gmd_mas_reciente_por_animal(por_animal: dict[int, list[EventoPesaje]]) -> list[float]:
    gmds = []
    for eventos_animal in por_animal.values():
        if len(eventos_animal) >= 2:
            gmd = _calcular_gmd(eventos_animal[-2], eventos_animal[-1])
            if gmd is not None:
                gmds.append(gmd)
    return gmds


def _gmd_entre_consecutivos_por_animal(por_animal: dict[int, list[EventoPesaje]]) -> list[float]:
    gmds = []
    for eventos_animal in por_animal.values():
        for anterior, siguiente in pairwise(eventos_animal):
            gmd = _calcular_gmd(anterior, siguiente)
            if gmd is not None:
                gmds.append(gmd)
    return gmds


def _evolucion_por_fecha(eventos: list[EventoPesaje]) -> list[PuntoEvolucion]:
    por_fecha: dict[date, list[float]] = {}
    for evento in eventos:
        por_fecha.setdefault(evento.fecha_pesaje.date(), []).append(evento.peso_kg)
    return [
        PuntoEvolucion(fecha=fecha, peso_promedio_kg=_promedio(pesos))
        for fecha, pesos in sorted(por_fecha.items())
    ]


def _calcular_gmd(anterior: EventoPesaje, siguiente: EventoPesaje) -> float | None:
    dias = (siguiente.fecha_pesaje.date() - anterior.fecha_pesaje.date()).days
    if dias <= 0 or anterior.peso_kg is None or siguiente.peso_kg is None:
        return None
    return round((siguiente.peso_kg - anterior.peso_kg) / dias, 2)


def _promedio(valores: list[float | None]) -> float | None:
    presentes = [v for v in valores if v is not None]
    return round(sum(presentes) / len(presentes), 1) if presentes else None
