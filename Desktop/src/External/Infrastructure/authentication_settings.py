from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import tomllib


@dataclass(frozen=True)
class AuthenticationSettings:
    username: str
    password: str
    email: str
    base_url: str
    token_expires_hours: int = 24
    verify_ssl: bool = True


def _src_root() -> Path:
    # .../Desktop/src/External/Infrastructure/this.py → src/
    return Path(__file__).resolve().parents[2]


def load_authentication_settings(
    config_path: Path | None = None,
) -> AuthenticationSettings:
    path = config_path or (_src_root() / "authentication.toml")
    if not path.is_file():
        raise FileNotFoundError(
            f"authentication.toml nicht gefunden: {path}. "
            "Vorlage: authentication.example.toml"
        )
    with path.open("rb") as handle:
        raw = tomllib.load(handle)

    auth = raw.get("authentication", {})
    webapi = raw.get("webapi", {})
    base_url = str(webapi.get("base_url", "http://localhost:5108")).rstrip("/")
    return AuthenticationSettings(
        username=str(auth.get("username", "")).strip(),
        password=str(auth.get("username_password", "")),
        email=str(auth.get("username_email", "")).strip(),
        base_url=base_url,
        token_expires_hours=int(webapi.get("token_expires_hours", 24)),
        verify_ssl=bool(webapi.get("verify_ssl", True)),
    )
