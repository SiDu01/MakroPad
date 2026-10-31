// MakroPad Keycap – parametrisch (OpenSCAD)
//
// Blanko-Keycap fuer MX-kompatible Switches, 1u, flaches Profil mit leichter
// zylindrischer Mulde (alle Tasten gleich; die Neigung kommt vom Gehaeuse).
// Masse passen zum Platzhalter in makropad_case.scad (Unterseite 18 mm, Oberseite 14 mm).
//
// Export:
//   openscad -D 'printer="fdm"' -o makropad_keycap_fdm.stl makropad_keycap.scad
//   openscad -D 'printer="sla"' -o makropad_keycap_sla.stl makropad_keycap.scad
//
// Druck FDM: aufrecht, offene Unterseite auf dem Bett, keine Stuetzen.
//   Der Stempel steht bis auf das Bett, die Decke ueberbrueckt ca. 15 mm.
// Druck SLA: im Slicer 30-45 Grad kippen, Oberseite zur Plattform, Stuetzen nur an
//   Kante und Innenwand (nicht auf die Mulde). Kreuz vor dem Nachhaerten freimachen.

/* [Drucker] */
printer = "fdm"; // [fdm, sla]

/* [Kappe] */
base = 18;         // Kantenlaenge unten
top = 14;          // Kantenlaenge oben
height = 8;        // Gesamthoehe am Rand
corner_r = 1.5;    // Eckenradius
wall = 1.5;        // Wandstaerke seitlich
ceiling = 1.5;     // Deckenstaerke an der tiefsten Stelle der Mulde
dish_depth = 0.8;  // Tiefe der Mulde (zylindrisch)

/* [MX-Stempel] */
stem_d = 5.5;      // Aussendurchmesser
// Kreuzarme [Laenge, Breite], MX: 4.0 x 1.17 + Spiel (NACHMESSEN am Drucker)
cross_fdm = [4.1, 1.35];
cross_sla = [4.05, 1.25];  // Resin ist genauer und sproeder: weniger Spiel, eher locker als stramm
cross_depth = 4;   // Tiefe des Kreuzes (MX-Stempel ragt ca. 3.6 mm in die Kappe)

$fn = 64;

cross = printer == "sla" ? cross_sla : cross_fdm;
cross_l = cross[0];
cross_w = cross[1];
dish_r =(pow(top / 2, 2) + pow(dish_depth, 2)) / (2 * dish_depth);
inner_top = height - dish_depth - ceiling;

module rsq(s, h) {
    linear_extrude(h) offset(corner_r) square(s - 2 * corner_r, center = true);
}

module shell(b, t, h) {
    hull() {
        rsq(b, 0.01);
        translate([0, 0, h - 0.01]) rsq(t, 0.01);
    }
}

module keycap() {
    difference() {
        union() {
            difference() {
                shell(base, top, height);
                // Hohlraum, Wand folgt der Schraege
                translate([0, 0, -0.01]) shell(base - 2 * wall, top - 2 * wall, inner_top + 0.01);
                // Mulde: Zylinderachse vorne -> hinten, Kappe ist links/rechts gewoelbt
                translate([0, 0, height + dish_r - dish_depth]) rotate([90, 0, 0])
                    // feine Facetten (~0.2 mm), SLA bildet groebere sichtbar ab
                    cylinder(r = dish_r, h = base + 2, center = true, $fn = 1024);
            }
            cylinder(d = stem_d, h = inner_top + 0.01);
        }
        translate([0, 0, -0.01]) {
            translate([-cross_l / 2, -cross_w / 2, 0]) cube([cross_l, cross_w, cross_depth]);
            translate([-cross_w / 2, -cross_l / 2, 0]) cube([cross_w, cross_l, cross_depth]);
        }
    }
}

keycap();
