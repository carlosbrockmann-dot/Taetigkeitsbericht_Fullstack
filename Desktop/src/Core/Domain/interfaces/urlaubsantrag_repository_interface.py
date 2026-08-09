from __future__ import annotations

from datetime import date
from typing import Optional, Protocol

from ..models.models_worktime import Urlaubsantrag


class IUrlaubsantragRepository(Protocol):
    def save(self, antrag: Urlaubsantrag) -> Urlaubsantrag:
        ...

    def get_by_id(self, antrag_id: int) -> Optional[Urlaubsantrag]:
        ...

    def list_all(
        self, jahr: Optional[int] = None, genehmigt: Optional[bool] = None
    ) -> list[Urlaubsantrag]:
        ...

    def liste_ueberschneidungen(
        self,
        datum_von: date,
        datum_bis: date,
        ausser_id: Optional[int] = None,
    ) -> list[Urlaubsantrag]:
        ...

    def delete_by_id(self, antrag_id: int) -> bool:
        ...
