from app.domain.entities.alerta import Alerta
from app.domain.entities.animal import Animal
from app.domain.entities.captura import Captura
from app.domain.entities.estimacion import ResultadoEstimacion
from app.domain.entities.evento_pesaje import EventoPesaje
from app.domain.entities.lote import Lote
from app.domain.entities.sesion_captura import SesionCaptura
from app.domain.ports.alerta_repository import AlertaRepository
from app.domain.ports.animal_repository import AnimalRepository
from app.domain.ports.captura_repository import CapturaRepository
from app.domain.ports.evento_pesaje_repository import EventoPesajeRepository
from app.domain.ports.image_storage import ImageStoragePort
from app.domain.ports.lote_repository import LoteRepository
from app.domain.ports.sesion_captura_repository import SesionCapturaRepository
from app.domain.ports.weight_estimator import WeightEstimatorPort


class FakeLoteRepository(LoteRepository):
    def __init__(self):
        self._lotes: dict[int, Lote] = {}
        self._next_id = 1

    def add(self, lote: Lote) -> Lote:
        lote.id = self._next_id
        self._lotes[lote.id] = lote
        self._next_id += 1
        return lote

    def list_by_establecimiento(self, establecimiento_id: int) -> list[Lote]:
        return [l for l in self._lotes.values() if l.establecimiento_id == establecimiento_id]

    def get(self, lote_id: int) -> Lote | None:
        return self._lotes.get(lote_id)


class FakeAnimalRepository(AnimalRepository):
    def __init__(self):
        self._animales: dict[int, Animal] = {}
        self._next_id = 1

    def add(self, animal: Animal) -> Animal:
        animal.id = self._next_id
        self._animales[animal.id] = animal
        self._next_id += 1
        return animal

    def get(self, animal_id: int) -> Animal | None:
        return self._animales.get(animal_id)

    def update(self, animal: Animal) -> Animal:
        self._animales[animal.id] = animal
        return animal

    def list_by_establecimiento(
        self,
        establecimiento_id: int,
        lote_id: int | None = None,
        search: str | None = None,
    ) -> list[Animal]:
        resultado = [
            a for a in self._animales.values() if a.establecimiento_id == establecimiento_id
        ]
        if lote_id is not None:
            resultado = [a for a in resultado if a.lote_id == lote_id]
        if search:
            resultado = [a for a in resultado if search.lower() in a.identificador.lower()]
        return resultado

    def exists_identificador(self, establecimiento_id: int, identificador: str) -> bool:
        return any(
            a.establecimiento_id == establecimiento_id and a.identificador == identificador
            for a in self._animales.values()
        )


class FakeSesionCapturaRepository(SesionCapturaRepository):
    def __init__(self):
        self._sesiones: dict[int, SesionCaptura] = {}
        self._next_id = 1

    def add(self, sesion: SesionCaptura) -> SesionCaptura:
        sesion.id = self._next_id
        self._sesiones[sesion.id] = sesion
        self._next_id += 1
        return sesion

    def get(self, sesion_id: int) -> SesionCaptura | None:
        return self._sesiones.get(sesion_id)


class FakeCapturaRepository(CapturaRepository):
    def __init__(self):
        self._capturas: dict[int, Captura] = {}
        self._next_id = 1

    def add(self, captura: Captura) -> Captura:
        captura.id = self._next_id
        self._capturas[captura.id] = captura
        self._next_id += 1
        return captura

    def get(self, captura_id: int) -> Captura | None:
        return self._capturas.get(captura_id)

    def list_by_sesion(self, sesion_id: int) -> list[Captura]:
        return [c for c in self._capturas.values() if c.sesion_id == sesion_id]


class FakeEventoPesajeRepository(EventoPesajeRepository):
    def __init__(self):
        self._eventos: dict[int, EventoPesaje] = {}
        self._next_id = 1

    def add(self, evento: EventoPesaje) -> EventoPesaje:
        evento.id = self._next_id
        self._eventos[evento.id] = evento
        self._next_id += 1
        return evento

    def get(self, evento_id: int) -> EventoPesaje | None:
        return self._eventos.get(evento_id)

    def get_by_captura(self, captura_id: int) -> EventoPesaje | None:
        return next((e for e in self._eventos.values() if e.captura_id == captura_id), None)

    def list_by_animal(self, animal_id: int) -> list[EventoPesaje]:
        return [e for e in self._eventos.values() if e.animal_id == animal_id]

    def list_by_establecimiento(
        self,
        establecimiento_id: int,
        lote_id: int | None = None,
        desde=None,
        hasta=None,
    ) -> list[EventoPesaje]:
        # Los fakes no modelan establecimiento/lote del animal: en los tests que la
        # usan, todos los eventos cargados pertenecen al mismo establecimiento/lote.
        eventos = list(self._eventos.values())
        if desde is not None:
            eventos = [e for e in eventos if e.fecha_pesaje.date() >= desde]
        if hasta is not None:
            eventos = [e for e in eventos if e.fecha_pesaje.date() <= hasta]
        return eventos


class FakeAlertaRepository(AlertaRepository):
    def __init__(self):
        self._alertas: dict[int, Alerta] = {}
        self._next_id = 1

    def add(self, alerta: Alerta) -> Alerta:
        alerta.id = self._next_id
        self._alertas[alerta.id] = alerta
        self._next_id += 1
        return alerta

    def get_by_evento(self, evento_pesaje_id: int) -> Alerta | None:
        return next(
            (a for a in self._alertas.values() if a.evento_pesaje_id == evento_pesaje_id), None
        )

    def list_by_animal(self, animal_id: int) -> list[Alerta]:
        return [a for a in self._alertas.values() if a.animal_id == animal_id]

    def list_by_establecimiento(self, establecimiento_id: int) -> list[Alerta]:
        orden_prioridad = {"alta": 0, "media": 1, "baja": 2}
        alertas = [a for a in self._alertas.values() if a.establecimiento_id == establecimiento_id]
        return sorted(
            alertas,
            key=lambda a: (orden_prioridad.get(a.prioridad, 9), -a.fecha_generacion.timestamp()),
        )


class FakeWeightEstimatorAdapter(WeightEstimatorPort):
    def __init__(self, resultado: ResultadoEstimacion | None = None):
        self._resultado = resultado or ResultadoEstimacion(
            exitoso=True, peso_kg=350.0, version_modelo="fake-v1"
        )

    def estimar_peso(self, imagen_path: str) -> ResultadoEstimacion:
        return self._resultado


class FakeImageStorageAdapter(ImageStoragePort):
    def __init__(self):
        self.guardados: list[bytes] = []

    def guardar(self, nombre_sugerido: str, contenido: bytes) -> str:
        self.guardados.append(contenido)
        return f"fake://{nombre_sugerido}"
