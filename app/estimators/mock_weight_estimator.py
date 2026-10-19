import random

from app.domain.entities.estimacion import ResultadoEstimacion
from app.domain.ports.weight_estimator import WeightEstimatorPort


class MockWeightEstimatorAdapter(WeightEstimatorPort):
    """Reemplaza temporalmente al servicio real de estimación de peso por visión
    artificial (en desarrollo en otro repo), para poder construir y probar el resto
    del flujo de captura/pesaje sin depender de él. Se reemplaza más adelante
    cambiando el provider en app/dependencies.py, sin tocar servicios ni rutas."""

    def estimar_peso(self, imagen_path: str) -> ResultadoEstimacion:
        peso_kg = round(random.uniform(180, 550), 1)
        return ResultadoEstimacion(exitoso=True, peso_kg=peso_kg, version_modelo="mock-v1")
