from sqlalchemy import ForeignKey, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column

from app.core.database import Base


class LoteModel(Base):
    __tablename__ = "lotes"
    __table_args__ = (UniqueConstraint("establecimiento_id", "codigo"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    codigo: Mapped[str]
    establecimiento_id: Mapped[int] = mapped_column(ForeignKey("establecimientos.id"))
