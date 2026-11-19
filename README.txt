MUD CALCULATOR PRO 2.0
======================

BUILD (one time, no installs needed)
  Double-click build.bat. It compiles MudCalcPro.exe with the C# compiler that is
  part of Windows (.NET Framework 4) and starts the program.
  If it fails, the errors are in build_log.txt.

RUN
  Double-click MudCalcPro.exe. You can copy the exe to any Windows PC; it creates
  a "data" folder next to itself on first run.

YOUR DATA (data folder, open with Excel or File > Open data folder)
  products.csv  - product names, category, SG, package size/unit, price, default ppb
                  (edit in the program: Data & records > Products & prices)
  salts.csv     - salt density correlation + crystallization curve (CaCl2, NaCl, KCl)
  reports.csv   - daily mud reports
  settings.csv  - selected units

CALCULATIONS
  Weight up & dilution, mixing, barite additions
  Oil mud: OWR, WPS, water/oil additions, water contamination
  Basic WBM/OBM mixes, slugs
  WBM / OBM formulation from scratch with packages and cost
  Volumes & hydraulics: capacities, well volume & circulation, pump output,
    annular velocity, cuttings, tanks, ECD
  Pressure & well control: hydrostatic, EMW/LOT/MAASP, kill sheet basics
  Brines: dry-salt brine, blending, chlorides conversion, salt tables
  Mud testing: rheology, WBM solids analysis, OBM retort & salinity

All results are engineering estimates. Verify critical numbers before use.
