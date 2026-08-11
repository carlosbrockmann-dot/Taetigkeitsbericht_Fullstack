from __future__ import annotations

from PySide6.QtCore import QSize, Qt
from PySide6.QtGui import QColor, QPainter, QPen, QPixmap
from PySide6.QtWidgets import (
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMessageBox,
    QToolButton,
    QVBoxLayout,
    QWidget,
)

from Core.Application.backend_anwendung import BackendAnwendung
from External.Infrastructure.backend_graphql_client import BackendApiError


def _zeichne_auge_icon(*, durchgestrichen: bool, groesse: int = 20) -> QPixmap:
    """Einfaches Auge-Icon (ohne Emoji), damit der Button unter Windows sichtbar ist."""
    pixmap = QPixmap(groesse, groesse)
    pixmap.fill(Qt.GlobalColor.transparent)
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing)
    pen = QPen(QColor("#424242"))
    pen.setWidth(2)
    painter.setPen(pen)
    painter.setBrush(Qt.BrushStyle.NoBrush)

    # Augenkontur
    painter.drawEllipse(2, 6, groesse - 4, groesse - 12)
    # Pupille
    painter.setBrush(QColor("#424242"))
    painter.drawEllipse(groesse // 2 - 3, groesse // 2 - 3, 6, 6)

    if durchgestrichen:
        pen.setWidth(2)
        painter.setPen(pen)
        painter.drawLine(3, groesse - 4, groesse - 3, 3)

    painter.end()
    return pixmap


class RegistrierungDialog(QDialog):
    """Modales Formular zur Registrierung am Backend."""

    def __init__(
        self,
        backend: BackendAnwendung,
        parent=None,
    ) -> None:
        super().__init__(parent)
        self._backend = backend
        self.setWindowTitle("Registrierung am Backend")
        self.setModal(True)
        self.setMinimumWidth(420)

        settings = backend.settings
        layout = QVBoxLayout(self)
        hinweis = QLabel(
            "Neues Konto am Tätigkeitsbericht-Backend anlegen. "
            "Danach E-Mail bestätigen, bevor ein Login/Upload möglich ist.",
            self,
        )
        hinweis.setWordWrap(True)
        layout.addWidget(hinweis)

        form = QFormLayout()
        self._benutzername = QLineEdit(self)
        self._benutzername.setText(settings.username)

        passwort_zeile = QWidget(self)
        passwort_layout = QHBoxLayout(passwort_zeile)
        passwort_layout.setContentsMargins(0, 0, 0, 0)
        passwort_layout.setSpacing(4)
        self._passwort = QLineEdit(passwort_zeile)
        self._passwort.setEchoMode(QLineEdit.EchoMode.Password)
        self._passwort.setText(settings.password)
        self._passwort_toggle = QToolButton(passwort_zeile)
        self._passwort_toggle.setCheckable(True)
        self._passwort_toggle.setAutoRaise(True)
        self._passwort_toggle.setCursor(Qt.CursorShape.PointingHandCursor)
        self._passwort_toggle.setIconSize(QSize(20, 20))
        self._passwort_toggle.setFixedSize(28, 28)
        self._passwort_toggle.setToolTip("Passwort anzeigen")
        self._passwort_toggle.setIcon(_zeichne_auge_icon(durchgestrichen=False))
        self._passwort_toggle.toggled.connect(self._on_passwort_sichtbarkeit)
        passwort_layout.addWidget(self._passwort, 1)
        passwort_layout.addWidget(self._passwort_toggle, 0)

        self._email = QLineEdit(self)
        self._email.setText(settings.email)
        form.addRow("Benutzername:", self._benutzername)
        form.addRow("Passwort:", passwort_zeile)
        form.addRow("E-Mail:", self._email)
        layout.addLayout(form)

        self._status = QLabel("", self)
        self._status.setWordWrap(True)
        self._status.setStyleSheet("color: #b71c1c;")
        layout.addWidget(self._status)

        buttons = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel,
            parent=self,
        )
        buttons.button(QDialogButtonBox.StandardButton.Ok).setText("Registrieren")
        buttons.button(QDialogButtonBox.StandardButton.Cancel).setText("Abbrechen")
        buttons.accepted.connect(self._on_registrieren)
        buttons.rejected.connect(self.reject)
        layout.addWidget(buttons)

    def _on_passwort_sichtbarkeit(self, sichtbar: bool) -> None:
        if sichtbar:
            self._passwort.setEchoMode(QLineEdit.EchoMode.Normal)
            self._passwort_toggle.setIcon(_zeichne_auge_icon(durchgestrichen=True))
            self._passwort_toggle.setToolTip("Passwort verbergen")
        else:
            self._passwort.setEchoMode(QLineEdit.EchoMode.Password)
            self._passwort_toggle.setIcon(_zeichne_auge_icon(durchgestrichen=False))
            self._passwort_toggle.setToolTip("Passwort anzeigen")

    def _on_registrieren(self) -> None:
        benutzername = self._benutzername.text().strip()
        passwort = self._passwort.text()
        email = self._email.text().strip()
        if not benutzername or not passwort or not email:
            self._status.setText("Benutzername, Passwort und E-Mail sind Pflichtfelder.")
            return

        self._status.setText("Registrierung läuft …")
        self._status.setStyleSheet("color: #424242;")
        try:
            result = self._backend.registrieren(benutzername, passwort, email)
        except BackendApiError as exc:
            self._status.setStyleSheet("color: #b71c1c;")
            self._status.setText(str(exc))
            return
        except Exception as exc:  # noqa: BLE001
            self._status.setStyleSheet("color: #b71c1c;")
            self._status.setText(f"Unerwarteter Fehler: {exc}")
            return

        if not result.ok:
            self._status.setStyleSheet("color: #b71c1c;")
            self._status.setText(result.error or "Registrierung fehlgeschlagen.")
            return

        text_teile = [result.hinweis or "Registrierung erfolgreich."]
        if result.confirmation_link:
            text_teile.append(f"Bestätigungslink:\n{result.confirmation_link}")
        QMessageBox.information(self, "Registrierung", "\n\n".join(text_teile))
        self.accept()
