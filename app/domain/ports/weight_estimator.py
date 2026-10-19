from abc import ABC, abstractmethod

from app.domain.entities.estimacion import ResultadoEstimacion


class WeightEstimatorPort(ABC):
    @abstractmethod
    def estimar_peso(self, imagen_path: str) -> ResultadoEstimacion: ...
