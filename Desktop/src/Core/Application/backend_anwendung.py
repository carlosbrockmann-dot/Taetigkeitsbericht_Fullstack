from __future__ import annotations

from typing import Optional

from Core.Domain.interfaces.backend_api_interface import (
    BackendLoginResult,
    BackendRegisterResult,
    BackendUploadResult,
    IBackendApiClient,
)
from Core.Domain.models.models_worktime import Zeiteintrag, ZeiteintragsDTO
from External.Infrastructure.authentication_settings import AuthenticationSettings


class BackendAnwendung:
    """Anwendungsfälle für Registrierung, Login und Monats-Upload gegen das Backend."""

    def __init__(
        self,
        client: IBackendApiClient,
        settings: AuthenticationSettings,
    ) -> None:
        self._client = client
        self._settings = settings
        self._token: Optional[str] = None

    @property
    def settings(self) -> AuthenticationSettings:
        return self._settings

    @property
    def token(self) -> Optional[str]:
        return self._token

    def registrieren(
        self,
        benutzername: str,
        passwort: str,
        email: str,
    ) -> BackendRegisterResult:
        return self._client.registrieren(
            benutzername=benutzername.strip(),
            passwort=passwort,
            email=email.strip(),
        )

    def anmelden(
        self,
        benutzername: str | None = None,
        passwort: str | None = None,
    ) -> BackendLoginResult:
        user = (benutzername if benutzername is not None else self._settings.username).strip()
        pwd = passwort if passwort is not None else self._settings.password
        result = self._client.anmelden(user, pwd)
        if result.ok and result.token:
            self._token = result.token
        return result

    def stelle_sicher_angemeldet(self) -> BackendLoginResult:
        if self._token:
            return BackendLoginResult(ok=True, token=self._token)
        return self.anmelden()

    def lade_monat_hoch(
        self,
        eintraege: list[Zeiteintrag] | list[ZeiteintragsDTO],
        *,
        benutzername: str | None = None,
        passwort: str | None = None,
    ) -> BackendUploadResult:
        if not eintraege:
            return BackendUploadResult(
                ok=False,
                error="Keine Zeiteinträge für diesen Monat zum Abgeben vorhanden.",
            )

        login = (
            self.anmelden(benutzername, passwort)
            if not self._token
            else self.stelle_sicher_angemeldet()
        )
        if not login.ok or not login.token:
            return BackendUploadResult(
                ok=False,
                error=login.error or "Login am Backend fehlgeschlagen.",
            )

        try:
            return self._client.speichere_zeiteintraege(login.token, eintraege)
        except Exception:
            self._token = None
            login = self.anmelden(benutzername, passwort)
            if not login.ok or not login.token:
                raise
            return self._client.speichere_zeiteintraege(login.token, eintraege)
