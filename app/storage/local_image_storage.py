import uuid
from pathlib import Path

from app.domain.ports.image_storage import ImageStoragePort


class LocalImageStorageAdapter(ImageStoragePort):
    """Guarda las imágenes en disco, en el servidor de la API. A futuro (VPS en
    producción) se reemplaza por un adaptador que suba a un bucket de almacenamiento,
    sin tocar los servicios que dependen de ImageStoragePort."""

    def __init__(self, base_dir: Path):
        self._base_dir = base_dir
        self._base_dir.mkdir(parents=True, exist_ok=True)

    def guardar(self, nombre_sugerido: str, contenido: bytes) -> str:
        extension = Path(nombre_sugerido).suffix or ".jpg"
        nombre_archivo = f"{uuid.uuid4().hex}{extension}"
        destino = self._base_dir / nombre_archivo
        destino.write_bytes(contenido)
        return str(destino)
