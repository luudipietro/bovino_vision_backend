from fastapi import APIRouter, Depends, File, Form, UploadFile

from app.dependencies import get_captura_service, get_establecimiento_id
from app.schemas.captura import CapturaRead, SesionCapturaRead
from app.services.captura_service import CapturaService

router = APIRouter(tags=["captura"])


@router.post("/sesiones-captura", response_model=SesionCapturaRead, status_code=201)
def iniciar_sesion_captura(
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: CapturaService = Depends(get_captura_service),
):
    return service.iniciar_sesion(establecimiento_id)


@router.get("/sesiones-captura/{sesion_id}/capturas", response_model=list[CapturaRead])
def listar_capturas_de_sesion(
    sesion_id: int,
    service: CapturaService = Depends(get_captura_service),
):
    return service.listar_capturas_de_sesion(sesion_id)


@router.post("/capturas", response_model=CapturaRead, status_code=201)
async def registrar_captura(
    sesion_id: int = Form(...),
    animal_id: int = Form(...),
    imagen: UploadFile = File(...),
    service: CapturaService = Depends(get_captura_service),
):
    contenido = await imagen.read()
    return service.registrar_captura(
        sesion_id=sesion_id,
        animal_id=animal_id,
        nombre_archivo=imagen.filename or "captura.jpg",
        contenido=contenido,
        content_type=imagen.content_type or "",
    )
