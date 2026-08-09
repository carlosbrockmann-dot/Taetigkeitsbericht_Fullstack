from __future__ import annotations

from datetime import date
from typing import Optional

from sqlalchemy import and_
from sqlmodel import Session, select

from Core.Domain.models.models_worktime import Krankmeldung
from External.Infrastructure.sqlmodel_tables import KrankmeldungTable


def _row_to_domain(row: KrankmeldungTable) -> Krankmeldung:
    return Krankmeldung(
        id=row.id,
        krank_von=row.krank_von,
        krank_bis=row.krank_bis,
        krankmeldungstage=row.krankmeldungstage,
    )


class SqlKrankmeldungRepository:
    def __init__(self, session: Session) -> None:
        self._session = session

    def save(self, eintrag: Krankmeldung) -> Krankmeldung:
        row: KrankmeldungTable | None = None
        if eintrag.id is not None:
            row = self._session.get(KrankmeldungTable, eintrag.id)
        if row is None:
            row = KrankmeldungTable(
                id=eintrag.id,
                krank_von=eintrag.krank_von,
                krank_bis=eintrag.krank_bis,
                krankmeldungstage=eintrag.krankmeldungstage,
            )
            self._session.add(row)
        else:
            row.krank_von = eintrag.krank_von
            row.krank_bis = eintrag.krank_bis
            row.krankmeldungstage = eintrag.krankmeldungstage
        self._session.commit()
        self._session.refresh(row)
        return _row_to_domain(row)

    def get_by_id(self, eintrag_id: int) -> Optional[Krankmeldung]:
        row = self._session.get(KrankmeldungTable, eintrag_id)
        if row is None:
            return None
        return _row_to_domain(row)

    def list_all(self, jahr: Optional[int] = None) -> list[Krankmeldung]:
        stmt = select(KrankmeldungTable).order_by(
            KrankmeldungTable.krank_von, KrankmeldungTable.id
        )
        if jahr is not None:
            jahresanfang = date(jahr, 1, 1)
            jahresende = date(jahr, 12, 31)
            stmt = stmt.where(
                and_(
                    KrankmeldungTable.krank_von <= jahresende,
                    KrankmeldungTable.krank_bis >= jahresanfang,
                )
            )
        rows = list(self._session.exec(stmt).all())
        return [_row_to_domain(r) for r in rows]

    def delete_by_id(self, eintrag_id: int) -> bool:
        row = self._session.get(KrankmeldungTable, eintrag_id)
        if row is None:
            return False
        self._session.delete(row)
        self._session.commit()
        return True
