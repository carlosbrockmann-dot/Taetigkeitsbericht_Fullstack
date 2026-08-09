from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
import tomllib
from typing import Optional
from uuid import uuid4

from ..models.models_auth import Login, User


def _lade_default_remote_login() -> tuple[User, str]:
    config_path = Path(__file__).resolve().parent.parent / "auth_config.toml"
    with config_path.open("rb") as config_file:
        config = tomllib.load(config_file)

    auth_config = config["auth"]["default_user"]
    user = User(
        user_id=auth_config["user_id"],
        username=auth_config["username"],
        email=auth_config["email"],
        password_hash=auth_config["password_hash"],
        is_active=auth_config.get("is_active", True),
    )
    return user, auth_config["password"]


class _AuthSessionSingleton:
    _instance: Optional["_AuthSessionSingleton"] = None

    def __new__(cls) -> "_AuthSessionSingleton":
        if cls._instance is None:
            cls._instance = super().__new__(cls)
            cls._instance._current_user = None
            cls._instance._last_login = None
            cls._instance._login_try_counter = 0
        return cls._instance

    @property
    def current_user(self) -> Optional[User]:
        return self._current_user

    @property
    def last_login(self) -> Optional[Login]:
        return self._last_login

    def register_attempt(
        self,
        success: bool,
        user_id: int,
        user: Optional[User] = None,
        token: Optional[str] = None,
    ) -> Login:
        self._login_try_counter += 1
        login = Login(
            user_id=user_id,
            timestamp=datetime.now(timezone.utc),
            logintrycounter=self._login_try_counter,
            success=success,
            token=token,
        )
        self._last_login = login
        if success:
            self._current_user = user
        return login

    def clear(self) -> None:
        self._current_user = None


class AuthService:
    def __init__(self) -> None:
        self._session = _AuthSessionSingleton()
        self._default_user, self._default_password = _lade_default_remote_login()

    def anmelden(self, username: str, password: str) -> Login:
        success = (
            username == self._default_user.username
            and password == self._default_password
            and self._default_user.is_active
        )
        token = uuid4().hex if success else None
        return self._session.register_attempt(
            success=success,
            user_id=self._default_user.user_id,
            user=self._default_user if success else None,
            token=token,
        )

    def abmelden(self) -> None:
        self._session.clear()

    def ist_eingeloggt(self) -> bool:
        return self._session.current_user is not None

    def hole_eingeloggten_benutzer(self) -> Optional[User]:
        return self._session.current_user

    def hole_letzten_login(self) -> Optional[Login]:
        return self._session.last_login
