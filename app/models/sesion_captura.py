from datetime import datetime

from sqlalchemy import ForeignKey
from sqlalchemy.orm import Mapped, mapped_column

from app.core.database import Base


class SesionCapturaModel(Base):
    __tablename__ = "sesiones_captura"

    id: Mapped[int] = mapped_column(primary_key=True)
    establecimiento_id: Mapped[int] = mapped_column(ForeignKey("establecimientos.id"))
    fecha_inicio: Mapped[datetime]
