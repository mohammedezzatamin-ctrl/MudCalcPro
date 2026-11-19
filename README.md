# Mud Calculator Pro

A drilling fluids (mud) engineering calculator. It comes in two versions with the same calculations:

- **Web app:** open **https://mohammedezzatamin-ctrl.github.io/MudCalcPro/** in any browser, on PC, tablet or phone.
- **Windows program:** download `MudCalcPro.exe` from [Releases](https://github.com/mohammedezzatamin-ctrl/MudCalcPro/releases/latest). There's nothing to install; just double-click it.

Every calculation shows the equations it uses.

## Calculations

| Group | Calculations |
| --- | --- |
| Weight up & dilution | Weight up (volume increase / constant volume), cut mud weight (volume increase / constant volume), mix 2–3 muds, add a known weight of barite, barite per unit volume |
| Oil mud adjustments | Adjust OWR, increase WPS, add water / base oil, suspected water contamination |
| Basic mixes & slugs | Basic WBM and OBM mixes, slug flowback, slug size, slug weight |
| Mud formulation | WBM and OBM from scratch, with every product, packages and cost |
| Volumes & hydraulics | Hole/pipe/annular capacity, well volume and circulation times, pump output, annular velocity, cuttings, tank volume, ECD (Bingham) |
| Pressure & well control | Hydrostatic pressure, EMW / LOT / MAASP, kill sheet basics |
| Brines & salt | Brine from dry salt, brine blending, chlorides to salt, salt tables (CaCl2, NaCl, KCl) |
| Mud testing & solids | Rheology (PV, YP, n, K), WBM solids analysis, OBM retort and salinity |
| Records & tools | Daily mud reports, editable product list with prices, unit converter, oilfield or metric units |

## Repository layout

| Path | Contents |
| --- | --- |
| `src/` | Windows program source (C# WinForms, .NET Framework 4) |
| `build.bat` | Builds `MudCalcPro.exe` with the C# compiler that ships with Windows |
| `docs/index.html` | Web app (served by GitHub Pages) |
| `web/` | Web app source: `engine.js` (calculations) and `ui.html` (interface); run `python3 web/build_web.py` to rebuild `docs/index.html` |

## Building the Windows program

Double-click `build.bat`. It compiles `MudCalcPro.exe` and starts it. If the build fails, the errors are written to `build_log.txt`.

All results are engineering estimates. Check critical numbers before you rely on them.
