from fastapi import APIRouter, Depends

from app.dependencies import (
    get_analitica_service,
    get_animal_service,
    get_establecimiento_id,
)
from app.domain.entities.alerta import Alerta
from app.schemas.alerta import AlertaRead
from app.services.analitica_service import AnaliticaService
from app.services.animal_service import AnimalService

router = APIRouter(prefix="/alertas", tags=["alertas"])


@router.get("", response_model=list[AlertaRead])
def listar_alertas_del_rodeo(
    establecimiento_id: int = Depends(get_establecimiento_id),
    analitica_service: AnaliticaService = Depends(get_analitica_service),
    animal_service: AnimalService = Depends(get_animal_service),
):
    alertas = analitica_service.alertas_priorizadas(establecimiento_id)
    return [_a_schema(a, animal_service.obtener_ficha(a.animal_id).identificador) for a in alertas]


def _a_schema(alerta: Alerta, animal_identificador: str) -> AlertaRead:
    return AlertaRead(
        id=alerta.id,
        animal_id=alerta.animal_id,
        tipo=alerta.tipo,
        prioridad=alerta.prioridad,
        variacion_pct=alerta.variacion_pct,
        fecha_generacion=alerta.fecha_generacion,
        estado=alerta.estado,
        animal_identificador=animal_identificador,
    )
