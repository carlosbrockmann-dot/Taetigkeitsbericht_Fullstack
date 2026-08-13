from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Optional, Protocol

from Core.Domain.models.models_worktime import Zeiteintrag, ZeiteintragsDTO


@dataclass(frozen=True)
class BackendRegisterResult:
    ok: bool
    error: Optional[str] = None
    hinweis: Optional[str] = None
    confirmation_link: Optional[str] = None
    mitarbeiter_id: Optional[int] = None


@dataclass(frozen=True)
class BackendLoginResult:
    ok: bool
    error: Optional[str] = None
    token: Optional[str] = None
    expires_at: Optional[str] = None
    mitarbeiter_id: Optional[int] = None
    benutzername: Optional[str] = None


@dataclass(frozen=True)
class BackendUploadResult:
    ok: bool
    error: Optional[str] = None
    anzahl: int = 0


class IBackendApiClient(Protocol):
    def registrieren(
        self, benutzername: str, passwort: str, email: str
    ) -> BackendRegisterResult:
        ...

    def anmelden(self, benutzername: str, passwort: str) -> BackendLoginResult:
        ...

    def token_ist_gueltig(self, token: str) -> bool:
        ...

    def speichere_zeiteintraege(
        self, token: str, eintraege: list[Zeiteintrag] | list[ZeiteintragsDTO]
    ) -> BackendUploadResult:
        ...
