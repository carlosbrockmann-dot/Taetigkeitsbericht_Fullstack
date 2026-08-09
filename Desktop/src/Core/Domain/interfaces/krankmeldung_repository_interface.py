from __future__ import annotations

from typing import Optional, Protocol

from ..models.models_worktime import Krankmeldung


class IKrankmeldungRepository(Protocol):
    def save(self, eintrag: Krankmeldung) -> Krankmeldung:
        ...

    def get_by_id(self, eintrag_id: int) -> Optional[Krankmeldung]:
        ...

    def list_all(self, jahr: Optional[int] = None) -> list[Krankmeldung]:
        ...

    def delete_by_id(self, eintrag_id: int) -> bool:
        ...
