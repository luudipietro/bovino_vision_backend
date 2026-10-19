from pydantic import BaseModel, ConfigDict


class LoteCreate(BaseModel):
    codigo: str


class LoteRead(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: int
    codigo: str
    establecimiento_id: int
