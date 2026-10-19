from datetime import datetime

from sqlalchemy import ForeignKey
from sqlalchemy.orm import Mapped, mapped_column

from app.core.database import Base


class AlertaModel(Base):
    __tablename__ = "alertas"

    id: Mapped[int] = mapped_column(primary_key=True)
    animal_id: Mapped[int] = mapped_column(ForeignKey("animales.id"))
    establecimiento_id: Mapped[int] = mapped_column(ForeignKey("establecimientos.id"))
    evento_pesaje_id: Mapped[int] = mapped_column(ForeignKey("eventos_pesaje.id"), unique=True)
    tipo: Mapped[str]
    prioridad: Mapped[str]
    variacion_pct: Mapped[float]
    fecha_generacion: Mapped[datetime]
    estado: Mapped[str]
