# MakroPad Gehäuse

Parametrisches OpenSCAD-Modell: [makropad_case.scad](makropad_case.scad).

- Deckel ca. 199 × 115 mm, 12° Neigung (vorne 17 mm, hinten 42 mm hoch), überstehender Rand 5 mm hoch.
- 16 Tasten im 5×4-Raster, oben rechts ersetzt das OLED einen 2×2-Block, 3 Slider rechts.
- Switches sitzen in einer 7.5 mm tiefen Mulde (eigenes Druckteil), sichtbar sind nur die Keycaps.
- Die Schale ist gegenüber dem Deckel eingezogen (8 mm seitlich, 5 mm vorne/hinten),
  darunter verjüngt sie sich in Facetten zu einer achteckigen Bodenfläche.
- OLED in einem separaten Rahmen, der den 2×2-Block füllt und mit dem Tastenfeld fluchtet.
- Deckel 5 mm stark, Kanten rechtwinklig zur Oberfläche.
- ESP32 liegt waagerecht hinten links, USB-C geht links aus dem Gehäuse.

## Teile & Export

| Teil | Druckausrichtung |
| --- | --- |
| `lid` | Sichtseite auf dem Bett (automatisch flach gelegt) |
| `switch_plate` | Platte auf dem Bett |
| `tray` | Boden auf dem Bett |
| `oled_frame` | Sichtseite auf dem Bett, z. B. Silk-Silber-PLA |

```powershell
foreach ($p in "lid", "switch_plate", "tray", "oled_frame") {
  & "D:\Program Files\OpenSCAD\openscad.exe" -D "part=""$p""" -o "export\makropad_$p.stl" makropad_case.scad
}
```

`part` = `assembly` | `exploded` | `lid` | `switch_plate` | `tray` | `oled_frame`.
Alle Maße stehen oben in der Datei (im OpenSCAD-Customizer editierbar).
Werte mit `NACHMESSEN` sind Annahmen.

Keines der Teile braucht Stützen. PLA/PETG, 0.2 mm Schicht, 3–4 Wände
(die Switch-Clips halten an 1.5 mm Plattenstärke).

## Montage

1. Switches von oben in die Switch-Platte klicken.
2. Switch-Platte von unten in den Deckel setzen, 4× M2×6 durch die Laschen in den Deckel.
3. OLED von unten in den Rahmen stecken (Glas liegt 2 mm unter der Rahmenkante) und
   mit 4× M1.7×4 durch die Platine in den Rahmen schrauben (Lochabstand 21 × 22 mm).
   Keine Stiftleiste auf der Display-Seite → Kabel direkt anlöten.
   Rahmen samt Display von oben in den Deckel setzen und mit 2× M1.7×5 von unten durch
   die Auflage im Deckel verschrauben.
4. Slider-Module von unten an die Abstandshalter schrauben (M3, selbstschneidend).
   Stiftleisten möglichst seitlich/abgewinkelt, nach unten ist wenig Platz.
5. ESP32 waagerecht in die Halterung der Schale legen, USB-C zur linken Wand.
6. Deckel aufsetzen, bis die 4 Schnapphaken (rechts und hinten) einrasten, dann
   4× M2×10 Zylinderkopf (DIN 912) von unten durch die Kanäle der Schale in die Dome
   am Deckel schrauben (vorne links, vorne und hinten zwischen Tasten und Slidern,
   hinten neben dem OLED).
