from dataclasses import dataclass
from datetime import datetime


@dataclass
class SesionCaptura:
    id: int | None
    establecimiento_id: int
    fecha_inicio: datetime
