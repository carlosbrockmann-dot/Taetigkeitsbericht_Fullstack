from __future__ import annotations

from datetime import date
from typing import Optional, Protocol

from ..models.models_worktime import Feiertag


class IFeiertagRepository(Protocol):
    def add(self, eintrag: Feiertag) -> Feiertag:
        ...

    def update(self, eintrag: Feiertag) -> bool:
        ...

    def get_by_datum(self, datum: date) -> list[Feiertag]:
        ...

    def list_all(self, jahr: Optional[int] = None) -> list[Feiertag]:
        ...

    def delete_by_datum(self, datum: date) -> bool:
        ...
