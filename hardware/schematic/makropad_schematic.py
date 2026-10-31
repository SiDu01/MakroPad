# Generates the MakroPad wiring schematic (makropad.kicad_sch). Plain Python, no KiCad import:
#   python makropad_schematic.py
#   & "D:\Program Files\KiCad\10.0\bin\kicad-cli.exe" sch export pdf -o makropad.pdf makropad.kicad_sch
#
# Pins: ../../firmware/include/config.h, header order: ../pcb/makropad_switch.kicad_pcb (J1).
# Nets connect by label name; K1..K16 match the switch PCB net names.
import os
import uuid

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "makropad.kicad_sch")
NS = uuid.UUID("6f1c2b9e-3a7d-4c55-9a0e-5d1b7f2e8c40")
_n = 0


def uid():
    global _n
    _n += 1
    return str(uuid.uuid5(NS, str(_n)))


ROOT = uid()
FONT = "(effects (font (size 1.27 1.27)))"
HIDE = "(effects (font (size 1.27 1.27)) hide)"

# DevKitC-1 v1.1 headers, pin 1 at the antenna end. None = not connected.
DEVKIT_J1 = [("3V3", "+3V3"), ("3V3", "+3V3"), ("RST", None), ("GPIO4", "K1"), ("GPIO5", "K2"),
             ("GPIO6", "K3"), ("GPIO7", "K4"), ("GPIO15", "K10"), ("GPIO16", "K11"), ("GPIO17", "K12"),
             ("GPIO18", "K13"), ("GPIO8", "SDA"), ("GPIO3", "SLIDER3"), ("GPIO46", None), ("GPIO9", "SCL"),
             ("GPIO10", "K5"), ("GPIO11", "K6"), ("GPIO12", "K7"), ("GPIO13", "K8"), ("GPIO14", "K9"),
             ("5V", None), ("GND", "GND")]
DEVKIT_J3 = [("GND", "GND"), ("GPIO43", None), ("GPIO44", None), ("GPIO1", "SLIDER1"), ("GPIO2", "SLIDER2"),
             ("GPIO42", None), ("GPIO41", None), ("GPIO40", "K16"), ("GPIO39", "K15"), ("GPIO38", None),
             ("GPIO37", None), ("GPIO36", None), ("GPIO35", None), ("GPIO0", None), ("GPIO45", None),
             ("GPIO48", None), ("GPIO47", None), ("GPIO21", "K14"), ("GPIO20", None), ("GPIO19", None),
             ("GND", "GND"), ("GND", "GND")]
# Switch PCB header J1, pin -> net (read from makropad_switch.kicad_pcb).
HEADER = ["K2", "K1", "K4", "K3", "K6", "K5", "K8", "K7", "K12", "K9", "K14", "K13", "K16", "K15",
          "K11", "K10", "GND", "GND"]


def pin(num, name, x, y, angle, length=2.54):
    return (f'(pin passive line (at {x} {y} {angle}) (length {length}) (name "{name}" {FONT}) '
            f'(number "{num}" {FONT}))')


def rect(x0, y0, x1, y1):
    return (f"(rectangle (start {x0} {y0}) (end {x1} {y1}) (stroke (width 0.254) (type default)) "
            f"(fill (type background)))")


def poly(*pts):
    xy = " ".join(f"(xy {x} {y})" for x, y in pts)
    return f"(polyline (pts {xy}) (stroke (width 0.254) (type default)) (fill (type none)))"


def libsym(name, ref, graphics, pins, pin_names=True):
    names = "(pin_names (offset 1.016))" if pin_names else "(pin_names (offset 0) hide)"
    return (f'(symbol "makropad:{name}" {names} (in_bom yes) (on_board yes) '
            f'(property "Reference" "{ref}" (at 0 0 0) {HIDE}) (property "Value" "{name}" (at 0 0 0) {HIDE}) '
            f'(symbol "{name}_0_1" {" ".join(graphics)}) (symbol "{name}_1_1" {" ".join(pins)}))')


# Symbols in library coordinates (y up). Pin (x, y) is the connection point.
DK_Y = [27.94 - i * 2.54 for i in range(22)]
SYMBOLS = {
    "ESP32-S3-DevKitC-1": (
        [rect(-12.7, 30.48, 12.7, -27.94)],
        [pin(f"J1-{i + 1}", n, -17.78, y, 0, 5.08) for i, ((n, _), y) in enumerate(zip(DEVKIT_J1, DK_Y))]
        + [pin(f"J3-{i + 1}", n, 17.78, y, 180, 5.08) for i, ((n, _), y) in enumerate(zip(DEVKIT_J3, DK_Y))]),
    "Conn_02x09": (
        [rect(-2.54, 11.43, 2.54, -11.43)],
        [pin(str(2 * k + 1), "", -5.08, 10.16 - 2.54 * k, 0) for k in range(9)]
        + [pin(str(2 * k + 2), "", 5.08, 10.16 - 2.54 * k, 180) for k in range(9)]),
    "OLED_SSD1306_I2C": (
        [rect(-5.08, 7.62, 7.62, -5.08)],
        [pin(str(k + 1), n, -7.62, 5.08 - 2.54 * k, 0) for k, n in enumerate(["GND", "VCC", "SCL", "SDA"])]),
    "B103": (
        [rect(-1.016, 2.54, 1.016, -2.54), poly((2.54, 0), (1.016, 0)), poly((1.016, 0), (1.778, 0.508), (1.778, -0.508), (1.016, 0))],
        [pin("1", "", 0, 5.08, 270), pin("2", "", 5.08, 0, 180), pin("3", "", 0, -5.08, 90)]),
    "MX": (
        [poly((-2.54, 0), (-1.27, 0)), poly((-1.27, 0), (1.27, 1.27)), poly((1.27, 0), (2.54, 0)),
         poly((0, 0.635), (0, 2.54))],
        [pin("2", "", -5.08, 0, 0), pin("1", "", 5.08, 0, 180)]),
}

items = []


def place(lib, ref, x, y, value=None, ref_at=(0, 0), val_at=(0, 0)):
    items.append(
        f'(symbol (lib_id "makropad:{lib}") (at {x} {y} 0) (unit 1) (in_bom yes) (on_board yes) (dnp no) '
        f'(uuid "{uid()}") '
        f'(property "Reference" "{ref}" (at {x + ref_at[0]} {y + ref_at[1]} 0) {FONT}) '
        f'(property "Value" "{value or lib}" (at {x + val_at[0]} {y + val_at[1]} 0) {FONT}) '
        f'(instances (project "makropad" (path "/{ROOT}" (reference "{ref}") (unit 1)))))')


def stub(x, y, dx, dy, net):
    # Wire from a pin end to a label (or a no-connect flag when net is None).
    if net is None:
        items.append(f'(no_connect (at {x} {y}) (uuid "{uid()}"))')
        return
    x2, y2 = round(x + dx, 2), round(y + dy, 2)
    items.append(f'(wire (pts (xy {x} {y}) (xy {x2} {y2})) (stroke (width 0) (type default)) (uuid "{uid()}"))')
    angle = {(1, 0): 0, (-1, 0): 180, (0, -1): 90, (0, 1): 270}[((dx > 0) - (dx < 0), (dy > 0) - (dy < 0))]
    just = "left" if angle in (0, 90) else "right"
    items.append(f'(label "{net}" (at {x2} {y2} {angle}) (effects (font (size 1.27 1.27)) (justify {just} bottom)) '
                 f'(uuid "{uid()}"))')


def text(s, x, y, size=1.27):
    s = s.replace('"', '\\"')
    items.append(f'(text "{s}" (at {x} {y} 0) (effects (font (size {size} {size})) (justify left bottom)) '
                 f'(uuid "{uid()}"))')


def frame(x0, y0, x1, y1):
    items.append(f'(rectangle (start {x0} {y0}) (end {x1} {y1}) (stroke (width 0) (type dash)) '
                 f'(fill (type none)) (uuid "{uid()}"))')


# Sheet coordinates (mm, y down, 1.27 grid). Symbol pin (px, py) lands at (X + px, Y - py).
# --- ESP32-S3-DevKitC-1 ---
UX, UY = 80.01, 129.54
place("ESP32-S3-DevKitC-1", "U1", UX, UY, ref_at=(0, -33.02), val_at=(0, -35.56))
for (_, net), y in zip(DEVKIT_J1, DK_Y):
    stub(UX - 17.78, UY - y, -5.08, 0, net)
for (_, net), y in zip(DEVKIT_J3, DK_Y):
    stub(UX + 17.78, UY - y, 5.08, 0, net)
text("ESP32-S3-DevKitC-1 v1.1 (USB zum PC über die native USB-Buchse, GPIO19/20)", 40.64, 80.01, 1.8)
text("Nicht verbundene Pins: frei oder zu meiden, siehe docs/HARDWARE.md", 40.64, 175.26)

# --- Sliders ---
for k in range(3):
    sx, sy = 142.24, 101.6 + 30.48 * k
    place("B103", f"RV{k + 1}", sx, sy, "B103 10k", ref_at=(-6.35, -1.27), val_at=(-6.35, 1.27))
    stub(sx, sy - 5.08, 0, -2.54, "+3V3")
    stub(sx + 5.08, sy, 5.08, 0, f"SLIDER{k + 1}")
    stub(sx, sy + 5.08, 0, 2.54, "GND")
text("Slider (B103, 10 kOhm linear)", 129.54, 87.63, 1.8)
text("Bei Jitter optional 100 nF vom Schleifer nach GND", 129.54, 182.88)

# --- OLED ---
OX, OY = 147.32, 213.36
place("OLED_SSD1306_I2C", "U2", OX, OY, "OLED 128x64 I2C (0x3C)", ref_at=(0, -10.16), val_at=(0, 7.62))
for k, net in enumerate(["GND", "+3V3", "SCL", "SDA"]):
    stub(OX - 7.62, OY - (5.08 - 2.54 * k), -5.08, 0, net)
text("OLED (SSD1306, an 3V3)", 129.54, 199.39, 1.8)

# --- Switch PCB ---
frame(195.58, 53.34, 405.13, 236.22)
text("Switch-Platine (hardware/pcb/makropad_switch.kicad_pcb)", 199.39, 60.96, 1.8)
text("Tasten schalten gegen GND, Pullups intern im ESP32 (INPUT_PULLUP), keine Dioden", 199.39, 66.04)
HX, HY = 218.44, 129.54
place("Conn_02x09", "J1", HX, HY, "Stiftleiste 2x9 zum ESP32", ref_at=(0, -13.97), val_at=(0, 13.97))
for k in range(9):
    stub(HX - 5.08, HY - (10.16 - 2.54 * k), -5.08, 0, HEADER[2 * k])
    stub(HX + 5.08, HY - (10.16 - 2.54 * k), 5.08, 0, HEADER[2 * k + 1])
for n in range(16):
    c, r = n % 4, n // 4
    kx, ky = 270.51 + 33.02 * c, 87.63 + 33.02 * r
    place("MX", f"K{n + 1}", kx, ky, "MX", ref_at=(0, -5.08), val_at=(0, 3.81))
    stub(kx - 5.08, ky, -5.08, 0, f"K{n + 1}")
    stub(kx + 5.08, ky, 5.08, 0, "GND")
text("Tastennummern zeilenweise von links oben (OLED ersetzt den 2x2-Block oben rechts):", 254, 210.82)
text(" 1  2  3  .  .\\n 4  5  6  .  .\\n 7  8  9 10 11\\n12 13 14 15 16", 254, 218.44)

lib = " ".join(libsym(n, "K" if n == "MX" else "J" if n.startswith("Conn") else "RV" if n == "B103" else "U",
                      g, p, pin_names=n not in ("MX", "B103", "Conn_02x09"))
               for n, (g, p) in SYMBOLS.items())
sch = (f'(kicad_sch (version 20231120) (generator "makropad_schematic") (generator_version "8.0") '
       f'(uuid "{ROOT}") (paper "A3") '
       f'(title_block (title "MakroPad Schaltplan") (date "2026-10-30") (rev "1") '
       f'(comment 1 "Erzeugt von hardware/schematic/makropad_schematic.py")) '
       f'(lib_symbols {lib}) {" ".join(items)} (sheet_instances (path "/" (page "1"))))\n')
with open(OUT, "w", encoding="utf-8") as f:
    f.write(sch)
print(OUT)
