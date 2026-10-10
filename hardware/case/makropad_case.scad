// MakroPad Gehaeuse – parametrisch (OpenSCAD)
//
// Vier Druckteile:
//   - lid          : flacher Deckel mit Slider-Schlitzen und Ausschnitten fuer Tastenfeld und OLED
//                    (wird flach und kopfueber exportiert -> Sichtseite auf dem Druckbett)
//   - switch_plate : versenkte Switch-Platte mit Muldenwand, wird von unten in den Deckel gesetzt
//                    und mit Laschen angeschraubt (Platte auf dem Druckbett)
//   - tray         : verjuengte Unterschale, rundum gegenueber dem Deckel eingezogen,
//                    mit USB-Ausschnitt, ESP32-Halterung und Schraubsaeulen
//   - oled_frame   : Rahmen um das OLED (z. B. Silk-Silber-PLA; Sichtseite auf dem Druckbett)
//
// Export:
//   openscad -D 'part="lid"'          -o lid.stl          makropad_case.scad
//   openscad -D 'part="switch_plate"' -o switch_plate.stl makropad_case.scad
//   openscad -D 'part="tray"'         -o tray.stl         makropad_case.scad
//   openscad -D 'part="oled_frame"'   -o oled_frame.stl   makropad_case.scad
//
// Koordinaten: x = Breite (links->rechts), y = Tiefe (vorne->hinten), z = Hoehe.
// Deckel-Bauteile werden im geneigten Deckel-System beschrieben (z = 0 an der
// Deckel-Oberseite, y entlang der Schraege) und ueber tilted() in die Welt gedreht.
// Schale und ESP32 sind in Weltkoordinaten beschrieben (Boden waagerecht).
// Masse mit "NACHMESSEN" sind Annahmen und sollten am echten Bauteil geprueft werden.

/* [Ausgabe] */
part = "assembly"; // [assembly, exploded, lid, switch_plate, tray, oled_frame]
show_dummies = true; // Tastenkappen/Slider/OLED/ESP32 in der Vorschau anzeigen

/* [Gehaeuse] */
tilt = 12;          // Neigung der Oberseite in Grad
front_h = 17;       // Gesamthoehe an der Vorderkante
wall = 3;           // Wandstaerke der Schale
floor_t = 2.5;      // Bodenstaerke
lid_t = 5;          // Deckelstaerke = Hoehe des ueberstehenden Rands (Kanten rechtwinklig zur Oberflaeche)
corner_r = 2;       // Eckenradius aussen
margin_left = 14;   // Deckelrand links bis Tastenfeld
margin_right = 14;  // Deckelrand rechts bis Slider-Modul
margin_front = 12;  // Deckelrand vorne bis Slider-Modul
margin_back = 17;   // Deckelrand hinten bis Slider-Modul (Platz fuer den ESP32 hinten)
section_gap = 9;    // Abstand Tastenfeld <-> Slider (Platz fuer zwei Schraubsaeulen)
fit = 0.2;          // Spiel fuer Passungen

/* [Unterschale] */
tray_offset = [8, 5]; // Einzug der Schale gegenueber dem Deckel [Seiten, vorne/hinten]
band_h = 5;           // senkrechtes Band unter dem Deckel
taper = 4;            // weiterer Einzug bis zur Bodenflaeche (je Seite)
bottom_corner = 6;    // Eckenabschnitt der Bodenflaeche (Achteck)

/* [Schrauben (Deckel <-> Schale)] */
// 4x M2x10 Zylinderkopf: von unten durch Saeulen der Schale in kurze Dome am Deckel.
// Wo fuer Saeulen kein Platz ist (rechts neben den Slidern, hinten), halten Schnapphaken.
boss_d = 5;          // Dom-Durchmesser am Deckel
boss_h = 5;          // Domlaenge unter dem Deckel
boss_pilot_d = 1.6;  // Kernloch M2 selbstschneidend
pillar_d = 5.5;      // Saeule in der Schale
screw_d = 2.4;       // Durchgang in der Saeule
screw_head_d = 4.2;  // Kanal fuer den Schraubenkopf (M2 DIN 912: 3.8)
screw_grip = 3;      // Saeulenmaterial zwischen Schraubenkopf und Dom
hook_w = 8;          // Schnapphaken am Deckel
hook_t = 1.4;
hook_len = 5;        // Laenge unter der Deckel-Unterseite
catch_h = 1.5;       // Hoehe der Rastnase
catch_d = 0.8;       // Ueberstand der Rastnase
recess_d = 1;        // Tiefe der Rasttasche in der Schalenwand

/* [Tasten (MX-kompatibel)] */
key_cols = 5;
key_rows = 4;
oled_block = [2, 2]; // Tasten oben rechts, die durch das OLED ersetzt werden [Spalten, Reihen]
key_pitch = 19.05;
sw_hole = 14.0;      // Ausschnitt in der Platte
sw_plate = 1.5;      // Plattenstaerke an den Clips
sw_pocket = 15.5;    // Freistich unter der Platte fuer die Clips
plate_t = 3;         // Gesamtstaerke der versenkten Switch-Platte
well_depth = 7.5;    // Platten-Oberseite -> Deckel-Oberflaeche (~ Unterkante Keycap in Ruhe)
well_gap = 0.5;      // Spiel Mulde <-> Keycap-Raster
well_wall = 2;       // Wandstaerke der Mulde
tab_len = 5;         // Laschen der Switch-Platte (unter dem Deckel)
tab_w = 8;
tab_t = 2.5;
tab_screw_d = 2.2;   // M2 Durchgang in der Lasche
tab_pilot_d = 1.6;   // M2 selbstschneidend im Deckel
tab_head_d = 4.5;    // Kanal fuer den Schraubenkopf

/* [Slider-Module] */
slider_n = 3;
slider_pcb = [21, 91];          // Breite, Laenge des Moduls
slider_pitch = 23;              // Mittenabstand der Module
slider_travel = 60;             // Hub (gemessen)
slider_slot_w = 3;              // Schlitzbreite fuer den Hebel
slider_slot_extra = 5;          // Hebelbreite + Spiel in Hubrichtung -> Schlitz 65 mm (gemessen)
slider_slot_offset = 0;         // Versatz des Schlitzes entlang der Laenge (NACHMESSEN)
slider_pot_offset = 3.5;        // Poti sitzt aussermittig: Rand 2 mm / 9 mm -> Schlitz 3.5 mm nach rechts (gemessen)
slider_pot_top = 7;             // Platinen-Oberseite -> Oberkante Poti-Gehaeuse (gemessen)
slider_hole_inset = [3, 3];     // Lochmitte vom Platinenrand [quer, laengs] -> Lochabstand 15 x 85 mm (gemessen)
slider_standoff_d = 5.5;
slider_pilot_d = 2.5;           // M3 selbstschneidend

/* [OLED 0.96" SSD1306] */
oled_pcb = [27.3, 27.8];
oled_hole_spacing = [21, 22];      // Lochabstand Mitte-Mitte [quer, laengs] (gemessen)
oled_pilot_d = 1.7;                // Kernloch im Rahmen; gedruckt passen M1.7-Schrauben
oled_window = [24, 13];            // sichtbares Fenster (im Rahmen)
oled_window_offset = 1.0;          // Fenstermitte relativ zur Platinenmitte (+ = nach hinten)
oled_glass = [27.4, 19.8, 1.5];    // Tasche fuer das Glas (B, H, Tiefe), mittig zwischen den Loechern
oled_pcb_fit = 0.3;                // Spiel Platine <-> Tasche im Rahmen
oled_recess = 2;                   // Glas liegt so tief unter der Rahmen-Oberkante
oled_header = [12, 3.5, 1.2];      // Freiraum fuer die Loetstellen (hinten, ueber der Platine)
oled_ledge = 1;                    // Auflage des Rahmens im Deckel (Staerke)
oled_frame_screw_d = 2.2;          // Durchgang in der Auflage (Rahmen von unten verschrauben, M1.7)
oled_frame_screw_inset = 1.9;      // Abstand der Rahmenschrauben von der Rahmenecke
oled_frame_gap = 2.5;              // Steg zwischen Rahmen und Tastenfeld-Ausschnitt
oled_frame_proud = 0.6;            // Ueberstand des Rahmens ueber die Deckel-Oberflaeche
oled_angle = 60;                   // Winkel des Displays zum Tisch (Deckel ist um tilt geneigt)
oled_face_len = 33;                // Laenge der schraegen Display-Flaeche
oled_wall = 2;                     // Wandstaerke des Display-Gehaeuses
oled_back = 10;                    // Tiefe des Gehaeuses hinter der Display-Flaeche
oled_comp_h = 3;                   // Bauteile auf der Platinen-Rueckseite
// Das Display steht kopfueber (Anschluesse unten, Firmware dreht das Bild): oben steckt die
// Platine unter einer Haltelippe, unten halten 2 Schrauben.
oled_slide = 2.5;                  // Schiebeweg beim Einsetzen unter die Lippe
oled_lip_len = 2;                  // Ueberdeckung der Lippe am oberen Platinenrand
oled_lip_t = 1.5;                  // Staerke der Lippe
oled_groove = 0.5;                 // Luft hinter dem oberen Platinenrand
oled_pcb_band = 1.8;               // Platinenstaerke + Spiel in der Nut
oled_frame_fit = 0.15;             // Spiel Rahmen <-> Tasche

/* [ESP32-S3-DevKitC-1] */
esp_l = 74;           // Gesamtlaenge (gemessen)
esp_w = 28;           // Breite am USB-Ende (gemessen)
esp_tip = [6, 18];    // schmales Ende gegenueber USB: [Laenge, Breite] (gemessen)
esp_pcb_t = 1.6;
esp_pins = 8.5;       // Stiftleisten unter der Platine
esp_x0 = 14;          // USB-Ende der Platine (Abstand zur linken Deckelkante)
esp_y0 = 76.5;        // Vorderkante der Platine (Weltkoordinate)
usb_slot = [27, 8];   // Ausschnitt in der linken Seitenwand (deckt beide USB-C-Buchsen ab)
usb_conn_h = 3.2;

/* [Fuesse] */
feet_d = 10.5;       // Vertiefung fuer Gummifuesse
feet_depth = 1;
feet_inset = [24, 21];

$fn = 48;

// ---------- abgeleitete Masse ----------
keys_w = key_cols * key_pitch;
keys_d = key_rows * key_pitch;
sl_w = (slider_n - 1) * slider_pitch + slider_pcb[0];
sl_d = slider_pcb[1];
content_d = max(keys_d, sl_d);

W = margin_left + keys_w + section_gap + sl_w + margin_right;
D = margin_front + content_d + margin_back;   // Tiefe entlang der Schraege
Dw = D * cos(tilt);                           // Grundflaeche des Deckels (Oberseite)
lid_ys = lid_t * sin(tilt);                   // Unterkante des Deckels liegt um so viel weiter hinten
back_h = front_h + D * sin(tilt);
lid_bot = -lid_t;

keys_x0 = margin_left;
keys_y0 = margin_front + (content_d - keys_d) / 2;
sl_x0 = margin_left + keys_w + section_gap;
sl_y0 = margin_front + (content_d - sl_d) / 2;

// Switch-Platte (Masse fuer Mulde und Deckelausschnitt)
plate_bot = -(well_depth + plate_t);
well_out = well_gap + well_wall;               // Aussenkante der Muldenwand ab Tastenraster
cut_out = well_gap;                            // Kante des Deckelausschnitts ab Tastenraster (= Muldeninnenkante,
                                               // die Muldenwand der Switch-Platte endet unter dem Deckel)

// OLED-Rahmen: fuellt den 2x2-Block; rechte und hintere Kante fluchten mit dem
// Deckelausschnitt des Tastenfelds, zu den Tasten bleibt ein gleichmaessiger Steg.
oled_block0 = [keys_x0 + (key_cols - oled_block[0]) * key_pitch, keys_y0 + (key_rows - oled_block[1]) * key_pitch];
oled_frame_min = oled_block0 + [cut_out + oled_frame_gap, cut_out + oled_frame_gap];
oled_frame_max = [keys_x0 + keys_w + cut_out, keys_y0 + keys_d + cut_out];
oled_frame_size = oled_frame_max - oled_frame_min;
oled_frame_c = (oled_frame_min + oled_frame_max) / 2;
oled_c = oled_frame_c;                           // Platinenmitte = Rahmenmitte

// ESP32 liegt waagerecht hinten links auf Stuetzen, USB-Ende zur linken Seitenwand
esp_zb = floor_t + esp_pins + 0.5;   // Platinen-Unterseite (Welt)
esp_x1 = esp_x0 + esp_l;
esp_y1 = esp_y0 + esp_w;
esp_cy = (esp_y0 + esp_y1) / 2;
usb_z = esp_zb + esp_pcb_t + usb_conn_h / 2;

// Schraubsaeulen in den Innenecken der Schale
// Innenkanten der Schale im Bandbereich (Welt)
tray_in_x = [tray_offset[0] + wall, W - tray_offset[0] - wall];
tray_in_y = [lid_ys + tray_offset[1] + wall, lid_ys + Dw - tray_offset[1] - wall];
pillar_y = [tray_in_y[0] + fit + pillar_d / 2, tray_in_y[1] - fit - pillar_d / 2];
gap_x = (keys_x0 + keys_w + well_gap + well_wall + sl_x0) / 2;   // Mitte der Luecke Tasten/Slider
boss_pos = [[keys_x0 + key_pitch, pillar_y[0]], [gap_x, pillar_y[0]],
            [gap_x, pillar_y[1]], [keys_x0 + 4.4 * key_pitch, pillar_y[1]]];
// Schnapphaken: [Punkt auf der Innenwand der Schale, Richtung nach aussen]
hooks = [[[tray_in_x[1], lid_ys + 30], [1, 0]], [[tray_in_x[1], lid_ys + Dw - 30], [1, 0]],
         [[keys_x0 + 1.5 * key_pitch, tray_in_y[1]], [0, 1]], [[sl_x0 + sl_w / 2, tray_in_y[1]], [0, 1]]];
feet_pos = [[feet_inset[0], lid_ys + feet_inset[1]], [W - feet_inset[0], lid_ys + feet_inset[1]],
            [feet_inset[0], lid_ys + Dw - feet_inset[1]], [W - feet_inset[0], lid_ys + Dw - feet_inset[1]]];

// Weltkoordinate z einer Ebene parallel zum Deckel (Deckel-System z = zt) an Position y
function z_at(y, zt) = front_h + y * tan(tilt) + zt / cos(tilt);
pillar_top_zt = lid_bot - boss_h;
function screw_seat(p) = z_at(p[1], pillar_top_zt) - pillar_d / 2 * tan(tilt) - screw_grip;

function is_oled_key(c, r) = c >= key_cols - oled_block[0] && r >= key_rows - oled_block[1];
keys = [for (c = [0 : key_cols - 1], r = [0 : key_rows - 1]) if (!is_oled_key(c, r))
    [keys_x0 + (c + 0.5) * key_pitch, keys_y0 + (r + 0.5) * key_pitch]];
function slider_c(i) = [sl_x0 + slider_pcb[0] / 2 + i * slider_pitch, sl_y0 + sl_d / 2];
slider_slot_len = slider_travel + slider_slot_extra;
slider_holes = [for (sx = [-1, 1], sy = [-1, 1])
    [sx * (slider_pcb[0] / 2 - slider_hole_inset[0]), sy * (slider_pcb[1] / 2 - slider_hole_inset[1])]];
oled_holes = [for (sx = [-1, 1], sy = [-1, 1])
    [sx * oled_hole_spacing[0] / 2, sy * oled_hole_spacing[1] / 2]];
// Hoehen im Deckel-System: Rahmen sitzt auf einer Auflage im Deckel, Display steckt von unten im Rahmen
oled_frame_bot = lid_bot + oled_ledge;
oled_face_t = oled_recess + oled_glass[2];       // Display-Flaeche -> Platinen-Oberseite
oled_frame_screws = [for (s = [-1, 1]) s * (oled_frame_size / 2 - [oled_frame_screw_inset, oled_frame_screw_inset])];

// Switch-Platte
// Laschen: [Punkt auf der Aussenkante der Muldenwand, Richtung nach aussen]
tabs = [
    [[keys_x0 + 1.5 * key_pitch, keys_y0 - well_out], [0, -1]],
    [[keys_x0 + 3.5 * key_pitch, keys_y0 - well_out], [0, -1]],
    [[keys_x0 + 1.5 * key_pitch, keys_y0 + keys_d + well_out], [0, 1]],
    [[keys_x0 + keys_w + well_out, keys_y0 + key_pitch], [1, 0]]
];
function tab_hole(t) = t[0] + t[1] * (tab_len / 2 + 0.5);

echo(str("Deckel: ", W, " x ", Dw, " mm, Hoehe vorne ", front_h, " / hinten ", back_h, " mm, ",
         len(keys), " Tasten"));

// ---------- Hilfsformen ----------
module rr2d(size, r) {
    translate([r, r]) offset(r) square([size[0] - 2 * r, size[1] - 2 * r]);
}

module rr2d_c(size, r) {
    translate(-size / 2) rr2d(size, r);
}

module slot2d(len, w) {
    hull() for (s = [-1, 1]) translate([0, s * (len - w) / 2]) circle(d = w);
}

// geneigtes Deckel-System -> Welt
module tilted() {
    translate([0, 0, front_h]) rotate([tilt, 0, 0]) children();
}

// Durchbruch durch den Deckel mit 45-Grad-Fase an der Oberseite (Deckel-System)
module chamfered_cut(ch = 1) {
    translate([0, 0, lid_bot - 1]) linear_extrude(lid_t + 2) children();
    hull() {
        translate([0, 0, -ch]) linear_extrude(0.01) children();
        linear_extrude(0.01) offset(delta = ch) children();
    }
}

// Umriss des Tastenfelds (Raster-Quadrate, vereinigt) + Zugabe
module keys_outline(extra = 0) {
    offset(r = extra) union() for (k = keys) translate(k) square(key_pitch, center = true);
}

// ---------- Unterschale: Umrisse ----------
// Oberkante der Schale: gegenueber der Deckel-Unterkante eingezogen
module tray2d(inset = 0) {
    offset(delta = -inset) translate(tray_offset + [0, lid_ys])
        rr2d([W - 2 * tray_offset[0], Dw - 2 * tray_offset[1]], corner_r);
}

// Bodenflaeche: weiter eingezogenes Achteck
module bottom2d(inset = 0) {
    x0 = tray_offset[0] + taper; x1 = W - x0;
    y0 = lid_ys + tray_offset[1] + taper; y1 = lid_ys + Dw - tray_offset[1] - taper;
    c = bottom_corner;
    offset(delta = -inset)
        polygon([[x0 + c, y0], [x1 - c, y0], [x1, y0 + c], [x1, y1 - c],
                 [x1 - c, y1], [x0 + c, y1], [x0, y1 - c], [x0, y0 + c]]);
}

// Schalenkoerper: senkrechtes Band unter dem Deckel, darunter Facetten bis zum Boden.
// inset > 0 ergibt den Innenraum.
module body(inset = 0, z0 = 0, top_extra = 0) {
    hull() {
        intersection() {
            linear_extrude(300) tray2d(inset);
            tilted() translate([-50, -50, lid_bot - band_h]) cube([W + 100, D + 100, band_h + top_extra]);
        }
        translate([0, 0, z0]) linear_extrude(0.01) bottom2d(inset);
    }
}

// ---------- Deckel ----------
module lid_cuts() {
    // Ausschnitt fuer die Switch-Platte
    translate([0, 0, lid_bot - 1]) linear_extrude(lid_t + 2) keys_outline(cut_out);
    for (t = tabs) translate(tab_hole(t)) translate([0, 0, lid_bot - 1]) cylinder(d = tab_pilot_d, h = 4);

    // Slider-Schlitze + Kernloecher in den Abstandshaltern
    for (i = [0 : slider_n - 1]) {
        s = slider_c(i);
        translate([s[0] + slider_pot_offset, s[1] + slider_slot_offset]) chamfered_cut(0.8) slot2d(slider_slot_len, slider_slot_w);
        for (h = slider_holes)
            translate([s[0] + h[0], s[1] + h[1], lid_bot - slider_pot_top - 1]) cylinder(d = slider_pilot_d, h = 9);
    }

    // OLED: Tasche fuer den Rahmen, darunter Auflage mit Oeffnung fuer Platine und Kabel
    translate([oled_frame_c[0], oled_frame_c[1], oled_frame_bot])
        linear_extrude(-oled_frame_bot + 1)
            offset(delta = oled_frame_fit) rr2d_c(oled_frame_size, 1.5);
    translate([oled_c[0], oled_c[1], lid_bot - 1])
        linear_extrude(oled_ledge + 1.01) square(oled_pcb + [1.4, 1.4], center = true);
    for (h = oled_frame_screws)
        translate([oled_frame_c[0] + h[0], oled_frame_c[1] + h[1], lid_bot - 1]) cylinder(d = oled_frame_screw_d, h = oled_ledge + 2);
}

module lid() {
    difference() {
        union() {
            // Deckelplatte, Kanten rechtwinklig zur Oberflaeche
            tilted() translate([0, 0, lid_bot]) linear_extrude(lid_t) rr2d([W, D], corner_r);

            // Schnapphaken
            for (h = hooks) hook(h);

            // kurze Schraubdome
            for (p = boss_pos) intersection() {
                translate([p[0], p[1], 0]) cylinder(d = boss_d, h = 300);
                tilted() translate([-50, -50, pillar_top_zt + 0.3]) cube([W + 100, D + 100, boss_h - 0.3 + 0.01]);
            }

            // Abstandshalter fuer die Slider-Module
            tilted() for (i = [0 : slider_n - 1], h = slider_holes)
                translate([slider_c(i)[0] + h[0], slider_c(i)[1] + h[1], lid_bot - slider_pot_top])
                    cylinder(d = slider_standoff_d, h = slider_pot_top + 0.01);
        }

        tilted() lid_cuts();

        // Kernloecher in den Schraubdomen (bis 1 mm unter die Oberflaeche)
        for (p = boss_pos)
            translate([p[0], p[1], z_at(p[1], pillar_top_zt) - 2])
                cylinder(d = boss_pilot_d, h = z_at(p[1], -1) - z_at(p[1], pillar_top_zt) + 2 - boss_d / 2 * tan(tilt));
    }
}

// Schnapphaken am Deckel (Welt): Lasche senkrecht nach unten, Rastnase nach aussen
module hook(h) {
    p = h[0]; zt = z_at(p[1], lid_bot);
    translate([p[0], p[1], 0]) rotate([0, 0, atan2(h[1][1], h[1][0])]) {
        translate([-fit - hook_t, -hook_w / 2, zt - hook_len]) cube([hook_t, hook_w, hook_len + 1]);
        hull() {
            translate([-fit - hook_t, -hook_w / 2, zt - hook_len + catch_h]) cube([hook_t + catch_d, hook_w, 0.01]);
            translate([-fit - hook_t, -hook_w / 2, zt - hook_len]) cube([hook_t, hook_w, 0.01]);
        }
    }
}

// Rasttasche in der Schalenwand (Welt)
module hook_recess(h) {
    p = h[0]; zt = z_at(p[1], lid_bot);
    translate([p[0], p[1], 0]) rotate([0, 0, atan2(h[1][1], h[1][0])])
        translate([-0.01, -hook_w / 2 - 0.5, zt - hook_len - 0.5]) cube([recess_d + 0.01, hook_w + 1, catch_h + 0.8]);
}

// ---------- Switch-Platte (Deckel-System, eingebaute Lage) ----------
module switch_plate() {
    difference() {
        union() {
            // Platte
            translate([0, 0, plate_bot]) linear_extrude(plate_t) keys_outline(well_out);
            // Muldenwand bis zur Deckel-Unterseite (oberer Teil der Mulde ist der Deckel selbst)
            translate([0, 0, plate_bot]) linear_extrude(lid_bot - plate_bot) difference() {
                keys_outline(well_out);
                keys_outline(well_gap);
            }
            // Laschen unter dem Deckel, mit Stuetzschraege nach unten (druckbar ohne Stuetzen)
            for (t = tabs) hull() {
                tab_box(t, lid_bot - tab_t, tab_t, tab_len);
                tab_box(t, plate_bot, 0.01, 0.5);
            }
        }
        // Switch-Ausschnitte + Freistich fuer die Clips
        for (k = keys) {
            translate([k[0] - sw_hole / 2, k[1] - sw_hole / 2, plate_bot - 1]) cube([sw_hole, sw_hole, plate_t + 2]);
            translate([k[0] - sw_pocket / 2, k[1] - sw_pocket / 2, plate_bot - 1]) cube([sw_pocket, sw_pocket, plate_t - sw_plate + 1]);
        }
        // Schraubloecher in den Laschen, Kopfkanal durch die Stuetzschraege
        for (t = tabs) translate(tab_hole(t)) {
            translate([0, 0, lid_bot - tab_t - 1]) cylinder(d = tab_screw_d, h = tab_t + 2);
            translate([0, 0, plate_bot - 1]) cylinder(d = tab_head_d, h = lid_bot - tab_t - plate_bot + 1);
        }
    }
}

// Quader einer Lasche: beginnt 0.5 mm in der Muldenwand, ragt len nach aussen
module tab_box(t, z, h, len) {
    p = t[0]; d = t[1];
    a = atan2(d[1], d[0]);
    translate([p[0], p[1], z]) rotate([0, 0, a]) translate([-0.5, -tab_w / 2, 0]) cube([len + 0.5, tab_w, h]);
}

// ---------- OLED-Rahmen (Deckel-System, eingebaute Lage) ----------
// Display-System: Ursprung an der Vorderkante der schraegen Flaeche, y entlang der
// Flaeche nach oben, z = Normale nach aussen. Die Flaeche steht oled_angle zum Tisch.
module oled_face() {
    translate([0, -oled_frame_size[1] / 2, oled_frame_proud]) rotate([oled_angle - tilt, 0, 0]) children();
}

// keilfoermiges Display-Gehaeuse (Deckel-System, eingebaute Lage): Zapfen in der
// Deckeltasche, darueber schraege Display-Flaeche; Display steckt von unten im Gehaeuse
module oled_frame() {
    S = oled_frame_size; w = oled_wall; Lf = oled_face_len;
    translate([oled_frame_c[0], oled_frame_c[1], 0]) difference() {
        union() {
            difference() {
                union() {
                    intersection() {
                        hull() {
                            translate([0, 0, -0.01]) linear_extrude(oled_frame_proud + 0.01) rr2d_c(S, 1.5);
                            oled_face() translate([-S[0] / 2, 0, -0.01]) cube([S[0], Lf, 0.01]);
                            oled_face() translate([-S[0] / 2, 0, -oled_back]) cube([S[0], Lf, 0.01]);
                        }
                        linear_extrude(100) rr2d_c(S, 1.5);
                    }
                    translate([0, 0, oled_frame_bot]) linear_extrude(-oled_frame_bot + 0.01) rr2d_c(S, 1.5);
                }
                // Innenraum, unten offen
                hull() {
                    translate([0, 0, oled_frame_bot - 1]) linear_extrude(0.01) square(S - [2 * w, 2 * w], center = true);
                    oled_face() translate([-(S[0] / 2 - w), w, -oled_face_t]) cube([S[0] - 2 * w, Lf - 2 * w, 0.01]);
                    oled_face() translate([-(S[0] / 2 - w), w, -(oled_back - w)]) cube([S[0] - 2 * w, Lf - 2 * w, 0.01]);
                }
            }
            // Eckbloecke fuer die Rahmenschrauben
            intersection() {
                for (h = oled_frame_screws)
                    translate([h[0] - 2.2, h[1] - 2.2, oled_frame_bot]) cube([4.4, 4.4, -oled_frame_bot]);
                translate([0, 0, oled_frame_bot]) linear_extrude(-oled_frame_bot) rr2d_c(S, 1.5);
            }
            // Block fuer Nut und Haltelippe am oberen Platinenrand
            oled_face() translate([-(S[0] / 2 - w) - 0.01, Lf / 2 + oled_pcb[1] / 2 - oled_lip_len, -oled_face_t - oled_pcb_band - oled_lip_t])
                cube([S[0] - 2 * w + 0.02, Lf / 2 - oled_pcb[1] / 2 + oled_lip_len, oled_face_t + oled_pcb_band + oled_lip_t]);
        }

        // Display-Halterung in der schraegen Flaeche
        oled_face() translate([0, Lf / 2, 0]) {
            // Fenster mit Fase nach aussen (Display kopfueber -> Versatz gespiegelt)
            translate([0, -oled_window_offset]) {
                translate([0, 0, -oled_recess - 0.01]) linear_extrude(oled_recess + 1)
                    offset(r = 0.5) offset(delta = -0.5) square(oled_window, center = true);
                hull() {
                    translate([0, 0, -1.2]) linear_extrude(0.01)
                        offset(r = 0.5) offset(delta = -0.5) square(oled_window, center = true);
                    linear_extrude(0.01) offset(r = 1.7) offset(delta = -0.5) square(oled_window, center = true);
                }
            }
            g = [oled_glass[0], oled_glass[1]] + [oled_pcb_fit, oled_pcb_fit];
            p = oled_pcb + [oled_pcb_fit, oled_pcb_fit];
            // Glas (nach unten um den Schiebeweg verlaengert)
            translate([-g[0] / 2, -g[1] / 2 - oled_slide, -oled_face_t - 0.01])
                cube([g[0], g[1] + oled_slide, oled_glass[2] + 0.01]);
            // Platine: Nut bis unter die Lippe
            translate([-p[0] / 2, -p[1] / 2 - oled_slide, -oled_face_t - oled_pcb_band])
                cube([p[0], p[1] + oled_slide + oled_groove, oled_pcb_band + 0.01]);
            // Bauteile auf der Rueckseite (Lippe bleibt stehen)
            translate([-p[0] / 2, -p[1] / 2 - oled_slide, -oled_face_t - 1.6 - oled_comp_h])
                cube([p[0], p[1] + oled_slide - oled_lip_len, 1.6 + oled_comp_h - oled_pcb_band + 0.01]);
            // Loetstellen der Anschluesse (jetzt unten) auf der Vorderseite
            translate([-oled_header[0] / 2, -oled_pcb[1] / 2 - oled_slide, -oled_face_t - 0.01])
                cube([oled_header[0], 1 + oled_header[1] + oled_slide, oled_header[2]]);
            // Kernloecher fuer die beiden unteren OLED-Schrauben (von innen durch die Platine)
            for (h = oled_holes) if (h[1] < 0)
                translate([h[0], h[1], -oled_face_t - 0.01]) cylinder(d = oled_pilot_d, h = 2.5);
        }
        // Kernloecher fuer die Rahmenschrauben (von unten durch die Auflage im Deckel)
        for (h = oled_frame_screws)
            translate([h[0], h[1], oled_frame_bot - 0.01]) cylinder(d = oled_pilot_d, h = 3);
    }
}

// ---------- Unterschale ----------
module esp_cradle() {
    support_w = 16;   // am USB-Ende nur zwischen den Stiftleisten abstuetzen
    tip_x0 = esp_x1 - esp_tip[0];
    // Auflage am USB-Ende
    translate([esp_x0 + 0.5, esp_cy - support_w / 2, 0]) cube([3, support_w, esp_zb]);
    // Auflage unter dem schmalen Ende + Anschlag
    translate([tip_x0 + 0.5, esp_cy - esp_tip[1] / 2 + 1, 0]) cube([esp_tip[0] - 0.5, esp_tip[1] - 2, esp_zb]);
    translate([esp_x1 + fit, esp_cy - esp_tip[1] / 2 + 1, 0]) cube([2.3 - fit, esp_tip[1] - 2, esp_zb + 3]);
    // seitliche Fuehrungen: am USB-Ende (volle Breite) und am schmalen Ende
    for (sy = [-1, 1]) {
        translate([esp_x0 + 4, esp_cy + sy * (esp_w / 2 + fit) + (sy < 0 ? -1.6 : 0), 0])
            cube([6, 1.6, esp_zb + esp_pcb_t + 2]);
        translate([tip_x0 + 0.5, esp_cy + sy * (esp_tip[1] / 2 + fit) + (sy < 0 ? -1.6 : 0), 0])
            cube([esp_tip[0] - 0.5, 1.6, esp_zb + esp_pcb_t + 2]);
    }
}

// Freiraum der Bauteile, der von den Saeulen frei bleiben muss
module component_clearance() {
    tilted() for (i = [0 : slider_n - 1]) {
        s = slider_c(i);
        translate([s[0] - slider_pcb[0] / 2 - 0.5, s[1] - slider_pcb[1] / 2 - 0.5, lid_bot - slider_pot_top - 1.6 - 3])
            cube([slider_pcb[0] + 1, slider_pcb[1] + 1, slider_pot_top + 1.6 + 3]);
    }
    tilted() translate([0, 0, plate_bot - 6]) linear_extrude(-plate_bot + 6) keys_outline(well_out + 0.5);
}

module pillars() {
    difference() {
        for (p = boss_pos) intersection() {
            translate([p[0], p[1], 0]) cylinder(d = pillar_d, h = 300);
            tilted() translate([-50, -50, pillar_top_zt - 300]) cube([W + 100, D + 100, 300]);
        }
        component_clearance();
    }
}

module tray() {
    difference() {
        union() {
            difference() {
                body(0, 0, 0);
                body(wall, floor_t, 1);
            }
            intersection() {
                union() {
                    pillars();
                    esp_cradle();
                }
                body(0.01, 0, 0);
            }
        }

        // Schraubkanaele von unten
        for (p = boss_pos) {
            translate([p[0], p[1], -1]) cylinder(d = screw_head_d, h = screw_seat(p) + 1);
            translate([p[0], p[1], screw_seat(p) - 0.01]) cylinder(d = screw_d, h = 300);
        }

        // Rasttaschen fuer die Schnapphaken
        for (h = hooks) hook_recess(h);

        // USB-Ausschnitt in der linken Seitenwand
        translate([esp_x0 + 2, esp_cy, usb_z]) rotate([0, -90, 0])
            linear_extrude(40) slot2d(usb_slot[0], usb_slot[1]);

        // Gummifuesse
        for (f = feet_pos) translate([f[0], f[1], -0.01]) cylinder(d = feet_d, h = feet_depth);
    }
}

// ---------- Platzhalter fuer die Vorschau ----------
module dummies() {
    tilted() {
        for (k = keys) {
            // Keycap-Unterkante liegt in Ruhe etwa auf Hoehe der Deckel-Oberflaeche
            color("#d9d4c7") translate([k[0], k[1], 0.1])
                hull() {
                    linear_extrude(0.1) offset(1) square(16, center = true);
                    translate([0, 0, 7.5]) linear_extrude(0.1) offset(1.5) square(11, center = true);
                }
            color("#3a3a3a") translate([k[0] - 7.8, k[1] - 7.8, -well_depth]) cube([15.6, 15.6, 6.6]);
        }
        for (i = [0 : slider_n - 1]) {
            s = slider_c(i);
            color("#2b2b2b") translate([s[0] + slider_pot_offset - 1, s[1] + slider_slot_offset - 2, 0]) cube([2, 4, 8]);
            color("#e0662b") translate([s[0] + slider_pot_offset - 6, s[1] + slider_slot_offset - 9, 8]) cube([12, 18, 10]);
            color("#2f6f5e", 0.6) translate([s[0] - slider_pcb[0] / 2, s[1] - slider_pcb[1] / 2, lid_bot - slider_pot_top - 1.6])
                cube([slider_pcb[0], slider_pcb[1], 1.6]);
        }
        color("#101820") translate([oled_frame_c[0], oled_frame_c[1], 0]) oled_face()
            translate([-oled_window[0] / 2, oled_face_len / 2 - oled_window_offset - oled_window[1] / 2, -oled_recess - 0.5])
                cube([oled_window[0], oled_window[1], 0.5]);
    }
}

module esp_dummy() {
    color("#1d3b8a", 0.8) translate([esp_x0, esp_y0, esp_zb]) {
        cube([esp_l - esp_tip[0], esp_w, esp_pcb_t]);
        translate([esp_l - esp_tip[0], (esp_w - esp_tip[1]) / 2, 0]) cube([esp_tip[0], esp_tip[1], esp_pcb_t]);
    }
}

// ---------- Ausgabe ----------
if (part == "lid") {
    // flach legen, Sichtseite nach unten
    rotate([180, 0, 0]) rotate([-tilt, 0, 0]) translate([0, 0, -front_h]) lid();
} else if (part == "switch_plate") {
    // Platte auf dem Druckbett
    translate([0, 0, -plate_bot]) switch_plate();
} else if (part == "tray") {
    tray();
} else if (part == "oled_frame") {
    // aufrecht drucken (Zapfen auf dem Druckbett)
    translate([-oled_frame_c[0], -oled_frame_c[1], -oled_frame_bot]) oled_frame();
} else if (part != "none") {
    dz = part == "exploded" ? 45 : 0;
    color("#5f7f7a") tray();
    if (show_dummies) esp_dummy();
    translate([0, 0, dz]) {
        color("#8fb3ad") lid();
        color("#7c9c97") tilted() switch_plate();
        color("#c9ccd1") tilted() oled_frame();
        if (show_dummies) dummies();
    }
}
