from datetime import datetime

from pydantic import BaseModel, ConfigDict


class AlertaRead(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: int
    animal_id: int
    tipo: str
    prioridad: str
    variacion_pct: float
    fecha_generacion: datetime
    estado: str
    animal_identificador: str | None = None
