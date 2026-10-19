from dataclasses import dataclass


@dataclass
class Lote:
    id: int | None
    codigo: str
    establecimiento_id: int
