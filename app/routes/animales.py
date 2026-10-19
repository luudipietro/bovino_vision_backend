from fastapi import APIRouter, Depends, Query

from app.dependencies import (
    get_analitica_service,
    get_animal_service,
    get_establecimiento_id,
    get_lote_service,
)
from app.domain.entities.animal import Animal
from app.schemas.alerta import AlertaRead
from app.schemas.animal import AnimalCreate, AnimalRead, AnimalUpdate
from app.schemas.pesaje import PesajeHistorialRead
from app.services.analitica_service import AnaliticaService
from app.services.animal_service import AnimalService
from app.services.lote_service import LoteService

router = APIRouter(prefix="/animales", tags=["animales"])


@router.get("", response_model=list[AnimalRead])
def listar_animales(
    lote_id: int | None = None,
    search: str | None = Query(default=None, alias="q"),
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: AnimalService = Depends(get_animal_service),
    analitica_service: AnaliticaService = Depends(get_analitica_service),
    lote_service: LoteService = Depends(get_lote_service),
):
    animales = service.listar_animales(establecimiento_id, lote_id, search)
    mapa_lotes = {lote.id: lote.codigo for lote in lote_service.listar_lotes(establecimiento_id)}
    return [
        _a_schema(a, mapa_lotes.get(a.lote_id), analitica_service.ultimo_peso(a.id))
        for a in animales
    ]


@router.post("", response_model=AnimalRead, status_code=201)
def registrar_animal(
    payload: AnimalCreate,
    establecimiento_id: int = Depends(get_establecimiento_id),
    service: AnimalService = Depends(get_animal_service),
    lote_service: LoteService = Depends(get_lote_service),
):
    animal = service.registrar_animal(
        establecimiento_id=establecimiento_id,
        identificador=payload.identificador,
        fecha_nacimiento=payload.fecha_nacimiento,
        sexo=payload.sexo,
        raza=payload.raza,
        lote_id=payload.lote_id,
    )
    lote_codigo = lote_service.obtener(animal.lote_id).codigo if animal.lote_id else None
    return _a_schema(animal, lote_codigo, ultimo_peso_kg=None)


@router.get("/{animal_id}", response_model=AnimalRead)
def ver_ficha_animal(
    animal_id: int,
    service: AnimalService = Depends(get_animal_service),
    analitica_service: AnaliticaService = Depends(get_analitica_service),
    lote_service: LoteService = Depends(get_lote_service),
):
    animal = service.obtener_ficha(animal_id)
    lote_codigo = lote_service.obtener(animal.lote_id).codigo if animal.lote_id else None
    return _a_schema(animal, lote_codigo, analitica_service.ultimo_peso(animal_id))


@router.put("/{animal_id}", response_model=AnimalRead)
def modificar_animal(
    animal_id: int,
    payload: AnimalUpdate,
    service: AnimalService = Depends(get_animal_service),
    analitica_service: AnaliticaService = Depends(get_analitica_service),
    lote_service: LoteService = Depends(get_lote_service),
):
    animal = service.modificar_animal(
        animal_id,
        fecha_nacimiento=payload.fecha_nacimiento,
        sexo=payload.sexo,
        raza=payload.raza,
        lote_id=payload.lote_id,
    )
    lote_codigo = lote_service.obtener(animal.lote_id).codigo if animal.lote_id else None
    return _a_schema(animal, lote_codigo, analitica_service.ultimo_peso(animal_id))


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


def _a_schema(animal: Animal, lote_codigo: str | None, ultimo_peso_kg: float | None) -> AnimalRead:
    return AnimalRead(
        id=animal.id,
        identificador=animal.identificador,
        establecimiento_id=animal.establecimiento_id,
        lote_id=animal.lote_id,
        lote_codigo=lote_codigo,
        fecha_nacimiento=animal.fecha_nacimiento,
        sexo=animal.sexo,
        raza=animal.raza,
        ultimo_peso_kg=ultimo_peso_kg,
    )
