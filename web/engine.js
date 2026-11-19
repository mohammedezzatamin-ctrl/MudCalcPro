// Mud Calculator Pro - calculation engine (web). Mirrors src/*.cs of the Windows version.
// Internal units: ppg, bbl, lb, ft, in, psi, gpm, lb/bbl, degF.
"use strict";
var MC = (function () {
  var WD = 8.345, LBSG = 42 * WD;

  // ---------------------------------------------------------------- units
  function U(n, f, o) { return { name: n, f: f, o: o || 0 }; }
  var UNITS = {
    none: { kind: "", metric: 0, opts: [U("", 1)] },
    density: { kind: "Density / mud weight", metric: 1, opts: [U("ppg", 1), U("SG", 8.345), U("kg/m³", 0.008345), U("psi/1000ft", 1 / 52), U("lb/ft³", 1 / 7.48052)] },
    volume: { kind: "Volume", metric: 1, opts: [U("bbl", 1), U("m³", 6.28981), U("litre", 0.00628981), U("US gal", 1 / 42)] },
    mass: { kind: "Mass", metric: 1, opts: [U("lb", 1), U("kg", 2.20462), U("MT", 2204.62)] },
    depth: { kind: "Depth / length", metric: 1, opts: [U("ft", 1), U("m", 3.28084)] },
    diameter: { kind: "Diameter", metric: 0, opts: [U("in", 1), U("mm", 1 / 25.4)] },
    pressure: { kind: "Pressure", metric: 1, opts: [U("psi", 1), U("bar", 14.5038), U("kPa", 0.145038), U("kg/cm²", 14.2233)] },
    flow: { kind: "Flow rate", metric: 1, opts: [U("gpm", 1), U("L/min", 0.264172), U("bbl/min", 42), U("m³/min", 264.172)] },
    conc: { kind: "Concentration", metric: 1, opts: [U("ppb", 1), U("kg/m³", 1 / 2.853)] },
    percent: { kind: "", metric: 0, opts: [U("%", 1)] },
    temp: { kind: "Temperature", metric: 1, opts: [U("°F", 1, 0), U("°C", 1.8, 32)] },
    visc: { kind: "Viscosity", metric: 0, opts: [U("cP", 1), U("mPa·s", 1)] },
    yp: { kind: "Yield point / gels", metric: 1, opts: [U("lb/100ft²", 1), U("Pa", 1 / 0.4788)] },
    cl: { kind: "", metric: 0, opts: [U("mg/L", 1)] },
    velocity: { kind: "Annular velocity", metric: 1, opts: [U("ft/min", 1), U("m/min", 3.28084)] },
    capacity: { kind: "Capacity", metric: 1, opts: [U("bbl/ft", 1), U("m³/m", 1.917134), U("L/m", 0.001917134)] },
    time: { kind: "Time", metric: 0, opts: [U("min", 1), U("hr", 60)] },
    gradient: { kind: "Pressure gradient", metric: 1, opts: [U("psi/ft", 1), U("kPa/m", 0.0442075), U("bar/m", 4.42075)] },
    sg: { kind: "", metric: 0, opts: [U("SG", 1)] },
    pumpout: { kind: "Pump output", metric: 1, opts: [U("bbl/stk", 1), U("L/stk", 0.00628981), U("gal/stk", 1 / 42)] },
    ppm: { kind: "", metric: 0, opts: [U("ppm", 1)] },
    dial: { kind: "", metric: 0, opts: [U("dial", 1)] },
    rate: { kind: "Drilling rate", metric: 1, opts: [U("ft/hr", 1), U("m/hr", 3.28084)] }
  };
  var sel = {};
  Object.keys(UNITS).forEach(function (k) { sel[k] = 0; });
  function cur(k) { return UNITS[k].opts[sel[k]]; }
  function uname(k) { return cur(k).name; }
  function toInt(k, d) { var u = cur(k); return d * u.f + u.o; }
  function fromInt(k, v) { var u = cur(k); return (v - u.o) / u.f; }
  function fmt(v) {
    if (!isFinite(v)) return "-";
    var a = Math.abs(v);
    if (a === 0) return "0";
    function g(d) { return v.toLocaleString("en-US", { minimumFractionDigits: d, maximumFractionDigits: d }); }
    if (a >= 10000) return g(0);
    if (a >= 100) return g(1);
    if (a >= 1) return v.toFixed(2);
    if (a >= 0.01) return v.toFixed(4);
    return v.toFixed(6);
  }
  function fmtIn(v) { if (!isFinite(v)) return "0"; return String(Math.round(v * 10000) / 10000); }
  function show(v, k) { var n = uname(k); return fmt(fromInt(k, v)) + (n ? " " + n : ""); }
  function parseNum(s) {
    s = String(s == null ? "" : s).trim().replace(/\s/g, "");
    if (s === "") return 0;
    if (s.indexOf(",") >= 0 && s.indexOf(".") < 0 && s.split(",").length === 2 && s.length - s.indexOf(",") - 1 !== 3) s = s.replace(",", ".");
    else s = s.replace(/,/g, "");
    if (!/^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/.test(s)) return NaN;
    return parseFloat(s);
  }
  function preset(metric) { Object.keys(UNITS).forEach(function (k) { sel[k] = metric ? UNITS[k].metric : 0; }); }

  // ---------------------------------------------------------------- salts
  function Salt(name, A, B, clf, max, sgs, curve) {
    var tw = [], tf = [];
    curve.split(";").forEach(function (p) { var kv = p.split(":"); tw.push(+kv[0]); tf.push(+kv[1]); });
    var s = { name: name, A: A, B: B, clf: clf, max: max, sgSolid: sgs, tw: tw, tf: tf };
    s.SG = function (wt) { return 1.0036 * (0.99707 + A * wt + B * wt * wt); };
    s.TCT = function (wt) {
      if (wt <= tw[0]) return tf[0];
      for (var i = 1; i < tw.length; i++) if (wt <= tw[i]) return tf[i - 1] + (tf[i] - tf[i - 1]) * (wt - tw[i - 1]) / (tw[i] - tw[i - 1]);
      return NaN;
    };
    s.wtFromSG = function (sg) { var lo = 0, hi = max * 1.2; for (var i = 0; i < 80; i++) { var m = (lo + hi) / 2; if (s.SG(m) < sg) lo = m; else hi = m; } return (lo + hi) / 2; };
    s.wtFromCl = function (cl) { var w = 0; for (var i = 0; i < 60; i++) w = (cl / clf) / (s.SG(w) * 1e4); return w; };
    s.clFromWt = function (wt) { return wt / 100 * s.SG(wt) * 1e6 * clf; };
    return s;
  }
  var SALTS = [
    Salt("CaCl2", 0.007923, 0.00004964, 0.6389, 40.0, 2.15, "0:32;5:27.7;10:20;15:12.2;20:-4;25:-21;29.8:-60;30:-52;32.5:-4;35:14;37.3:32;40:61"),
    Salt("NaCl", 0.006504, 0.00004395, 0.6066, 26.4, 2.165, "0:32;5:26.7;10:20.2;15:12.4;20:2.4;23.3:-6;26.29:32.3;26.4:60"),
    Salt("KCl", 0.0059, 0.00003415, 0.4756, 24.0, 1.98, "0:32;4:28.7;8:25.2;12:21.4;16:17.3;19.5:12.7;21:22;24.5:60")
  ];
  function getSalt(n) { for (var i = 0; i < SALTS.length; i++) if (SALTS[i].name.toLowerCase() === String(n).toLowerCase()) return SALTS[i]; return null; }
  var BRINES = [["Potassium chloride KCl", 9.7], ["Sodium chloride NaCl", 10.0], ["Potassium bromide KBr", 11.5], ["Calcium chloride CaCl2", 11.6],
    ["Sodium bromide NaBr", 12.6], ["Calcium bromide CaBr2", 15.1], ["Zinc bromide / CaBr2 blends", 19.2], ["Sodium formate (approx.)", 11.1],
    ["Potassium formate (approx.)", 13.1], ["Cesium formate (approx.)", 19.2]];

  // ---------------------------------------------------------------- products
  var PRODUCT_COLS = ["Name", "Category", "System", "SG", "PackageSize", "PackageUnit", "PricePerPackage", "ConcWBM_ppb", "ConcOBM_ppb"];
  var DEFAULT_PRODUCTS = [
    ["Barite (API)", "Weighting", "Both", 4.2, 50, "kg", 0, 0, 0], ["Bentonite (API)", "Viscosifier", "WBM", 2.6, 50, "kg", 0, 0, 0],
    ["Xanthan gum", "Viscosifier", "WBM", 1.5, 25, "kg", 0, 1, 0], ["PAC-LV", "Fluid loss", "WBM", 1.6, 25, "kg", 0, 2, 0],
    ["PAC-R", "Fluid loss", "WBM", 1.6, 25, "kg", 0, 1, 0], ["Modified starch", "Fluid loss", "WBM", 1.5, 25, "kg", 0, 4, 0],
    ["Caustic soda", "Alkalinity", "WBM", 2.13, 25, "kg", 0, 0.5, 0], ["Soda ash", "Alkalinity", "WBM", 2.53, 50, "kg", 0, 0.25, 0],
    ["Lime", "Alkalinity", "Both", 2.24, 25, "kg", 0, 0, 6], ["Calcium carbonate (fine)", "Bridging", "Both", 2.7, 25, "kg", 0, 10, 10],
    ["Primary emulsifier", "Emulsifier", "OBM", 0.94, 55, "gal", 0, 0, 8], ["Wetting agent", "Emulsifier", "OBM", 0.95, 55, "gal", 0, 0, 2],
    ["Organophilic clay", "Viscosifier", "OBM", 1.7, 25, "kg", 0, 0, 6], ["Fluid loss additive (gilsonite)", "Fluid loss", "OBM", 1.05, 25, "kg", 0, 0, 6],
    ["Calcium chloride CaCl2 (95%)", "Salt", "Both", 2.15, 25, "kg", 0, 0, 0], ["Sodium chloride NaCl", "Salt", "Both", 2.165, 50, "kg", 0, 0, 0],
    ["Potassium chloride KCl", "Salt", "Both", 1.98, 50, "kg", 0, 0, 0]
  ].map(function (r) { return { name: r[0], category: r[1], system: r[2], sg: r[3], pkgSize: r[4], pkgUnit: r[5], price: r[6], concWBM: r[7], concOBM: r[8] }; });
  var products = DEFAULT_PRODUCTS.map(function (p) { return Object.assign({}, p); });
  function pkgLb(p) {
    var u = String(p.pkgUnit || "").trim().toLowerCase(), s = +p.pkgSize || 0, sg = +p.sg || 1;
    if (u === "lb" || u === "lbs") return s;
    if (u === "kg") return s * 2.20462;
    if (u === "mt" || u === "t" || u === "tonne") return s * 2204.62;
    if (u === "gal" || u === "usgal") return s * WD * sg;
    if (u === "l" || u === "litre" || u === "liter") return s * 0.264172 * WD * sg;
    if (u === "bbl") return s * 42 * WD * sg;
    return s * 2.20462;
  }
  function pkgText(p) { return fmtIn(+p.pkgSize) + " " + p.pkgUnit; }
  function weightMaterial() { for (var i = 0; i < products.length; i++) if (/^weighting$/i.test(products[i].category)) return products[i]; return null; }
  function saltProduct(n) { for (var i = 0; i < products.length; i++) if (/^salt$/i.test(products[i].category) && products[i].name.toLowerCase().indexOf(n.toLowerCase()) >= 0) return products[i]; return null; }

  // ---------------------------------------------------------------- results
  function CalcError(m) { this.message = m; }
  function need(ok, m) { if (!ok) throw new CalcError(m); }
  function pad(s) { s = s + " "; while (s.length < 36) s += "."; return s + " "; }
  function Res() { this.L = []; }
  Res.prototype.title = function (s) { this.L.push(s.toUpperCase()); this.L.push(new Array(Math.min(60, s.length) + 1).join("-")); };
  Res.prototype.line = function (s) { this.L.push(s); };
  Res.prototype.blank = function () { this.L.push(""); };
  Res.prototype.q = function (l, v, k) { this.L.push(pad(l) + show(v, k)); };
  Res.prototype.n = function (l, v, u) { this.L.push(pad(l) + fmt(v) + (u ? " " + u : "")); };
  Res.prototype.t = function (l, s) { this.L.push(pad(l) + s); };
  Res.prototype.warn = function (s) { this.L.push("!! " + s); };
  Res.prototype.mass = function (l, lb, p) {
    this.q(l, lb, "mass");
    if (uname("mass") !== "MT") this.n("   = tonnes", lb / 2204.62, "MT");
    if (p && pkgLb(p) > 0 && lb > 0) this.t("   = packages @ " + pkgText(p), Math.ceil(lb / pkgLb(p) - 1e-9).toLocaleString("en-US"));
  };
  Res.prototype.barite = function (l, lb) { this.mass(l, lb, weightMaterial()); };
  Res.prototype.text = function () { return this.L.join("\n"); };

  // ---------------------------------------------------------------- calculations
  var CALCS = [];
  function F(key, label, kind, def) { return { key: key, label: label, kind: kind, def: def }; }
  function CH(key, label, def, choices) { return { key: key, label: label, kind: "none", def: def, choices: choices }; }
  function add(group, name, help, fields, eq, fn) { CALCS.push({ group: group, name: name, help: help, fields: fields, eq: eq, fn: fn }); }
  var G1 = "Weight up & dilution", G2 = "Oil mud adjustments", G3 = "Basic mixes & slugs", G4 = "Volumes & hydraulics",
      G5 = "Pressure & well control", G6 = "Brines & salt", G7 = "Mud testing & solids";
  function owrSplit(oil, owr) {
    need(owr > 0 && owr < 100, "OWR (oil %) must be between 0 and 100.");
    need(oil > 0 && oil < 100, "Oil % (retort) must be between 0 and 100.");
    var water = oil * (100 - owr) / owr;
    need(oil + water < 100, "Oil % and OWR give more than 100% liquid - check the inputs.");
    return water;
  }
  var SALT3 = ["CaCl2", "NaCl", "KCl"];

  // 1. weight up & dilution
  add(G1, "Weight up (volume increase)", "Weight material needed to raise the mud density of the whole system. Mud volume increases by the volume of weight material.",
    [F("w1", "Initial mud density", "density", 10), F("w2", "Desired mud density", "density", 12), F("v", "Starting mud volume", "volume", 500), F("sg", "SG of weight material", "sg", 4.2)],
    "Vb = V1 × (W2 − W1) / (ρb − W2)\nM  = 42 × ρb × Vb          lb/bbl = M / V1\nVfinal = V1 + Vb\n(SG 4.2:  lb/bbl = 1470 × (W2 − W1) / (35 − W2))",
    function (v, r) {
      var rb = WD * v.sg;
      need(v.w2 > v.w1, "Desired density must be higher than initial density.");
      need(rb > v.w2, "Weight material SG is too low for the desired density.");
      var vb = v.v * (v.w2 - v.w1) / (rb - v.w2), lb = vb * 42 * rb;
      r.barite("Weight material required", lb);
      r.q("   per unit volume of starting mud", lb / v.v, "conc");
      r.q("Volume increase", vb, "volume");
      r.q("Final mud volume", v.v + vb, "volume");
    });
  add(G1, "Weight up (constant volume)", "Dump part of the mud first, then weight up so the final volume equals the starting volume.",
    [F("w1", "Initial mud density", "density", 10), F("w2", "Desired mud density", "density", 12), F("v", "Mud volume (to keep)", "volume", 500), F("sg", "SG of weight material", "sg", 4.2)],
    "Vkeep = V × (ρb − W2) / (ρb − W1)\nVdump = V − Vkeep\nM = 42 × ρb × Vdump",
    function (v, r) {
      var rb = WD * v.sg;
      need(v.w2 > v.w1, "Desired density must be higher than initial density.");
      need(rb > v.w2, "Weight material SG is too low for the desired density.");
      var keep = v.v * (rb - v.w2) / (rb - v.w1), dump = v.v - keep;
      r.q("Mud to dump / jettison first", dump, "volume");
      r.q("Mud kept", keep, "volume");
      r.barite("Weight material to add", dump * 42 * rb);
      r.q("Final mud volume", v.v, "volume");
    });
  add(G1, "Cut mud weight (volume increase)", "Volume of dilution fluid (water, base oil or light mud) needed to reduce the mud density.",
    [F("w1", "Initial mud density", "density", 12), F("w2", "Desired mud density", "density", 11), F("v", "Starting mud volume", "volume", 500), F("wd", "Density of dilution fluid", "density", WD)],
    "Vd = V1 × (W1 − W2) / (W2 − Wd)\nVfinal = V1 + Vd",
    function (v, r) {
      need(v.w2 < v.w1, "Desired density must be lower than initial density.");
      need(v.w2 > v.wd, "Dilution fluid must be lighter than the desired density.");
      var vd = v.v * (v.w1 - v.w2) / (v.w2 - v.wd);
      r.q("Dilution fluid to add", vd, "volume");
      r.q("Final mud volume", v.v + vd, "volume");
    });
  add(G1, "Cut mud weight (constant volume)", "Dump part of the mud, then add dilution fluid back to the same volume.",
    [F("w1", "Initial mud density", "density", 12), F("w2", "Desired mud density", "density", 11), F("v", "Mud volume", "volume", 500), F("wd", "Density of dilution fluid", "density", WD)],
    "Vkeep = V × (W2 − Wd) / (W1 − Wd)\nVdump = Vadd = V − Vkeep",
    function (v, r) {
      need(v.w2 < v.w1, "Desired density must be lower than initial density.");
      need(v.w2 > v.wd, "Dilution fluid must be lighter than the desired density.");
      var keep = v.v * (v.w2 - v.wd) / (v.w1 - v.wd);
      r.q("Mud to dump first", v.v - keep, "volume");
      r.q("Dilution fluid to add", v.v - keep, "volume");
      r.q("Mud kept", keep, "volume");
    });
  add(G1, "Mix two or three muds", "Density and volume of a blend. Leave the third fluid at zero volume to mix only two.",
    [F("d1", "Density - fluid 1", "density", 12), F("v1", "Volume - fluid 1", "volume", 300), F("d2", "Density - fluid 2", "density", 10),
     F("v2", "Volume - fluid 2", "volume", 200), F("d3", "Density - fluid 3", "density", 0), F("v3", "Volume - fluid 3", "volume", 0)],
    "Vmix = V1 + V2 + V3\nWmix = (W1·V1 + W2·V2 + W3·V3) / Vmix",
    function (v, r) {
      var vt = v.v1 + v.v2 + v.v3; need(vt > 0, "Enter at least one volume.");
      r.q("Density of mixture", (v.d1 * v.v1 + v.d2 * v.v2 + v.d3 * v.v3) / vt, "density");
      r.q("Volume of mixture", vt, "volume");
    });
  add(G1, "Add a known weight of barite", "New density and volume after adding a known weight of weight material.",
    [F("w1", "Initial mud density", "density", 10), F("v", "Initial mud volume", "volume", 500), F("m", "Weight of barite added", "mass", 22046.2), F("sg", "SG of weight material", "sg", 4.2)],
    "Vb = M / (42 × ρb)\nWnew = (42 × W1 × V1 + M) / [42 × (V1 + Vb)]",
    function (v, r) {
      var rb = WD * v.sg, vb = v.m / (42 * rb);
      r.q("New mud density", (v.v * 42 * v.w1 + v.m) / (42 * (v.v + vb)), "density");
      r.q("Volume increase", vb, "volume");
      r.q("Final mud volume", v.v + vb, "volume");
    });
  add(G1, "Barite to raise density of a pill / system (per unit volume)", "Quick per-volume figures for weighting up (no volume entered).",
    [F("w1", "Initial mud density", "density", 10), F("w2", "Desired mud density", "density", 12), F("sg", "SG of weight material", "sg", 4.2)],
    "C = 42 × ρb × (W2 − W1) / (ρb − W2)   lb/bbl\nVolume increase = (W2 − W1) / (ρb − W2)   bbl/bbl\nSacks (100 lb) per 100 bbl = C",
    function (v, r) {
      var rb = WD * v.sg; need(v.w2 > v.w1 && rb > v.w2, "Check densities.");
      var per = 42 * rb * (v.w2 - v.w1) / (rb - v.w2);
      r.q("Weight material per unit of starting mud", per, "conc");
      r.n("Volume increase per bbl of mud", (v.w2 - v.w1) / (rb - v.w2), "bbl/bbl");
      r.n("Sacks (100 lb) per 100 bbl", per, "sx");
    });

  // 2. oil mud
  add(G2, "Adjust OWR (add oil or water)", "Raises or lowers the oil/water ratio. Uses retort oil % to find the oil and water fractions. Optionally restores the mud weight with barite.",
    [F("v", "Mud volume", "volume", 500), F("w", "Mud density", "density", 12), F("owr1", "Current OWR (oil %)", "percent", 70), F("oil", "Retort oil % (by volume)", "percent", 56),
     F("owr2", "Desired OWR (oil %)", "percent", 80), F("sgo", "SG of base oil", "sg", 0.80), F("sgw", "SG of water / brine added", "sg", 1.0), F("sg", "SG of weight material", "sg", 4.2),
     CH("keep", "Restore mud weight with barite?", 0, ["Yes", "No"])],
    "fo = oil%/100 ;  fw = fo × (100 − OWR1) / OWR1\nRaise OWR:  Voil   = V × [fw × OWR2/(100 − OWR2) − fo]\nLower OWR:  Vwater = V × [fo × (100 − OWR2)/OWR2 − fw]\nWn = (V·W + Vf·ρf) / (V + Vf)\nBarite to restore:  Vb = (V + Vf) × (W − Wn) / (ρb − W)",
    function (v, r) {
      var water = owrSplit(v.oil, v.owr1), fo = v.oil / 100, fw = water / 100, owr2 = v.owr2;
      need(owr2 > 0 && owr2 < 100, "Desired OWR must be between 0 and 100.");
      var V = v.v, W = v.w, rb = WD * v.sg, vf, rf, what, up = owr2 >= v.owr1;
      if (up) { vf = V * (fw * owr2 / (100 - owr2) - fo); rf = WD * v.sgo; what = "Base oil to add"; }
      else { vf = V * (fo * (100 - owr2) / owr2 - fw); rf = WD * v.sgw; what = "Water / brine to add"; }
      r.t("Retort water % (calculated)", fmt(water) + " %");
      r.q(what, vf, "volume");
      var wn = (V * W + vf * rf) / (V + vf), v2 = V + vf, bar = 0;
      r.q("Mud density after fluid addition", wn, "density");
      if (v.keep === 0 && W > wn) {
        var vb = v2 * (W - wn) / (rb - W); bar = vb;
        r.barite("Barite to restore mud weight", vb * 42 * rb);
        r.q("Volume increase from barite", vb, "volume");
      }
      var vt = v2 + bar, oilV = fo * V + (up ? vf : 0), watV = fw * V + (up ? 0 : vf);
      r.q("Final mud volume", vt, "volume");
      r.t("New OWR", fmt(100 * oilV / (oilV + watV)) + " / " + fmt(100 * watV / (oilV + watV)));
      r.t("New oil % / water % of mud", fmt(100 * oilV / vt) + " % / " + fmt(100 * watV / vt) + " %");
    });
  add(G2, "Increase water-phase salinity (WPS)", "Salt needed to raise the water phase salinity of an oil mud. WPS in ppm = weight % x 10,000.",
    [F("v", "Mud volume", "volume", 500), F("wat", "Retort water % (by volume)", "percent", 20), CH("salt", "Salt type", 0, SALT3),
     F("p1", "Current WPS", "ppm", 200000), F("p2", "Desired WPS", "ppm", 250000), F("pur", "Salt purity", "percent", 95)],
    "w = WPS / 1,000,000\nMw = V × water%/100 × 42 × 8.345          (lb of water)\nSalt = Mw × [w2/(1 − w2) − w1/(1 − w1)] / purity\nVbrine = Mw / [(1 − w) × 42 × 8.345 × SGbr(w)]",
    function (v, r) {
      var s = getSalt(SALT3[v.salt]), w1 = v.p1 / 1e6, w2 = v.p2 / 1e6;
      need(w2 > w1, "Desired WPS must be higher than current WPS.");
      need(w2 * 100 <= s.max, "Desired WPS exceeds saturation for " + s.name + " (" + fmt(s.max * 1e4) + " ppm).");
      need(v.pur > 0 && v.pur <= 100, "Purity must be 1-100 %.");
      var wl = v.v * v.wat / 100 * 42 * WD, salt = wl * (w2 / (1 - w2) - w1 / (1 - w1));
      var b1 = wl / (1 - w1) / (42 * WD * s.SG(w1 * 100)), b2 = wl / (1 - w2) / (42 * WD * s.SG(w2 * 100));
      r.mass("Salt to add (as received)", salt / (v.pur / 100), saltProduct(s.name));
      r.q("Volume increase (brine)", b2 - b1, "volume");
      r.t("Brine SG: current -> new", fmt(s.SG(w1 * 100)) + " -> " + fmt(s.SG(w2 * 100)));
      var t = s.TCT(w2 * 100); if (isFinite(t)) r.q("Approx. crystallization point (new)", t, "temp");
    });
  function addFluidCalc(name, help, isOil) {
    add(G2, name, help,
      [F("v", "Initial mud volume", "volume", 500), F("w", "Initial mud density", "density", 12), F("owr", "Initial OWR (oil %)", "percent", isOil ? 75 : 80),
       F("oil", "Initial retort oil %", "percent", isOil ? 55 : 60), F("va", isOil ? "Base oil volume added" : "Water volume added", "volume", isOil ? 30 : 20),
       F("sga", isOil ? "SG of base oil" : "SG of water / brine", "sg", isOil ? 0.8 : 1.0)],
      isOil ? "Wnew = (V·W + Vo·ρo) / (V + Vo)\nVoil = fo·V + Vo ;  Vwater = fw·V\nOWR = 100 × Voil / (Voil + Vwater)"
            : "Wnew = (V·W + Vw·ρw) / (V + Vw)\nVoil = fo·V ;  Vwater = fw·V + Vw\nOWR = 100 × Voil / (Voil + Vwater)",
      function (v, r) {
        var water = owrSplit(v.oil, v.owr), V = v.v, va = v.va;
        var oV = v.oil / 100 * V + (isOil ? va : 0), wV = water / 100 * V + (isOil ? 0 : va);
        r.q("New mud density", (V * v.w + va * WD * v.sga) / (V + va), "density");
        r.q("New mud volume", V + va, "volume");
        r.t("Resultant OWR", fmt(100 * oV / (oV + wV)) + " / " + fmt(100 * wV / (oV + wV)));
        r.t("Resultant oil % / water %", fmt(100 * oV / (V + va)) + " % / " + fmt(100 * wV / (V + va)) + " %");
      });
  }
  addFluidCalc("Add a known volume of water to oil mud", "New density, OWR and oil % after a known water (or brine) addition.", false);
  addFluidCalc("Add a known volume of base oil to oil mud", "New density, OWR and oil % after a known base oil addition.", true);
  add(G2, "Suspected water contamination of oil mud", "Estimates the water influx from the drop in mud density (assumes the density drop is caused only by water).",
    [F("w1", "Initial mud density", "density", 12), F("w2", "Contaminated mud density", "density", 11.8), F("v", "Initial mud volume", "volume", 500),
     F("owr", "Initial OWR (oil %)", "percent", 80), F("oil", "Initial retort oil %", "percent", 60), F("sgw", "SG of contaminating water", "sg", 1.0)],
    "Vwater = V × (W1 − W2) / (W2 − ρw)\nOWR = 100 × fo·V / [fo·V + fw·V + Vwater]",
    function (v, r) {
      var water = owrSplit(v.oil, v.owr), rw = WD * v.sgw;
      need(v.w2 < v.w1 && v.w2 > rw, "Contaminated density must be between water and initial density.");
      var vw = v.v * (v.w1 - v.w2) / (v.w2 - rw), oV = v.oil / 100 * v.v, wV = water / 100 * v.v + vw;
      r.q("Volume of water contamination", vw, "volume");
      r.q("Mud volume now", v.v + vw, "volume");
      r.t("Resultant OWR", fmt(100 * oV / (oV + wV)) + " / " + fmt(100 * wV / (oV + wV)));
    });

  // 3. basic mixes & slugs
  add(G3, "Basic mix - water-based mud", "Water and weight material needed to build a simple weighted water mud (no other products).",
    [F("v", "Volume of mud", "volume", 500), F("w", "Mud density", "density", 11), F("sgw", "SG of base water", "sg", 1.0), F("sg", "SG of weight material", "sg", 4.2)],
    "Vb = V × (W − ρw) / (ρb − ρw)\nVwater = V − Vb ;  M = 42 × ρb × Vb",
    function (v, r) {
      var rw = WD * v.sgw, rb = WD * v.sg;
      need(v.w >= rw && v.w < rb, "Mud density must be between water and weight material.");
      var vb = v.v * (v.w - rw) / (rb - rw);
      r.q("Volume of water", v.v - vb, "volume");
      r.q("Volume of weight material", vb, "volume");
      r.barite("Weight of weight material", vb * 42 * rb);
    });
  add(G3, "Basic mix - oil-based mud", "Base oil, water/brine and weight material for a simple oil mud at a set OWR (no other products).",
    [F("v", "Volume of mud", "volume", 500), F("w", "Mud density", "density", 12), F("owr", "OWR (oil %)", "percent", 80), F("sgo", "SG of base oil", "sg", 0.80),
     F("sgw", "SG of water / brine", "sg", 1.0), F("sg", "SG of weight material", "sg", 4.2)],
    "ρliq = fo·ρo + (1 − fo)·ρw          (fo = OWR/100)\nVb = V × (W − ρliq) / (ρb − ρliq)\nVliq = V − Vb ;  Voil = fo·Vliq ;  Vwater = (1 − fo)·Vliq",
    function (v, r) {
      var fo = v.owr / 100; need(fo > 0 && fo <= 1, "OWR must be 1-100.");
      var rl = fo * WD * v.sgo + (1 - fo) * WD * v.sgw, rb = WD * v.sg;
      need(v.w >= rl && v.w < rb, "Mud density is below the liquid density (" + show(rl, "density") + ") or above weight material.");
      var vb = v.v * (v.w - rl) / (rb - rl), vl = v.v - vb;
      r.q("Volume of base oil", vl * fo, "volume");
      r.q("Volume of water / brine", vl * (1 - fo), "volume");
      r.q("Volume of weight material", vb, "volume");
      r.barite("Weight of weight material", vb * 42 * rb);
    });
  add(G3, "Slug - flowback (pit gain) after pumping", "Estimated volume of mud that flows back from the drill pipe after a heavy slug is pumped.",
    [F("w", "Mud density", "density", 10), F("ws", "Slug density", "density", 12), F("vs", "Slug volume", "volume", 25)],
    "Vflowback = Vs × (Ws − W) / W",
    function (v, r) { need(v.ws > v.w && v.w > 0, "Slug must be heavier than the mud."); r.q("Flowback volume of mud", v.vs * (v.ws - v.w) / v.w, "volume"); });
  add(G3, "Slug size (volume for a dry-pipe length)", "Slug volume needed to give the required length of dry pipe.",
    [F("w", "Mud density", "density", 10), F("ws", "Slug density", "density", 12), F("l", "Length of dry pipe required", "depth", 300), F("id", "Drill pipe ID", "diameter", 4.276)],
    "Cp = ID² / 1029.4                (bbl/ft)\nVs = Cp × L × W / (Ws − W)\nSlug length = Vs / Cp",
    function (v, r) {
      need(v.ws > v.w, "Slug must be heavier than the mud.");
      var c = v.id * v.id / 1029.4, vs = c * v.l * v.w / (v.ws - v.w);
      r.q("Drill pipe capacity", c, "capacity"); r.q("Slug volume", vs, "volume");
      r.q("Slug length in pipe", vs / c, "depth"); r.q("Pit gain (flowback)", vs * (v.ws - v.w) / v.w, "volume");
    });
  add(G3, "Slug weight (density for a given slug volume)", "Slug density needed to give the required length of dry pipe with a chosen slug volume.",
    [F("w", "Mud density", "density", 10), F("vs", "Slug volume", "volume", 25), F("l", "Length of dry pipe required", "depth", 300), F("id", "Drill pipe ID", "diameter", 4.276), F("sg", "SG of weight material", "sg", 4.2)],
    "Ls = Vs / Cp ;  Ws = W + L × W / Ls\nMud to slug pit = Vs × (ρb − Ws) / (ρb − W)\nBarite = 42 × ρb × (Vs − mud to slug pit)",
    function (v, r) {
      var c = v.id * v.id / 1029.4; need(v.vs > 0, "Enter slug volume.");
      var ls = v.vs / c, ws = v.w + v.l * v.w / ls, rb = WD * v.sg;
      r.q("Slug density required", ws, "density"); r.q("Slug length in pipe", ls, "depth");
      if (rb > ws) { var sv = v.vs * (rb - ws) / (rb - v.w); r.q("Mud to take into slug pit", sv, "volume"); r.barite("Barite to add to it", (v.vs - sv) * 42 * rb); }
      else r.warn("Slug density exceeds what the weight material can give.");
    });

  // 4. volumes & hydraulics
  add(G4, "Hole, pipe and annular capacity", "Capacities and displacements for one hole / pipe combination.",
    [F("dh", "Hole size or casing ID", "diameter", 8.5), F("od", "Pipe OD", "diameter", 5), F("id", "Pipe ID", "diameter", 4.276), F("l", "Length", "depth", 1000)],
    "Hole    = Dh² / 1029.4\nAnnulus = (Dh² − OD²) / 1029.4\nPipe    = ID² / 1029.4\nSteel (open end) = (OD² − ID²) / 1029.4 ;  closed end = OD² / 1029.4\nVolume = capacity × L",
    function (v, r) {
      need(v.dh > v.od && v.od > v.id, "Need hole > pipe OD > pipe ID.");
      var ch = v.dh * v.dh / 1029.4, ca = (v.dh * v.dh - v.od * v.od) / 1029.4, cp = v.id * v.id / 1029.4;
      r.q("Hole capacity", ch, "capacity"); r.q("Annular capacity", ca, "capacity"); r.q("Pipe capacity", cp, "capacity");
      r.q("Steel displacement (open end)", (v.od * v.od - v.id * v.id) / 1029.4, "capacity"); r.q("Closed-end displacement", v.od * v.od / 1029.4, "capacity");
      r.blank(); r.q("Hole volume over length", ch * v.l, "volume"); r.q("Annular volume over length", ca * v.l, "volume"); r.q("Pipe volume over length", cp * v.l, "volume");
    });
  add(G4, "Well volume & circulation times", "Hole, string and annulus volumes with bottoms-up and full circulation strokes/time. Drill pipe length = TD - HWDP - BHA.",
    [F("shoe", "Casing shoe depth (MD)", "depth", 8000), F("cid", "Casing ID", "diameter", 8.835), F("td", "Total depth (MD)", "depth", 10000), F("dh", "Open hole size", "diameter", 8.5),
     F("wo", "Open hole washout", "percent", 10), F("dpod", "Drill pipe OD", "diameter", 5), F("dpid", "Drill pipe ID", "diameter", 4.276),
     F("hwl", "HWDP length", "depth", 600), F("hwod", "HWDP OD", "diameter", 5), F("hwid", "HWDP ID", "diameter", 3),
     F("bhal", "BHA / drill collar length", "depth", 300), F("bhaod", "BHA OD", "diameter", 6.5), F("bhaid", "BHA ID", "diameter", 2.8125),
     F("surf", "Surface (pit) volume", "volume", 800), F("po", "Pump output", "pumpout", 0.1), F("spm", "Pump speed (SPM)", "none", 100)],
    "Vhole = Ccsg × Lshoe + Coh × (TD − Lshoe) × (1 + washout/100)\nVstring = Σ ID²·L / 1029.4 ;  Vann = Vhole − Σ OD²·L / 1029.4\nStrokes = volume / pump output ;  time = strokes / SPM\nQ (gpm) = 42 × output × SPM",
    function (v, r) {
      var shoe = Math.min(v.shoe, v.td), td = v.td;
      var hole = v.cid * v.cid / 1029.4 * shoe + v.dh * v.dh / 1029.4 * (td - shoe) * (1 + v.wo / 100);
      var dpl = td - v.hwl - v.bhal; need(dpl >= 0, "HWDP + BHA longer than TD.");
      var sc = (v.dpid * v.dpid * dpl + v.hwid * v.hwid * v.hwl + v.bhaid * v.bhaid * v.bhal) / 1029.4;
      var cl = (v.dpod * v.dpod * dpl + v.hwod * v.hwod * v.hwl + v.bhaod * v.bhaod * v.bhal) / 1029.4, ann = hole - cl;
      r.q("Hole volume (no string)", hole, "volume"); r.q("String capacity", sc, "volume"); r.q("String steel displacement", cl - sc, "volume");
      r.q("Annular volume", ann, "volume"); r.q("Hole volume with string in", sc + ann, "volume"); r.q("Total circulating volume (+surface)", sc + ann + v.surf, "volume");
      if (v.po > 0) {
        r.blank();
        var sS = sc / v.po, sB = ann / v.po, sT = (sc + ann + v.surf) / v.po;
        r.n("Surface-to-bit strokes", sS, "stk"); r.n("Bottoms-up strokes", sB, "stk"); r.n("Total circulation strokes", sT, "stk");
        if (v.spm > 0) { r.q("Surface-to-bit time", sS / v.spm, "time"); r.q("Bottoms-up time", sB / v.spm, "time"); r.q("Total circulation time", sT / v.spm, "time"); r.q("Flow rate", v.po * v.spm * 42, "flow"); }
      }
    });
  add(G4, "Pump output", "Mud pump output per stroke and flow rate. For a duplex pump enter the rod diameter.",
    [CH("type", "Pump type", 0, ["Triplex (single acting)", "Duplex (double acting)"]), F("d", "Liner diameter", "diameter", 6), F("s", "Stroke length", "diameter", 12),
     F("rod", "Rod diameter (duplex only)", "diameter", 2.5), F("eff", "Volumetric efficiency", "percent", 95), F("spm", "Pump speed (SPM)", "none", 100)],
    "Triplex: bbl/stk = 0.000243 × D² × S × eff\nDuplex:  bbl/stk = 0.000162 × S × (2D² − d²) × eff\nQ (gpm) = 42 × bbl/stk × SPM",
    function (v, r) {
      var e = v.eff / 100, bps = v.type === 0 ? 0.000243 * v.d * v.d * v.s * e : 0.000162 * v.s * (2 * v.d * v.d - v.rod * v.rod) * e;
      r.q("Pump output", bps, "pumpout"); r.q("Flow rate", bps * v.spm * 42, "flow"); r.n("Output", bps * v.spm, "bbl/min");
    });
  add(G4, "Annular velocity", "Average annular velocity for a flow rate and annulus.",
    [F("q", "Flow rate", "flow", 500), F("dh", "Hole size or casing ID", "diameter", 8.5), F("od", "Pipe OD", "diameter", 5)],
    "AV (ft/min) = 24.51 × Q / (Dh² − OD²)",
    function (v, r) { need(v.dh > v.od, "Hole must be larger than pipe OD."); r.q("Annular velocity", 24.51 * v.q / (v.dh * v.dh - v.od * v.od), "velocity"); });
  add(G4, "Cuttings generated", "Volume and weight of cuttings drilled per hour (gauge hole).",
    [F("dh", "Hole size", "diameter", 12.25), F("rop", "Rate of penetration", "rate", 60), F("sg", "Cuttings SG", "sg", 2.6), F("por", "Porosity", "percent", 0)],
    "Vc (bbl/hr) = Dh² / 1029.4 × ROP × (1 − porosity)\nMc (lb/hr) = 42 × 8.345 × SGc × Vc",
    function (v, r) {
      var vol = v.dh * v.dh / 1029.4 * v.rop * (1 - v.por / 100);
      r.n("Cuttings volume", fromInt("volume", vol), uname("volume") + "/hr");
      r.n("Cuttings weight", fromInt("mass", vol * 42 * WD * v.sg), uname("mass") + "/hr");
    });
  add(G4, "Tank / pit volume", "Volume of a tank and volume per unit height. Dimensions are in depth/length units.",
    [CH("shape", "Tank shape", 0, ["Rectangular", "Vertical cylinder", "Horizontal cylinder"]), F("l", "Length (rect./horizontal)", "depth", 30),
     F("wdt", "Width or diameter", "depth", 10), F("h", "Total height", "depth", 8), F("lev", "Fluid level", "depth", 6)],
    "Rectangular: L × W × H ;  Vertical cylinder: π/4 × D² × H\nHorizontal cylinder at level h:  L × [r²·acos((r−h)/r) − (r−h)·√(2rh − h²)]\nbbl = ft³ / 5.6146",
    function (v, r) {
      var L = v.l, Wd = v.wdt, H = v.h, lev = Math.min(v.lev, v.shape === 2 ? Wd : H), tot, atl, per;
      if (v.shape === 0) { tot = L * Wd * H; atl = L * Wd * lev; per = L * Wd; }
      else if (v.shape === 1) { var a = Math.PI * Wd * Wd / 4; tot = a * H; atl = a * lev; per = a; }
      else { var rr = Wd / 2; tot = Math.PI * rr * rr * L; atl = (rr * rr * Math.acos((rr - lev) / rr) - (rr - lev) * Math.sqrt(Math.max(0, 2 * rr * lev - lev * lev))) * L; per = NaN; }
      r.q("Total tank volume", tot / 5.6146, "volume"); r.q("Volume at fluid level", atl / 5.6146, "volume");
      if (isFinite(per)) { r.n("Volume per unit height", fromInt("volume", per / 5.6146) / fromInt("depth", 1), uname("volume") + "/" + uname("depth")); r.n("Volume per inch", per / 5.6146 / 12, "bbl/in"); }
    });
  add(G4, "ECD & annular pressure loss (Bingham)", "Annular pressure loss for one annular section (Bingham plastic, laminar) and the resulting ECD.",
    [F("w", "Mud density", "density", 10), F("pv", "Plastic viscosity", "visc", 20), F("yp", "Yield point", "yp", 15), F("q", "Flow rate", "flow", 500),
     F("dh", "Hole size or casing ID", "diameter", 8.5), F("od", "Pipe OD", "diameter", 5), F("l", "Section length (MD)", "depth", 10000), F("tvd", "True vertical depth", "depth", 10000)],
    "AV = 24.51 × Q / (Dh² − OD²) ;  v = AV / 60 (ft/s)\nAPL = PV·v·L / [1500 × (Dh − OD)²] + YP·L / [225 × (Dh − OD)]\nECD = W + APL / (0.052 × TVD)",
    function (v, r) {
      var dd = v.dh - v.od; need(dd > 0, "Hole must be larger than pipe OD.");
      var av = 24.51 * v.q / (v.dh * v.dh - v.od * v.od), apl = v.pv * (av / 60) * v.l / (1500 * dd * dd) + v.yp * v.l / (225 * dd);
      r.q("Annular velocity", av, "velocity"); r.q("Annular pressure loss", apl, "pressure"); r.q("ECD", v.w + apl / (0.052 * v.tvd), "density");
      r.line("(Laminar-flow estimate; use a hydraulics program for detailed design.)");
    });

  // 5. pressure
  add(G5, "Hydrostatic pressure", "Hydrostatic pressure and gradient of a mud column.",
    [F("w", "Mud density", "density", 10), F("tvd", "True vertical depth", "depth", 10000)],
    "P = 0.052 × W × TVD\nGradient = 0.052 × W   (psi/ft)",
    function (v, r) { r.q("Hydrostatic pressure", 0.052 * v.w * v.tvd, "pressure"); r.q("Pressure gradient", 0.052 * v.w, "gradient"); });
  add(G5, "Equivalent mud weight / leak-off test", "EMW from a pressure at depth, LOT/FIT result and MAASP.",
    [F("p", "Surface pressure (LOT/FIT) or extra pressure", "pressure", 1500), F("w", "Mud density in hole during test", "density", 10),
     F("tvd", "Shoe / point TVD", "depth", 8000), F("wnow", "Current mud density (for MAASP)", "density", 11)],
    "EMW = Wtest + P / (0.052 × TVD)\nPfrac = 0.052 × EMW × TVD\nMAASP = (EMW − Wcurrent) × 0.052 × TVD",
    function (v, r) {
      need(v.tvd > 0, "Enter TVD.");
      var emw = v.w + v.p / (0.052 * v.tvd);
      r.q("Equivalent mud weight", emw, "density"); r.q("Fracture / test pressure at depth", 0.052 * emw * v.tvd, "pressure"); r.q("MAASP with current mud", (emw - v.wnow) * 0.052 * v.tvd, "pressure");
    });
  add(G5, "Kill sheet basics", "Kill mud weight, circulating pressures, influx gradient and barite for weighting up the active system.",
    [F("omw", "Original mud density", "density", 10), F("sidpp", "SIDPP", "pressure", 500), F("sicp", "SICP", "pressure", 700), F("tvd", "Hole TVD", "depth", 10000),
     F("scr", "Slow circulating rate pressure", "pressure", 600), F("gain", "Pit gain", "volume", 20), F("ca", "Annular capacity around BHA", "capacity", 0.0291),
     F("vol", "Active system volume", "volume", 1200), F("sg", "SG of weight material", "sg", 4.2), F("margin", "Trip / safety margin", "density", 0)],
    "KMW = OMW + SIDPP / (0.052 × TVD) + margin\nICP = SCR + SIDPP ;  FCP = SCR × KMW / OMW\nInflux height h = pit gain / Ca\nInflux gradient = 0.052 × OMW − (SICP − SIDPP) / h\n  < 0.25 gas ;  0.25–0.35 oil/gas ;  > 0.35 salt water\nBarite: Vb = Vactive × (KMW − OMW) / (ρb − KMW)",
    function (v, r) {
      var kmw = v.omw + v.sidpp / (0.052 * v.tvd) + v.margin;
      r.q("Kill mud weight", kmw, "density"); r.q("Initial circulating pressure (ICP)", v.scr + v.sidpp, "pressure"); r.q("Final circulating pressure (FCP)", v.scr * kmw / v.omw, "pressure");
      if (v.ca > 0 && v.gain > 0) {
        var h = v.gain / v.ca, g = 0.052 * v.omw - (v.sicp - v.sidpp) / h;
        r.q("Influx height", h, "depth"); r.q("Influx gradient", g, "gradient");
        r.t("Probable influx", g < 0.25 ? "Gas" : (g < 0.35 ? "Oil / gas mixture" : "Salt water / liquid"));
      }
      var rb = WD * v.sg;
      if (rb > kmw) { var vb = v.vol * (kmw - v.omw) / (rb - kmw); r.barite("Barite to weight up active system", vb * 42 * rb); r.q("Volume increase", vb, "volume"); }
    });

  // 6. brines
  add(G6, "Brine from dry salt", "Salt and water needed to make a single-salt brine, by target density or by weight %.",
    [CH("salt", "Salt type", 0, SALT3), CH("by", "Target given as", 0, ["Density", "Weight %"]), F("w", "Target brine density", "density", 10.5),
     F("wt", "Target weight % salt", "percent", 20), F("v", "Brine volume required", "volume", 500), F("pur", "Salt purity", "percent", 95)],
    "SG = 1.0036 × (0.99707 + A·wt + B·wt²)   (wt solved by bisection for a target density)\nSalt  = 42 × 8.345 × SG × wt/100   lb per bbl brine\nWater = SG × (1 − wt/100)          bbl per bbl brine\nCl⁻ = wt/100 × SG × 10⁶ × Cl fraction ;  salt as received = salt / purity",
    function (v, r) {
      var s = getSalt(SALT3[v.salt]), wt = v.by === 0 ? s.wtFromSG(v.w / WD) : v.wt;
      need(wt > 0, "Target must be heavier than fresh water.");
      need(wt <= s.max + 1e-6, s.name + " saturates at about " + fmt(s.max) + " wt% (" + show(WD * s.SG(s.max), "density") + ").");
      var sg = s.SG(wt), spb = LBSG * sg * wt / 100, wpb = sg * (1 - wt / 100);
      r.t("Weight % salt", fmt(wt) + " %"); r.q("Brine density", WD * sg, "density"); r.t("Chlorides", fmt(s.clFromWt(wt)) + " mg/L");
      r.q("Salt per unit volume of brine", spb, "conc"); r.n("Water per bbl of brine", wpb, "bbl");
      var t = s.TCT(wt); if (isFinite(t)) r.q("Approx. crystallization point", t, "temp");
      r.blank(); r.q("Fresh water required", wpb * v.v, "volume"); r.mass("Salt required (as received)", spb * v.v / (v.pur / 100), saltProduct(s.name));
    });
  add(G6, "Brine blending / dilution", "Volumes of a heavy and a light brine (or water) to make a target density. Assumes volumes are additive.",
    [F("v", "Final volume required", "volume", 500), F("wt", "Target density", "density", 10.5), F("w1", "Heavy brine density", "density", 11.6), F("w2", "Light brine / water density", "density", WD)],
    "Vheavy = V × (Wt − Wlight) / (Wheavy − Wlight)\nVlight = V − Vheavy",
    function (v, r) {
      need(v.w1 > v.wt && v.wt > v.w2, "Target must lie between the two densities.");
      var v1 = v.v * (v.wt - v.w2) / (v.w1 - v.w2);
      r.q("Heavy brine", v1, "volume"); r.q("Light brine / water", v.v - v1, "volume"); r.t("Heavy brine fraction", fmt(100 * v1 / v.v) + " %");
    });
  add(G6, "Chlorides to salt concentration", "Converts a chloride titration to salt weight %, mg/L, brine density and crystallization point.",
    [CH("salt", "Salt type", 1, SALT3), F("cl", "Chlorides of the brine / filtrate", "cl", 60000)],
    "wt = (Cl⁻ / Cl fraction) / (SG(wt) × 10⁴)    (iterated)\nSalt mg/L = Cl⁻ / Cl fraction ;  ppm = wt × 10⁴\nSG = 1.0036 × (0.99707 + A·wt + B·wt²)",
    function (v, r) {
      var s = getSalt(SALT3[v.salt]), wt = s.wtFromCl(v.cl);
      need(wt <= s.max * 1.02, "Chlorides exceed saturation for " + s.name + ".");
      r.t("Weight % salt", fmt(wt) + " %"); r.t("Salt concentration", fmt(v.cl / s.clf) + " mg/L"); r.t("Salinity", fmt(wt * 1e4) + " ppm");
      r.q("Brine density", WD * s.SG(wt), "density"); r.q("Salt per unit volume of brine", LBSG * s.SG(wt) * wt / 100, "conc");
      var t = s.TCT(wt); if (isFinite(t)) r.q("Approx. crystallization point", t, "temp");
    });

  // 7. mud testing
  add(G7, "Rheology from viscometer readings", "PV, YP, low-shear YP and power-law n/K from 600/300/6/3 rpm dial readings.",
    [F("r600", "600 rpm reading", "dial", 60), F("r300", "300 rpm reading", "dial", 38), F("r6", "6 rpm reading", "dial", 9), F("r3", "3 rpm reading", "dial", 7)],
    "PV = θ600 − θ300 ;  YP = θ300 − PV ;  AV = θ600 / 2\nLSYP = 2·θ3 − θ6\nn = 3.32 × log10(θ600 / θ300) ;  K = 510 × θ300 / 511ⁿ  (eq. cP)",
    function (v, r) {
      need(v.r600 > v.r300 && v.r300 > 0, "600 rpm must be higher than 300 rpm.");
      var pv = v.r600 - v.r300, yp = v.r300 - pv, n = 3.32 * Math.log10(v.r600 / v.r300), k = 510 * v.r300 / Math.pow(511, n);
      r.q("Plastic viscosity (PV)", pv, "visc"); r.q("Yield point (YP)", yp, "yp"); r.q("Apparent viscosity", v.r600 / 2, "visc");
      if (v.r3 > 0) r.q("Low-shear yield point (2x3rpm - 6rpm)", 2 * v.r3 - v.r6, "yp");
      r.n("Flow behaviour index n", n, ""); r.n("Consistency index K", k, "eq. cP");
    });
  function solidsTail(r, v, brine, sgb, vs) {
    var asg = (100 * v.w / WD - brine * sgb - v.oil * v.sgo) / vs;
    var lgs = Math.max(0, Math.min(vs, vs * (v.sgh - asg) / (v.sgh - v.sgl)));
    r.n("Average SG of solids", asg, "");
    r.t("Low-gravity solids", fmt(lgs) + " %  = " + show(lgs * 3.5 * v.sgl, "conc"));
    r.t("High-gravity solids", fmt(vs - lgs) + " %  = " + show((vs - lgs) * 3.5 * v.sgh, "conc"));
  }
  add(G7, "Solids analysis - water-based mud", "Corrected solids, low/high gravity solids from mud weight and retort (NaCl salt correction from chlorides).",
    [F("w", "Mud density", "density", 11), F("wat", "Retort water %", "percent", 84), F("oil", "Retort oil %", "percent", 0), F("cl", "Filtrate chlorides", "cl", 20000),
     F("sgo", "SG of oil", "sg", 0.8), F("sgl", "SG of low-gravity solids", "sg", 2.6), F("sgh", "SG of high-gravity solids", "sg", 4.2)],
    "SGbr = 1 + 1.94×10⁻⁶ × Cl^0.95 ;  w = (Cl / 0.6066) / (SGbr × 10⁶)\nVbrine = Vw / [SGbr × (1 − w)] ;  Vs = 100 − Vbrine − Vo\nASG = [100·W/8.345 − Vbrine·SGbr − Vo·SGo] / Vs\nLGS = Vs × (SGh − ASG) / (SGh − SGl) ;  HGS = Vs − LGS\nlb/bbl = 3.5 × SG × vol%",
    function (v, r) {
      var cl = v.cl, sgb = cl > 0 ? 1 + 1.94e-6 * Math.pow(cl, 0.95) : 1, w = cl > 0 ? (cl / 0.6066) / (sgb * 1e6) : 0;
      var brine = v.wat / (sgb * (1 - w)), vs = 100 - brine - v.oil;
      need(vs > 0, "Retort water + oil leave no room for solids.");
      r.n("Brine SG / salt wt%", sgb, "/ " + fmt(w * 100) + " %"); r.t("Corrected brine volume", fmt(brine) + " %"); r.t("Corrected solids", fmt(vs) + " %");
      solidsTail(r, v, brine, sgb, vs);
    });
  add(G7, "Oil mud retort & salinity analysis", "OWR, water phase salinity (CaCl2) from whole-mud chlorides, corrected solids and excess lime.",
    [F("w", "Mud density", "density", 12), F("oil", "Retort oil %", "percent", 60), F("wat", "Retort water %", "percent", 18), F("cl", "Whole mud chlorides", "cl", 40000),
     F("pom", "Pom (ml 0.1N H2SO4)", "none", 4), F("sgo", "SG of base oil", "sg", 0.8), F("sgl", "SG of low-gravity solids", "sg", 2.6), F("sgh", "SG of high-gravity solids", "sg", 4.2)],
    "OWR = 100 × Vo / (Vo + Vw)\nCaCl2 wt% = 100 × 1.565·Clm / (1.565·Clm + 10000·Vw) ;  WPS = wt% × 10⁴\nVbrine = Vw / [SGbr × (1 − w)] ;  Vs = 100 − Vo − Vbrine\nLGS / HGS as WBM solids analysis\nExcess lime = 1.295 × Pom   lb/bbl",
    function (v, r) {
      need(v.oil + v.wat > 0 && v.oil + v.wat < 100, "Check retort values.");
      var owr = 100 * v.oil / (v.oil + v.wat), wtp = v.wat > 0 ? 100 * 1.565 * v.cl / (1.565 * v.cl + 10000 * v.wat) : 0;
      var s = getSalt("CaCl2"), sgb = s.SG(wtp), brine = v.wat / (sgb * (1 - wtp / 100)), vs = 100 - v.oil - brine;
      r.t("OWR", fmt(owr) + " / " + fmt(100 - owr)); r.t("Water phase salinity (CaCl2)", fmt(wtp) + " wt% = " + fmt(wtp * 1e4) + " ppm"); r.n("Brine SG", sgb, "");
      var t = s.TCT(wtp); if (isFinite(t)) r.q("Approx. brine crystallization point", t, "temp");
      r.t("Corrected brine volume", fmt(brine) + " %"); r.t("Corrected solids", fmt(vs) + " %");
      if (vs > 0) solidsTail(r, v, brine, sgb, vs);
      r.q("Excess lime (1.295 x Pom)", 1.295 * v.pom, "conc");
    });

  // ---------------------------------------------------------------- formulation
  var EQ_FORMULATION = "Brine:  OBM w = WPS / 1,000,000;  WBM w from chlorides;  SGbr = 1.0036(0.99707 + A·wt + B·wt²)\nProduct i:  m = C × V ;  v = m / (42 × 8.345 × SG)\nρliq = fo·ρo + (1 − fo)·ρbr          (fo = OWR/100)\nVwm  = [V·W − Σm/42 − (V − Σv)·ρliq] / (ρb − ρliq)\nVliq = V − Σv − Vwm ;  Voil = fo·Vliq ;  Vbrine = Vliq − Voil\nWater = Vbrine × SGbr × (1 − w)\nSalt  = Vbrine × 42 × ρbr × w / purity\nPackages = ceiling(M / package mass) ;  Cost = packages × price";
  var EQ_SALT = "SG = 1.0036 × (0.99707 + A·wt + B·wt²)      wt = salt weight %\nSalt lb/bbl brine = 350.5 × SG × wt/100     Water bbl/bbl brine = SG × (1 − wt/100)\nSalt mg/L = wt/100 × SG × 10⁶               Chlorides mg/L = salt mg/L × Cl fraction\nBrine vol / water vol = 1 / [SG × (1 − wt/100)]     WPS ppm = wt × 10,000";
  var EQ_COMMON = "Units in equations: W ppg, V bbl, M lb, L/TVD ft, D in, P psi, Q gpm, C lb/bbl\nρw = 8.345 ppg (fresh water)   ρb = 8.345 × SG(weight material)   ρo = 8.345 × SG(oil)\n1 bbl = 42 gal;  mass of weight material M = 42 × ρb × Vb";

  function formulate(obm, V, W, oilPct, saltName, salinity, purity, sgWM, sgOil, lines) {
    var r = new Res();
    r.title((obm ? "Oil" : "Water") + "-based mud formulation from scratch");
    r.line("To mix " + show(V, "volume") + " of " + show(W, "density") + " mud" + (obm ? " with OWR " + fmt(oilPct) + "/" + fmt(100 - oilPct) : (oilPct > 0 ? " with " + fmt(oilPct) + " % oil in liquid" : "")));
    need(V > 0, "Enter a volume to mix."); need(oilPct >= 0 && oilPct <= 100, "Oil % must be 0-100."); need(purity > 0 && purity <= 100, "Salt purity must be 1-100 %.");
    var w = 0, sgBr = 1.0, salt = null;
    if (saltName === "Seawater") sgBr = 1.025;
    else if (saltName && salinity > 0) {
      salt = getSalt(saltName);
      var wt = obm ? salinity / 1e4 : salt.wtFromCl(salinity);
      need(wt <= salt.max + 1e-6, "Salinity is above saturation for " + salt.name + " (max " + fmt(salt.max * 1e4) + " ppm WPS / " + fmt(salt.clFromWt(salt.max)) + " mg/L Cl).");
      w = wt / 100; sgBr = salt.SG(wt);
    }
    var rBr = WD * sgBr, rOil = WD * sgOil, rWM = WD * sgWM, fo = oilPct / 100, rLiq = fo * rOil + (1 - fo) * rBr, sumM = 0, sumV = 0;
    lines.forEach(function (l) { if (l.conc > 0) { var m = l.conc * V; sumM += m; sumV += m / (42 * WD * (+l.p.sg || 1)); } });
    var vWM = (V * W - sumM / 42 - (V - sumV) * rLiq) / (rWM - rLiq);
    if (vWM < 0) { r.warn("No weight material needed: the products and liquids alone give " + show((sumM / 42 + (V - sumV) * rLiq) / V, "density") + "."); vWM = 0; }
    var vLiq = V - sumV - vWM; need(vLiq > 0, "Products and weight material take up the whole volume - density too high.");
    var vOil = fo * vLiq, vBrine = vLiq - vOil, vWater = vBrine * sgBr * (1 - w), saltLb = vBrine * 42 * rBr * w / (purity / 100);
    r.blank(); r.title("Basic requirements");
    if (fo > 0) r.q("Base oil", vOil, "volume");
    r.q(saltName === "Seawater" ? "Seawater" : "Water", vWater, "volume");
    if (salt) {
      r.q("Brine volume", vBrine, "volume"); r.t("Brine salt / SG", salt.name + " " + fmt(w * 100) + " wt% / " + fmt(sgBr));
      var t = salt.TCT(w * 100); if (isFinite(t)) r.q("Approx. brine crystallization point", t, "temp");
    }
    r.q("Weight material volume", vWM, "volume");
    r.blank(); r.title("Total amount of each product");
    var cost = 0;
    function line(name, lb, p) {
      if (lb <= 0) return;
      r.t(name, show(lb, "mass"));
      if (p && pkgLb(p) > 0) {
        var n = Math.ceil(lb / pkgLb(p) - 1e-9), pk = n.toLocaleString("en-US") + " x " + pkgText(p);
        if (+p.price > 0) { var c = n * p.price; cost += c; pk += ", cost " + c.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
        r.t("   packages", pk);
      }
    }
    if (salt) line("Salt " + salt.name + " (as received)", saltLb, saltProduct(salt.name));
    lines.forEach(function (l) { if (l.conc > 0) line(l.p.name, l.conc * V, l.p); });
    line("Weight material", vWM * 42 * rWM, weightMaterial());
    if (vWM > 0) r.n("   weight material in tonnes", vWM * 42 * rWM / 2204.62, "MT");
    r.blank(); r.title("Check");
    r.t("Products + weight material volume", fmt(100 * (sumV + vWM) / V) + " % of mud");
    if (fo > 0) r.t("Oil % / brine % of mud", fmt(100 * vOil / V) + " % / " + fmt(100 * vBrine / V) + " %");
    if (cost > 0) { r.t("Total product cost", cost.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })); r.t("Cost per unit volume", (cost / fromInt("volume", V)).toFixed(2) + " per " + uname("volume")); }
    r.blank(); r.line("All amounts are approximate.");
    return r.text();
  }

  function run(calc, inputs) {
    var r = new Res(); r.title("Results");
    try { calc.fn(inputs, r); }
    catch (e) { if (e instanceof CalcError) r.warn(e.message); else r.warn("Calculation error: " + e.message); }
    return r.text();
  }

  return {
    WD: WD, LBSG: LBSG, UNITS: UNITS, sel: sel, uname: uname, toInt: toInt, fromInt: fromInt, fmt: fmt, fmtIn: fmtIn, show: show, parseNum: parseNum, preset: preset,
    SALTS: SALTS, getSalt: getSalt, BRINES: BRINES, PRODUCT_COLS: PRODUCT_COLS, DEFAULT_PRODUCTS: DEFAULT_PRODUCTS,
    get products() { return products; }, set products(p) { products = p; }, pkgLb: pkgLb, pkgText: pkgText, weightMaterial: weightMaterial,
    CALCS: CALCS, GROUPS: [G1, G2, G3, G4, G5, G6, G7], run: run, formulate: formulate, CalcError: CalcError, Res: Res, pad: pad,
    EQ_FORMULATION: EQ_FORMULATION, EQ_SALT: EQ_SALT, EQ_COMMON: EQ_COMMON
  };
})();
if (typeof module !== "undefined") module.exports = MC;
