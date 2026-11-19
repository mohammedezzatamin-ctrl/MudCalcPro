// Mud Calculator Pro - user interface
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MudCalcPro
{
    public class MainForm : Form
    {
        TreeView tree;
        Panel host;
        ToolStripStatusLabel status;
        object current;
        Dictionary<Calc, Dictionary<string, double>> memory = new Dictionary<Calc, Dictionary<string, double>>();
        static readonly Color Accent = Color.FromArgb(0, 92, 151);
        static readonly Font MonoFont = new Font("Consolas", 10f);

        const string P_FORM_WBM = "Water-based mud from scratch";
        const string P_FORM_OBM = "Oil-based mud from scratch";
        const string P_SALT = "Salt & brine tables";
        const string P_PRODUCTS = "Products & prices";
        const string P_REPORTS = "Daily mud reports";
        const string P_CONVERT = "Unit converter";
        const string P_UNITS = "Unit settings";

        public MainForm()
        {
            Text = "Mud Calculator Pro " + Data.Version;
            Font = new Font("Segoe UI", 9.5f);
            Size = new Size(1320, 820);
            MinimumSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("&File");
            mFile.DropDownItems.Add("Open data folder", null, delegate { Process.Start(Data.Dir); });
            mFile.DropDownItems.Add("Reload data files", null, delegate { Data.LoadSalts(); Data.LoadProducts(); ShowPage(current); SetStatus("Data files reloaded."); });
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add("E&xit", null, delegate { Close(); });
            var mUnits = new ToolStripMenuItem("&Units");
            mUnits.DropDownItems.Add("Oilfield units (ppg, bbl, ft, psi)", null, delegate { Units.Preset(false); ShowPage(current); SetStatus("Oilfield units selected."); });
            mUnits.DropDownItems.Add("Metric units (SG, m³, m, bar)", null, delegate { Units.Preset(true); ShowPage(current); SetStatus("Metric units selected."); });
            mUnits.DropDownItems.Add("Choose units...", null, delegate { GoTo(P_UNITS); });
            var mHelp = new ToolStripMenuItem("&Help");
            mHelp.DropDownItems.Add("About", null, delegate { About(); });
            menu.Items.Add(mFile); menu.Items.Add(mUnits); menu.Items.Add(mHelp);

            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
            tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, Font = new Font("Segoe UI", 10f), ItemHeight = 24, BorderStyle = BorderStyle.None };
            tree.AfterSelect += delegate (object s, TreeViewEventArgs e) { if (e.Node.Tag != null) ShowPage(e.Node.Tag); };
            host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            split.Panel1.Controls.Add(tree);
            split.Panel2.Controls.Add(host);

            var st = new StatusStrip();
            status = new ToolStripStatusLabel("Ready.  Data folder: " + Data.Dir);
            st.Items.Add(status);

            Controls.Add(split); Controls.Add(menu); Controls.Add(st);
            split.BringToFront();
            MainMenuStrip = menu;
            Load += delegate { try { split.SplitterDistance = 300; } catch { } };

            BuildTree();
            ShowWelcome();
        }

        void SetStatus(string s) { status.Text = s; }

        void BuildTree()
        {
            tree.BeginUpdate();
            tree.Nodes.Clear();
            var groups = new Dictionary<string, TreeNode>();
            foreach (string g in new[] { Calcs.G1, Calcs.G2, Calcs.G3 })
                groups[g] = tree.Nodes.Add(g);
            var form = tree.Nodes.Add("Mud formulation");
            form.Nodes.Add(new TreeNode(P_FORM_WBM) { Tag = P_FORM_WBM });
            form.Nodes.Add(new TreeNode(P_FORM_OBM) { Tag = P_FORM_OBM });
            foreach (string g in new[] { Calcs.G4, Calcs.G5, Calcs.G6, Calcs.G7 })
                groups[g] = tree.Nodes.Add(g);
            groups[Calcs.G6].Nodes.Add(new TreeNode(P_SALT) { Tag = P_SALT });
            foreach (Calc c in Calcs.All) groups[c.Group].Nodes.Add(new TreeNode(c.Name) { Tag = c });
            var data = tree.Nodes.Add("Data & records");
            data.Nodes.Add(new TreeNode(P_REPORTS) { Tag = P_REPORTS });
            data.Nodes.Add(new TreeNode(P_PRODUCTS) { Tag = P_PRODUCTS });
            var tools = tree.Nodes.Add("Tools");
            tools.Nodes.Add(new TreeNode(P_CONVERT) { Tag = P_CONVERT });
            tools.Nodes.Add(new TreeNode(P_UNITS) { Tag = P_UNITS });
            foreach (TreeNode n in tree.Nodes) { n.ForeColor = Accent; n.Text = n.Text.ToUpperInvariant(); }
            tree.ExpandAll();
            tree.EndUpdate();
            if (tree.Nodes.Count > 0) tree.Nodes[0].EnsureVisible();
        }

        void GoTo(string tag)
        {
            foreach (TreeNode g in tree.Nodes)
                foreach (TreeNode n in g.Nodes)
                    if (tag.Equals(n.Tag)) { tree.SelectedNode = n; return; }
            ShowPage(tag);
        }

        void ShowPage(object tag)
        {
            if (tag == null) { ShowWelcome(); return; }
            current = tag;
            host.SuspendLayout();
            foreach (Control c in host.Controls.Cast<Control>().ToList()) c.Dispose();
            host.Controls.Clear();
            host.AutoScrollPosition = new Point(0, 0);
            AcceptButton = null;
            try
            {
                if (tag is Calc) ShowCalc((Calc)tag);
                else if ((string)tag == P_FORM_WBM) ShowFormulation(false);
                else if ((string)tag == P_FORM_OBM) ShowFormulation(true);
                else if ((string)tag == P_SALT) ShowSaltTables();
                else if ((string)tag == P_PRODUCTS) ShowProducts();
                else if ((string)tag == P_REPORTS) ShowReports();
                else if ((string)tag == P_CONVERT) ShowConverter();
                else if ((string)tag == P_UNITS) ShowUnits();
            }
            finally
            {
                foreach (Label l in host.Controls.OfType<Label>()) l.UseMnemonic = false;
                host.ResumeLayout();
            }
        }

        // ---------------------------------------------------------------- helpers
        Label Title(string text, string help)
        {
            var t = new Label { Text = text, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Accent, AutoSize = true, Location = new Point(16, 12) };
            host.Controls.Add(t);
            if (!string.IsNullOrEmpty(help))
                host.Controls.Add(new Label { Text = help, ForeColor = Color.DimGray, Location = new Point(18, 48), Size = new Size(900, 38) });
            return t;
        }
        Button Btn(string text, int x, int y, int w, EventHandler click)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 32), FlatStyle = FlatStyle.System };
            b.Click += click; host.Controls.Add(b); return b;
        }
        TextBox ResultsBox(int x, int y, int w, int h)
        {
            var tb = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = MonoFont,
                Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.FromArgb(248, 250, 252),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            host.Controls.Add(tb); return tb;
        }
        void ResultButtons(TextBox box, string title, int x, int y)
        {
            Btn("Copy", x, y, 90, delegate { if (box.Text.Length > 0) { Clipboard.SetText(box.Text); SetStatus("Results copied to clipboard."); } });
            Btn("Print...", x + 98, y, 90, delegate { if (box.Text.Length > 0) TextPrinter.Print("Mud Calculator Pro - " + title, box.Text); });
            Btn("Save as text...", x + 196, y, 120, delegate { if (box.Text.Length > 0) TextPrinter.SaveText(title + ".txt", box.Text); });
        }
        static string Header(string title)
        {
            return "Mud Calculator Pro " + Data.Version + "   " + DateTime.Now.ToString("dd-MMM-yyyy HH:mm") + Environment.NewLine + title + Environment.NewLine + Environment.NewLine;
        }

        // ---------------------------------------------------------------- generic calculator page
        Dictionary<string, double> Mem(Calc c)
        {
            Dictionary<string, double> d;
            if (!memory.TryGetValue(c, out d))
            {
                d = new Dictionary<string, double>();
                foreach (Field f in c.Fields) d[f.Key] = f.Def;
                memory[c] = d;
            }
            return d;
        }

        void ShowCalc(Calc c)
        {
            Title(c.Name, c.Help);
            var vals = Mem(c);
            var inputs = new Dictionary<Field, Control>();
            int y = 96;
            foreach (Field f in c.Fields)
            {
                host.Controls.Add(new Label { Text = f.Label, Location = new Point(16, y + 4), Size = new Size(270, 22), TextAlign = ContentAlignment.MiddleRight });
                if (f.Choices != null)
                {
                    var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(294, y), Width = 200 };
                    cb.Items.AddRange(f.Choices);
                    cb.SelectedIndex = Math.Max(0, Math.Min(f.Choices.Length - 1, (int)vals[f.Key]));
                    host.Controls.Add(cb); inputs[f] = cb;
                }
                else
                {
                    var tb = new TextBox { Location = new Point(294, y), Width = 110, TextAlign = HorizontalAlignment.Right,
                        Text = Units.FmtIn(Units.FromInt(f.Kind, vals[f.Key])) };
                    host.Controls.Add(tb); inputs[f] = tb;
                    host.Controls.Add(new Label { Text = Units.Name(f.Kind), Location = new Point(410, y + 4), AutoSize = true, ForeColor = Color.DimGray });
                }
                y += 32;
            }
            int rx = 500;
            var box = ResultsBox(rx, 96, Math.Max(420, host.ClientSize.Width - rx - 20), Math.Max(y - 96, 360));
            var calcBtn = Btn("Calculate", 294, y + 8, 200, null);
            calcBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            calcBtn.Click += delegate
            {
                var v = new Dictionary<string, double>();
                var echo = new Res();
                echo.Title("Inputs");
                foreach (var kv in inputs)
                {
                    Field f = kv.Key;
                    if (f.Choices != null)
                    {
                        int i = ((ComboBox)kv.Value).SelectedIndex;
                        v[f.Key] = i; echo.T(f.Label, f.Choices[i]);
                    }
                    else
                    {
                        double d;
                        if (!Units.TryParse(kv.Value.Text, out d))
                        {
                            kv.Value.Focus(); ((TextBox)kv.Value).SelectAll();
                            MessageBox.Show("'" + kv.Value.Text + "' is not a valid number for \"" + f.Label + "\".", "Mud Calculator Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        v[f.Key] = Units.ToInt(f.Kind, d);
                        echo.T(f.Label, Units.FmtIn(d) + (Units.Name(f.Kind).Length > 0 ? " " + Units.Name(f.Kind) : ""));
                    }
                }
                foreach (var kv in v) vals[kv.Key] = kv.Value;
                var r = new Res();
                r.Title("Results");
                try { c.Fn(v, r); }
                catch (CalcError ex) { r.Warn(ex.Message); }
                catch (Exception ex) { r.Warn("Calculation error: " + ex.Message); }
                box.Text = Header(c.Name) + echo.SB.ToString() + Environment.NewLine + r.SB.ToString() +
                           Environment.NewLine + "EQUATIONS" + Environment.NewLine + "---------" + Environment.NewLine + Equations.For(c);
                SetStatus("Calculated: " + c.Name);
            };
            AcceptButton = calcBtn;
            ResultButtons(box, c.Name, rx, box.Bottom + 8);
            host.Controls.Add(new Label { Text = "Units are set from the Units menu. Press Enter to calculate.", ForeColor = Color.Gray, Location = new Point(294, y + 46), AutoSize = true });
            EquationBox(Equations.For(c) + "\r\n\r\n" + Equations.Common, 16, y + 76, rx - 30);
        }

        // read-only panel showing the equations used on this page
        TextBox EquationBox(string text, int x, int y, int w)
        {
            host.Controls.Add(new Label { Text = "Equations used", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Accent, Location = new Point(x, y), AutoSize = true });
            int lines = text.Split('\n').Length;
            var tb = new TextBox
            {
                Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9.5f),
                Location = new Point(x, y + 26), Size = new Size(w, Math.Min(360, lines * 17 + 34)), BackColor = Color.FromArgb(255, 251, 235),
                Text = text
            };
            host.Controls.Add(tb);
            return tb;
        }

        // ---------------------------------------------------------------- formulation
        void ShowFormulation(bool obm)
        {
            Title(obm ? P_FORM_OBM : P_FORM_WBM,
                "Builds a full mixing sheet: base fluids, salt, every product, weight material, packages and cost. Product list, package sizes and prices come from Products & prices.");
            int y = 96;
            Func<string, string, UK, double, TextBox> num = (label, key, kind, def) =>
            {
                host.Controls.Add(new Label { Text = label, Location = new Point(16, y + 4), Size = new Size(200, 22), TextAlign = ContentAlignment.MiddleRight });
                var tb = new TextBox { Location = new Point(222, y), Width = 100, TextAlign = HorizontalAlignment.Right, Text = Units.FmtIn(Units.FromInt(kind, def)), Tag = kind };
                host.Controls.Add(tb);
                host.Controls.Add(new Label { Text = Units.Name(kind), Location = new Point(328, y + 4), AutoSize = true, ForeColor = Color.DimGray });
                y += 32; return tb;
            };
            var tV = num("Volume to mix", "v", UK.Volume, 500);
            var tW = num("Mud weight", "w", UK.Density, obm ? 12 : 10);
            var tOil = num(obm ? "OWR (oil %)" : "Oil in liquid phase", "oil", UK.Percent, obm ? 80 : 0);
            host.Controls.Add(new Label { Text = "Salt / base water", Location = new Point(16, y + 4), Size = new Size(200, 22), TextAlign = ContentAlignment.MiddleRight });
            var cbSalt = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(222, y), Width = 160 };
            if (obm) cbSalt.Items.AddRange(new object[] { "CaCl2", "NaCl", "KCl", "None (fresh water)" });
            else cbSalt.Items.AddRange(new object[] { "Fresh water", "Seawater", "NaCl", "KCl", "CaCl2" });
            cbSalt.SelectedIndex = 0; host.Controls.Add(cbSalt); y += 32;
            var tSal = num(obm ? "Water phase salinity" : "Chlorides (salt brine)", "sal", obm ? UK.Ppm : UK.Chlorides, obm ? 250000 : 0);
            var tPur = num("Salt purity", "pur", UK.Percent, obm ? 95 : 99);
            Product wm = Data.WeightMaterial();
            var tSgWM = num("SG of weight material", "sgw", UK.SG, wm != null ? wm.SG : 4.2);
            var tSgOil = num("SG of base oil", "sgo", UK.SG, 0.80);

            // product grid
            var grid = new DataGridView
            {
                Location = new Point(16, y + 10), Size = new Size(500, 300), AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            grid.Columns.Add("p", "Product"); grid.Columns.Add("c", "Conc. (" + Units.Name(UK.Conc) + ")");
            grid.Columns.Add("pk", "Package"); grid.Columns.Add("sg", "SG");
            grid.Columns[0].FillWeight = 190; grid.Columns[0].ReadOnly = true; grid.Columns[2].ReadOnly = true; grid.Columns[3].ReadOnly = true;
            var prods = new List<Product>();
            foreach (Product p in Data.Products)
            {
                string cat = p.Category.ToLowerInvariant();
                if (cat == "weighting" || cat == "salt") continue;
                string sys = p.System.ToUpperInvariant();
                if (sys != "BOTH" && sys != (obm ? "OBM" : "WBM")) continue;
                prods.Add(p);
                grid.Rows.Add(p.Name, Units.FmtIn(Units.FromInt(UK.Conc, obm ? p.ConcOBM : p.ConcWBM)), p.PackageText(), Units.FmtIn(p.SG));
            }
            host.Controls.Add(grid);
            host.Controls.Add(new Label { Text = "Edit the concentrations above (0 = not used). To add products, go to Products & prices.", ForeColor = Color.Gray, Location = new Point(16, grid.Bottom + 4), AutoSize = true });

            int rx = 540;
            var box = ResultsBox(rx, 96, Math.Max(420, host.ClientSize.Width - rx - 20), grid.Bottom - 96);
            var calc = Btn("Calculate", 16, grid.Bottom + 30, 200, null);
            calc.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            AcceptButton = calc;
            ResultButtons(box, obm ? "OBM formulation" : "WBM formulation", rx, box.Bottom + 8);
            EquationBox(Equations.Formulation, 16, calc.Bottom + 16, 1000);
            calc.Click += delegate
            {
                try
                {
                    grid.EndEdit();
                    Func<TextBox, double> get = tb =>
                    {
                        double d;
                        if (!Units.TryParse(tb.Text, out d)) { tb.Focus(); throw new CalcError("'" + tb.Text + "' is not a valid number."); }
                        return Units.ToInt((UK)tb.Tag, d);
                    };
                    var lines = new List<FormLine>();
                    for (int i = 0; i < prods.Count; i++)
                    {
                        double d;
                        string s = Convert.ToString(grid.Rows[i].Cells[1].Value);
                        if (!Units.TryParse(s, out d)) throw new CalcError("Concentration for " + prods[i].Name + " is not a number.");
                        lines.Add(new FormLine { P = prods[i], Conc = Units.ToInt(UK.Conc, d) });
                    }
                    string salt = cbSalt.SelectedItem.ToString();
                    if (salt.StartsWith("None") || salt == "Fresh water") salt = "";
                    string text = Formulate.Run(obm, get(tV), get(tW), get(tOil), salt, get(tSal), get(tPur), get(tSgWM), get(tSgOil), lines);
                    box.Text = Header(obm ? "Oil-based mud formulation" : "Water-based mud formulation") + text +
                               Environment.NewLine + "EQUATIONS" + Environment.NewLine + "---------" + Environment.NewLine + Equations.Formulation;
                }
                catch (CalcError ex) { box.Text = "!! " + ex.Message; }
                catch (Exception ex) { box.Text = "!! Error: " + ex.Message; }
            };
        }

        // ---------------------------------------------------------------- salt tables
        void ShowSaltTables()
        {
            Title(P_SALT, "Brine properties at about 68°F calculated from salts.csv (density correlation and crystallization curve). Crystallization points are approximate - confirm with the supplier's data sheet.");
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(16, 96), Width = 160 };
            foreach (Salt s in Data.Salts) cb.Items.Add(s.Name);
            host.Controls.Add(cb);
            var grid = new DataGridView
            {
                Location = new Point(16, 132), Size = new Size(Math.Max(900, host.ClientSize.Width - 40), 380), ReadOnly = true, AllowUserToAddRows = false,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            host.Controls.Add(grid);
            cb.SelectedIndexChanged += delegate
            {
                Salt s = Data.Salts[cb.SelectedIndex];
                grid.Columns.Clear(); grid.Rows.Clear();
                grid.Columns.Add("a", "Wt % salt"); grid.Columns.Add("b", "SG"); grid.Columns.Add("c", "Density (" + Units.Name(UK.Density) + ")");
                grid.Columns.Add("d", "Salt (" + Units.Name(UK.Conc) + " brine)"); grid.Columns.Add("e", "Water (bbl/bbl brine)");
                grid.Columns.Add("f", "Salt (mg/L)"); grid.Columns.Add("g", "Chlorides (mg/L)"); grid.Columns.Add("h", "Brine vol / water vol");
                grid.Columns.Add("i", "Cryst. point (" + Units.Name(UK.Temp) + ")"); grid.Columns.Add("j", "WPS (ppm)");
                var wts = new List<double>();
                for (double w = 1; w < s.MaxWt; w += 1) wts.Add(w);
                wts.Add(s.MaxWt);
                foreach (double w in wts)
                {
                    double sg = s.SG(w); double t = s.TCT(w);
                    grid.Rows.Add(Units.Fmt(w), sg.ToString("0.0000"), Units.Fmt(Units.FromInt(UK.Density, Calcs.WD * sg)),
                        Units.Fmt(Units.FromInt(UK.Conc, Calcs.LbPerBblPerSG * sg * w / 100)), (sg * (1 - w / 100)).ToString("0.0000"),
                        (w / 100 * sg * 1e6).ToString("#,##0"), s.ClFromWt(w).ToString("#,##0"), (1 / (sg * (1 - w / 100))).ToString("0.0000"),
                        double.IsNaN(t) ? "-" : Units.FromInt(UK.Temp, t).ToString("0"), (w * 1e4).ToString("#,##0"));
                }
            };
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;

            host.Controls.Add(new Label { Text = "Clear brines - typical maximum density (single / blended salts)", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(16, 528), AutoSize = true });
            var g2 = new DataGridView
            {
                Location = new Point(16, 556), Size = new Size(620, 250), ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White
            };
            g2.Columns.Add("a", "Brine"); g2.Columns.Add("b", "Max density (" + Units.Name(UK.Density) + ")"); g2.Columns.Add("c", "Max SG");
            var brines = new[] {
                new object[] { "Potassium chloride KCl", 9.7 }, new object[] { "Sodium chloride NaCl", 10.0 }, new object[] { "Potassium bromide KBr", 11.5 },
                new object[] { "Calcium chloride CaCl2", 11.6 }, new object[] { "Sodium bromide NaBr", 12.6 }, new object[] { "Calcium bromide CaBr2", 15.1 },
                new object[] { "Zinc bromide / CaBr2 blends", 19.2 }, new object[] { "Sodium formate (approx.)", 11.1 }, new object[] { "Potassium formate (approx.)", 13.1 },
                new object[] { "Cesium formate (approx.)", 19.2 } };
            foreach (var b in brines)
            {
                double ppg = (double)b[1];
                g2.Rows.Add(b[0], Units.Fmt(Units.FromInt(UK.Density, ppg)), (ppg / Calcs.WD).ToString("0.000"));
            }
            host.Controls.Add(g2);
            EquationBox(Equations.SaltTable, 652, 528, 640);
        }

        // ---------------------------------------------------------------- products
        void ShowProducts()
        {
            Title(P_PRODUCTS, "Your product list. Category 'Weighting' = weight material, 'Salt' = salts (name must contain CaCl2, NaCl or KCl). System = WBM, OBM or Both. Package unit: kg, lb, MT, gal, L or bbl. Concentrations are default ppb used by the formulation pages.");
            var table = new DataTable();
            foreach (string c in Data.ProductCols) table.Columns.Add(c, c == "Name" || c == "Category" || c == "System" || c == "PackageUnit" ? typeof(string) : typeof(double));
            foreach (Product p in Data.Products)
                table.Rows.Add(p.Name, p.Category, p.System, p.SG, p.PkgSize, p.PkgUnit, p.Price, p.ConcWBM, p.ConcOBM);
            var grid = new DataGridView
            {
                Location = new Point(16, 96), Size = new Size(Math.Max(900, host.ClientSize.Width - 40), Math.Max(400, host.ClientSize.Height - 170)),
                DataSource = table, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            grid.DataError += delegate (object s, DataGridViewDataErrorEventArgs e) { MessageBox.Show("Please enter a number in this column."); e.Cancel = true; };
            host.Controls.Add(grid);
            grid.DataBindingComplete += delegate { if (grid.Columns.Count > 0) grid.Columns[0].FillWeight = 220; };
            int by = grid.Bottom + 10;
            var save = Btn("Save products", 16, by, 140, delegate
            {
                grid.EndEdit();
                var list = new List<Product>();
                foreach (DataRow r in table.Rows)
                {
                    if (r.RowState == DataRowState.Deleted) continue;
                    string name = Convert.ToString(r["Name"]).Trim();
                    if (name.Length == 0) continue;
                    Func<string, double> dv = col => r[col] == DBNull.Value ? 0 : Convert.ToDouble(r[col]);
                    list.Add(new Product
                    {
                        Name = name, Category = Convert.ToString(r["Category"]), System = Convert.ToString(r["System"]).Length == 0 ? "Both" : Convert.ToString(r["System"]),
                        SG = dv("SG") <= 0 ? 1 : dv("SG"), PkgSize = dv("PackageSize"), PkgUnit = Convert.ToString(r["PackageUnit"]).Length == 0 ? "kg" : Convert.ToString(r["PackageUnit"]),
                        Price = dv("PricePerPackage"), ConcWBM = dv("ConcWBM_ppb"), ConcOBM = dv("ConcOBM_ppb")
                    });
                }
                Data.Products = list;
                try { Data.SaveProducts(); SetStatus("Saved " + list.Count + " products to products.csv"); MessageBox.Show("Products saved.", "Mud Calculator Pro"); }
                catch (Exception ex) { MessageBox.Show("Could not save: " + ex.Message); }
            });
            save.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            var imp = Btn("Import CSV...", 166, by, 130, delegate
            {
                using (var dlg = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv" })
                {
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    if (MessageBox.Show("Replace the product list with this file? It must have the same columns as products.csv.", "Import", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    File.Copy(dlg.FileName, Data.F("products.csv"), true);
                    Data.LoadProducts(); ShowPage(P_PRODUCTS); SetStatus("Products imported.");
                }
            });
            imp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            var exp = Btn("Open in Excel", 306, by, 130, delegate { try { Process.Start(Data.F("products.csv")); } catch (Exception ex) { MessageBox.Show(ex.Message); } });
            exp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        }

        // ---------------------------------------------------------------- daily reports
        void ShowReports()
        {
            Title(P_REPORTS, "Record daily mud properties, volumes and products. Saved to reports.csv in the data folder (opens in Excel).");
            var list = new ListBox { Location = new Point(16, 96), Size = new Size(232, 560), Font = new Font("Segoe UI", 9.5f), IntegralHeight = false };
            host.Controls.Add(list);
            var boxes = new Dictionary<string, TextBox>();
            int col = 0, y = 96;
            foreach (string f in Data.ReportFields)
            {
                bool multi = f == "Products used" || f == "Remarks";
                if (multi)
                {
                    if (col != 0) { col = 0; y += 30; }
                    host.Controls.Add(new Label { Text = f, Location = new Point(262, y + 4), Size = new Size(130, 22), TextAlign = ContentAlignment.MiddleRight });
                    var mt = new TextBox { Location = new Point(398, y), Size = new Size(620, 60), Multiline = true, ScrollBars = ScrollBars.Vertical };
                    host.Controls.Add(mt); boxes[f] = mt; y += 66; continue;
                }
                int x = 262 + col * 256;
                host.Controls.Add(new Label { Text = f, Location = new Point(x, y + 4), Size = new Size(130, 22), TextAlign = ContentAlignment.MiddleRight });
                var tb = new TextBox { Location = new Point(x + 134, y), Width = 112 };
                host.Controls.Add(tb); boxes[f] = tb;
                col++; if (col == 3) { col = 0; y += 30; }
            }
            Action refresh = null;
            Action<Dictionary<string, string>> load = d =>
            {
                foreach (var kv in boxes) kv.Value.Text = d != null && d.ContainsKey(kv.Key) ? d[kv.Key] : "";
            };
            refresh = () =>
            {
                list.Items.Clear();
                foreach (var d in Data.Reports)
                    list.Items.Add((d.ContainsKey("Date") ? d["Date"] : "") + "  " + (d.ContainsKey("Well") ? d["Well"] : "") + "  " + (d.ContainsKey("Depth MD") ? d["Depth MD"] : ""));
            };
            refresh();
            list.SelectedIndexChanged += delegate { if (list.SelectedIndex >= 0) load(Data.Reports[list.SelectedIndex]); };
            int by = y + 12;
            Btn("New report", 270, by, 120, delegate
            {
                Dictionary<string, string> last = Data.Reports.Count > 0 ? Data.Reports[Data.Reports.Count - 1] : null;
                load(null);
                if (last != null) foreach (string k in new[] { "Rig", "Well", "Operator", "Mud engineer", "Mud system", "Hole size", "Casing shoe" }) if (last.ContainsKey(k)) boxes[k].Text = last[k];
                boxes["Date"].Text = DateTime.Today.ToString("dd-MMM-yyyy");
                list.ClearSelected();
            });
            Btn("Save report", 398, by, 120, delegate
            {
                var d = new Dictionary<string, string>();
                foreach (var kv in boxes) d[kv.Key] = kv.Value.Text;
                int i = list.SelectedIndex;
                if (i >= 0) Data.Reports[i] = d; else { Data.Reports.Add(d); i = Data.Reports.Count - 1; }
                try { Data.SaveReports(); } catch (Exception ex) { MessageBox.Show("Could not save: " + ex.Message); return; }
                refresh(); list.SelectedIndex = i; SetStatus("Report saved.");
            });
            Btn("Delete", 526, by, 90, delegate
            {
                int i = list.SelectedIndex; if (i < 0) return;
                if (MessageBox.Show("Delete this report?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                Data.Reports.RemoveAt(i); Data.SaveReports(); refresh(); load(null);
            });
            Btn("Print...", 624, by, 90, delegate
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in boxes) sb.AppendLine(Res.Pad(kv.Key) + kv.Value.Text.Replace(Environment.NewLine, Environment.NewLine + new string(' ', 42)));
                TextPrinter.Print("Daily Mud Report", sb.ToString());
            });
            Btn("Open all in Excel", 722, by, 150, delegate
            {
                if (!File.Exists(Data.F("reports.csv"))) { MessageBox.Show("No reports saved yet."); return; }
                try { Process.Start(Data.F("reports.csv")); } catch (Exception ex) { MessageBox.Show(ex.Message); }
            });
            list.Height = Math.Max(300, by + 32 - 96);
        }

        // ---------------------------------------------------------------- unit converter
        void ShowConverter()
        {
            Title(P_CONVERT, "Type a value, choose its unit, and see it in every other unit of the same kind.");
            var kinds = Units.Opts.Keys.Where(k => Units.Opts[k].Length > 1).ToList();
            var cbKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(16, 96), Width = 220 };
            foreach (UK k in kinds) cbKind.Items.Add(Units.KindName[k]);
            var tb = new TextBox { Location = new Point(246, 96), Width = 120, TextAlign = HorizontalAlignment.Right, Text = "1" };
            var cbUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(376, 96), Width = 120 };
            var box = ResultsBox(16, 136, 480, 300);
            host.Controls.Add(cbKind); host.Controls.Add(tb); host.Controls.Add(cbUnit);
            Action run = () =>
            {
                if (cbKind.SelectedIndex < 0 || cbUnit.SelectedIndex < 0) return;
                UK k = kinds[cbKind.SelectedIndex];
                double d; if (!Units.TryParse(tb.Text, out d)) { box.Text = "Not a number."; return; }
                UnitOpt from = Units.Opts[k][cbUnit.SelectedIndex];
                double inner = d * from.Factor + from.Offset;
                var sb = new System.Text.StringBuilder();
                foreach (UnitOpt u in Units.Opts[k])
                    sb.AppendLine(Res.Pad(u.Name) + Units.Fmt((inner - u.Offset) / u.Factor));
                box.Text = sb.ToString();
            };
            cbKind.SelectedIndexChanged += delegate
            {
                cbUnit.Items.Clear();
                foreach (UnitOpt u in Units.Opts[kinds[cbKind.SelectedIndex]]) cbUnit.Items.Add(u.Name);
                cbUnit.SelectedIndex = 0; run();
            };
            cbUnit.SelectedIndexChanged += delegate { run(); };
            tb.TextChanged += delegate { run(); };
            cbKind.SelectedIndex = 0;
        }

        // ---------------------------------------------------------------- unit settings
        void ShowUnits()
        {
            Title(P_UNITS, "Choose the unit used for each quantity on every page. Saved automatically.");
            Btn("All oilfield units", 16, 96, 160, delegate { Units.Preset(false); ShowPage(P_UNITS); });
            Btn("All metric units", 186, 96, 160, delegate { Units.Preset(true); ShowPage(P_UNITS); });
            int y = 144;
            foreach (UK k in Units.Opts.Keys.ToList())
            {
                if (Units.Opts[k].Length < 2) continue;
                host.Controls.Add(new Label { Text = Units.KindName[k], Location = new Point(16, y + 4), Size = new Size(200, 22), TextAlign = ContentAlignment.MiddleRight });
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(226, y), Width = 150 };
                foreach (UnitOpt u in Units.Opts[k]) cb.Items.Add(u.Name);
                cb.SelectedIndex = Units.Sel[k];
                UK kk = k;
                cb.SelectedIndexChanged += delegate { Units.Sel[kk] = cb.SelectedIndex; Data.SaveSettings(); SetStatus("Units updated."); };
                host.Controls.Add(cb);
                y += 32;
            }
        }

        // ---------------------------------------------------------------- welcome / about
        void ShowWelcome()
        {
            current = null;
            Title("Mud Calculator Pro", null);
            var t = new Label
            {
                Location = new Point(18, 56), Size = new Size(900, 520), Font = new Font("Segoe UI", 10.5f),
                Text = "Mud engineering calculator for drilling fluids.\r\n\r\n" +
                    "Choose a calculation from the list on the left:\r\n\r\n" +
                    "  •  Weight up, cut weight, mix muds, add barite\r\n" +
                    "  •  Oil mud: OWR, WPS, water/oil additions, water contamination\r\n" +
                    "  •  Basic mixes, slugs, full WBM/OBM formulation with packages and cost\r\n" +
                    "  •  Hole and well volumes, pump output, annular velocity, ECD, cuttings, tanks\r\n" +
                    "  •  Hydrostatic, EMW / LOT / MAASP, kill sheet basics\r\n" +
                    "  •  Brines: dry-salt brines, blending, chlorides conversion, salt tables\r\n" +
                    "  •  Mud testing: rheology, WBM solids analysis, OBM retort & salinity\r\n" +
                    "  •  Daily mud reports, product list with prices, unit converter\r\n\r\n" +
                    "Units: use the Units menu (oilfield, metric or per-quantity).\r\n" +
                    "Your data (products, salt tables, reports, settings) is kept as CSV files in:\r\n" + Data.Dir + "\r\n\r\n" +
                    "All results are engineering estimates. Check critical numbers before use."
            };
            t.UseMnemonic = false;
            host.Controls.Add(t);
        }

        void About()
        {
            MessageBox.Show("Mud Calculator Pro " + Data.Version + "\r\n\r\nDrilling fluids engineering calculator.\r\n" +
                "Rebuilt from Mud Calculator V1.61 (1999) with new data, calculations, formulation costing and daily reports.\r\n\r\n" +
                "Data folder:\r\n" + Data.Dir + "\r\n\r\nResults are estimates - verify critical figures.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
