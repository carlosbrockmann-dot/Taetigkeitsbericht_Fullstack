from __future__ import annotations

from typing import Optional

from Core.Domain.models.models_worktime import Urlaubsantrag
from Core.Domain.services.urlaubsantrag_service import UrlaubsantragService


class UrlaubsantragAnwendung:
    def __init__(self, service: UrlaubsantragService) -> None:
        self._service = service

    def erfasse(self, antrag: Urlaubsantrag) -> Urlaubsantrag:
        return self._service.erfasse_urlaubsantrag(antrag)

    def hole(self, antrag_id: int) -> Optional[Urlaubsantrag]:
        return self._service.hole_urlaubsantrag(antrag_id)

    def liste(
        self, jahr: Optional[int] = None, genehmigt: Optional[bool] = None
    ) -> list[Urlaubsantrag]:
        return self._service.liste_urlaubsantraege(jahr=jahr, genehmigt=genehmigt)

    def loesche(self, antrag_id: int) -> bool:
        return self._service.loesche_urlaubsantrag(antrag_id)
