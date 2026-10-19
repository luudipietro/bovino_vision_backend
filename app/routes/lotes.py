from fastapi import APIRouter, Depends

from app.dependencies import get_establecimiento_id, get_lote_service
from app.schemas.lote import LoteCreate, LoteRead
from app.services.lote_service import LoteService

router = APIRouter(prefix="/lotes", tags=["lotes"])


@router.get("", response_model=list[LoteRead])
def listar_lotes(
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: LoteService = Depends(get_lote_service),
):
    return service.listar_lotes(establecimiento_id)


@router.post("", response_model=LoteRead, status_code=201)
def crear_lote(
    payload: LoteCreate,
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: LoteService = Depends(get_lote_service),
):
    return service.crear_lote(payload.codigo, establecimiento_id)
