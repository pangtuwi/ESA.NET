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

- Delphi's 80-bit `Extended` has no .NET equivalent; those values are `double`.
  This affects the equilibrium model and is the first thing to suspect if phase 4
  numbers drift from the legacy reference runs.
- `.eng` keys must be matched **case-insensitively**. `Edit.pas` reads `CdIvIn`
  while every shipped file writes `CdIVIn`; Delphi's `TIniFile` did not care and
  neither can we.
- Five `Example1` engines use an older, undocumented `.eng` schema with
  `[InManifold]` and `[ExManifold]` sections. The reader must not drop them.
- The original menu assigned `Ctrl+Q` to both Exit and QuickRun. The shell
  reproduces both captions; in Avalonia `MenuItem.InputGesture` is display-only,
  so nothing actually clashes yet. Resolve it when the commands get real
  behaviour.

## Build and test

Requires the .NET 10 SDK (`global.json` pins 10.0.x). On Ubuntu:
`sudo apt-get install -y dotnet-sdk-10.0`.

```bash
dotnet build ESA.NET.slnx -c Release   # must produce zero warnings
dotnet test  ESA.NET.slnx
dotnet run   --project src/App.Ui      # launches the shell
```

The solution is an `.slnx` file, the SDK 10 default format.

On a headless Linux box, `xvfb-run -a dotnet run --project src/App.Ui` will start
the app; `MenuStructureTests` also exercises the window through
`Avalonia.Headless` with no display at all.

`EngRoundTripTests` is the gate that phase 2 was built around: every legacy
`.eng` file must read and write back byte for byte identically. If it goes red,
something in the persistence layer has started reformatting user data.

## Phase plan

| Phase | Scope | Status |
|---|---|---|
| 1 | Reverse-engineer the Delphi application into `SPEC.md` | **Complete** |
| 2 | Project skeleton: solution, layering, domain models, `.eng` round-trip, shell window | **Complete** |
| 3 | Remaining file formats (`.maf`, `.vcd`, `.cam`, `.spk`, `.cwt`, `.exh`, `ESA.ini`), an expression evaluator to replace `TAdCalc`, and the engine Edit form | Not started |
| 4 | Simulation core: RKF5 integrator, gas and equilibrium models, manifold CFD, performance calculations, validated against Example1 and Example2 | Not started |
| 5 | ScottPlot charts, the multi-run grid, PVT and manifold text exports | Not started |
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

No business logic and no ported forms: that is phase 3 onward.
