from fastapi import APIRouter, Depends, Query

from app.dependencies import (
    get_analitica_service,
    get_animal_service,
    get_establecimiento_id,
)
from app.schemas.alerta import AlertaRead
from app.schemas.animal import AnimalCreate, AnimalRead, AnimalUpdate
from app.schemas.pesaje import PesajeHistorialRead
from app.services.analitica_service import AnaliticaService
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


@router.get("/{animal_id}/pesajes", response_model=list[PesajeHistorialRead])
def historial_de_pesajes(
    animal_id: int,
    animal_service: AnimalService = Depends(get_animal_service),
    analitica_service: AnaliticaService = Depends(get_analitica_service),
):
    animal_service.obtener_ficha(animal_id)
    return [
        PesajeHistorialRead(
            id=item.evento.id,
            estado=item.evento.estado,
            peso_kg=item.evento.peso_kg,
            fecha_pesaje=item.evento.fecha_pesaje,
            version_modelo=item.evento.version_modelo,
            gmd_kg_dia=item.gmd_kg_dia,
        )
        for item in analitica_service.historial_con_gmd(animal_id)
    ]


@router.get("/{animal_id}/alertas", response_model=list[AlertaRead])
def alertas_de_animal(
    animal_id: int,
    animal_service: AnimalService = Depends(get_animal_service),
    analitica_service: AnaliticaService = Depends(get_analitica_service),
):
    animal = animal_service.obtener_ficha(animal_id)
    return [
        AlertaRead(
            id=a.id,
            animal_id=a.animal_id,
            tipo=a.tipo,
            prioridad=a.prioridad,
            variacion_pct=a.variacion_pct,
            fecha_generacion=a.fecha_generacion,
            estado=a.estado,
            animal_identificador=animal.identificador,
        )
        for a in analitica_service.alertas_de_animal(animal_id)
    ]
