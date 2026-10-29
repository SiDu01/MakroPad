# Generates the switch PCB (makropad_switch.kicad_pcb) from the case geometry.
# Run with KiCad's Python:
#   & "D:\Program Files\KiCad\10.0\bin\python.exe" makropad_switch_pcb.py
#
# Design rules: Wuerth Elektronik BASIC design rules v2.0 (04/2023), STANDARD category,
# outer layers up to 1 oz starting foil.
#
# Coordinates below are in the lid system of ../case/makropad_case.scad
# (x to the right, y towards the back, mm). Keep them in sync with the case.
import os
import pcbnew

KEYS_X0 = 14.0       # keys_x0 = margin_left
KEYS_Y0 = 19.4       # keys_y0 = margin_front + (content_d - keys_d) / 2
PITCH = 19.05
COLS, ROWS = 5, 4    # OLED replaces the top right 2x2 block (cols 3-4, rows 2-3)

# Board outline: key grid, a corridor next to the OLED cable opening and a tab
# for the connector. The back edge stays short of the plate's back screw (x 42.6, y 101.1),
# the tab stays east of the ESP32 (x < 78.2 incl. its end stop).
OUTLINE = [(14, 19.4), (109.25, 19.4), (109.25, 57.5), (77, 57.5), (77, 83),
           (104.5, 83), (104.5, 103.5), (77, 103.5), (77, 98), (14, 98)]

TRACK_W = 0.25
LANE = 0.5           # track pitch in the routing channels
HDR_X0 = 82.0        # first header column (pins point down, east of the ESP32 end stop)
HDR_YA = 95.6        # front header row; row B is 2.54 further back

FP_LIB = os.path.join(os.environ.get("KICAD10_FOOTPRINT_DIR",
                      r"D:\Program Files\KiCad\10.0\share\kicad\footprints"))
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "makropad_switch.kicad_pcb")

OX, OY = 20.0, 140.0  # place the board on the KiCad sheet; KiCad y points down


def P(x, y):
    return pcbnew.VECTOR2I(pcbnew.FromMM(OX + x), pcbnew.FromMM(OY - y))


def mm(v):
    return (pcbnew.ToMM(v.x) - OX, OY - pcbnew.ToMM(v.y))


def button_number(c, r):
    # Firmware numbering (config::ButtonPins): row by row from the back left, OLED block skipped.
    order = [(c2, r2) for r2 in range(ROWS - 1, -1, -1) for c2 in range(COLS) if not is_oled(c2, r2)]
    return order.index((c, r)) + 1


def is_oled(c, r):
    return c >= 3 and r >= 2


board = pcbnew.BOARD()
ds = board.GetDesignSettings()
ds.SetCopperLayerCount(2)
ds.m_TrackMinWidth = pcbnew.FromMM(0.2)
ds.m_MinClearance = pcbnew.FromMM(0.18)             # copper spacing, 1 oz starting foil
ds.m_HoleToHoleMin = pcbnew.FromMM(0.5)             # holes on different potential (same: 0.3, NPTH: 0.35)
ds.m_SolderMaskExpansion = pcbnew.FromMM(0.05)      # mask clearance >= 50 um
ds.m_SolderMaskMinWidth = pcbnew.FromMM(0.07)       # mask web >= 70 um
ds.m_SolderMaskToCopperClearance = pcbnew.FromMM(0.05)  # mask coverage >= 50 um
ds.m_SilkClearance = pcbnew.FromMM(0.2)             # between markings
ds.m_MinSilkTextHeight = pcbnew.FromMM(1.5)         # legend print on solder mask
ds.m_MinSilkTextThickness = pcbnew.FromMM(0.15)
SILK_W = 0.15             # minimum legend line width
SILK_TO_COPPER = 0.3      # legend to exposed copper and NPTH edges

nets = {}


def net(name):
    if name not in nets:
        n = pcbnew.NETINFO_ITEM(board, name)
        board.Add(n)
        nets[name] = n
    return nets[name]


for (x0, y0), (x1, y1) in zip(OUTLINE, OUTLINE[1:] + OUTLINE[:1]):
    s = pcbnew.PCB_SHAPE(board)
    s.SetShape(pcbnew.SHAPE_T_SEGMENT)
    s.SetLayer(pcbnew.Edge_Cuts)
    s.SetWidth(pcbnew.FromMM(0.1))
    s.SetStart(P(x0, y0))
    s.SetEnd(P(x1, y1))
    board.Add(s)


def text(s, x, y, layer, size=1.5, angle=0):
    t = pcbnew.PCB_TEXT(board)
    t.SetText(s)
    t.SetLayer(layer)
    t.SetPosition(P(x, y))
    t.SetTextSize(pcbnew.VECTOR2I(pcbnew.FromMM(size), pcbnew.FromMM(size)))
    t.SetTextThickness(pcbnew.FromMM(0.2))
    t.SetTextAngleDegrees(angle)
    if layer == pcbnew.B_SilkS:
        t.SetMirrored(True)
    board.Add(t)


# --- Switches: pad "2" (centre -3.81, +2.54) is the signal, pad "1" (+2.54, +5.08) is GND ---
switches = {}
for r in range(ROWS):
    for c in range(COLS):
        if is_oled(c, r):
            continue
        n = button_number(c, r)
        cx, cy = KEYS_X0 + (c + 0.5) * PITCH, KEYS_Y0 + (r + 0.5) * PITCH
        fp = pcbnew.FootprintLoad(os.path.join(FP_LIB, "Button_Switch_Keyboard.pretty"), "SW_Cherry_MX_1.00u_PCB")
        fp.SetReference(f"K{n}")
        fp.SetValue("MX")
        fp.Reference().SetTextSize(pcbnew.VECTOR2I(pcbnew.FromMM(1.5), pcbnew.FromMM(1.5)))
        fp.Reference().SetTextThickness(pcbnew.FromMM(0.2))
        board.Add(fp)
        fp.SetPosition(P(cx + 2.54, cy + 5.08))  # footprint origin is pad 1
        for pad in fp.Pads():
            if pad.GetNumber() == "1":
                pad.SetNet(net("GND"))
            elif pad.GetNumber() == "2":
                pad.SetNet(net(f"K{n}"))
        text(f"K{n}", cx, cy - 6.5, pcbnew.B_SilkS)
        switches[(c, r)] = (n, cx, cy)

# --- Connector: 2x9 header on the back, 9 columns along x ---
hdr = pcbnew.FootprintLoad(os.path.join(FP_LIB, "Connector_PinHeader_2.54mm.pretty"), "PinHeader_2x09_P2.54mm_Vertical")
hdr.SetReference("J1")
hdr.SetValue("to ESP32")
board.Add(hdr)
hdr.SetOrientationDegrees(90)
hdr.Flip(hdr.GetPosition(), pcbnew.FLIP_DIRECTION_TOP_BOTTOM)
pads = list(hdr.Pads())
xs = [mm(p.GetPosition())[0] for p in pads]
ys = [mm(p.GetPosition())[1] for p in pads]
hdr.Move(P(HDR_X0 - min(xs), HDR_YA - min(ys)) - P(0, 0))
cols = sorted({round(mm(p.GetPosition())[0], 3) for p in pads})
assert len(cols) == 9, cols
row_a = {round(mm(p.GetPosition())[0], 3): p for p in pads if abs(mm(p.GetPosition())[1] - HDR_YA) < 0.01}
row_b = {round(mm(p.GetPosition())[0], 3): p for p in pads if abs(mm(p.GetPosition())[1] - HDR_YA - 2.54) < 0.01}
assert len(row_a) == 9 and len(row_b) == 9
YB = HDR_YA + 2.54


# --- Routing (all signals on F.Cu, GND as zones on both layers) ---
# Each switch runs north into the channel behind its row, then east/west into a vertical
# corridor between column 2 and 3 (lanes ordered so nothing crosses), then east into the tab.
# The back row runs straight east behind its switches. Trace i (counted from the north)
# ends on vertical v_i: even i -> row A pin, odd i -> between row A pins to row B.
def track(points, n):
    for a, b in zip(points, points[1:]):
        t = pcbnew.PCB_TRACK(board)
        t.SetLayer(pcbnew.F_Cu)
        t.SetWidth(pcbnew.FromMM(TRACK_W))
        t.SetStart(P(*a))
        t.SetEnd(P(*b))
        t.SetNet(n)
        board.Add(t)


def teardrop(pad, toward, pad_d, n):
    # Teardrops are recommended at every pad connection; KiCad's generator is GUI-only.
    dx, dy = toward[0] - pad[0], toward[1] - pad[1]
    seg = (dx * dx + dy * dy) ** 0.5
    ux, uy = dx / seg, dy / seg
    length, w = min(pad_d / 2 + 0.8, seg), 0.4 * pad_d
    tip = (pad[0] + ux * length, pad[1] + uy * length)
    pts = [(pad[0] - uy * w, pad[1] + ux * w), (tip[0] - uy * TRACK_W / 2, tip[1] + ux * TRACK_W / 2),
           (tip[0] + uy * TRACK_W / 2, tip[1] - ux * TRACK_W / 2), (pad[0] + uy * w, pad[1] - ux * w)]
    sh = pcbnew.PCB_SHAPE(board)
    sh.SetShape(pcbnew.SHAPE_T_POLY)
    sh.SetPolyPoints([P(*q) for q in pts])
    sh.SetFilled(True)
    sh.SetWidth(0)
    sh.SetLayer(pcbnew.F_Cu)
    sh.SetNet(n)
    board.Add(sh)


for (c, r), (n, cx, cy) in switches.items():
    s = (cx - 3.81, cy + 2.54)
    if r == ROWS - 1:
        y_h = 93.0 + (2 - c) * LANE
        i = c
        path = [s, (s[0], y_h)]
    else:
        lane = (2 - r) * 3 + c if c <= 2 else 9 + 2 * r + (c - 3)
        y_h = cy + 7.0 + ((2 - c) if c <= 2 else (c - 3)) * LANE
        x_lane = 68.0 + lane * LANE
        y_turn = 84.6 + (12 - lane) * LANE
        i = 3 + lane
        path = [s, (s[0], y_h), (x_lane, y_h), (x_lane, y_turn)]
        y_h = y_turn
    k = i // 2
    x_pin = cols[k]
    if i % 2 == 0:
        row_a[x_pin].SetNet(net(f"K{n}"))
        path += [(x_pin, y_h), (x_pin, HDR_YA)]
        text(str(n), x_pin, HDR_YA - 3.0, pcbnew.B_SilkS, angle=90)
    else:
        row_b[x_pin].SetNet(net(f"K{n}"))
        path += [(x_pin + 1.27, y_h), (x_pin + 1.27, YB), (x_pin, YB)]
        text(str(n), x_pin, YB + 3.0, pcbnew.B_SilkS, angle=90)
    track(path, net(f"K{n}"))
    teardrop(path[0], path[1], 2.2, net(f"K{n}"))
    teardrop(path[-1], path[-2], 1.7, net(f"K{n}"))

row_a[cols[8]].SetNet(net("GND"))
row_b[cols[8]].SetNet(net("GND"))
text("G", cols[8], HDR_YA - 3.0, pcbnew.B_SilkS, angle=90)
text("G", cols[8], YB + 3.0, pcbnew.B_SilkS, angle=90)
text("MakroPad switch PCB", 40, 96.0, pcbnew.B_SilkS)

for layer in (pcbnew.F_Cu, pcbnew.B_Cu):
    z = pcbnew.ZONE(board)
    z.SetLayer(layer)
    z.SetNet(net("GND"))
    z.SetLocalClearance(pcbnew.FromMM(0.3))
    z.SetMinThickness(pcbnew.FromMM(0.25))
    z.SetPadConnection(pcbnew.ZONE_CONNECTION_THERMAL)
    ol = z.Outline()
    ol.NewOutline()
    for x, y in OUTLINE:
        p = P(x, y)
        ol.Append(p.x, p.y)
    board.Add(z)

# Library footprints draw silk at 0.12 mm; the legend minimum is 0.15 mm.
for fp in board.GetFootprints():
    for g in fp.GraphicalItems():
        if g.GetLayer() in (pcbnew.F_SilkS, pcbnew.B_SilkS) and g.GetClass() == "PCB_SHAPE":
            g.SetWidth(max(g.GetWidth(), pcbnew.FromMM(SILK_W)))

# KiCad's DRC only checks silk against mask openings; check the legend-to-copper rule here.
silk = [t for t in board.GetDrawings() if t.GetLayer() in (pcbnew.F_SilkS, pcbnew.B_SilkS)]
for fp in board.GetFootprints():
    silk += [g for g in fp.GraphicalItems() if g.GetLayer() in (pcbnew.F_SilkS, pcbnew.B_SilkS)]
    silk += [fp.Reference()] if fp.Reference().GetLayer() in (pcbnew.F_SilkS, pcbnew.B_SilkS) else []
gap = pcbnew.FromMM(SILK_TO_COPPER) + ds.m_SolderMaskExpansion
bad = [(item.GetLayerName(), mm(item.GetPosition()), pad.GetParentFootprint().GetReference())
       for item in silk for pad in board.GetPads()
       if item.GetEffectiveShape(item.GetLayer()).Collide(pad.GetEffectiveShape(item.GetLayer()), gap)]
assert not bad, bad

board.BuildConnectivity()
pcbnew.ZONE_FILLER(board).Fill(board.Zones())
board.Save(OUT)
print("wrote", OUT, "with", len(switches), "switches")
