from __future__ import annotations

from typing import Optional

from Core.Domain.models.models_worktime import Krankmeldung
from Core.Domain.services.krankmeldung_service import KrankmeldungService


class KrankmeldungAnwendung:
    def __init__(self, service: KrankmeldungService) -> None:
        self._service = service

    def erfasse(self, eintrag: Krankmeldung) -> Krankmeldung:
        return self._service.erfasse_krankmeldung(eintrag)

    def hole(self, eintrag_id: int) -> Optional[Krankmeldung]:
        return self._service.hole_krankmeldung(eintrag_id)

    def liste(self, jahr: Optional[int] = None) -> list[Krankmeldung]:
        return self._service.liste_krankmeldungen(jahr=jahr)

    def loesche(self, eintrag_id: int) -> bool:
        return self._service.loesche_krankmeldung(eintrag_id)
