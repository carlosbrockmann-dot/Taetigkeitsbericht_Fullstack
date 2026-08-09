from __future__ import annotations

from datetime import date
from typing import Optional

from sqlalchemy import and_
from sqlmodel import Session, select

from Core.Domain.models.models_worktime import Urlaubsantrag
from External.Infrastructure.sqlmodel_tables import UrlaubsantragTable


def _row_to_domain(row: UrlaubsantragTable) -> Urlaubsantrag:
    return Urlaubsantrag(
        id=row.id,
        datum_von=row.datum_von,
        datum_bis=row.datum_bis,
        urlaubstyp=row.urlaubstyp,
        urlaubstage=row.urlaubstage,
        genehmigt=row.genehmigt,
    )


class SqlUrlaubsantragRepository:
    def __init__(self, session: Session) -> None:
        self._session = session

    def save(self, antrag: Urlaubsantrag) -> Urlaubsantrag:
        row: UrlaubsantragTable | None = None
        if antrag.id is not None:
            row = self._session.get(UrlaubsantragTable, antrag.id)
        if row is None:
            row = UrlaubsantragTable(
                id=antrag.id,
                datum_von=antrag.datum_von,
                datum_bis=antrag.datum_bis,
                urlaubstyp=antrag.urlaubstyp,
                urlaubstage=antrag.urlaubstage,
                genehmigt=antrag.genehmigt,
            )
            self._session.add(row)
        else:
            row.datum_von = antrag.datum_von
            row.datum_bis = antrag.datum_bis
            row.urlaubstyp = antrag.urlaubstyp
            row.urlaubstage = antrag.urlaubstage
            row.genehmigt = antrag.genehmigt
        self._session.commit()
        self._session.refresh(row)
        return _row_to_domain(row)

    def get_by_id(self, antrag_id: int) -> Optional[Urlaubsantrag]:
        row = self._session.get(UrlaubsantragTable, antrag_id)
        if row is None:
            return None
        return _row_to_domain(row)

    def list_all(
        self, jahr: Optional[int] = None, genehmigt: Optional[bool] = None
    ) -> list[Urlaubsantrag]:
        stmt = select(UrlaubsantragTable).order_by(
            UrlaubsantragTable.datum_von, UrlaubsantragTable.id
        )
        if jahr is not None:
            jahresanfang = date(jahr, 1, 1)
            jahresende = date(jahr, 12, 31)
            stmt = stmt.where(
                and_(
                    UrlaubsantragTable.datum_von <= jahresende,
                    UrlaubsantragTable.datum_bis >= jahresanfang,
                )
            )
        if genehmigt is not None:
            stmt = stmt.where(UrlaubsantragTable.genehmigt == genehmigt)
        rows = list(self._session.exec(stmt).all())
        return [_row_to_domain(r) for r in rows]

    def liste_ueberschneidungen(
        self,
        datum_von: date,
        datum_bis: date,
        ausser_id: Optional[int] = None,
    ) -> list[Urlaubsantrag]:
        stmt = (
            select(UrlaubsantragTable)
            .where(UrlaubsantragTable.datum_von <= datum_bis)
            .where(UrlaubsantragTable.datum_bis >= datum_von)
            .order_by(UrlaubsantragTable.datum_von, UrlaubsantragTable.id)
        )
        if ausser_id is not None:
            stmt = stmt.where(UrlaubsantragTable.id != ausser_id)
        rows = list(self._session.exec(stmt).all())
        return [_row_to_domain(r) for r in rows]

    def delete_by_id(self, antrag_id: int) -> bool:
        row = self._session.get(UrlaubsantragTable, antrag_id)
        if row is None:
            return False
        self._session.delete(row)
        self._session.commit()
        return True
