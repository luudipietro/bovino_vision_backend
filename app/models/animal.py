from datetime import date

from sqlalchemy import ForeignKey, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column

from app.core.database import Base


class AnimalModel(Base):
    __tablename__ = "animales"
    __table_args__ = (UniqueConstraint("establecimiento_id", "identificador"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    identificador: Mapped[str]
    establecimiento_id: Mapped[int] = mapped_column(ForeignKey("establecimientos.id"))
    lote_id: Mapped[int | None] = mapped_column(ForeignKey("lotes.id"), default=None)
    fecha_nacimiento: Mapped[date | None] = mapped_column(default=None)
    sexo: Mapped[str | None] = mapped_column(default=None)
    raza: Mapped[str | None] = mapped_column(default=None)
