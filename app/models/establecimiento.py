from sqlalchemy.orm import Mapped, mapped_column

from app.core.database import Base


class EstablecimientoModel(Base):
    __tablename__ = "establecimientos"

    id: Mapped[int] = mapped_column(primary_key=True)
    nombre: Mapped[str]
