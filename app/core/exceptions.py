from fastapi import FastAPI
from fastapi.responses import JSONResponse


class DominioError(Exception):
    """Error de negocio que debe traducirse a una respuesta HTTP."""


class NoEncontradoError(DominioError):
    """Una entidad referenciada no existe."""


class ConflictoError(DominioError):
    """La operación pisaría un estado ya existente."""


class OperacionInvalidaError(DominioError):
    """La operación no es válida para el estado actual de la entidad."""


class AnimalDuplicadoError(ConflictoError):
    def __init__(self, identificador: str):
        super().__init__(f"Ya existe un animal con identificador '{identificador}'")


class AnimalNoEncontradoError(NoEncontradoError):
    def __init__(self, animal_id: int):
        super().__init__(f"No existe el animal {animal_id}")


class LoteNoEncontradoError(NoEncontradoError):
    def __init__(self, lote_id: int):
        super().__init__(f"No existe el lote {lote_id}")


class SesionCapturaNoEncontradaError(NoEncontradoError):
    def __init__(self, sesion_id: int):
        super().__init__(f"No existe la sesión de captura {sesion_id}")


class CapturaNoEncontradaError(NoEncontradoError):
    def __init__(self, captura_id: int):
        super().__init__(f"No existe la captura {captura_id}")


class EventoPesajeNoEncontradoError(NoEncontradoError):
    def __init__(self, evento_id: int):
        super().__init__(f"No existe el evento de pesaje {evento_id}")


class CapturaInvalidaError(OperacionInvalidaError):
    def __init__(self, captura_id: int):
        super().__init__(f"La captura {captura_id} no pasó la validación de calidad de imagen")


class CapturaYaProcesadaError(ConflictoError):
    def __init__(self, captura_id: int):
        super().__init__(f"La captura {captura_id} ya tiene un pesaje asociado")


def register_exception_handlers(app: FastAPI) -> None:
    @app.exception_handler(NoEncontradoError)
    def _handle_no_encontrado(request, exc: NoEncontradoError):
        return JSONResponse(status_code=404, content={"detail": str(exc)})

    @app.exception_handler(ConflictoError)
    def _handle_conflicto(request, exc: ConflictoError):
        return JSONResponse(status_code=409, content={"detail": str(exc)})

    @app.exception_handler(OperacionInvalidaError)
    def _handle_operacion_invalida(request, exc: OperacionInvalidaError):
        return JSONResponse(status_code=422, content={"detail": str(exc)})
