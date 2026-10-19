from datetime import UTC, datetime

from app.core.exceptions import AnimalNoEncontradoError, SesionCapturaNoEncontradaError
from app.domain.entities.captura import Captura
from app.domain.entities.sesion_captura import SesionCaptura
from app.domain.ports.animal_repository import AnimalRepository
from app.domain.ports.captura_repository import CapturaRepository
from app.domain.ports.image_storage import ImageStoragePort
from app.domain.ports.sesion_captura_repository import SesionCapturaRepository

TIPOS_DE_IMAGEN_VALIDOS = {"image/jpeg", "image/png", "image/webp"}
TAMANIO_MAXIMO_BYTES = 10 * 1024 * 1024


class CapturaService:
    def __init__(
        self,
        sesion_repository: SesionCapturaRepository,
        captura_repository: CapturaRepository,
        animal_repository: AnimalRepository,
        image_storage: ImageStoragePort,
    ):
        self._sesiones = sesion_repository
        self._capturas = captura_repository
        self._animales = animal_repository
        self._storage = image_storage

    def iniciar_sesion(self, establecimiento_id: int) -> SesionCaptura:
        sesion = SesionCaptura(
            id=None,
            establecimiento_id=establecimiento_id,
            fecha_inicio=datetime.now(UTC).replace(tzinfo=None),
        )
        return self._sesiones.add(sesion)

    def registrar_captura(
        self,
        sesion_id: int,
        animal_id: int,
        nombre_archivo: str,
        contenido: bytes,
        content_type: str,
    ) -> Captura:
        if self._sesiones.get(sesion_id) is None:
            raise SesionCapturaNoEncontradaError(sesion_id)
        if self._animales.get(animal_id) is None:
            raise AnimalNoEncontradoError(animal_id)

        validada, motivo_invalidez = self._validar_calidad(content_type, contenido)
        archivo_path = self._storage.guardar(nombre_archivo, contenido)

        captura = Captura(
            id=None,
            sesion_id=sesion_id,
            animal_id=animal_id,
            archivo_path=archivo_path,
            validada=validada,
            motivo_invalidez=motivo_invalidez,
        )
        return self._capturas.add(captura)

    def listar_capturas_de_sesion(self, sesion_id: int) -> list[Captura]:
        if self._sesiones.get(sesion_id) is None:
            raise SesionCapturaNoEncontradaError(sesion_id)
        return self._capturas.list_by_sesion(sesion_id)

    def _validar_calidad(self, content_type: str, contenido: bytes) -> tuple[bool, str | None]:
        # Validación simplificada (tipo y tamaño de archivo). La validación real de
        # calidad de imagen (nitidez, animal detectable, encuadre) depende del mismo
        # servicio de visión artificial que el estimador de peso, todavía no integrado.
        if content_type not in TIPOS_DE_IMAGEN_VALIDOS:
            return False, "Formato de imagen no soportado"
        if not contenido:
            return False, "El archivo está vacío"
        if len(contenido) > TAMANIO_MAXIMO_BYTES:
            return False, "La imagen supera el tamaño máximo permitido"
        return True, None
