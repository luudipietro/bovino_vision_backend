from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")

    app_name: str = "BovinoVision API"
    database_url: str = "postgresql+psycopg://bovinovision:bovinovision@localhost:5432/bovinovision"


@lru_cache
def get_settings() -> Settings:
    return Settings()
