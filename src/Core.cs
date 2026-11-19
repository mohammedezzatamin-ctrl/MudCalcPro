// Mud Calculator Pro - core: units, data files, salts, products, printing
// Target: .NET Framework 4.x, C# 5 (compiles with the csc.exe built into Windows)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MudCalcPro
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Data.Init(); }
            catch (Exception ex) { MessageBox.Show("Could not load data files:\r\n" + ex.Message, "Mud Calculator Pro"); }
            Application.Run(new MainForm());
        }
    }

    // ------------------------------------------------------------------ UNITS
    public enum UK { None, Density, Volume, Mass, Depth, Diameter, Pressure, Flow, Conc, Percent, Temp, Visc, YieldPt, Chlorides, Velocity, Capacity, Time, Gradient, SG, PumpOut, Ppm, Dial, Rate }

    public class UnitOpt
    {
        public string Name; public double Factor; public double Offset;
        public UnitOpt(string n, double f, double o) { Name = n; Factor = f; Offset = o; }
    }

    // Internal (calculation) units are oilfield: ppg, bbl, lb, ft, in, psi, gpm, lb/bbl, degF, ft/min, bbl/ft, min.
    // internal = display * Factor + Offset
    public static class Units
    {
        public static Dictionary<UK, UnitOpt[]> Opts = new Dictionary<UK, UnitOpt[]>();
        public static Dictionary<UK, int> Sel = new Dictionary<UK, int>();
        public static Dictionary<UK, string> KindName = new Dictionary<UK, string>();
        static Dictionary<UK, int> metric = new Dictionary<UK, int>();

        static void A(UK k, string kindName, int metricIdx, params UnitOpt[] o) { Opts[k] = o; Sel[k] = 0; KindName[k] = kindName; metric[k] = metricIdx; }
        static UnitOpt U(string n, double f) { return new UnitOpt(n, f, 0); }

        static Units()
        {
            A(UK.None, "", 0, U("", 1));
            A(UK.Density, "Density / mud weight", 1, U("ppg", 1), U("SG", 8.345), U("kg/m³", 0.008345), U("psi/1000ft", 1.0 / 52.0), U("lb/ft³", 1.0 / 7.48052));
            A(UK.Volume, "Volume", 1, U("bbl", 1), U("m³", 6.28981), U("litre", 0.00628981), U("US gal", 1.0 / 42.0));
            A(UK.Mass, "Mass", 1, U("lb", 1), U("kg", 2.20462), U("MT", 2204.62));
            A(UK.Depth, "Depth / length", 1, U("ft", 1), U("m", 3.28084));
            A(UK.Diameter, "Diameter", 0, U("in", 1), U("mm", 1.0 / 25.4));
            A(UK.Pressure, "Pressure", 1, U("psi", 1), U("bar", 14.5038), U("kPa", 0.145038), U("kg/cm²", 14.2233));
            A(UK.Flow, "Flow rate", 1, U("gpm", 1), U("L/min", 0.264172), U("bbl/min", 42), U("m³/min", 264.172));
            A(UK.Conc, "Concentration", 1, U("ppb", 1), U("kg/m³", 1.0 / 2.853));
            A(UK.Percent, "", 0, U("%", 1));
            A(UK.Temp, "Temperature", 1, new UnitOpt("°F", 1, 0), new UnitOpt("°C", 1.8, 32));
            A(UK.Visc, "Viscosity", 0, U("cP", 1), U("mPa·s", 1));
            A(UK.YieldPt, "Yield point / gels", 1, U("lb/100ft²", 1), U("Pa", 1.0 / 0.4788));
            A(UK.Chlorides, "", 0, U("mg/L", 1));
            A(UK.Velocity, "Annular velocity", 1, U("ft/min", 1), U("m/min", 3.28084));
            A(UK.Capacity, "Capacity", 1, U("bbl/ft", 1), U("m³/m", 1.917134), U("L/m", 0.001917134));
            A(UK.Time, "Time", 0, U("min", 1), U("hr", 60));
            A(UK.Gradient, "Pressure gradient", 1, U("psi/ft", 1), U("kPa/m", 0.0442075), U("bar/m", 4.42075));
            A(UK.SG, "", 0, U("SG", 1));
            A(UK.PumpOut, "Pump output", 1, U("bbl/stk", 1), U("L/stk", 0.00628981), U("gal/stk", 1.0 / 42.0));
            A(UK.Ppm, "", 0, U("ppm", 1));
            A(UK.Dial, "", 0, U("dial", 1));
            A(UK.Rate, "Drilling rate", 1, U("ft/hr", 1), U("m/hr", 3.28084));
        }

        public static UnitOpt Cur(UK k) { return Opts[k][Sel[k]]; }
        public static string Name(UK k) { return Cur(k).Name; }
        public static double ToInt(UK k, double disp) { UnitOpt u = Cur(k); return disp * u.Factor + u.Offset; }
        public static double FromInt(UK k, double v) { UnitOpt u = Cur(k); return (v - u.Offset) / u.Factor; }
        public static string Show(double vInt, UK k) { string n = Name(k); return Fmt(FromInt(k, vInt)) + (n.Length > 0 ? " " + n : ""); }

        public static void Preset(bool useMetric)
        {
            foreach (UK k in Opts.Keys.ToList()) Sel[k] = useMetric ? metric[k] : 0;
            Data.SaveSettings();
        }

        public static string Fmt(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "-";
            double a = Math.Abs(v);
            if (a == 0) return "0";
            if (a >= 10000) return v.ToString("#,##0");
            if (a >= 100) return v.ToString("#,##0.0");
            if (a >= 1) return v.ToString("0.00");
            if (a >= 0.01) return v.ToString("0.0000");
            return v.ToString("0.000000");
        }
        public static string FmtIn(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return Math.Round(v, 4).ToString("0.####");
        }
        public static bool TryParse(string s, out double v)
        {
            s = (s ?? "").Trim().Replace(" ", "");
            if (s.Length == 0) { v = 0; return true; }
            if (s.Contains(",") && !s.Contains(".") && s.Count(ch => ch == ',') == 1 && s.Length - s.IndexOf(',') - 1 != 3)
                s = s.Replace(",", ".");              // "12,5" typed as decimal comma
            else s = s.Replace(",", "");               // "250,000" thousands separator
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }

    // ------------------------------------------------------------------ SALTS
    public class Salt
    {
        public string Name; public double A, B, ClFrac, MaxWt, SGsolid;
        public double[] TW = new double[0], TF = new double[0];   // crystallization curve: wt% -> degF

        // Brine specific gravity (68 degF) as a function of salt weight percent.
        public double SG(double wt) { return 1.0036 * (0.99707 + A * wt + B * wt * wt); }

        public double TCT(double wt)
        {
            if (TW.Length == 0) return double.NaN;
            if (wt <= TW[0]) return TF[0];
            for (int i = 1; i < TW.Length; i++)
                if (wt <= TW[i]) return TF[i - 1] + (TF[i] - TF[i - 1]) * (wt - TW[i - 1]) / (TW[i] - TW[i - 1]);
            return double.NaN;
        }
        public double WtFromSG(double sg)
        {
            double lo = 0, hi = MaxWt * 1.2;
            for (int i = 0; i < 80; i++) { double m = (lo + hi) / 2; if (SG(m) < sg) lo = m; else hi = m; }
            return (lo + hi) / 2;
        }
        // chlorides mg/L of the brine -> salt wt%
        public double WtFromCl(double cl)
        {
            double w = 0;
            for (int i = 0; i < 60; i++) w = (cl / ClFrac) / (SG(w) * 1e4);
            return w;
        }
        public double ClFromWt(double wt) { return wt / 100.0 * SG(wt) * 1e6 * ClFrac; }
    }

    // ------------------------------------------------------------------ PRODUCTS
    public class Product
    {
        public string Name = "", Category = "", System = "Both", PkgUnit = "kg";
        public double SG = 1, PkgSize = 25, Price = 0, ConcWBM = 0, ConcOBM = 0;

        public double PackageLb()
        {
            string u = (PkgUnit ?? "").Trim().ToLowerInvariant();
            if (u == "lb" || u == "lbs") return PkgSize;
            if (u == "kg") return PkgSize * 2.20462;
            if (u == "mt" || u == "t" || u == "tonne") return PkgSize * 2204.62;
            if (u == "gal" || u == "usgal") return PkgSize * 8.345 * SG;
            if (u == "l" || u == "litre" || u == "liter") return PkgSize * 0.264172 * 8.345 * SG;
            if (u == "bbl") return PkgSize * 42 * 8.345 * SG;
            if (u == "drum") return 55 * 8.345 * SG;
            return PkgSize * 2.20462;
        }
        public string PackageText() { return Units.FmtIn(PkgSize) + " " + PkgUnit; }
    }

    // ------------------------------------------------------------------ DATA FILES
    public static class Data
    {
        public const string Version = "2.0";
        public static string Dir;
        public static List<Salt> Salts = new List<Salt>();
        public static List<Product> Products = new List<Product>();
        public static string[] ReportFields = {
            "Date","Rig","Well","Operator","Mud engineer","Mud system","Depth MD","Depth TVD","Hole size","Casing shoe","Activity",
            "Mud weight","Funnel visc (s/qt)","PV (cP)","YP","Gels 10s/10m/30m","600/300","6/3","API fluid loss (ml)","HTHP fluid loss (ml)","HTHP temp",
            "Cake (1/32 in)","pH","Pm","Pf/Mf","Chlorides (mg/L)","Hardness Ca (mg/L)","MBT (ppb)","Solids %","Oil %","Water %","Sand %",
            "OWR","ES (V)","WPS (ppm)","Excess lime (ppb)","LGS %","HGS %","Active volume","Reserve volume","Surface losses","Downhole losses",
            "Volume built","Products used","Remarks" };
        public static List<Dictionary<string, string>> Reports = new List<Dictionary<string, string>>();

        public static string F(string name) { return Path.Combine(Dir, name); }

        public static void Init()
        {
            Dir = Path.Combine(Application.StartupPath, "data");
            Directory.CreateDirectory(Dir);
            if (!File.Exists(F("salts.csv"))) File.WriteAllText(F("salts.csv"), DefaultSalts, Encoding.UTF8);
            if (!File.Exists(F("products.csv"))) File.WriteAllText(F("products.csv"), DefaultProducts, Encoding.UTF8);
            LoadSalts(); LoadProducts(); LoadSettings(); LoadReports();
        }

        // --- CSV helpers
        public static List<string[]> ReadCsv(string path)
        {
            var rows = new List<string[]>();
            if (!File.Exists(path)) return rows;
            string text = File.ReadAllText(path, Encoding.UTF8);
            var cur = new List<string>(); var sb = new StringBuilder(); bool q = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (q)
                {
                    if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; } else q = false; }
                    else sb.Append(c);
                }
                else if (c == '"') q = true;
                else if (c == ',') { cur.Add(sb.ToString()); sb.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n') { cur.Add(sb.ToString()); sb.Clear(); if (cur.Count > 1 || cur[0].Length > 0) rows.Add(cur.ToArray()); cur = new List<string>(); }
                else sb.Append(c);
            }
            cur.Add(sb.ToString());
            if (cur.Count > 1 || cur[0].Length > 0) rows.Add(cur.ToArray());
            return rows;
        }
        public static string CsvCell(string s)
        {
            s = s ?? "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
        public static void WriteCsv(string path, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(CsvCell)));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        static double D(string s) { double v; return Units.TryParse(s, out v) ? v : 0; }
        static string S(double v) { return v.ToString("0.########", CultureInfo.InvariantCulture); }

        // --- salts
        const string DefaultSalts =
"Name,A,B,ClFraction,MaxWtPct,SGsolid,CrystallizationCurve(wt%:degF)\r\n" +
"CaCl2,0.007923,0.00004964,0.6389,40.0,2.15,0:32;5:27.7;10:20;15:12.2;20:-4;25:-21;29.8:-60;30:-52;32.5:-4;35:14;37.3:32;40:61\r\n" +
"NaCl,0.006504,0.00004395,0.6066,26.4,2.165,0:32;5:26.7;10:20.2;15:12.4;20:2.4;23.3:-6;26.29:32.3;26.4:60\r\n" +
"KCl,0.0059,0.00003415,0.4756,24.0,1.98,0:32;4:28.7;8:25.2;12:21.4;16:17.3;19.5:12.7;21:22;24.5:60\r\n";

        public static void LoadSalts()
        {
            Salts.Clear();
            var rows = ReadCsv(F("salts.csv"));
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i]; if (r.Length < 6) continue;
                var s = new Salt { Name = r[0].Trim(), A = D(r[1]), B = D(r[2]), ClFrac = D(r[3]), MaxWt = D(r[4]), SGsolid = D(r[5]) };
                if (r.Length > 6 && r[6].Trim().Length > 0)
                {
                    var w = new List<double>(); var t = new List<double>();
                    foreach (string p in r[6].Split(';'))
                    {
                        var kv = p.Split(':'); if (kv.Length != 2) continue;
                        w.Add(D(kv[0])); t.Add(D(kv[1]));
                    }
                    s.TW = w.ToArray(); s.TF = t.ToArray();
                }
                Salts.Add(s);
            }
        }
        public static Salt GetSalt(string name)
        {
            foreach (var s in Salts) if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        // --- products
        public static readonly string[] ProductCols = { "Name", "Category", "System", "SG", "PackageSize", "PackageUnit", "PricePerPackage", "ConcWBM_ppb", "ConcOBM_ppb" };
        const string DefaultProducts =
"Name,Category,System,SG,PackageSize,PackageUnit,PricePerPackage,ConcWBM_ppb,ConcOBM_ppb\r\n" +
"Barite (API),Weighting,Both,4.2,50,kg,0,0,0\r\n" +
"Bentonite (API),Viscosifier,WBM,2.6,50,kg,0,0,0\r\n" +
"Xanthan gum,Viscosifier,WBM,1.5,25,kg,0,1,0\r\n" +
"PAC-LV,Fluid loss,WBM,1.6,25,kg,0,2,0\r\n" +
"PAC-R,Fluid loss,WBM,1.6,25,kg,0,1,0\r\n" +
"Modified starch,Fluid loss,WBM,1.5,25,kg,0,4,0\r\n" +
"Caustic soda,Alkalinity,WBM,2.13,25,kg,0,0.5,0\r\n" +
"Soda ash,Alkalinity,WBM,2.53,50,kg,0,0.25,0\r\n" +
"Lime,Alkalinity,Both,2.24,25,kg,0,0,6\r\n" +
"Calcium carbonate (fine),Bridging,Both,2.7,25,kg,0,10,10\r\n" +
"Primary emulsifier,Emulsifier,OBM,0.94,55,gal,0,0,8\r\n" +
"Wetting agent,Emulsifier,OBM,0.95,55,gal,0,0,2\r\n" +
"Organophilic clay,Viscosifier,OBM,1.7,25,kg,0,0,6\r\n" +
"Fluid loss additive (gilsonite),Fluid loss,OBM,1.05,25,kg,0,0,6\r\n" +
"Calcium chloride CaCl2 (95%),Salt,Both,2.15,25,kg,0,0,0\r\n" +
"Sodium chloride NaCl,Salt,Both,2.165,50,kg,0,0,0\r\n" +
"Potassium chloride KCl,Salt,Both,1.98,50,kg,0,0,0\r\n";

        public static void LoadProducts()
        {
            Products.Clear();
            var rows = ReadCsv(F("products.csv"));
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i]; if (r.Length < 6 || r[0].Trim().Length == 0) continue;
                var p = new Product { Name = r[0].Trim(), Category = r[1].Trim(), System = r[2].Trim(), SG = D(r[3]), PkgSize = D(r[4]), PkgUnit = r[5].Trim() };
                if (r.Length > 6) p.Price = D(r[6]);
                if (r.Length > 7) p.ConcWBM = D(r[7]);
                if (r.Length > 8) p.ConcOBM = D(r[8]);
                if (p.SG <= 0) p.SG = 1;
                Products.Add(p);
            }
        }
        public static void SaveProducts()
        {
            var rows = new List<string[]> { ProductCols };
            foreach (var p in Products)
                rows.Add(new[] { p.Name, p.Category, p.System, S(p.SG), S(p.PkgSize), p.PkgUnit, S(p.Price), S(p.ConcWBM), S(p.ConcOBM) });
            WriteCsv(F("products.csv"), rows);
        }
        public static Product WeightMaterial()
        {
            foreach (var p in Products) if (p.Category.Equals("Weighting", StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
        public static Product SaltProduct(string saltName)
        {
            foreach (var p in Products)
                if (p.Category.Equals("Salt", StringComparison.OrdinalIgnoreCase) && p.Name.IndexOf(saltName, StringComparison.OrdinalIgnoreCase) >= 0) return p;
            return null;
        }

        // --- settings (selected units)
        public static void LoadSettings()
        {
            foreach (var r in ReadCsv(F("settings.csv")))
            {
                if (r.Length < 2) continue;
                if (r[0].StartsWith("unit."))
                {
                    UK k;
                    if (Enum.TryParse(r[0].Substring(5), out k) && Units.Opts.ContainsKey(k))
                    {
                        int i = (int)D(r[1]);
                        if (i >= 0 && i < Units.Opts[k].Length) Units.Sel[k] = i;
                    }
                }
            }
        }
        public static void SaveSettings()
        {
            try
            {
                var rows = new List<string[]> { new[] { "Key", "Value" } };
                foreach (var kv in Units.Sel) rows.Add(new[] { "unit." + kv.Key, kv.Value.ToString() });
                WriteCsv(F("settings.csv"), rows);
            }
            catch { }
        }

        // --- daily reports
        public static void LoadReports()
        {
            Reports.Clear();
            var rows = ReadCsv(F("reports.csv"));
            if (rows.Count < 2) return;
            var head = rows[0];
            for (int i = 1; i < rows.Count; i++)
            {
                var d = new Dictionary<string, string>();
                for (int c = 0; c < head.Length; c++) d[head[c]] = c < rows[i].Length ? rows[i][c] : "";
                Reports.Add(d);
            }
        }
        public static void SaveReports()
        {
            var rows = new List<string[]> { ReportFields };
            foreach (var d in Reports)
                rows.Add(ReportFields.Select(f => d.ContainsKey(f) ? d[f] : "").ToArray());
            WriteCsv(F("reports.csv"), rows);
        }
    }

    // ------------------------------------------------------------------ PRINTING
    public static class TextPrinter
    {
        public static void Print(string title, string text)
        {
            string[] lines = (title + "\n\n" + text).Replace("\r", "").Split('\n');
            int idx = 0;
            var font = new Font("Consolas", 9.5f);
            var pd = new PrintDocument();
            pd.DocumentName = title;
            pd.BeginPrint += delegate { idx = 0; };
            pd.PrintPage += delegate (object s, PrintPageEventArgs e)
            {
                float y = e.MarginBounds.Top;
                float lh = font.GetHeight(e.Graphics);
                while (idx < lines.Length && y + lh < e.MarginBounds.Bottom)
                {
                    e.Graphics.DrawString(lines[idx], font, Brushes.Black, e.MarginBounds.Left, y);
                    y += lh; idx++;
                }
                e.HasMorePages = idx < lines.Length;
            };
            using (var dlg = new PrintDialog())
            {
                dlg.Document = pd; dlg.UseEXDialog = true;
                if (dlg.ShowDialog() == DialogResult.OK) pd.Print();
            }
        }
        public static void SaveText(string suggested, string text)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Text file (*.txt)|*.txt"; dlg.FileName = suggested;
                if (dlg.ShowDialog() == DialogResult.OK) File.WriteAllText(dlg.FileName, text, Encoding.UTF8);
            }
        }
    }
}
