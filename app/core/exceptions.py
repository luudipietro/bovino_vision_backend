from fastapi import FastAPI
from fastapi.responses import JSONResponse


class DominioError(Exception):
    """Error de negocio que debe traducirse a una respuesta HTTP."""


class AnimalDuplicadoError(DominioError):
    def __init__(self, identificador: str):
        super().__init__(f"Ya existe un animal con identificador '{identificador}'")


class AnimalNoEncontradoError(DominioError):
    def __init__(self, animal_id: int):
        super().__init__(f"No existe el animal {animal_id}")


class LoteNoEncontradoError(DominioError):
    def __init__(self, lote_id: int):
        super().__init__(f"No existe el lote {lote_id}")


def register_exception_handlers(app: FastAPI) -> None:
    @app.exception_handler(AnimalDuplicadoError)
    def _handle_duplicado(request, exc: AnimalDuplicadoError):
        return JSONResponse(status_code=409, content={"detail": str(exc)})

    @app.exception_handler(AnimalNoEncontradoError)
    def _handle_animal_no_encontrado(request, exc: AnimalNoEncontradoError):
        return JSONResponse(status_code=404, content={"detail": str(exc)})

    @app.exception_handler(LoteNoEncontradoError)
    def _handle_lote_no_encontrado(request, exc: LoteNoEncontradoError):
        return JSONResponse(status_code=404, content={"detail": str(exc)})
