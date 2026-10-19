from dataclasses import dataclass


@dataclass
class Captura:
    id: int | None
    sesion_id: int
    animal_id: int
    archivo_path: str
    validada: bool
    motivo_invalidez: str | None = None
