from dataclasses import dataclass


@dataclass
class ResultadoEstimacion:
    exitoso: bool
    peso_kg: float | None = None
    version_modelo: str | None = None
    motivo_error: str | None = None
