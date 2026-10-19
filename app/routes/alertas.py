from fastapi import APIRouter, Depends

from app.dependencies import get_analitica_service, get_establecimiento_id
from app.schemas.alerta import AlertaRead
from app.services.analitica_service import AnaliticaService

router = APIRouter(prefix="/alertas", tags=["alertas"])


@router.get("", response_model=list[AlertaRead])
def listar_alertas_del_rodeo(
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: AnaliticaService = Depends(get_analitica_service),
):
    return service.alertas_priorizadas(establecimiento_id)
