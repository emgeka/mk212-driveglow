# MK212 DriveGlow

Eine kleine Windows-Tray-Anwendung, die ausschließlich die seitliche
Lichtleiste der **OMOTON MK212** als klassische Datenträger-LED verwendet.
Die Tastenbeleuchtung bleibt unverändert.

<p align="center">
  <img src="assets/mk212-side-light.png" alt="Die Seitenleuchte der OMOTON MK212 zeigt Datenträgeraktivität" width="520">
</p>

<p align="center"><em>Die MK212-Seitenleuchte als klassische Datenträgeranzeige.</em></p>

![Nativer Windows-Farbdialog mit Live-Vorschau](assets/screenshot.png)

## Einrichtung

1. `MK212-DriveGlow.exe` in einem dauerhaften Ordner ablegen.
2. Folgenden Befehl ausführen:

   ```powershell
   .\MK212-DriveGlow.exe --install
   ```

Das Laufwerkssymbol erscheint im Infobereich von Windows und wird beim Start
durch eine Benachrichtigung kenntlich gemacht. Es zeigt den Verbindungsstatus
und blinkt zusammen mit der Tastaturleiste bei Zugriffen. Windows kann es
anfangs im ausgeblendeten Bereich ablegen; von dort kann es auf die Taskleiste
gezogen werden.

Per Rechtsklick lässt sich die Anzeigefarbe im originalen Windows-Farbdialog
wählen und direkt auf der Lichtleiste ansehen. Der automatische Start lässt sich
umschalten oder das Programm beenden. Die Farbe wird für den aktuellen
Windows-Benutzer gespeichert. Beim normalen Beenden wird die vorherige
Einstellung der Seitenleiste wiederhergestellt.

Unter **Anzeigeart** kann zwischen dem klassischen Blinken und dem
**Aktivitätspegel** gewechselt werden. Der Pegel bildet die gemessene
Datenträgerrate logarithmisch auf die Helligkeit der gesamten Seitenleiste ab.

DriveGlow setzt die gewählte Farbe regelmäßig erneut. Dadurch wird sie nach
gelegentlichen VIA-Schreibzugriffen anderer RGB-Programme wie SignalRGB wieder
hergestellt. Steuert ein anderes Programm die MK212 dauerhaft mit hoher
Aktualisierungsrate, sollte die Tastatur dort nach Möglichkeit ausgeschlossen werden.

## Möglicher QMK-/GPL-Lizenzverstoß

Die MK212 wird als QMK/VIA-kompatibel angeboten, und OMOTON stellt auf seiner
[offiziellen Downloadseite](https://omoton.com/collections/keyboard-mouse) eine
VIA-JSON-Definition bereit. Mit Stand vom 15. September 2026 konnten wir jedoch
keinen vollständigen korrespondierenden Quellcode der mit der Tastatur
ausgelieferten Firmware finden.

Das QMK-Projekt weist darauf hin, dass bei QMK-basierter Firmware sowie bei der
Übernahme von QMK/VIA-Firmwarecode der vollständige Quellcode der ausgelieferten
Firmware gemäß GPL verfügbar gemacht werden muss. Siehe dazu die offiziellen
[Hinweise zu Lizenzverstößen](https://github.com/qmk/qmk_firmware/blob/master/docs/license_violations.md)
und den [Text der GPLv2](https://github.com/qmk/qmk_firmware/blob/master/license_GPLv2.md).
Falls die MK212-Firmware entsprechenden Code enthält und kein korrespondierender
Quellcode angeboten wird, könnte dies einen GPL-Verstoß darstellen. Dies ist
keine rechtliche Feststellung. Sollte eine offizielle Quellcodeveröffentlichung
existieren, bitten wir um einen Issue mit dem Link, damit dieser Hinweis
korrigiert werden kann.

MK212 DriveGlow ist eine unabhängige Anwendung auf dem Windows-PC und enthält
keinen kopierten Firmwarecode der Tastatur.

## Entfernen

```powershell
.\MK212-DriveGlow.exe --uninstall
```

Anschließend kann die EXE gelöscht werden.

## Selbst bauen

```text
build.cmd
```

Es sind keine zusätzlichen Pakete und kein separates .NET SDK erforderlich.
Die Ausführungsrichtlinie wird nur für diesen einzelnen Build-Prozess umgangen
und nicht systemweit geändert.

Weitere technische Informationen stehen in der [englischen README](README.md).
