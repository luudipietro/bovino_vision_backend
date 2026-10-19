from sqlalchemy.orm import Session

from app.domain.entities.animal import Animal
from app.domain.ports.animal_repository import AnimalRepository
from app.models.animal import AnimalModel


class SqlAlchemyAnimalRepository(AnimalRepository):
    def __init__(self, db: Session):
        self._db = db

    def add(self, animal: Animal) -> Animal:
        model = AnimalModel(
            identificador=animal.identificador,
            establecimiento_id=animal.establecimiento_id,
            lote_id=animal.lote_id,
            fecha_nacimiento=animal.fecha_nacimiento,
            sexo=animal.sexo,
            raza=animal.raza,
        )
        self._db.add(model)
        self._db.commit()
        self._db.refresh(model)
        return _to_entity(model)

    def get(self, animal_id: int) -> Animal | None:
        model = self._db.get(AnimalModel, animal_id)
        return _to_entity(model) if model else None

    def update(self, animal: Animal) -> Animal:
        model = self._db.get(AnimalModel, animal.id)
        model.fecha_nacimiento = animal.fecha_nacimiento
        model.sexo = animal.sexo
        model.raza = animal.raza
        model.lote_id = animal.lote_id
        self._db.commit()
        self._db.refresh(model)
        return _to_entity(model)

    def list_by_establecimiento(
        self,
        establecimiento_id: int,
        lote_id: int | None = None,
        search: str | None = None,
    ) -> list[Animal]:
        query = self._db.query(AnimalModel).filter(
            AnimalModel.establecimiento_id == establecimiento_id
        )
        if lote_id is not None:
            query = query.filter(AnimalModel.lote_id == lote_id)
        if search:
            query = query.filter(AnimalModel.identificador.ilike(f"%{search}%"))

        models = query.order_by(AnimalModel.identificador).all()
        return [_to_entity(model) for model in models]

    def exists_identificador(self, establecimiento_id: int, identificador: str) -> bool:
        existe = (
            self._db.query(AnimalModel)
            .filter(
                AnimalModel.establecimiento_id == establecimiento_id,
                AnimalModel.identificador == identificador,
            )
            .first()
        )
        return existe is not None


def _to_entity(model: AnimalModel) -> Animal:
    return Animal(
        id=model.id,
        identificador=model.identificador,
        establecimiento_id=model.establecimiento_id,
        lote_id=model.lote_id,
        fecha_nacimiento=model.fecha_nacimiento,
        sexo=model.sexo,
        raza=model.raza,
    )
