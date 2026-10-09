# ESA.NET

A .NET port of **ESA — Engine Simulation and Analysis**, a Delphi 4 / VCL
application written by Paul Williams and last released as version 3.0 in October
2001.

## What the app does

ESA simulates the thermodynamic cycle of a spark-ignition internal combustion
engine and reports its performance. You describe an engine — bore, stroke,
compression ratio, cam profiles, valve sizes and discharge coefficients, manifold
geometry, fuel and ambient conditions — and it integrates the cycle over crank
angle, computes cylinder pressure and temperature, models one-dimensional
pressure-wave flow in the inlet and exhaust manifolds, and solves a twelve-species
equilibrium combustion model. Out of that come torque, power, IMEP/BMEP/FMEP/PMEP,
volumetric and thermal efficiency, specific fuel consumption, an energy balance,
and emissions. Results are shown live on charts during the run and can be exported
as text.

An engine definition is a `.eng` file — a plain text INI file that also names the
side files holding cam profiles (`.cam`), manifold areas (`.maf`), discharge
coefficient grids (`.vcd`), spark maps (`.spk`), wall temperatures (`.cwt`) and
exhaust back pressure (`.exh`).

## Layering rules

These are not suggestions. `tests/App.Tests/LayeringTests.cs` fails the build if
the first one is broken.

| Project | May reference | Contains |
|---|---|---|
| `src/App.Core` | nothing | Domain models, business rules, interfaces. **No UI framework. Ever.** |
| `src/App.Persistence` | Core | Legacy file and INI readers/writers implementing Core interfaces |
| `src/App.Ui` | Core, Persistence | Avalonia views and view models. Binding only, no logic |
| `tests/App.Tests` | all of the above | xUnit tests |

Further rules:

- **No static mutable state.** The Delphi original leaned on globals — `Engine2z`
  in `ICEngine2z.pas`, and `Choice`, `QI`, `QE`, `W` in `Manifolds.pas`. Those
  become instance state. Services and view models come from the DI container.
- **Views hold no logic.** Code-behind exists to call `InitializeComponent` and
  nothing else. Commands live on view models as `[RelayCommand]` methods.
- **Nullable reference types are on and warnings are errors** for every project,
  set once in `Directory.Build.props`.
- **Package versions are managed centrally** in `Directory.Packages.props`. Do not
  put a `Version` attribute on a `PackageReference`.
- `Avalonia.Controls.DataGrid` ships its theme separately from `FluentTheme`; the
  `StyleInclude` in `App.axaml` is what makes the multi-run grid draw at all.

## Where the Delphi originals live

`legacy/` holds the original source, untouched. It is reference material — read
it, never edit it.

- `legacy/ESA/*.pas`, `*.dfm` — the Delphi units and forms. Note that the `.dfm`
  files are **binary** DFM, not the text form; they need string extraction rather
  than a text editor.
- `legacy/ESA/Components/adcalc41_paid/` — the proprietary `TAdCalc` expression
  evaluator that ESA uses to evaluate the manifold grid-size and valve-flow
  expressions stored in `.eng` files.
- `legacy/ESA/Data/Example1` and `legacy/ESA/Data/Example2` — the two calibration
  cases named by SPEC.md section 6. Phase 4 validates the ported physics against
  these.
- `legacy/samples/` — copies of five `.eng` files used as frozen test fixtures.
  `.gitattributes` marks them `-text` so git can never renormalise their bytes;
  the round-trip test depends on that.

`SPEC.md` is the reverse-engineered specification of the Delphi application and is
the reference for what behaviour to port. `archive/` holds the working notes that
produced it.

`data/baseline/` holds the phase 4 validation baseline: a complete reference run
of the original Delphi application, captured deliberately with every input and
output recorded — the engine, its ten side files, screenshots of all eight
settings tabs and the results screen, and a full-cycle 720-row PVT trace.
**`BASELINE.md` documents it** and is required reading before any phase 4 work.
Unlike the output scattered through `legacy/ESA/Data/`, this set carries its own
provenance. Treat every file in it as read-only: regenerating it means running
the original application on Windows.

## The thesis validation suite

`validation/` re-runs every dynamometer correlation of the thesis (sections 6.4 to 6.6,
Figures 6.5 to 6.15), not just the 6.12 that `ThesisCorrelationTests` gates.
`validation/README.md` is the reference.

- **`validation/cases/`** holds one folder per engine configuration: byte copies of legacy
  files, plus a derived `.eng` where a case changes a value, which opens with a `;` comment
  naming its source. `.msr` grids hold the run points. `validation/suite.json` maps figures to
  cases. Both are written by `validation/tools/build_cases.py`; edit the script, not the
  folder.
- **`data/thesis/figures/`** holds what the thesis plotted, measured and 2002-model,
  extracted from the PDF's vector graphics by `validation/tools/digitise_thesis.py`.
  **`data/thesis/source/`** holds the author's original spreadsheets they were drawn from.
- **`legacy/CAEEng/`** is the only surviving definition of the eight-valve engine of
  section 6.5, from the 1998-99 predecessor program. It does not run in the port yet
  (`validation/README.md`, "What does not run").
- **A grid row's file overrides (`IManfFile` to `ECamFile`) are ignored by the port**:
  `MultiRunner` sets the name after `EngineLoader` has read the table. The suite therefore
  keeps one manifold per `.eng`.
- `ThesisSuiteTests` keeps the suite loadable without running it.
- **Run it** with `dotnet run --project tools/App.Validation -c Release`. It runs in
  Corrected by default, takes about half a minute, and writes `report.html`,
  `results.csv`, `failures.csv` and `run.json` into a git-ignored folder under
  `validation-reports/`. Pass `--previous <that folder>` to see what a change moved. The
  tool draws its own SVG charts rather than using ScottPlot, so the report is a single
  file with hover tooltips, light and dark themes, and no dual axes: torque and power are
  separate charts.

## Where the data lives

The application has a data folder of its own, which the Delphi original did not: it opened
its output under bare relative names and wrote wherever the working directory happened to
be (`ISSUES.md` C4).

- **The root** is `Documents/ESA`, overridden by `[Folders] Data` in `ESA.ini` and, above
  that, by the `ESA_DATA_ROOT` environment variable. `Workspace.ResolveRoot` is the order.
- **`Engines/`** holds input files. **`Runs/`** holds one folder per run, named
  `2026-08-28_141530_A2China`; a sweep is one folder with a `Row01_4000rpm` subfolder per
  grid row.
- **A run folder holds copies of its inputs** — the `.eng` and every side file it read,
  byte for byte — plus `SimulDat.txt`, `Lastcyc.txt`, the nine manifold files and a
  `run.txt` manifest. Nothing reads any of it back except the engines.
- `IWorkspace` and `RunFolderName` are in Core; `Workspace`, `RunArchive` and `RunManifest`
  are in Persistence. The output **formats** live where they always did — this routes the
  existing writers, it does not add formats.
- **`ESA_DATA_ROOT` is how the tests stay out of `Documents`.** `TestServices` registers a
  temp-folder `IWorkspace` over the real one, and several tests drive a whole simulation
  through the view model; a test that builds its own service graph must do the same.
- *Save Manifold Data* no longer gates anything: every run archives its manifold files. The
  gate still exists on `SimulationRunner` and is still pinned by `ManifoldOutputGateTests`;
  the application overrides it with `recordManifoldData: true`.

## Naming conventions

- Namespaces mirror project names: `App.Core`, `App.Persistence`, `App.Ui`,
  `App.Tests`. Domain models sit in `App.Core.Model`.
- Delphi's `T` type prefix is dropped: `TEngine2z` becomes `Engine`, `TGas2Z`
  becomes `Gas`, `TCdValve` becomes `DischargeCoefficientTable`.
- Delphi abbreviations are spelled out where the meaning is not obvious
  (`TotalMInIV` becomes `TotalMassInInletValve`), and kept where they are the
  domain's own vocabulary (`Imep`, `Bmep`, `Sfc`, `Bore`).
- Where a .NET name diverges from the Delphi one, the XML doc comment names the
  Delphi field so the two can be matched up. Do this for every ported member.
- Delphi 1-based arrays become 0-based .NET arrays, except where an indexer keeps
  the original numbering because the domain uses it — `SpeciesValues[Species.CO2]`
  and `CrankAngleTrace[-359]` both index the way the original did.

## Known port caveats

`ISSUES.md` is the full register: defects in this port, legacy defects reproduced
on purpose, behaviour that catches out the operator, errors in `SPEC.md`, dead
data, and open questions. The list below is the subset that affects day-to-day
work.

- Delphi's 80-bit `Extended` has no .NET equivalent; those values are `double`.
  This affects the equilibrium model and is the first thing to suspect if phase 4
  numbers drift from the legacy reference runs. It has bitten once outright: rich burnt gas
  near 1000 K leaves the equilibrium Jacobian too ill-conditioned for double, and every
  run below lambda 0.98 stopped on "Insufficient Resolution" (`ISSUES.md` A34, #194). The
  solver now retries any matrix double cannot resolve in double-double
  (`DelphiNumerics.GaussReduceExtended`). A matrix double does resolve is untouched,
  which keeps the baseline bit-identical.
- `.eng` keys must be matched **case-insensitively**. `Edit.pas` reads `CdIvIn`
  while every shipped file writes `CdIVIn`; Delphi's `TIniFile` did not care and
  neither can we.
- Five `Example1` engines and `legacy/CAEEng/A4LowCost.eng` use an older, undocumented
  `.eng` schema with `[InManifold]` and `[ExManifold]` sections. The reader must not drop
  them. **Its units are not the current schema's**: `PlenumP` is kPa, `THead` and the other
  wall temperatures are Celsius, and grid sizes are fixed counts under `[Calculation]`. The
  `Effective*` and `UsesInline*` accessors on `EngineDefinition` translate them as the
  predecessor's `Edit.pas` did, without writing anything; where a file carries both schemas
  (`Nissan1-3`) the current keys win (`ISSUES.md` A31-A35).
- The original menu assigned `Ctrl+Q` to both Exit and QuickRun. Exit keeps it (and
  `Cmd+Q`); QuickRun is `Ctrl+Shift+R` (C8). In Avalonia `MenuItem.InputGesture` is
  display-only, so every caption needs a matching `KeyBinding`; `MenuStructureTests`
  enforces it.
- **Run ▸ Pause holds a run between steps through `RunPause`** and must never do more
  than wait. A paused-and-resumed run is pinned bit-for-bit to an uninterrupted one.
- **Test a dialog through its real window, not just its view model.** The multi-run
  grid's view model was tested thoroughly while its `DataGrid` refused to edit a
  single cell, and four buttons shipped with no command at all (`ISSUES.md`
  A20-A25). A `DataGridTextColumn` bound through an indexer is inferred read-only,
  and `IsCancel` closes nothing in Avalonia. File pickers parent to the active
  window, because a modal dialog disables the main one.
- **`^` is left-associative**, so `2^3^2` is 64 and not 512. AdCalc recurses only
  while the next operator scores *strictly* higher, and `^` following `^` fails
  that test (`ADCALC.PAS:2555-2620`). Almost every other language disagrees.
  Unary minus binds looser still, so `-2^2` is `-4`. A sign is legal only at the
  start of an expression or a bracket: `3*-2` is an error in the original and
  here, which is why the shipped files write `3*(-2)`.
- **`^` must not use `Math.Pow`.** Delphi's `Math.Power` routes integer exponents
  through `IntPower`'s repeated squaring and only falls back to `Exp(y*Ln(x))` for
  fractional ones. `DelphiMath.Power` reproduces that branching. The paths differ
  in the last bits, and a grid size is the `Round` of an expression built from
  `N^6` terms — close enough to a boundary to change an integer point count.
- `Round` in Delphi is round-half-to-even, which .NET's `Math.Round` matches but a
  cast or `Floor(x + 0.5)` would not.
- **`.exh` columns are RPM, temperature, pressure** — temperature first. SPEC.md
  section 3 has them the other way round and is wrong.
- **The exhaust valve's Cd tables are crossed.** `ICEngine2Z.pas:998-1005` assigns
  `EV.CdForward` from `CdEvOut` and `EV.CdReverse` from `CdEvIn`, because forward
  flow through an exhaust valve is outward. The inlet valve is wired the obvious
  way. Straightening this out would silently change the physics.
- `TAManf.GetValue` returns **zero** past the end of the area table, not the last
  area — a cliff, not a clamp. Legacy reproduces it in `LegacyInterpolation`;
  Corrected (B4) holds the last area. `TCdValve.GetValue` names its two y points in
  the reverse order to its x ones, which looks like a slip and is not: each y keeps its
  own value, so it is an ordinary bilinear interpolation (`ISSUES.md` B5).
- **`Partial_dxd` takes pressure in atmospheres, not pascals.** Its first parameter
  is named `Pres`, but `go2` passes it the local `p`, already divided by 101325
  (`Eqbm.pas:117, 137`). Passing pascals inflates every `dC/dT` by `sqrt(101325)`
  and, through `MixdhdT` and `MixdRdT`, `dudT` with them — which the burnt
  temperature equation divides by. This was the port's own defect for a while, and
  was written up as a *legacy* one (the retracted `ISSUES.md` B15) because it was
  only ever checked against a reimplementation of the same misreading. Gamma cannot
  catch it: `Get_gamma` passes a zero derivative array, so it matched the baseline
  trace throughout. See `ISSUES.md` A7. The same convention makes every `DxDp` per
  atmosphere, so an analytic `dudp` divides by 101325 to reach the per-pascal the
  cylinder equations take — the reason the original's was abandoned for a difference
  (`ISSUES.md` B16).
- **The RKF5 tableau has a transposed digit.** `RKf5.pas:76` reads `854/4104`
  where Fehlberg published `845/4104`. The fifth stage's row then sums to 455/456
  rather than 1, and the method converges at **first order, not fifth** — no
  better than the Euler alternative it is offered against on an analytic problem.
  Legacy keeps the digit and `Rkf5IntegratorTests` fails if that is ever "fixed";
  Corrected (or `B14=1` in `ESA.ini`) uses Fehlberg's, the first correction behind
  the switch. **The reference run cannot tell the two apart**, though: a converged
  whole-cycle comparison is 0.225 % rms with the transposed digit and 0.190 % with
  Fehlberg's, both inside the A8 bias. So "reproduced because `data/baseline/` was
  produced by it" was never established by measurement — see `ISSUES.md` B14.
- **In Legacy, between steps the gas pairs the end-of-step solution with the start-of-step
  angle.** `Run` updates the cylinder at `VCyl(x)` with `x` the angle the step began at,
  after `Integrate` has moved `y` on by `dx`; work is accumulated a step out of phase, and
  each PVT row is filed under the angle the step started at. Corrected (B77) updates at
  `x + dx`, takes work and heat by the trapezoid and files rows at their own angle. To
  measure a Legacy run, use `Integration.Y` and the volume at `CrankAngle + CrankAngleStep`,
  not `Cylinder.PGas` and `Cylinder.VGas`: the mismatch biases a polytropic index by about
  0.02. See `ISSUES.md` B77.
- `Manifolds.pas:2739-2742` **ignores the `IVFFn` expression at or below 1000 rpm**,
  substituting a hard-coded line. The port evaluates the operator's expression at every
  speed; no baseline run goes that slow. See `ISSUES.md` B6.
- No `.eng` file has ever stored fuel composition, so the Delphi form reset it to
  C7H17 on every load even though the equilibrium model depends on it. The port
  reads optional `[Fuel]` `C`/`H`/`O`/`N` keys, defaults to 7/17/0/0, and writes
  them only when they change, so existing files stay byte-identical.
- `Inlet.grd` and `Exhaust.grd` in `Example1` are **not dead**, though ESA 3.0 never
  reads them: the 1999 predecessor's `TGridSize.Load` took their second line, `35`, as a
  grid size and ignored the Pascal fragments after it. The port reads them the same way
  for `Nissan4.eng` and `Nissan5.eng`, which give no grid size of their own (`ISSUES.md`
  A35, E1).
- Side-file paths in `.eng` files mix bare names, backslash-relative paths and
  absolute paths to drives that no longer exist. `LegacyPathResolver` handles all
  three; on Linux and macOS nothing resolves without it.
- **`mf` and `ThEff` in `TEngine2z.Performance` hard-code four cylinders.** Both
  use a factor of `2 * Nrpm` where the physics needs `NCyl * Nrpm / 2`; the two
  agree only at `NCyl == 4`, and all 71 shipped engines are `NoCyls=4`. The port uses
  `NCyl / 2` as a single factor, which is exactly 2.0 for a four-cylinder, so the
  baseline is reproduced to the last bit and every other cylinder count is right.
  See `ISSUES.md` B1 and `BASELINE.md`.
- **Fixing a section B entry is governed by `CORRECTIONS.md`.** Entries that cannot move
  a converged result are fixed directly, gated on a bit-identical full-precision
  fingerprint of the baseline runs; entries that move the baseline go behind a
  Legacy/Corrected switch. Read it before touching anything in section B.
- **Corrected is the default; Legacy has to be asked for.** `PhysicsCorrections` starts in
  Corrected, so a new `SimulationSettings` runs every correction, and an `ESA.ini` with no
  `[Physics] Mode`, or an unreadable one, reads Corrected. A test or comparison against
  `data/baseline/` must say `new PhysicsCorrections { Mode = PhysicsMode.Legacy }`, or hand
  its solvers no physics at all (`physics: null` is every correction off). See
  `CORRECTIONS.md` section 5.
- **Corrected is no longer close to `data/baseline/`.** Since B46 it runs expansion
  pressure up to 17 % above the reference trace, since B38 heat loss about 15 % lower, and
  since B65 it draws the charge at the inlet valve's own temperature, about 100 K above
  the plenum's, and since B77 it counts work without the original's one-step phase error,
  about 4 % more, and since B78 its burns conserve energy where the original's created about
  15 % of the fuel energy, and since B37 overlap runs as two zones, the burnt residual and the
  fresh charge. With every correction torque is about 8 % higher at 4000 rpm and about 10 %
  lower at 3000, because the reference reproduces the defects. Baseline comparisons are Legacy's; a correction is
  checked against its physical oracle.
- **Both modes match the dynamometer.** `data/thesis/` holds the baseline engine's measured
  torque curve from the thesis (Figure 6.12), and `ThesisCorrelationTests` gates Legacy
  against it (5.3 % rms, 151.8 Nm at the 151.5 Nm peak) and reports Corrected (5.3 %, 163.3
  Nm). Before B78 Corrected was 13.4 %: the original's burns created about 15 % of the fuel
  energy, which its calibration absorbed through defects the earlier corrections removed. A
  correction's oracle says it is right; the thesis curve says whether Corrected as a whole is.
  See `ISSUES.md` B78 and F6.

## Build and test

Requires the .NET 10 SDK (`global.json` pins 10.0.x). On Ubuntu:
`sudo apt-get install -y dotnet-sdk-10.0`.

```bash
dotnet build ESA.NET.slnx -c Release   # must produce zero warnings
dotnet test  ESA.NET.slnx
dotnet run   --project src/App.Ui      # launches the shell
```

The solution is an `.slnx` file, the SDK 10 default format.

**Check "zero warnings" with `--no-incremental`.** Avalonia's XAML compiler reports its
warnings (an obsolete property, say) only when it recompiles the `.axaml`, and they do
not fail the build the way C# warnings do. An incremental build can report none while a
clean one reports several; that is how #159 merged with six.

On a headless Linux box, `xvfb-run -a dotnet run --project src/App.Ui` will start
the app; `MenuStructureTests` also exercises the window through
`Avalonia.Headless` with no display at all.

`EngRoundTripTests` is the gate that phase 2 was built around: every legacy
`.eng` file must read and write back byte for byte identically. If it goes red,
something in the persistence layer has started reformatting user data.
`TableRoundTripTests` holds the same line for `.maf` and `.vcd`, and
`EditEngineViewModelTests` for the editor's save path — opening an engine and
pressing OK must not restyle a single byte.

## Phase plan

| Phase | Scope | Status |
|---|---|---|
| 1 | Reverse-engineer the Delphi application into `SPEC.md` | **Complete** |
| 2 | Project skeleton: solution, layering, domain models, `.eng` round-trip, shell window | **Complete** |
| 3 | Remaining file formats (`.maf`, `.vcd`, `.cam`, `.spk`, `.cwt`, `.exh`, `ESA.ini`), an expression evaluator to replace `TAdCalc`, and the engine Edit form | **Complete** |
| 4 | Simulation core: RKF5 integrator, gas and equilibrium models, manifold CFD, performance calculations, validated against the `data/baseline/` reference run (see `BASELINE.md`) | **Complete.** A converged whole-cycle run matches the reference trace to 0.33 % at every crank angle; see `ISSUES.md` A8 for what is still open |
| 5 | ScottPlot charts, the multi-run grid, PVT and manifold text exports | **Complete.** The Run menu drives the simulation, the results screen is the original's four quadrants, and the multi-run grid can be typed in as well as loaded. `Graph ▸ Multi-Run Results` plots any variable of the last sweep against any other (`SweepVariables`, `EngineCharts.SweepPlot`), which the original could not. The text exports were finished with the data folder: every run writes its performance row, PVT trace and manifold files into a folder of its own, and `Text ▸ PVT Trace` saves a copy where the operator asks |
| 6 | Packaging and distribution | Not started |

### What phase 2 delivered

- `src/App.Core` — a data-only domain type for every record and class in SPEC.md
  section 2, plus `EsaLimits`, the state and integrator enums, the four ported
  exception types, and the `IEngineDefinitionStore` contract.
- `src/App.Persistence` — `IniDocument`, a format-preserving INI model, and
  `EngineDefinitionStore` on top of it.
- `src/App.Ui` — an Avalonia shell with the menu structure recovered from
  `Main.dfm`, wired to no-op commands, with view models resolved from a generic
  host container.
- `tests/App.Tests` — 31 tests including the byte-exact round trip over all 65
  legacy `.eng` files and the layering guard.

No business logic and no ported forms: that was phase 3 onward.

### Where phase 4a has got to

Everything except the manifold CFD, which is 4b. Each layer is checked against
`data/baseline/` rather than against itself — that is what caught A7.

- `src/App.Core/Simulation` — `Rkf5Integrator`, `CylinderGeometry`, `ValveMotion`,
  `CrankAngleStateMap`, `CylinderModel` (Woschni heat transfer and all thirteen
  derivative functions), `CycleSolver` (`InitVars`, the `Run` state machine and the
  cycle loop) and `PerformanceCalculator`.
- `src/App.Core/Thermo` — `EquilibriumSolver`, `GasPropertyModel`, `TwoZoneGas`,
  `ThermoTables`, `DelphiNumerics`.
- `IManifoldSource` is the seam the manifolds arrive through. 4b substitutes the
  real solver for the recorded fixture; nothing else changes.

What the reference run says, worst case over the closed period:

| Layer | Reference values | Agreement |
|---|---|---|
| Cylinder volume | 720 | inside printed precision |
| Valve flow areas | 1440 | inside printed precision |
| Heat loss per step | 1440 | one unit in the last printed place |
| Compression pressure | 79 | 0.081 % |
| Expansion pressure | 82 | 0.036 % |
| Combustion pressure | 55 | 0.72 % at the spark, falling to ~0.2 % across the burn (`ISSUES.md` A8) |
| Reported performance | all 15 figures | exact at their printed precision |

**A free run cannot be driven from the recorded fixture.** `dPMass` is the one
`Main_Prog` output the trace does not record, and the single-zone pressure equation
has no mass term without it, so the cylinder never empties and the model diverges.
The recorded mass flows also belong to a converged cycle carrying 580 mg where
`InitVars` guesses 415 mg. Validation is therefore one-step-ahead residuals from the
reference state, which is the stronger test for an ODE port anyway. Whole-cycle
comparison waits for 4b.

### What phase 3 delivered

- `src/App.Core/Expressions` — the `TAdCalc` replacement. A recursive-descent
  parser over the dialect the data actually uses (literals, `N`, `L`,
  `+ - * / ^`, brackets), `DelphiMath.Power`, a caching evaluator, and
  `GridSizeCalculator` for the `NI`/`NE` limit checks.
- `src/App.Core/Interpolation` — the legacy lookup behaviour, quirks intact.
- `src/App.Persistence/Tables` — readers for `.cam`, `.spk`, `.cwt` and `.exh`,
  and format-preserving stores for `.maf` and `.vcd`, the two the app writes back.
- `src/App.Persistence` — `EngineLoader`, which assembles a whole `Engine` from a
  `.eng` and its side files and reports what it could not find rather than
  failing mid-run; `LegacyPathResolver`; and `SimulationSettingsStore` for
  `ESA.ini`.
- `src/App.Ui` — `EditEngineWindow`, the eight-tab engine editor, with live
  capacity, per-field validation, and writes that touch only changed values.
- `tests/App.Tests` — 124 tests. The ones that matter most: every expression in
  every `.eng` file parses and evaluates; every `.maf` and `.vcd` round-trips
  byte for byte; all 70 engine files load with every side file resolved; and
  opening an engine in the editor and pressing OK leaves the file untouched.

Still no simulation: the integrator, gas and equilibrium models and the manifold
solver are phase 4.
