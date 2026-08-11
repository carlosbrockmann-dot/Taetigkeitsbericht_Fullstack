from __future__ import annotations

from datetime import date
from typing import Optional
from uuid import UUID

from pydantic import Field, model_validator

from .arbeitszeit_basis import ArbeitszeitBasis


class Zeiteintrag(ArbeitszeitBasis):
    id: Optional[UUID] = None
    mandant_id: Optional[int] = Field(default=None, description="Mandant ID")
    datum: date

    @model_validator(mode="after")
    def pruefe_zeitraeume(self) -> Zeiteintrag:
        if self.uhrzeit_von is None or self.uhrzeit_bis is None:
            raise ValueError("uhrzeit_von und uhrzeit_bis sind erforderlich.")
        self._validiere_zeitraeume()
        return self

