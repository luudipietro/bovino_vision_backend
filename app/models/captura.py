from sqlalchemy import ForeignKey
from sqlalchemy.orm import Mapped, mapped_column

from app.core.database import Base


class CapturaModel(Base):
    __tablename__ = "capturas"

    id: Mapped[int] = mapped_column(primary_key=True)
    sesion_id: Mapped[int] = mapped_column(ForeignKey("sesiones_captura.id"))
    animal_id: Mapped[int] = mapped_column(ForeignKey("animales.id"))
    archivo_path: Mapped[str]
    validada: Mapped[bool]
    motivo_invalidez: Mapped[str | None] = mapped_column(default=None)
