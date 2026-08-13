from __future__ import annotations

import json
import ssl
import urllib.error
import urllib.request
from datetime import time
from typing import Any, Optional
from uuid import UUID

from Core.Domain.interfaces.backend_api_interface import (
    BackendLoginResult,
    BackendRegisterResult,
    BackendUploadResult,
)
from Core.Domain.models.models_worktime import Zeiteintrag, ZeiteintragsDTO
from External.Infrastructure.authentication_settings import AuthenticationSettings


def _zeit_als_graphql(wert: time | None) -> Optional[str]:
    if wert is None:
        return None
    return wert.strftime("%H:%M:%S")


def _kategorie_aus_dto(eintrag: ZeiteintragsDTO) -> str:
    """Mappt Spalte Kategorie (U/K/leer) auf GraphQL-Enum ZeiteintragKategorie."""
    kuerzel = (eintrag.kategorie or "").strip().upper()
    if kuerzel == "K":
        return "KRANKHEIT"
    if kuerzel == "U":
        return "URLAUB"
    # leer = Arbeitstag; weitere Kuerzel spaeter erweiterbar
    return "ARBEITSTAG"


def _zeiteintrag_zu_input(eintrag: Zeiteintrag | ZeiteintragsDTO) -> dict[str, Any]:
    kategorie = (
        _kategorie_aus_dto(eintrag)
        if isinstance(eintrag, ZeiteintragsDTO)
        else "ARBEITSTAG"
    )
    payload: dict[str, Any] = {
        "datum": eintrag.datum.isoformat(),
        "kategorie": kategorie,
        "uhrzeitVon": _zeit_als_graphql(eintrag.uhrzeit_von),
        "uhrzeitBis": _zeit_als_graphql(eintrag.uhrzeit_bis),
        "pauseBeginn": _zeit_als_graphql(eintrag.pause_beginn),
        "pauseEnde": _zeit_als_graphql(eintrag.pause_ende),
        "pause2Beginn": _zeit_als_graphql(eintrag.pause2_beginn),
        "pause2Ende": _zeit_als_graphql(eintrag.pause2_ende),
        "anmerkung": eintrag.anmerkung,
        "mandantId": eintrag.mandant_id,
    }
    if isinstance(eintrag.id, UUID):
        payload["id"] = str(eintrag.id)
    return payload


class BackendApiError(RuntimeError):
    """Fehler bei der Kommunikation mit dem Backend."""


class BackendGraphQlClient:
    def __init__(self, settings: AuthenticationSettings) -> None:
        self._settings = settings
        self._graphql_url = f"{settings.base_url}/graphql"

    def _ssl_context(self) -> ssl.SSLContext | None:
        if self._graphql_url.startswith("https://") and not self._settings.verify_ssl:
            return ssl._create_unverified_context()  # noqa: S323
        return None

    def _post(
        self,
        query: str,
        variables: dict[str, Any] | None = None,
        token: str | None = None,
    ) -> dict[str, Any]:
        body = json.dumps({"query": query, "variables": variables or {}}).encode("utf-8")
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json",
        }
        if token:
            headers["Authorization"] = f"Bearer {token}"

        request = urllib.request.Request(
            self._graphql_url,
            data=body,
            headers=headers,
            method="POST",
        )
        try:
            with urllib.request.urlopen(  # noqa: S310
                request,
                timeout=30,
                context=self._ssl_context(),
            ) as response:
                payload = json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise BackendApiError(
                f"HTTP {exc.code} vom Backend: {detail or exc.reason}"
            ) from exc
        except urllib.error.URLError as exc:
            raise BackendApiError(
                f"Backend nicht erreichbar ({self._graphql_url}): {exc.reason}"
            ) from exc
        except TimeoutError as exc:
            raise BackendApiError("Zeitüberschreitung bei der Backend-Anfrage.") from exc

        errors = payload.get("errors")
        if errors:
            messages = "; ".join(
                str(err.get("message", err)) for err in errors if isinstance(err, dict)
            )
            raise BackendApiError(messages or "Unbekannter GraphQL-Fehler.")
        data = payload.get("data")
        if not isinstance(data, dict):
            raise BackendApiError("Unerwartete GraphQL-Antwort (kein data).")
        return data

    def registrieren(
        self, benutzername: str, passwort: str, email: str
    ) -> BackendRegisterResult:
        query = """
        mutation Register($input: RegisterRequestInput!) {
          register(input: $input) {
            ok
            error
            hinweis
            confirmationLink
            mitarbeiterId
          }
        }
        """
        data = self._post(
            query,
            {
                "input": {
                    "benutzername": benutzername,
                    "passwort": passwort,
                    "email": email,
                }
            },
        )
        result = data.get("register") or {}
        return BackendRegisterResult(
            ok=bool(result.get("ok")),
            error=result.get("error"),
            hinweis=result.get("hinweis"),
            confirmation_link=result.get("confirmationLink"),
            mitarbeiter_id=result.get("mitarbeiterId"),
        )

    def anmelden(self, benutzername: str, passwort: str) -> BackendLoginResult:
        query = """
        mutation Login($input: LoginRequestInput!) {
          login(input: $input) {
            ok
            error
            login {
              token
              expiresAt
              mitarbeiterId
              benutzername
            }
          }
        }
        """
        data = self._post(
            query,
            {"input": {"benutzername": benutzername, "passwort": passwort}},
        )
        result = data.get("login") or {}
        login = result.get("login") or {}
        return BackendLoginResult(
            ok=bool(result.get("ok")),
            error=result.get("error"),
            token=login.get("token"),
            expires_at=str(login["expiresAt"]) if login.get("expiresAt") is not None else None,
            mitarbeiter_id=login.get("mitarbeiterId"),
            benutzername=login.get("benutzername"),
        )

    def token_ist_gueltig(self, token: str) -> bool:
        """Prüft per autorisierter Query, ob das JWT noch akzeptiert wird."""
        if not token.strip():
            return False
        query = """
        query TokenPruefung($von: LocalDate!, $bis: LocalDate!) {
          zeiteintraege(von: $von, bis: $bis) { id }
        }
        """
        try:
            self._post(
                query,
                {"von": "2099-01-01", "bis": "2099-01-01"},
                token=token,
            )
            return True
        except BackendApiError:
            return False

    def speichere_zeiteintraege(
        self, token: str, eintraege: list[Zeiteintrag] | list[ZeiteintragsDTO]
    ) -> BackendUploadResult:
        query = """
        mutation Speichere($eintraege: [ZeiteintragInput!]!) {
          speichereZeiteintraege(eintraege: $eintraege) {
            ok
            error
            eintraege { id }
          }
        }
        """
        data = self._post(
            query,
            {"eintraege": [_zeiteintrag_zu_input(e) for e in eintraege]},
            token=token,
        )
        result = data.get("speichereZeiteintraege") or {}
        gespeichert = result.get("eintraege") or []
        return BackendUploadResult(
            ok=bool(result.get("ok")),
            error=result.get("error"),
            anzahl=len(gespeichert) if isinstance(gespeichert, list) else 0,
        )
