from __future__ import annotations

from typing import Optional

from ..interfaces.urlaubsantrag_repository_interface import IUrlaubsantragRepository
from ..models.models_worktime import Urlaubsantrag


class UrlaubsantragService:
    def __init__(self, repository: IUrlaubsantragRepository) -> None:
        self._repository = repository

    def erfasse_urlaubsantrag(self, antrag: Urlaubsantrag) -> Urlaubsantrag:
        konflikte = self._repository.liste_ueberschneidungen(
            antrag.datum_von,
            antrag.datum_bis,
            ausser_id=antrag.id,
        )
        if konflikte:
            k = konflikte[0]
            dv = k.datum_von.strftime("%d.%m.%Y")
            db = k.datum_bis.strftime("%d.%m.%Y")
            raise ValueError(
                "Der Urlaubszeitraum ueberschneidet sich mit einem bestehenden Eintrag "
                f"({dv} bis {db})."
            )
        return self._repository.save(antrag)

    def hole_urlaubsantrag(self, antrag_id: int) -> Optional[Urlaubsantrag]:
        return self._repository.get_by_id(antrag_id)

    def liste_urlaubsantraege(
        self, jahr: Optional[int] = None, genehmigt: Optional[bool] = None
    ) -> list[Urlaubsantrag]:
        return self._repository.list_all(jahr=jahr, genehmigt=genehmigt)

    def loesche_urlaubsantrag(self, antrag_id: int) -> bool:
        return self._repository.delete_by_id(antrag_id)
