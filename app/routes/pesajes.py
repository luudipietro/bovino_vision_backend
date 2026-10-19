from fastapi import APIRouter, Depends

from app.dependencies import get_analitica_service, get_pesaje_service
from app.domain.entities.alerta import Alerta
from app.domain.entities.resultado_pesaje import ResultadoPesaje
from app.schemas.alerta import AlertaRead
from app.schemas.pesaje import PesajeCreate, PesajeRead, UltimoPesoRead
from app.services.analitica_service import AnaliticaService
from app.services.pesaje_service import PesajeService

router = APIRouter(prefix="/pesajes", tags=["pesajes"])


@router.post("", response_model=PesajeRead, status_code=201)
def procesar_pesaje(
    payload: PesajeCreate,
    service: PesajeService = Depends(get_pesaje_service),
    analitica_service: AnaliticaService = Depends(get_analitica_service),
):
    evento = service.procesar_pesaje(payload.captura_id)
    resultado = service.obtener_resultado(evento.id)
    alerta = analitica_service.alerta_de_evento(evento.id)
    return _a_schema(resultado, alerta)


@router.get("/{evento_id}", response_model=PesajeRead)
def ver_resultado_pesaje(
    evento_id: int,
    service: PesajeService = Depends(get_pesaje_service),
    analitica_service: AnaliticaService = Depends(get_analitica_service),
):
    resultado = service.obtener_resultado(evento_id)
    alerta = analitica_service.alerta_de_evento(evento_id)
    return _a_schema(resultado, alerta)


def _a_schema(resultado: ResultadoPesaje, alerta: Alerta | None) -> PesajeRead:
    return PesajeRead(
        id=resultado.evento.id,
        animal_id=resultado.evento.animal_id,
        estado=resultado.evento.estado,
        peso_kg=resultado.evento.peso_kg,
        motivo_error=resultado.evento.motivo_error,
        fecha_pesaje=resultado.evento.fecha_pesaje,
        version_modelo=resultado.evento.version_modelo,
        animal_identificador=resultado.animal.identificador,
        lote_codigo=resultado.lote.codigo if resultado.lote else None,
        ultimo_peso_previo=(
            UltimoPesoRead(
                peso_kg=resultado.peso_previo.peso_kg,
                fecha_pesaje=resultado.peso_previo.fecha_pesaje,
            )
            if resultado.peso_previo
            else None
        ),
        alerta=AlertaRead.model_validate(alerta) if alerta else None,
    )
