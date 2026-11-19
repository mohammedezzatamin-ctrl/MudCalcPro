// Mud Calculator Pro - calculation library
// All formulas work in internal oilfield units: ppg, bbl, lb, ft, in, psi, gpm, lb/bbl, degF.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MudCalcPro
{
    public class Field
    {
        public string Key, Label; public UK Kind; public double Def; public string[] Choices;
        public Field(string key, string label, UK kind, double def) { Key = key; Label = label; Kind = kind; Def = def; }
        public static Field Ch(string key, string label, int def, params string[] choices)
        {
            var f = new Field(key, label, UK.None, def); f.Choices = choices; return f;
        }
    }

    public class CalcError : Exception { public CalcError(string m) : base(m) { } }

    public class Res
    {
        public StringBuilder SB = new StringBuilder();
        public void Title(string s) { SB.AppendLine(s.ToUpperInvariant()); SB.AppendLine(new string('-', Math.Min(60, s.Length))); }
        public void Line(string s) { SB.AppendLine(s); }
        public void Blank() { SB.AppendLine(); }
        public void Q(string label, double v, UK k) { SB.AppendLine(Pad(label) + Units.Show(v, k)); }
        public void N(string label, double v, string unit) { SB.AppendLine(Pad(label) + Units.Fmt(v) + (unit.Length > 0 ? " " + unit : "")); }
        public void T(string label, string text) { SB.AppendLine(Pad(label) + text); }
        public void Warn(string s) { SB.AppendLine("!! " + s); }
        public static string Pad(string s) { return (s + " ").PadRight(36, '.') + " "; }

        // weight of a product with tonnes and package count
        public void Mass(string label, double lb, Product p)
        {
            Q(label, lb, UK.Mass);
            if (Units.Name(UK.Mass) != "MT") N("   = tonnes", lb / 2204.62, "MT");
            if (p != null && p.PackageLb() > 0 && lb > 0)
                T("   = packages @ " + p.PackageText(), Math.Ceiling(lb / p.PackageLb() - 1e-9).ToString("#,##0"));
        }
        public void Barite(string label, double lb) { Mass(label, lb, Data.WeightMaterial()); }
    }

    public delegate void CalcFn(Dictionary<string, double> v, Res r);

    public class Calc
    {
        public string Group, Name, Help; public Field[] Fields; public CalcFn Fn;
        public Calc(string group, string name, string help, Field[] fields, CalcFn fn) { Group = group; Name = name; Help = help; Fields = fields; Fn = fn; }
    }

    public static class Calcs
    {
        public const double WD = 8.345;           // fresh water, ppg
        public const double LbPerBblPerSG = 42 * WD; // 350.5 lb/bbl at SG 1
        static void Need(bool ok, string msg) { if (!ok) throw new CalcError(msg); }

        public static List<Calc> All = new List<Calc>();
        static void Add(string g, string n, string h, Field[] f, CalcFn fn) { All.Add(new Calc(g, n, h, f, fn)); }
        static Field F(string k, string l, UK u, double d) { return new Field(k, l, u, d); }

        public const string G1 = "Weight up & dilution";
        public const string G2 = "Oil mud adjustments";
        public const string G3 = "Basic mixes & slugs";
        public const string G4 = "Volumes & hydraulics";
        public const string G5 = "Pressure & well control";
        public const string G6 = "Brines & salt";
        public const string G7 = "Mud testing & solids";

        static double OilWaterFromOWR(double oilPct, double owr, out double waterPct)
        {
            Need(owr > 0 && owr < 100, "OWR (oil %) must be between 0 and 100.");
            Need(oilPct > 0 && oilPct < 100, "Oil % (retort) must be between 0 and 100.");
            waterPct = oilPct * (100 - owr) / owr;
            Need(oilPct + waterPct < 100, "Oil % and OWR give more than 100% liquid - check the inputs.");
            return oilPct;
        }

        static Calcs()
        {
            // =========================================================== 1. WEIGHT UP & DILUTION
            Add(G1, "Weight up (volume increase)",
                "Weight material needed to raise the mud density of the whole system. Mud volume increases by the volume of weight material.",
                new[] { F("w1", "Initial mud density", UK.Density, 10), F("w2", "Desired mud density", UK.Density, 12),
                        F("v", "Starting mud volume", UK.Volume, 500), F("sg", "SG of weight material", UK.SG, 4.2) },
                (v, r) =>
                {
                    double rb = WD * v["sg"];
                    Need(v["w2"] > v["w1"], "Desired density must be higher than initial density.");
                    Need(rb > v["w2"], "Weight material SG is too low for the desired density.");
                    double vb = v["v"] * (v["w2"] - v["w1"]) / (rb - v["w2"]);
                    double lb = vb * 42 * rb;
                    r.Barite("Weight material required", lb);
                    r.Q("   per unit volume of starting mud", lb / v["v"], UK.Conc);
                    r.Q("Volume increase", vb, UK.Volume);
                    r.Q("Final mud volume", v["v"] + vb, UK.Volume);
                });

            Add(G1, "Weight up (constant volume)",
                "Dump part of the mud first, then weight up so the final volume equals the starting volume.",
                new[] { F("w1", "Initial mud density", UK.Density, 10), F("w2", "Desired mud density", UK.Density, 12),
                        F("v", "Mud volume (to keep)", UK.Volume, 500), F("sg", "SG of weight material", UK.SG, 4.2) },
                (v, r) =>
                {
                    double rb = WD * v["sg"];
                    Need(v["w2"] > v["w1"], "Desired density must be higher than initial density.");
                    Need(rb > v["w2"], "Weight material SG is too low for the desired density.");
                    double keep = v["v"] * (rb - v["w2"]) / (rb - v["w1"]);
                    double dump = v["v"] - keep;
                    r.Q("Mud to dump / jettison first", dump, UK.Volume);
                    r.Q("Mud kept", keep, UK.Volume);
                    r.Barite("Weight material to add", dump * 42 * rb);
                    r.Q("Final mud volume", v["v"], UK.Volume);
                });

            Add(G1, "Cut mud weight (volume increase)",
                "Volume of dilution fluid (water, base oil or light mud) needed to reduce the mud density.",
                new[] { F("w1", "Initial mud density", UK.Density, 12), F("w2", "Desired mud density", UK.Density, 11),
                        F("v", "Starting mud volume", UK.Volume, 500), F("wd", "Density of dilution fluid", UK.Density, WD) },
                (v, r) =>
                {
                    Need(v["w2"] < v["w1"], "Desired density must be lower than initial density.");
                    Need(v["w2"] > v["wd"], "Dilution fluid must be lighter than the desired density.");
                    double vd = v["v"] * (v["w1"] - v["w2"]) / (v["w2"] - v["wd"]);
                    r.Q("Dilution fluid to add", vd, UK.Volume);
                    r.Q("Final mud volume", v["v"] + vd, UK.Volume);
                });

            Add(G1, "Cut mud weight (constant volume)",
                "Dump part of the mud, then add dilution fluid back to the same volume.",
                new[] { F("w1", "Initial mud density", UK.Density, 12), F("w2", "Desired mud density", UK.Density, 11),
                        F("v", "Mud volume", UK.Volume, 500), F("wd", "Density of dilution fluid", UK.Density, WD) },
                (v, r) =>
                {
                    Need(v["w2"] < v["w1"], "Desired density must be lower than initial density.");
                    Need(v["w2"] > v["wd"], "Dilution fluid must be lighter than the desired density.");
                    double keep = v["v"] * (v["w2"] - v["wd"]) / (v["w1"] - v["wd"]);
                    r.Q("Mud to dump first", v["v"] - keep, UK.Volume);
                    r.Q("Dilution fluid to add", v["v"] - keep, UK.Volume);
                    r.Q("Mud kept", keep, UK.Volume);
                });

            Add(G1, "Mix two or three muds",
                "Density and volume of a blend. Leave the third fluid at zero volume to mix only two.",
                new[] { F("d1", "Density - fluid 1", UK.Density, 12), F("v1", "Volume - fluid 1", UK.Volume, 300),
                        F("d2", "Density - fluid 2", UK.Density, 10), F("v2", "Volume - fluid 2", UK.Volume, 200),
                        F("d3", "Density - fluid 3", UK.Density, 0), F("v3", "Volume - fluid 3", UK.Volume, 0) },
                (v, r) =>
                {
                    double vt = v["v1"] + v["v2"] + v["v3"];
                    Need(vt > 0, "Enter at least one volume.");
                    double w = (v["d1"] * v["v1"] + v["d2"] * v["v2"] + v["d3"] * v["v3"]) / vt;
                    r.Q("Density of mixture", w, UK.Density);
                    r.Q("Volume of mixture", vt, UK.Volume);
                });

            Add(G1, "Add a known weight of barite",
                "New density and volume after adding a known weight of weight material.",
                new[] { F("w1", "Initial mud density", UK.Density, 10), F("v", "Initial mud volume", UK.Volume, 500),
                        F("m", "Weight of barite added", UK.Mass, 22046.2), F("sg", "SG of weight material", UK.SG, 4.2) },
                (v, r) =>
                {
                    double rb = WD * v["sg"];
                    double vb = v["m"] / (42 * rb);
                    double w = (v["v"] * 42 * v["w1"] + v["m"]) / (42 * (v["v"] + vb));
                    r.Q("New mud density", w, UK.Density);
                    r.Q("Volume increase", vb, UK.Volume);
                    r.Q("Final mud volume", v["v"] + vb, UK.Volume);
                });

            Add(G1, "Barite to raise density of a pill / system (per unit volume)",
                "Quick per-volume figures for weighting up (no volume entered).",
                new[] { F("w1", "Initial mud density", UK.Density, 10), F("w2", "Desired mud density", UK.Density, 12), F("sg", "SG of weight material", UK.SG, 4.2) },
                (v, r) =>
                {
                    double rb = WD * v["sg"];
                    Need(v["w2"] > v["w1"] && rb > v["w2"], "Check densities.");
                    double per = 42 * rb * (v["w2"] - v["w1"]) / (rb - v["w2"]);
                    r.Q("Weight material per unit of starting mud", per, UK.Conc);
                    r.N("Volume increase per bbl of mud", (v["w2"] - v["w1"]) / (rb - v["w2"]), "bbl/bbl");
                    r.N("Sacks (100 lb) per 100 bbl", per, "sx");
                });

            // =========================================================== 2. OIL MUD
            Add(G2, "Adjust OWR (add oil or water)",
                "Raises or lowers the oil/water ratio. Uses retort oil % to find the oil and water fractions. Optionally restores the mud weight with barite.",
                new[] { F("v", "Mud volume", UK.Volume, 500), F("w", "Mud density", UK.Density, 12),
                        F("owr1", "Current OWR (oil %)", UK.Percent, 70), F("oil", "Retort oil % (by volume)", UK.Percent, 56),
                        F("owr2", "Desired OWR (oil %)", UK.Percent, 80), F("sgo", "SG of base oil", UK.SG, 0.80),
                        F("sgw", "SG of water / brine added", UK.SG, 1.0), F("sg", "SG of weight material", UK.SG, 4.2),
                        Field.Ch("keep", "Restore mud weight with barite?", 0, "Yes", "No") },
                (v, r) =>
                {
                    double water; double fo = OilWaterFromOWR(v["oil"], v["owr1"], out water) / 100; double fw = water / 100;
                    double owr2 = v["owr2"]; Need(owr2 > 0 && owr2 < 100, "Desired OWR must be between 0 and 100.");
                    double V = v["v"], W = v["w"], rb = WD * v["sg"];
                    double vf, rf; string what;
                    if (owr2 >= v["owr1"]) { vf = V * (fw * owr2 / (100 - owr2) - fo); rf = WD * v["sgo"]; what = "Base oil to add"; }
                    else { vf = V * (fo * (100 - owr2) / owr2 - fw); rf = WD * v["sgw"]; what = "Water / brine to add"; }
                    r.T("Retort water % (calculated)", Units.Fmt(water) + " %");
                    r.Q(what, vf, UK.Volume);
                    double wn = (V * W + vf * rf) / (V + vf);
                    r.Q("Mud density after fluid addition", wn, UK.Density);
                    double v2 = V + vf, bar = 0;
                    if (v["keep"] == 0 && W > wn)
                    {
                        double vb = v2 * (W - wn) / (rb - W);
                        bar = vb;
                        r.Barite("Barite to restore mud weight", vb * 42 * rb);
                        r.Q("Volume increase from barite", vb, UK.Volume);
                    }
                    double vt = v2 + bar;
                    double oilV = fo * V + (owr2 >= v["owr1"] ? vf : 0), watV = fw * V + (owr2 >= v["owr1"] ? 0 : vf);
                    r.Q("Final mud volume", vt, UK.Volume);
                    r.T("New OWR", Units.Fmt(100 * oilV / (oilV + watV)) + " / " + Units.Fmt(100 * watV / (oilV + watV)));
                    r.T("New oil % / water % of mud", Units.Fmt(100 * oilV / vt) + " % / " + Units.Fmt(100 * watV / vt) + " %");
                });

            Add(G2, "Increase water-phase salinity (WPS)",
                "Salt needed to raise the water phase salinity of an oil mud. WPS in ppm = weight % x 10,000.",
                new[] { F("v", "Mud volume", UK.Volume, 500), F("wat", "Retort water % (by volume)", UK.Percent, 20),
                        Field.Ch("salt", "Salt type", 0, "CaCl2", "NaCl", "KCl"),
                        F("p1", "Current WPS", UK.Ppm, 200000), F("p2", "Desired WPS", UK.Ppm, 250000), F("pur", "Salt purity", UK.Percent, 95) },
                (v, r) =>
                {
                    Salt s = Data.GetSalt(new[] { "CaCl2", "NaCl", "KCl" }[(int)v["salt"]]);
                    Need(s != null, "Salt data missing (salts.csv).");
                    double w1 = v["p1"] / 1e6, w2 = v["p2"] / 1e6;
                    Need(w2 > w1, "Desired WPS must be higher than current WPS.");
                    Need(w2 * 100 <= s.MaxWt, "Desired WPS exceeds saturation for " + s.Name + " (" + Units.Fmt(s.MaxWt * 1e4) + " ppm).");
                    Need(v["pur"] > 0 && v["pur"] <= 100, "Purity must be 1-100 %.");
                    double waterLb = v["v"] * v["wat"] / 100 * 42 * WD;
                    double salt = waterLb * (w2 / (1 - w2) - w1 / (1 - w1));
                    double brine1 = waterLb / (1 - w1) / (42 * WD * s.SG(w1 * 100));
                    double brine2 = waterLb / (1 - w2) / (42 * WD * s.SG(w2 * 100));
                    r.Mass("Salt to add (as received)", salt / (v["pur"] / 100), Data.SaltProduct(s.Name));
                    r.Q("Volume increase (brine)", brine2 - brine1, UK.Volume);
                    r.T("Brine SG: current -> new", Units.Fmt(s.SG(w1 * 100)) + " -> " + Units.Fmt(s.SG(w2 * 100)));
                    double t = s.TCT(w2 * 100);
                    if (!double.IsNaN(t)) r.Q("Approx. crystallization point (new)", t, UK.Temp);
                });

            Add(G2, "Add a known volume of water to oil mud",
                "New density, OWR and oil % after a known water (or brine) addition.",
                new[] { F("v", "Initial mud volume", UK.Volume, 500), F("w", "Initial mud density", UK.Density, 12),
                        F("owr", "Initial OWR (oil %)", UK.Percent, 80), F("oil", "Initial retort oil %", UK.Percent, 60),
                        F("vw", "Water volume added", UK.Volume, 20), F("sgw", "SG of water / brine", UK.SG, 1.0) },
                (v, r) =>
                {
                    double water; double oil = OilWaterFromOWR(v["oil"], v["owr"], out water);
                    double V = v["v"], vw = v["vw"];
                    double wn = (V * v["w"] + vw * WD * v["sgw"]) / (V + vw);
                    double oV = oil / 100 * V, wV = water / 100 * V + vw;
                    r.Q("New mud density", wn, UK.Density);
                    r.Q("New mud volume", V + vw, UK.Volume);
                    r.T("Resultant OWR", Units.Fmt(100 * oV / (oV + wV)) + " / " + Units.Fmt(100 * wV / (oV + wV)));
                    r.T("Resultant oil % / water %", Units.Fmt(100 * oV / (V + vw)) + " % / " + Units.Fmt(100 * wV / (V + vw)) + " %");
                });

            Add(G2, "Add a known volume of base oil to oil mud",
                "New density, OWR and oil % after a known base oil addition.",
                new[] { F("v", "Initial mud volume", UK.Volume, 500), F("w", "Initial mud density", UK.Density, 12),
                        F("owr", "Initial OWR (oil %)", UK.Percent, 75), F("oil", "Initial retort oil %", UK.Percent, 55),
                        F("vo", "Base oil volume added", UK.Volume, 30), F("sgo", "SG of base oil", UK.SG, 0.80) },
                (v, r) =>
                {
                    double water; double oil = OilWaterFromOWR(v["oil"], v["owr"], out water);
                    double V = v["v"], vo = v["vo"];
                    double wn = (V * v["w"] + vo * WD * v["sgo"]) / (V + vo);
                    double oV = oil / 100 * V + vo, wV = water / 100 * V;
                    r.Q("New mud density", wn, UK.Density);
                    r.Q("New mud volume", V + vo, UK.Volume);
                    r.T("Resultant OWR", Units.Fmt(100 * oV / (oV + wV)) + " / " + Units.Fmt(100 * wV / (oV + wV)));
                    r.T("Resultant oil % / water %", Units.Fmt(100 * oV / (V + vo)) + " % / " + Units.Fmt(100 * wV / (V + vo)) + " %");
                });

            Add(G2, "Suspected water contamination of oil mud",
                "Estimates the water influx from the drop in mud density (assumes the density drop is caused only by water).",
                new[] { F("w1", "Initial mud density", UK.Density, 12), F("w2", "Contaminated mud density", UK.Density, 11.8),
                        F("v", "Initial mud volume", UK.Volume, 500), F("owr", "Initial OWR (oil %)", UK.Percent, 80),
                        F("oil", "Initial retort oil %", UK.Percent, 60), F("sgw", "SG of contaminating water", UK.SG, 1.0) },
                (v, r) =>
                {
                    double water; double oil = OilWaterFromOWR(v["oil"], v["owr"], out water);
                    double rw = WD * v["sgw"];
                    Need(v["w2"] < v["w1"] && v["w2"] > rw, "Contaminated density must be between water and initial density.");
                    double vw = v["v"] * (v["w1"] - v["w2"]) / (v["w2"] - rw);
                    double oV = oil / 100 * v["v"], wV = water / 100 * v["v"] + vw;
                    r.Q("Volume of water contamination", vw, UK.Volume);
                    r.Q("Mud volume now", v["v"] + vw, UK.Volume);
                    r.T("Resultant OWR", Units.Fmt(100 * oV / (oV + wV)) + " / " + Units.Fmt(100 * wV / (oV + wV)));
                });

            // =========================================================== 3. BASIC MIXES & SLUGS
            Add(G3, "Basic mix - water-based mud",
                "Water and weight material needed to build a simple weighted water mud (no other products).",
                new[] { F("v", "Volume of mud", UK.Volume, 500), F("w", "Mud density", UK.Density, 11),
                        F("sgw", "SG of base water", UK.SG, 1.0), F("sg", "SG of weight material", UK.SG, 4.2) },
                (v, r) =>
                {
                    double rw = WD * v["sgw"], rb = WD * v["sg"];
                    Need(v["w"] >= rw && v["w"] < rb, "Mud density must be between water and weight material.");
                    double vb = v["v"] * (v["w"] - rw) / (rb - rw);
                    r.Q("Volume of water", v["v"] - vb, UK.Volume);
                    r.Q("Volume of weight material", vb, UK.Volume);
                    r.Barite("Weight of weight material", vb * 42 * rb);
                });

            Add(G3, "Basic mix - oil-based mud",
                "Base oil, water/brine and weight material for a simple oil mud at a set OWR (no other products).",
                new[] { F("v", "Volume of mud", UK.Volume, 500), F("w", "Mud density", UK.Density, 12),
                        F("owr", "OWR (oil %)", UK.Percent, 80), F("sgo", "SG of base oil", UK.SG, 0.80),
                        F("sgw", "SG of water / brine", UK.SG, 1.0), F("sg", "SG of weight material", UK.SG, 4.2) },
                (v, r) =>
                {
                    double fo = v["owr"] / 100; Need(fo > 0 && fo <= 1, "OWR must be 1-100.");
                    double rl = fo * WD * v["sgo"] + (1 - fo) * WD * v["sgw"], rb = WD * v["sg"];
                    Need(v["w"] >= rl && v["w"] < rb, "Mud density is below the liquid density (" + Units.Show(rl, UK.Density) + ") or above weight material.");
                    double vb = v["v"] * (v["w"] - rl) / (rb - rl), vl = v["v"] - vb;
                    r.Q("Volume of base oil", vl * fo, UK.Volume);
                    r.Q("Volume of water / brine", vl * (1 - fo), UK.Volume);
                    r.Q("Volume of weight material", vb, UK.Volume);
                    r.Barite("Weight of weight material", vb * 42 * rb);
                });

            Add(G3, "Slug - flowback (pit gain) after pumping",
                "Estimated volume of mud that flows back from the drill pipe after a heavy slug is pumped.",
                new[] { F("w", "Mud density", UK.Density, 10), F("ws", "Slug density", UK.Density, 12), F("vs", "Slug volume", UK.Volume, 25) },
                (v, r) =>
                {
                    Need(v["ws"] > v["w"] && v["w"] > 0, "Slug must be heavier than the mud.");
                    r.Q("Flowback volume of mud", v["vs"] * (v["ws"] - v["w"]) / v["w"], UK.Volume);
                });

            Add(G3, "Slug size (volume for a dry-pipe length)",
                "Slug volume needed to give the required length of dry pipe.",
                new[] { F("w", "Mud density", UK.Density, 10), F("ws", "Slug density", UK.Density, 12),
                        F("l", "Length of dry pipe required", UK.Depth, 300), F("id", "Drill pipe ID", UK.Diameter, 4.276) },
                (v, r) =>
                {
                    Need(v["ws"] > v["w"], "Slug must be heavier than the mud.");
                    double c = v["id"] * v["id"] / 1029.4;
                    double vs = c * v["l"] * v["w"] / (v["ws"] - v["w"]);
                    r.Q("Drill pipe capacity", c, UK.Capacity);
                    r.Q("Slug volume", vs, UK.Volume);
                    r.Q("Slug length in pipe", vs / c, UK.Depth);
                    r.Q("Pit gain (flowback)", vs * (v["ws"] - v["w"]) / v["w"], UK.Volume);
                });

            Add(G3, "Slug weight (density for a given slug volume)",
                "Slug density needed to give the required length of dry pipe with a chosen slug volume.",
                new[] { F("w", "Mud density", UK.Density, 10), F("vs", "Slug volume", UK.Volume, 25),
                        F("l", "Length of dry pipe required", UK.Depth, 300), F("id", "Drill pipe ID", UK.Diameter, 4.276),
                        F("sg", "SG of weight material", UK.SG, 4.2) },
                (v, r) =>
                {
                    double c = v["id"] * v["id"] / 1029.4;
                    Need(v["vs"] > 0, "Enter slug volume.");
                    double ls = v["vs"] / c;
                    double ws = v["w"] + v["l"] * v["w"] / ls;
                    r.Q("Slug density required", ws, UK.Density);
                    r.Q("Slug length in pipe", ls, UK.Depth);
                    double rb = WD * v["sg"];
                    if (rb > ws)
                    {
                        double startV = v["vs"] * (rb - ws) / (rb - v["w"]);
                        r.Q("Mud to take into slug pit", startV, UK.Volume);
                        r.Barite("Barite to add to it", (v["vs"] - startV) * 42 * rb);
                    }
                    else r.Warn("Slug density exceeds what the weight material can give.");
                });

            // =========================================================== 4. VOLUMES & HYDRAULICS
            Add(G4, "Hole, pipe and annular capacity",
                "Capacities and displacements for one hole / pipe combination.",
                new[] { F("dh", "Hole size or casing ID", UK.Diameter, 8.5), F("od", "Pipe OD", UK.Diameter, 5),
                        F("id", "Pipe ID", UK.Diameter, 4.276), F("l", "Length", UK.Depth, 1000) },
                (v, r) =>
                {
                    double dh = v["dh"], od = v["od"], id = v["id"], L = v["l"];
                    Need(dh > od && od > id, "Need hole > pipe OD > pipe ID.");
                    double ch = dh * dh / 1029.4, ca = (dh * dh - od * od) / 1029.4, cp = id * id / 1029.4;
                    double dOpen = (od * od - id * id) / 1029.4, dClosed = od * od / 1029.4;
                    r.Q("Hole capacity", ch, UK.Capacity);
                    r.Q("Annular capacity", ca, UK.Capacity);
                    r.Q("Pipe capacity", cp, UK.Capacity);
                    r.Q("Steel displacement (open end)", dOpen, UK.Capacity);
                    r.Q("Closed-end displacement", dClosed, UK.Capacity);
                    r.Blank();
                    r.Q("Hole volume over length", ch * L, UK.Volume);
                    r.Q("Annular volume over length", ca * L, UK.Volume);
                    r.Q("Pipe volume over length", cp * L, UK.Volume);
                });

            Add(G4, "Well volume & circulation times",
                "Hole, string and annulus volumes with bottoms-up and full circulation strokes/time. Drill pipe length = TD - HWDP - BHA.",
                new[] { F("shoe", "Casing shoe depth (MD)", UK.Depth, 8000), F("cid", "Casing ID", UK.Diameter, 8.835),
                        F("td", "Total depth (MD)", UK.Depth, 10000), F("dh", "Open hole size", UK.Diameter, 8.5),
                        F("wo", "Open hole washout", UK.Percent, 10),
                        F("dpod", "Drill pipe OD", UK.Diameter, 5), F("dpid", "Drill pipe ID", UK.Diameter, 4.276),
                        F("hwl", "HWDP length", UK.Depth, 600), F("hwod", "HWDP OD", UK.Diameter, 5), F("hwid", "HWDP ID", UK.Diameter, 3),
                        F("bhal", "BHA / drill collar length", UK.Depth, 300), F("bhaod", "BHA OD", UK.Diameter, 6.5), F("bhaid", "BHA ID", UK.Diameter, 2.8125),
                        F("surf", "Surface (pit) volume", UK.Volume, 800), F("po", "Pump output", UK.PumpOut, 0.1), F("spm", "Pump speed (SPM)", UK.None, 100) },
                (v, r) =>
                {
                    double shoe = Math.Min(v["shoe"], v["td"]), td = v["td"];
                    double hole = v["cid"] * v["cid"] / 1029.4 * shoe + v["dh"] * v["dh"] / 1029.4 * (td - shoe) * (1 + v["wo"] / 100);
                    double dpl = td - v["hwl"] - v["bhal"]; Need(dpl >= 0, "HWDP + BHA longer than TD.");
                    double strCap = (v["dpid"] * v["dpid"] * dpl + v["hwid"] * v["hwid"] * v["hwl"] + v["bhaid"] * v["bhaid"] * v["bhal"]) / 1029.4;
                    double closed = (v["dpod"] * v["dpod"] * dpl + v["hwod"] * v["hwod"] * v["hwl"] + v["bhaod"] * v["bhaod"] * v["bhal"]) / 1029.4;
                    double steel = closed - strCap, ann = hole - closed;
                    r.Q("Hole volume (no string)", hole, UK.Volume);
                    r.Q("String capacity", strCap, UK.Volume);
                    r.Q("String steel displacement", steel, UK.Volume);
                    r.Q("Annular volume", ann, UK.Volume);
                    r.Q("Hole volume with string in", strCap + ann, UK.Volume);
                    r.Q("Total circulating volume (+surface)", strCap + ann + v["surf"], UK.Volume);
                    if (v["po"] > 0)
                    {
                        r.Blank();
                        double stS = strCap / v["po"], stB = ann / v["po"], stT = (strCap + ann + v["surf"]) / v["po"];
                        r.N("Surface-to-bit strokes", stS, "stk"); r.N("Bottoms-up strokes", stB, "stk"); r.N("Total circulation strokes", stT, "stk");
                        if (v["spm"] > 0)
                        {
                            r.Q("Surface-to-bit time", stS / v["spm"], UK.Time);
                            r.Q("Bottoms-up time", stB / v["spm"], UK.Time);
                            r.Q("Total circulation time", stT / v["spm"], UK.Time);
                            r.Q("Flow rate", v["po"] * v["spm"] * 42, UK.Flow);
                        }
                    }
                });

            Add(G4, "Pump output",
                "Mud pump output per stroke and flow rate. For a duplex pump enter the rod diameter.",
                new[] { Field.Ch("type", "Pump type", 0, "Triplex (single acting)", "Duplex (double acting)"),
                        F("d", "Liner diameter", UK.Diameter, 6), F("s", "Stroke length", UK.Diameter, 12),
                        F("rod", "Rod diameter (duplex only)", UK.Diameter, 2.5), F("eff", "Volumetric efficiency", UK.Percent, 95), F("spm", "Pump speed (SPM)", UK.None, 100) },
                (v, r) =>
                {
                    double e = v["eff"] / 100, d = v["d"], s = v["s"];
                    double bps = v["type"] == 0 ? 0.000243 * d * d * s * e : 0.000162 * s * (2 * d * d - v["rod"] * v["rod"]) * e;
                    r.Q("Pump output", bps, UK.PumpOut);
                    r.Q("Flow rate", bps * v["spm"] * 42, UK.Flow);
                    r.N("Output", bps * v["spm"], "bbl/min");
                });

            Add(G4, "Annular velocity",
                "Average annular velocity for a flow rate and annulus.",
                new[] { F("q", "Flow rate", UK.Flow, 500), F("dh", "Hole size or casing ID", UK.Diameter, 8.5), F("od", "Pipe OD", UK.Diameter, 5) },
                (v, r) =>
                {
                    Need(v["dh"] > v["od"], "Hole must be larger than pipe OD.");
                    double av = 24.51 * v["q"] / (v["dh"] * v["dh"] - v["od"] * v["od"]);
                    r.Q("Annular velocity", av, UK.Velocity);
                    r.Q("Flow rate for this AV (check)", av * (v["dh"] * v["dh"] - v["od"] * v["od"]) / 24.51, UK.Flow);
                });

            Add(G4, "Cuttings generated",
                "Volume and weight of cuttings drilled per hour (gauge hole).",
                new[] { F("dh", "Hole size", UK.Diameter, 12.25), F("rop", "Rate of penetration", UK.Rate, 60), F("sg", "Cuttings SG", UK.SG, 2.6), F("por", "Porosity", UK.Percent, 0) },
                (v, r) =>
                {
                    double vol = v["dh"] * v["dh"] / 1029.4 * v["rop"] * (1 - v["por"] / 100);
                    r.N("Cuttings volume", Units.FromInt(UK.Volume, vol), Units.Name(UK.Volume) + "/hr");
                    r.N("Cuttings weight", Units.FromInt(UK.Mass, vol * 42 * WD * v["sg"]), Units.Name(UK.Mass) + "/hr");
                });

            Add(G4, "Tank / pit volume",
                "Volume of a tank and volume per unit height. Dimensions are in depth/length units.",
                new[] { Field.Ch("shape", "Tank shape", 0, "Rectangular", "Vertical cylinder", "Horizontal cylinder"),
                        F("l", "Length (rect./horizontal)", UK.Depth, 30), F("wdt", "Width or diameter", UK.Depth, 10),
                        F("h", "Total height", UK.Depth, 8), F("lev", "Fluid level", UK.Depth, 6) },
                (v, r) =>
                {
                    double L = v["l"], Wd = v["wdt"], H = v["h"], lev = Math.Min(v["lev"], v["shape"] == 2 ? Wd : H);
                    double ft3Total, ft3Lev, perFt;
                    if (v["shape"] == 0) { ft3Total = L * Wd * H; ft3Lev = L * Wd * lev; perFt = L * Wd; }
                    else if (v["shape"] == 1) { double a = Math.PI * Wd * Wd / 4; ft3Total = a * H; ft3Lev = a * lev; perFt = a; }
                    else
                    {
                        double rr = Wd / 2; ft3Total = Math.PI * rr * rr * L;
                        double hh = lev;
                        double seg = rr * rr * Math.Acos((rr - hh) / rr) - (rr - hh) * Math.Sqrt(Math.Max(0, 2 * rr * hh - hh * hh));
                        ft3Lev = seg * L; perFt = double.NaN;
                    }
                    r.Q("Total tank volume", ft3Total / 5.6146, UK.Volume);
                    r.Q("Volume at fluid level", ft3Lev / 5.6146, UK.Volume);
                    if (!double.IsNaN(perFt))
                    {
                        r.N("Volume per unit height", Units.FromInt(UK.Volume, perFt / 5.6146) / Units.FromInt(UK.Depth, 1), Units.Name(UK.Volume) + "/" + Units.Name(UK.Depth));
                        r.N("Volume per inch", perFt / 5.6146 / 12, "bbl/in");
                    }
                });

            Add(G4, "ECD & annular pressure loss (Bingham)",
                "Annular pressure loss for one annular section (Bingham plastic, laminar) and the resulting ECD.",
                new[] { F("w", "Mud density", UK.Density, 10), F("pv", "Plastic viscosity", UK.Visc, 20), F("yp", "Yield point", UK.YieldPt, 15),
                        F("q", "Flow rate", UK.Flow, 500), F("dh", "Hole size or casing ID", UK.Diameter, 8.5), F("od", "Pipe OD", UK.Diameter, 5),
                        F("l", "Section length (MD)", UK.Depth, 10000), F("tvd", "True vertical depth", UK.Depth, 10000) },
                (v, r) =>
                {
                    double dd = v["dh"] - v["od"]; Need(dd > 0, "Hole must be larger than pipe OD.");
                    double av = 24.51 * v["q"] / (v["dh"] * v["dh"] - v["od"] * v["od"]);
                    double vfs = av / 60;
                    double apl = v["pv"] * vfs * v["l"] / (1500 * dd * dd) + v["yp"] * v["l"] / (225 * dd);
                    r.Q("Annular velocity", av, UK.Velocity);
                    r.Q("Annular pressure loss", apl, UK.Pressure);
                    r.Q("ECD", v["w"] + apl / (0.052 * v["tvd"]), UK.Density);
                    r.Line("(Laminar-flow estimate; use a hydraulics program for detailed design.)");
                });

            // =========================================================== 5. PRESSURE & WELL CONTROL
            Add(G5, "Hydrostatic pressure",
                "Hydrostatic pressure and gradient of a mud column.",
                new[] { F("w", "Mud density", UK.Density, 10), F("tvd", "True vertical depth", UK.Depth, 10000) },
                (v, r) =>
                {
                    r.Q("Hydrostatic pressure", 0.052 * v["w"] * v["tvd"], UK.Pressure);
                    r.Q("Pressure gradient", 0.052 * v["w"], UK.Gradient);
                });

            Add(G5, "Equivalent mud weight / leak-off test",
                "EMW from a pressure at depth, LOT/FIT result and MAASP.",
                new[] { F("p", "Surface pressure (LOT/FIT) or extra pressure", UK.Pressure, 1500), F("w", "Mud density in hole during test", UK.Density, 10),
                        F("tvd", "Shoe / point TVD", UK.Depth, 8000), F("wnow", "Current mud density (for MAASP)", UK.Density, 11) },
                (v, r) =>
                {
                    Need(v["tvd"] > 0, "Enter TVD.");
                    double emw = v["w"] + v["p"] / (0.052 * v["tvd"]);
                    r.Q("Equivalent mud weight", emw, UK.Density);
                    r.Q("Fracture / test pressure at depth", 0.052 * emw * v["tvd"], UK.Pressure);
                    r.Q("MAASP with current mud", (emw - v["wnow"]) * 0.052 * v["tvd"], UK.Pressure);
                });

            Add(G5, "Kill sheet basics",
                "Kill mud weight, circulating pressures, influx gradient and barite for weighting up the active system.",
                new[] { F("omw", "Original mud density", UK.Density, 10), F("sidpp", "SIDPP", UK.Pressure, 500), F("sicp", "SICP", UK.Pressure, 700),
                        F("tvd", "Hole TVD", UK.Depth, 10000), F("scr", "Slow circulating rate pressure", UK.Pressure, 600),
                        F("gain", "Pit gain", UK.Volume, 20), F("ca", "Annular capacity around BHA", UK.Capacity, 0.0291),
                        F("vol", "Active system volume", UK.Volume, 1200), F("sg", "SG of weight material", UK.SG, 4.2),
                        F("margin", "Trip / safety margin", UK.Density, 0) },
                (v, r) =>
                {
                    double kmw = v["omw"] + v["sidpp"] / (0.052 * v["tvd"]) + v["margin"];
                    r.Q("Kill mud weight", kmw, UK.Density);
                    r.Q("Initial circulating pressure (ICP)", v["scr"] + v["sidpp"], UK.Pressure);
                    r.Q("Final circulating pressure (FCP)", v["scr"] * kmw / v["omw"], UK.Pressure);
                    if (v["ca"] > 0 && v["gain"] > 0)
                    {
                        double h = v["gain"] / v["ca"];
                        double g = 0.052 * v["omw"] - (v["sicp"] - v["sidpp"]) / h;
                        r.Q("Influx height", h, UK.Depth);
                        r.Q("Influx gradient", g, UK.Gradient);
                        r.T("Probable influx", g < 0.25 ? "Gas" : (g < 0.35 ? "Oil / gas mixture" : "Salt water / liquid"));
                    }
                    double rb = WD * v["sg"];
                    if (rb > kmw)
                    {
                        double vb = v["vol"] * (kmw - v["omw"]) / (rb - kmw);
                        r.Barite("Barite to weight up active system", vb * 42 * rb);
                        r.Q("Volume increase", vb, UK.Volume);
                    }
                });

            // =========================================================== 6. BRINES & SALT
            Add(G6, "Brine from dry salt",
                "Salt and water needed to make a single-salt brine, by target density or by weight %.",
                new[] { Field.Ch("salt", "Salt type", 0, "CaCl2", "NaCl", "KCl"), Field.Ch("by", "Target given as", 0, "Density", "Weight %"),
                        F("w", "Target brine density", UK.Density, 10.5), F("wt", "Target weight % salt", UK.Percent, 20),
                        F("v", "Brine volume required", UK.Volume, 500), F("pur", "Salt purity", UK.Percent, 95) },
                (v, r) =>
                {
                    Salt s = Data.GetSalt(new[] { "CaCl2", "NaCl", "KCl" }[(int)v["salt"]]);
                    Need(s != null, "Salt data missing (salts.csv).");
                    double wt = v["by"] == 0 ? s.WtFromSG(v["w"] / WD) : v["wt"];
                    Need(wt > 0, "Target must be heavier than fresh water.");
                    Need(wt <= s.MaxWt + 1e-6, s.Name + " saturates at about " + Units.Fmt(s.MaxWt) + " wt% (" + Units.Show(WD * s.SG(s.MaxWt), UK.Density) + ").");
                    double sg = s.SG(wt);
                    double saltPerBbl = LbPerBblPerSG * sg * wt / 100;
                    double waterPerBbl = sg * (1 - wt / 100);
                    r.T("Weight % salt", Units.Fmt(wt) + " %");
                    r.Q("Brine density", WD * sg, UK.Density);
                    r.T("Chlorides", Units.Fmt(s.ClFromWt(wt)) + " mg/L");
                    r.Q("Salt per unit volume of brine", saltPerBbl, UK.Conc);
                    r.N("Water per bbl of brine", waterPerBbl, "bbl");
                    double t = s.TCT(wt); if (!double.IsNaN(t)) r.Q("Approx. crystallization point", t, UK.Temp);
                    r.Blank();
                    r.Q("Fresh water required", waterPerBbl * v["v"], UK.Volume);
                    r.Mass("Salt required (as received)", saltPerBbl * v["v"] / (v["pur"] / 100), Data.SaltProduct(s.Name));
                });

            Add(G6, "Brine blending / dilution",
                "Volumes of a heavy and a light brine (or water) to make a target density. Assumes volumes are additive.",
                new[] { F("v", "Final volume required", UK.Volume, 500), F("wt", "Target density", UK.Density, 10.5),
                        F("w1", "Heavy brine density", UK.Density, 11.6), F("w2", "Light brine / water density", UK.Density, WD) },
                (v, r) =>
                {
                    Need(v["w1"] > v["wt"] && v["wt"] > v["w2"], "Target must lie between the two densities.");
                    double v1 = v["v"] * (v["wt"] - v["w2"]) / (v["w1"] - v["w2"]);
                    r.Q("Heavy brine", v1, UK.Volume);
                    r.Q("Light brine / water", v["v"] - v1, UK.Volume);
                    r.T("Heavy brine fraction", Units.Fmt(100 * v1 / v["v"]) + " %");
                });

            Add(G6, "Chlorides to salt concentration",
                "Converts a chloride titration to salt weight %, mg/L, brine density and crystallization point.",
                new[] { Field.Ch("salt", "Salt type", 1, "CaCl2", "NaCl", "KCl"), F("cl", "Chlorides of the brine / filtrate", UK.Chlorides, 60000) },
                (v, r) =>
                {
                    Salt s = Data.GetSalt(new[] { "CaCl2", "NaCl", "KCl" }[(int)v["salt"]]);
                    Need(s != null, "Salt data missing.");
                    double wt = s.WtFromCl(v["cl"]);
                    Need(wt <= s.MaxWt * 1.02, "Chlorides exceed saturation for " + s.Name + ".");
                    r.T("Weight % salt", Units.Fmt(wt) + " %");
                    r.T("Salt concentration", Units.Fmt(v["cl"] / s.ClFrac) + " mg/L");
                    r.T("Salinity", Units.Fmt(wt * 1e4) + " ppm");
                    r.Q("Brine density", WD * s.SG(wt), UK.Density);
                    r.Q("Salt per unit volume of brine", LbPerBblPerSG * s.SG(wt) * wt / 100, UK.Conc);
                    double t = s.TCT(wt); if (!double.IsNaN(t)) r.Q("Approx. crystallization point", t, UK.Temp);
                });

            // =========================================================== 7. MUD TESTING
            Add(G7, "Rheology from viscometer readings",
                "PV, YP, low-shear YP and power-law n/K from 600/300/6/3 rpm dial readings.",
                new[] { F("r600", "600 rpm reading", UK.Dial, 60), F("r300", "300 rpm reading", UK.Dial, 38), F("r6", "6 rpm reading", UK.Dial, 9), F("r3", "3 rpm reading", UK.Dial, 7) },
                (v, r) =>
                {
                    Need(v["r600"] > v["r300"] && v["r300"] > 0, "600 rpm must be higher than 300 rpm.");
                    double pv = v["r600"] - v["r300"], yp = v["r300"] - pv;
                    double n = 3.32 * Math.Log10(v["r600"] / v["r300"]);
                    double k = 510 * v["r300"] / Math.Pow(511, n);
                    r.Q("Plastic viscosity (PV)", pv, UK.Visc);
                    r.Q("Yield point (YP)", yp, UK.YieldPt);
                    r.Q("Apparent viscosity", v["r600"] / 2, UK.Visc);
                    if (v["r3"] > 0) r.Q("Low-shear yield point (2x3rpm - 6rpm)", 2 * v["r3"] - v["r6"], UK.YieldPt);
                    r.N("Flow behaviour index n", n, "");
                    r.N("Consistency index K", k, "eq. cP");
                });

            Add(G7, "Solids analysis - water-based mud",
                "Corrected solids, low/high gravity solids from mud weight and retort (NaCl salt correction from chlorides).",
                new[] { F("w", "Mud density", UK.Density, 11), F("wat", "Retort water %", UK.Percent, 84), F("oil", "Retort oil %", UK.Percent, 0),
                        F("cl", "Filtrate chlorides", UK.Chlorides, 20000), F("sgo", "SG of oil", UK.SG, 0.8),
                        F("sgl", "SG of low-gravity solids", UK.SG, 2.6), F("sgh", "SG of high-gravity solids", UK.SG, 4.2) },
                (v, r) =>
                {
                    double cl = v["cl"];
                    double sgb = cl > 0 ? 1 + 1.94e-6 * Math.Pow(cl, 0.95) : 1.0;
                    double w = cl > 0 ? (cl / 0.6066) / (sgb * 1e6) : 0;
                    double brine = v["wat"] / (sgb * (1 - w));
                    double vs = 100 - brine - v["oil"];
                    Need(vs > 0, "Retort water + oil leave no room for solids.");
                    double asg = (100 * v["w"] / WD - brine * sgb - v["oil"] * v["sgo"]) / vs;
                    double lgs = vs * (v["sgh"] - asg) / (v["sgh"] - v["sgl"]);
                    if (lgs < 0) lgs = 0; if (lgs > vs) lgs = vs;
                    double hgs = vs - lgs;
                    r.N("Brine SG / salt wt%", sgb, "/ " + Units.Fmt(w * 100) + " %");
                    r.T("Corrected brine volume", Units.Fmt(brine) + " %");
                    r.T("Corrected solids", Units.Fmt(vs) + " %");
                    r.N("Average SG of solids", asg, "");
                    r.T("Low-gravity solids", Units.Fmt(lgs) + " %  = " + Units.Show(lgs * 3.5 * v["sgl"], UK.Conc));
                    r.T("High-gravity solids", Units.Fmt(hgs) + " %  = " + Units.Show(hgs * 3.5 * v["sgh"], UK.Conc));
                });

            Add(G7, "Oil mud retort & salinity analysis",
                "OWR, water phase salinity (CaCl2) from whole-mud chlorides, corrected solids and excess lime.",
                new[] { F("w", "Mud density", UK.Density, 12), F("oil", "Retort oil %", UK.Percent, 60), F("wat", "Retort water %", UK.Percent, 18),
                        F("cl", "Whole mud chlorides", UK.Chlorides, 40000), F("pom", "Pom (ml 0.1N H2SO4)", UK.None, 4),
                        F("sgo", "SG of base oil", UK.SG, 0.8), F("sgl", "SG of low-gravity solids", UK.SG, 2.6), F("sgh", "SG of high-gravity solids", UK.SG, 4.2) },
                (v, r) =>
                {
                    Need(v["oil"] + v["wat"] > 0 && v["oil"] + v["wat"] < 100, "Check retort values.");
                    double owr = 100 * v["oil"] / (v["oil"] + v["wat"]);
                    double wtp = v["wat"] > 0 ? 100 * 1.565 * v["cl"] / (1.565 * v["cl"] + 10000 * v["wat"]) : 0;
                    Salt s = Data.GetSalt("CaCl2");
                    double sgb = s != null ? s.SG(wtp) : 1;
                    double brine = v["wat"] / (sgb * (1 - wtp / 100));
                    double vs = 100 - v["oil"] - brine;
                    r.T("OWR", Units.Fmt(owr) + " / " + Units.Fmt(100 - owr));
                    r.T("Water phase salinity (CaCl2)", Units.Fmt(wtp) + " wt% = " + Units.Fmt(wtp * 1e4) + " ppm");
                    r.N("Brine SG", sgb, "");
                    if (s != null) { double t = s.TCT(wtp); if (!double.IsNaN(t)) r.Q("Approx. brine crystallization point", t, UK.Temp); }
                    r.T("Corrected brine volume", Units.Fmt(brine) + " %");
                    r.T("Corrected solids", Units.Fmt(vs) + " %");
                    if (vs > 0)
                    {
                        double asg = (100 * v["w"] / WD - brine * sgb - v["oil"] * v["sgo"]) / vs;
                        double lgs = Math.Max(0, Math.Min(vs, vs * (v["sgh"] - asg) / (v["sgh"] - v["sgl"])));
                        r.N("Average SG of solids", asg, "");
                        r.T("Low-gravity solids", Units.Fmt(lgs) + " %  = " + Units.Show(lgs * 3.5 * v["sgl"], UK.Conc));
                        r.T("High-gravity solids", Units.Fmt(vs - lgs) + " %  = " + Units.Show((vs - lgs) * 3.5 * v["sgh"], UK.Conc));
                    }
                    r.Q("Excess lime (1.295 x Pom)", 1.295 * v["pom"], UK.Conc);
                });
        }
    }

    // ================================================================= FORMULATION FROM SCRATCH
    public class FormLine { public Product P; public double Conc; }   // Conc in lb/bbl

    public static class Formulate
    {
        const double WD = Calcs.WD;

        // obm: OWR = oil % of liquid. wbm: oil % of liquid (usually 0).
        // salinity: OBM -> WPS ppm, WBM -> chlorides mg/L. saltName "" = fresh water, "Seawater" = seawater base.
        public static string Run(bool obm, double V, double W, double oilPct, string saltName, double salinity, double purity,
                                 double sgWM, double sgOil, List<FormLine> lines)
        {
            var r = new Res();
            r.Title((obm ? "Oil" : "Water") + "-based mud formulation from scratch");
            r.Line("To mix " + Units.Show(V, UK.Volume) + " of " + Units.Show(W, UK.Density) + " mud" +
                   (obm ? " with OWR " + Units.Fmt(oilPct) + "/" + Units.Fmt(100 - oilPct) : (oilPct > 0 ? " with " + Units.Fmt(oilPct) + " % oil in liquid" : "")));
            if (V <= 0) throw new CalcError("Enter a volume to mix.");
            if (oilPct < 0 || oilPct > 100) throw new CalcError("Oil % must be 0-100.");
            if (purity <= 0 || purity > 100) throw new CalcError("Salt purity must be 1-100 %.");

            // brine phase
            double wFrac = 0, sgBr = 1.0; Salt salt = null;
            if (saltName == "Seawater") { sgBr = 1.025; }
            else if (saltName.Length > 0 && salinity > 0)
            {
                salt = Data.GetSalt(saltName);
                if (salt == null) throw new CalcError("Salt " + saltName + " not found in salts.csv.");
                double wt = obm ? salinity / 1e4 : salt.WtFromCl(salinity);
                if (wt > salt.MaxWt + 1e-6)
                    throw new CalcError("Salinity is above saturation for " + salt.Name + " (max " + Units.Fmt(salt.MaxWt * 1e4) + " ppm WPS / " + Units.Fmt(salt.ClFromWt(salt.MaxWt)) + " mg/L Cl).");
                wFrac = wt / 100; sgBr = salt.SG(wt);
            }
            double rBr = WD * sgBr, rOil = WD * sgOil, rWM = WD * sgWM;
            double fo = oilPct / 100;
            double rLiq = fo * rOil + (1 - fo) * rBr;

            double sumM = 0, sumV = 0;
            foreach (var l in lines)
            {
                if (l.Conc <= 0) continue;
                double m = l.Conc * V; sumM += m; sumV += m / (42 * WD * l.P.SG);
            }
            double vWM = (V * W - sumM / 42 - (V - sumV) * rLiq) / (rWM - rLiq);
            if (vWM < 0)
            {
                double wMin = (sumM / 42 + (V - sumV) * rLiq) / V;
                r.Warn("No weight material needed: the products and liquids alone give " + Units.Show(wMin, UK.Density) + ".");
                vWM = 0;
            }
            double vLiq = V - sumV - vWM;
            if (vLiq <= 0) throw new CalcError("Products and weight material take up the whole volume - density too high.");
            double vOil = fo * vLiq, vBrine = vLiq - vOil;
            double vWater = vBrine * sgBr * (1 - wFrac);
            double saltLb = vBrine * 42 * rBr * wFrac / (purity / 100);

            r.Blank(); r.Title("Basic requirements");
            if (fo > 0) r.Q("Base oil", vOil, UK.Volume);
            r.Q(saltName == "Seawater" ? "Seawater" : "Water", vWater, UK.Volume);
            if (salt != null)
            {
                r.Q("Brine volume", vBrine, UK.Volume);
                r.T("Brine salt / SG", salt.Name + " " + Units.Fmt(wFrac * 100) + " wt% / " + Units.Fmt(sgBr));
                double t = salt.TCT(wFrac * 100); if (!double.IsNaN(t)) r.Q("Approx. brine crystallization point", t, UK.Temp);
            }
            r.Q("Weight material volume", vWM, UK.Volume);

            r.Blank(); r.Title("Total amount of each product");
            double cost = 0;
            Product wm = Data.WeightMaterial();
            Action<string, double, Product> line = (name, lb, p) =>
            {
                if (lb <= 0) return;
                r.T(name, Units.Show(lb, UK.Mass));
                if (p != null && p.PackageLb() > 0)
                {
                    double n = Math.Ceiling(lb / p.PackageLb() - 1e-9);
                    string pk = n.ToString("#,##0") + " x " + p.PackageText();
                    if (p.Price > 0) { double c = n * p.Price; cost += c; pk += ", cost " + c.ToString("#,##0.00"); }
                    r.T("   packages", pk);
                }
            };
            if (salt != null) line("Salt " + salt.Name + " (as received)", saltLb, Data.SaltProduct(salt.Name));
            foreach (var l in lines) if (l.Conc > 0) line(l.P.Name, l.Conc * V, l.P);
            line("Weight material", vWM * 42 * rWM, wm);
            if (vWM > 0) r.N("   weight material in tonnes", vWM * 42 * rWM / 2204.62, "MT");

            r.Blank(); r.Title("Check");
            double solidsPct = 100 * (sumV + vWM) / V;
            r.T("Products + weight material volume", Units.Fmt(solidsPct) + " % of mud");
            if (fo > 0) r.T("Oil % / brine % of mud", Units.Fmt(100 * vOil / V) + " % / " + Units.Fmt(100 * vBrine / V) + " %");
            if (cost > 0) { r.T("Total product cost", cost.ToString("#,##0.00")); r.T("Cost per unit volume", (cost / Units.FromInt(UK.Volume, V)).ToString("#,##0.00") + " per " + Units.Name(UK.Volume)); }
            r.Blank(); r.Line("All amounts are approximate.");
            return r.SB.ToString();
        }
    }
}
