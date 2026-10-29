# MakroPad Switch-Platine

2-lagige Platine für die 16 MX-Switches, direkt eingelötet. Jede Taste hat eine eigene Leitung
zum Stecker J1, der zweite Pin liegt auf GND (Masseflächen oben und unten). Keine Matrix, keine
Dioden: passt ohne Firmware-Änderung zu `config::ButtonPins`.

![Unterseite](export/preview_pcb_bottom.png)

Die Platine wird von [makropad_switch_pcb.py](makropad_switch_pcb.py) erzeugt, die Maße kommen aus
[../case/makropad_case.scad](../case/makropad_case.scad) (oben im Skript, bei Gehäuseänderungen mitziehen).

```powershell
& "D:\Program Files\KiCad\10.0\bin\python.exe" makropad_switch_pcb.py
$k = "D:\Program Files\KiCad\10.0\bin\kicad-cli.exe"
& $k pcb drc --severity-all makropad_switch.kicad_pcb
& $k pcb export gerbers --layers "F.Cu,B.Cu,F.Mask,B.Mask,F.SilkS,B.SilkS,Edge.Cuts" --subtract-soldermask --no-x2 -o export\gerber makropad_switch.kicad_pcb
& $k pcb export drill --format excellon --excellon-separate-th -o export\gerber\ makropad_switch.kicad_pcb
Compress-Archive -Force -Path export\gerber\* -DestinationPath export\makropad_switch_jlcpcb.zip
```

## Bestellen (JLCPCB)

`export/makropad_switch_jlcpcb.zip` hochladen. 2 Lagen, 1.6 mm, sonst Standard. Keine Bestückung nötig
(nur THT). Die Platine ist ca. 95 × 84 mm.

## Design Rules

Ausgelegt nach den [Würth Elektronik BASIC Design Rules](https://www.we-online.com/files/pdf1/basic-design-rules-0423-cbt-en.pdf)
v2.0 (04/2023), Kategorie STANDARD, Außenlagen bis 1 oz Startfolie. Die Werte stehen als
Board-Regeln in der Datei, der KiCad-DRC prüft sie:

| Regel | Würth | Platine |
| --- | --- | --- |
| Kupferabstand (1 oz Startfolie) | ≥ 180 µm | ≥ 0.2 mm (DRC) |
| Leiterbreite | ≥ 120 µm | 250 µm |
| Kupfer zur gefrästen Kante | ≥ 0.23 mm | ≥ 0.5 mm |
| Loch zu Loch (verschiedenes Potenzial) | ≥ 500 µm | ≥ 0.5 mm für alle Löcher |
| Restring PTH (0.6 mm Pad bei 0.25 mm Loch) | ≥ 0.175 mm | 0.35 mm |
| Lötstopp-Freistellung / Steg / Überdeckung | ≥ 50 / 70 / 50 µm | 50 / 70 / 50 µm |
| Teardrops an Pad-Anschlüssen | empfohlen | an allen 32 Anschlüssen |
| Bestückungsdruck: Schrifthöhe auf Lötstopp | ≥ 1.5 mm | 1.5 mm |
| Bestückungsdruck: Linienbreite | ≥ 150 µm | ≥ 150 µm |
| Bestückungsdruck zu Kupfer / NPTH-Kante | ≥ 300 µm | ≥ 300 µm zu Pads (Skript-Check) |
| Abstand zwischen Beschriftungen | ≥ 200 µm | ≥ 200 µm |

Den Abstand zwischen Bestückungsdruck und Kupfer prüft das Skript selbst, weil KiCad den Druck
nur gegen Lötstopp-Öffnungen prüft. Geprüft wird gegen freiliegendes Kupfer (Pads und NPTH).
Leiterbahnen und Masseflächen unter Lötstopp sind bedruckbar.

## Stecker J1 → ESP32-S3-DevKitC-1

2×9-Stiftleiste (2.54 mm), Kunststoff auf der **Unterseite**, Stifte zeigen nach unten.
Alternativ Kabel direkt einlöten. Die Unterseite ist mit den Tastennummern beschriftet:

| J1 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | G |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GPIO | 4 | 5 | 6 | 7 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 21 | 39 | 40 | GND |

Beide `G`-Pins sind GND, einer reicht.

## Einbau

1. Switches in die Switch-Platte klicken (3- oder 5-Pin, die Löcher passen für beide).
2. Platine von unten auf die Switch-Pins setzen, satt anliegend verlöten.
3. Stiftleiste von unten einsetzen und verlöten (oder Kabel).
4. Switch-Platte samt Platine wie im Gehäuse-README in den Deckel schrauben. Die hintere
   Laschen-Schraube bleibt hinter der Platinenkante erreichbar.

Die Aussparung rechts hinten ist der Durchgang für die OLED-Kabel. Die Lasche mit J1 sitzt rechts
neben dem ESP32 (dort ist unter der Platine ca. 19 mm Platz).

Platz geprüft (OpenSCAD, Platine + 1.8 mm Lötspitzen + Stiftleiste gegen Schale, Deckel,
Switch-Platte, OLED-Rahmen und ESP32 mit 3.3 mm Bauteilhöhe): keine Kollision, über dem ESP32
bleiben 0.5–1 mm. Lötspitzen nach dem Löten kurz abknipsen.
