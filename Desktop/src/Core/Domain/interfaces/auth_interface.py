from __future__ import annotations

from typing import Optional, Protocol

from ..models.models_auth import Login, User


class IAuthService(Protocol):
    def anmelden(self, username: str, password: str) -> Login:
        ...

    def abmelden(self) -> None:
        ...

    def ist_eingeloggt(self) -> bool:
        ...

    def hole_eingeloggten_benutzer(self) -> Optional[User]:
        ...
