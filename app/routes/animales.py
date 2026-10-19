from fastapi import APIRouter, Depends, Query

from app.dependencies import get_animal_service, get_establecimiento_id
from app.schemas.animal import AnimalCreate, AnimalRead, AnimalUpdate
from app.services.animal_service import AnimalService

router = APIRouter(prefix="/animales", tags=["animales"])


@router.get("", response_model=list[AnimalRead])
def listar_animales(
    lote_id: int | None = None,
    search: str | None = Query(default=None, alias="q"),
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: AnimalService = Depends(get_animal_service),
):
    return service.listar_animales(establecimiento_id, lote_id, search)


@router.post("", response_model=AnimalRead, status_code=201)
def registrar_animal(
    payload: AnimalCreate,
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: AnimalService = Depends(get_animal_service),
):
    return service.registrar_animal(
        establecimiento_id=establecimiento_id,
        identificador=payload.identificador,
        fecha_nacimiento=payload.fecha_nacimiento,
        sexo=payload.sexo,
        raza=payload.raza,
        lote_id=payload.lote_id,
    )


@router.get("/{animal_id}", response_model=AnimalRead)
def ver_ficha_animal(
    animal_id: int,
    service: AnimalService = Depends(get_animal_service),
):
    return service.obtener_ficha(animal_id)


@router.put("/{animal_id}", response_model=AnimalRead)
def modificar_animal(
    animal_id: int,
    payload: AnimalUpdate,
    service: AnimalService = Depends(get_animal_service),
):
    return service.modificar_animal(
        animal_id,
        fecha_nacimiento=payload.fecha_nacimiento,
        sexo=payload.sexo,
        raza=payload.raza,
        lote_id=payload.lote_id,
    )
