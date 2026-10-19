from app.domain.entities.lote import Lote
from app.domain.ports.lote_repository import LoteRepository


class LoteService:
    def __init__(self, lote_repository: LoteRepository):
        self._lotes = lote_repository

    def crear_lote(self, codigo: str, establecimiento_id: int) -> Lote:
        lote = Lote(id=None, codigo=codigo, establecimiento_id=establecimiento_id)
        return self._lotes.add(lote)

    def listar_lotes(self, establecimiento_id: int) -> list[Lote]:
        return self._lotes.list_by_establecimiento(establecimiento_id)

    def obtener(self, lote_id: int) -> Lote | None:
        return self._lotes.get(lote_id)
