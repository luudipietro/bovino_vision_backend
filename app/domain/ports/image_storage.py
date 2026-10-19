from abc import ABC, abstractmethod


class ImageStoragePort(ABC):
    @abstractmethod
    def guardar(self, nombre_sugerido: str, contenido: bytes) -> str:
        """Guarda el contenido y devuelve la ruta/URL donde quedó almacenado."""
