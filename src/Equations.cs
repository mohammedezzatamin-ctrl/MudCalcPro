// Mud Calculator Pro - equations shown on each calculation page and in printed results.
// Internal units: ppg, bbl, lb, ft, in, psi, gpm, lb/bbl.  ρw = 8.345 ppg, ρb = 8.345 x SG(weight material).
using System.Collections.Generic;

namespace MudCalcPro
{
    public static class Equations
    {
        public const string Common =
            "Units in equations: W ppg, V bbl, M lb, L/TVD ft, D in, P psi, Q gpm, C lb/bbl\r\n" +
            "ρw = 8.345 ppg (fresh water)   ρb = 8.345 × SG(weight material)   ρo = 8.345 × SG(oil)\r\n" +
            "1 bbl = 42 gal;  mass of weight material M = 42 × ρb × Vb";

        public const string Formulation =
            "Brine:  OBM w = WPS / 1,000,000;  WBM w from chlorides;  SGbr = 1.0036(0.99707 + A·wt + B·wt²)\r\n" +
            "Product i:  m = C × V ;  v = m / (42 × 8.345 × SG)\r\n" +
            "ρliq = fo·ρo + (1 − fo)·ρbr          (fo = OWR/100)\r\n" +
            "Vwm  = [V·W − Σm/42 − (V − Σv)·ρliq] / (ρb − ρliq)\r\n" +
            "Vliq = V − Σv − Vwm ;  Voil = fo·Vliq ;  Vbrine = Vliq − Voil\r\n" +
            "Water = Vbrine × SGbr × (1 − w)\r\n" +
            "Salt  = Vbrine × 42 × ρbr × w / purity\r\n" +
            "Packages = ceiling(M / package mass) ;  Cost = packages × price";

        public const string SaltTable =
            "SG = 1.0036 × (0.99707 + A·wt + B·wt²)      wt = salt weight %\r\n" +
            "Salt lb/bbl brine = 350.5 × SG × wt/100     Water bbl/bbl brine = SG × (1 − wt/100)\r\n" +
            "Salt mg/L = wt/100 × SG × 10⁶               Chlorides mg/L = salt mg/L × Cl fraction\r\n" +
            "Brine vol / water vol = 1 / [SG × (1 − wt/100)]     WPS ppm = wt × 10,000";

        public static readonly Dictionary<string, string> ByCalc = new Dictionary<string, string>
        {
            { "Weight up (volume increase)",
              "Vb = V1 × (W2 − W1) / (ρb − W2)\r\n" +
              "M  = 42 × ρb × Vb          lb/bbl = M / V1\r\n" +
              "Vfinal = V1 + Vb\r\n" +
              "(SG 4.2:  lb/bbl = 1470 × (W2 − W1) / (35 − W2))" },
            { "Weight up (constant volume)",
              "Vkeep = V × (ρb − W2) / (ρb − W1)\r\n" +
              "Vdump = V − Vkeep\r\n" +
              "M = 42 × ρb × Vdump" },
            { "Cut mud weight (volume increase)",
              "Vd = V1 × (W1 − W2) / (W2 − Wd)\r\n" +
              "Vfinal = V1 + Vd" },
            { "Cut mud weight (constant volume)",
              "Vkeep = V × (W2 − Wd) / (W1 − Wd)\r\n" +
              "Vdump = Vadd = V − Vkeep" },
            { "Mix two or three muds",
              "Vmix = V1 + V2 + V3\r\n" +
              "Wmix = (W1·V1 + W2·V2 + W3·V3) / Vmix" },
            { "Add a known weight of barite",
              "Vb = M / (42 × ρb)\r\n" +
              "Wnew = (42 × W1 × V1 + M) / [42 × (V1 + Vb)]" },
            { "Barite to raise density of a pill / system (per unit volume)",
              "C = 42 × ρb × (W2 − W1) / (ρb − W2)   lb/bbl\r\n" +
              "Volume increase = (W2 − W1) / (ρb − W2)   bbl/bbl\r\n" +
              "Sacks (100 lb) per 100 bbl = C" },
            { "Adjust OWR (add oil or water)",
              "fo = oil%/100 ;  fw = fo × (100 − OWR1) / OWR1\r\n" +
              "Raise OWR:  Voil   = V × [fw × OWR2/(100 − OWR2) − fo]\r\n" +
              "Lower OWR:  Vwater = V × [fo × (100 − OWR2)/OWR2 − fw]\r\n" +
              "Wn = (V·W + Vf·ρf) / (V + Vf)\r\n" +
              "Barite to restore:  Vb = (V + Vf) × (W − Wn) / (ρb − W)" },
            { "Increase water-phase salinity (WPS)",
              "w = WPS / 1,000,000\r\n" +
              "Mw = V × water%/100 × 42 × 8.345          (lb of water)\r\n" +
              "Salt = Mw × [w2/(1 − w2) − w1/(1 − w1)] / purity\r\n" +
              "Vbrine = Mw / [(1 − w) × 42 × 8.345 × SGbr(w)]" },
            { "Add a known volume of water to oil mud",
              "Wnew = (V·W + Vw·ρw) / (V + Vw)\r\n" +
              "Voil = fo·V ;  Vwater = fw·V + Vw\r\n" +
              "OWR = 100 × Voil / (Voil + Vwater)" },
            { "Add a known volume of base oil to oil mud",
              "Wnew = (V·W + Vo·ρo) / (V + Vo)\r\n" +
              "Voil = fo·V + Vo ;  Vwater = fw·V\r\n" +
              "OWR = 100 × Voil / (Voil + Vwater)" },
            { "Suspected water contamination of oil mud",
              "Vwater = V × (W1 − W2) / (W2 − ρw)\r\n" +
              "OWR = 100 × fo·V / [fo·V + fw·V + Vwater]" },
            { "Basic mix - water-based mud",
              "Vb = V × (W − ρw) / (ρb − ρw)\r\n" +
              "Vwater = V − Vb ;  M = 42 × ρb × Vb" },
            { "Basic mix - oil-based mud",
              "ρliq = fo·ρo + (1 − fo)·ρw          (fo = OWR/100)\r\n" +
              "Vb = V × (W − ρliq) / (ρb − ρliq)\r\n" +
              "Vliq = V − Vb ;  Voil = fo·Vliq ;  Vwater = (1 − fo)·Vliq" },
            { "Slug - flowback (pit gain) after pumping",
              "Vflowback = Vs × (Ws − W) / W" },
            { "Slug size (volume for a dry-pipe length)",
              "Cp = ID² / 1029.4                (bbl/ft)\r\n" +
              "Vs = Cp × L × W / (Ws − W)\r\n" +
              "Slug length = Vs / Cp" },
            { "Slug weight (density for a given slug volume)",
              "Ls = Vs / Cp ;  Ws = W + L × W / Ls\r\n" +
              "Mud to slug pit = Vs × (ρb − Ws) / (ρb − W)\r\n" +
              "Barite = 42 × ρb × (Vs − mud to slug pit)" },
            { "Hole, pipe and annular capacity",
              "Hole    = Dh² / 1029.4\r\n" +
              "Annulus = (Dh² − OD²) / 1029.4\r\n" +
              "Pipe    = ID² / 1029.4\r\n" +
              "Steel (open end) = (OD² − ID²) / 1029.4 ;  closed end = OD² / 1029.4\r\n" +
              "Volume = capacity × L" },
            { "Well volume & circulation times",
              "Vhole = Ccsg × Lshoe + Coh × (TD − Lshoe) × (1 + washout/100)\r\n" +
              "Vstring = Σ ID²·L / 1029.4 ;  Vann = Vhole − Σ OD²·L / 1029.4\r\n" +
              "Strokes = volume / pump output ;  time = strokes / SPM\r\n" +
              "Q (gpm) = 42 × output × SPM" },
            { "Pump output",
              "Triplex: bbl/stk = 0.000243 × D² × S × eff\r\n" +
              "Duplex:  bbl/stk = 0.000162 × S × (2D² − d²) × eff\r\n" +
              "Q (gpm) = 42 × bbl/stk × SPM" },
            { "Annular velocity",
              "AV (ft/min) = 24.51 × Q / (Dh² − OD²)" },
            { "Cuttings generated",
              "Vc (bbl/hr) = Dh² / 1029.4 × ROP × (1 − porosity)\r\n" +
              "Mc (lb/hr) = 42 × 8.345 × SGc × Vc" },
            { "Tank / pit volume",
              "Rectangular: L × W × H ;  Vertical cylinder: π/4 × D² × H\r\n" +
              "Horizontal cylinder at level h:  L × [r²·acos((r−h)/r) − (r−h)·√(2rh − h²)]\r\n" +
              "bbl = ft³ / 5.6146" },
            { "ECD & annular pressure loss (Bingham)",
              "AV = 24.51 × Q / (Dh² − OD²) ;  v = AV / 60 (ft/s)\r\n" +
              "APL = PV·v·L / [1500 × (Dh − OD)²] + YP·L / [225 × (Dh − OD)]\r\n" +
              "ECD = W + APL / (0.052 × TVD)" },
            { "Hydrostatic pressure",
              "P = 0.052 × W × TVD\r\n" +
              "Gradient = 0.052 × W   (psi/ft)" },
            { "Equivalent mud weight / leak-off test",
              "EMW = Wtest + P / (0.052 × TVD)\r\n" +
              "Pfrac = 0.052 × EMW × TVD\r\n" +
              "MAASP = (EMW − Wcurrent) × 0.052 × TVD" },
            { "Kill sheet basics",
              "KMW = OMW + SIDPP / (0.052 × TVD) + margin\r\n" +
              "ICP = SCR + SIDPP ;  FCP = SCR × KMW / OMW\r\n" +
              "Influx height h = pit gain / Ca\r\n" +
              "Influx gradient = 0.052 × OMW − (SICP − SIDPP) / h\r\n" +
              "  < 0.25 gas ;  0.25–0.35 oil/gas ;  > 0.35 salt water\r\n" +
              "Barite: Vb = Vactive × (KMW − OMW) / (ρb − KMW)" },
            { "Brine from dry salt",
              "SG = 1.0036 × (0.99707 + A·wt + B·wt²)   (wt solved by bisection for a target density)\r\n" +
              "Salt  = 42 × 8.345 × SG × wt/100   lb per bbl brine\r\n" +
              "Water = SG × (1 − wt/100)          bbl per bbl brine\r\n" +
              "Cl⁻ = wt/100 × SG × 10⁶ × Cl fraction ;  salt as received = salt / purity" },
            { "Brine blending / dilution",
              "Vheavy = V × (Wt − Wlight) / (Wheavy − Wlight)\r\n" +
              "Vlight = V − Vheavy" },
            { "Chlorides to salt concentration",
              "wt = (Cl⁻ / Cl fraction) / (SG(wt) × 10⁴)    (iterated)\r\n" +
              "Salt mg/L = Cl⁻ / Cl fraction ;  ppm = wt × 10⁴\r\n" +
              "SG = 1.0036 × (0.99707 + A·wt + B·wt²)" },
            { "Rheology from viscometer readings",
              "PV = θ600 − θ300 ;  YP = θ300 − PV ;  AV = θ600 / 2\r\n" +
              "LSYP = 2·θ3 − θ6\r\n" +
              "n = 3.32 × log10(θ600 / θ300) ;  K = 510 × θ300 / 511ⁿ  (eq. cP)" },
            { "Solids analysis - water-based mud",
              "SGbr = 1 + 1.94×10⁻⁶ × Cl^0.95 ;  w = (Cl / 0.6066) / (SGbr × 10⁶)\r\n" +
              "Vbrine = Vw / [SGbr × (1 − w)] ;  Vs = 100 − Vbrine − Vo\r\n" +
              "ASG = [100·W/8.345 − Vbrine·SGbr − Vo·SGo] / Vs\r\n" +
              "LGS = Vs × (SGh − ASG) / (SGh − SGl) ;  HGS = Vs − LGS\r\n" +
              "lb/bbl = 3.5 × SG × vol%" },
            { "Oil mud retort & salinity analysis",
              "OWR = 100 × Vo / (Vo + Vw)\r\n" +
              "CaCl2 wt% = 100 × 1.565·Clm / (1.565·Clm + 10000·Vw) ;  WPS = wt% × 10⁴\r\n" +
              "Vbrine = Vw / [SGbr × (1 − w)] ;  Vs = 100 − Vo − Vbrine\r\n" +
              "LGS / HGS as WBM solids analysis\r\n" +
              "Excess lime = 1.295 × Pom   lb/bbl" },
        };

        public static string For(Calc c)
        {
            string s;
            return ByCalc.TryGetValue(c.Name, out s) ? s : "";
        }
    }
}
