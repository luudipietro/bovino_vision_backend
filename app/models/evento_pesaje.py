from datetime import datetime

from sqlalchemy import ForeignKey
from sqlalchemy.orm import Mapped, mapped_column

from app.core.database import Base


class EventoPesajeModel(Base):
    __tablename__ = "eventos_pesaje"

    id: Mapped[int] = mapped_column(primary_key=True)
    animal_id: Mapped[int] = mapped_column(ForeignKey("animales.id"))
    captura_id: Mapped[int] = mapped_column(ForeignKey("capturas.id"), unique=True)
    estado: Mapped[str]
    fecha_pesaje: Mapped[datetime]
    peso_kg: Mapped[float | None] = mapped_column(default=None)
    version_modelo: Mapped[str | None] = mapped_column(default=None)
    motivo_error: Mapped[str | None] = mapped_column(default=None)
