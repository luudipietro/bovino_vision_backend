from datetime import date

from fastapi import APIRouter, Depends

from app.dependencies import get_analitica_service, get_establecimiento_id
from app.schemas.analitica import PanelRodeoRead, ReporteRead
from app.services.analitica_service import AnaliticaService

router = APIRouter(tags=["analitica"])


@router.get("/panel", response_model=PanelRodeoRead)
def ver_panel_del_rodeo(
    lote_id: int | None = None,
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: AnaliticaService = Depends(get_analitica_service),
):
    return service.panel_establecimiento(establecimiento_id, lote_id)


@router.get("/reportes", response_model=ReporteRead)
def generar_reporte(
    desde: date,
    hasta: date,
    lote_id: int | None = None,
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: AnaliticaService = Depends(get_analitica_service),
):
    return service.reporte(establecimiento_id, desde, hasta, lote_id)
