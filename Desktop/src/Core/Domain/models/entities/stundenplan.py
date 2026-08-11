from __future__ import annotations

from typing import Optional

from pydantic import Field, model_validator

from .arbeitszeit_basis import ArbeitszeitBasis


class Stundenplan(ArbeitszeitBasis):
    id: Optional[int] = None
    mandant_id: Optional[int] = Field(default=None, description="Mandant ID")
    wochentag: int = Field(ge=1, le=7, description="1=Montag, 7=Sonntag")

    @model_validator(mode="after")
    def pruefe_zeitraeume(self) -> Stundenplan:
        if self.uhrzeit_von is None or self.uhrzeit_bis is None:
            raise ValueError("uhrzeit_von und uhrzeit_bis sind erforderlich.")
        self._validiere_zeitraeume()
        return self

