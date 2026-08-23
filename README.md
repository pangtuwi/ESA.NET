# ESA.NET

A .NET port of **ESA — Engine Simulation and Analysis**, a Delphi 4 / VCL
application written by Paul Williams and last released as version 3.0 in
October 2001.

ESA simulates the thermodynamic cycle of a spark-ignition internal combustion
engine. You describe an engine — bore, stroke, compression ratio, cam profiles,
valve sizes and discharge coefficients, manifold geometry, fuel and ambient
conditions — and it integrates the cycle over crank angle, computes cylinder
pressure and temperature, models one-dimensional pressure-wave flow in the inlet
and exhaust manifolds, and solves a twelve-species equilibrium combustion model.
Out of that come torque, power, IMEP/BMEP/FMEP/PMEP, volumetric and thermal
efficiency, specific fuel consumption, an energy balance, and emissions.

An engine definition is a `.eng` file — a plain text INI file that also names the
side files holding cam profiles (`.cam`), manifold areas (`.maf`), discharge
coefficient grids (`.vcd`), spark maps (`.spk`), wall temperatures (`.cwt`) and
exhaust back pressure (`.exh`).

## Project status

**Phase 3 of 6 — file formats and the engine editor.** The solution builds, runs
and tests. It reads a complete engine — the `.eng` file, all six side-file
formats, and the expressions embedded in the engine definition — and it edits one
through an eight-tab form without reformatting a byte the user did not change.
There is **no simulation yet**: the Run and Graph menus remain no-ops. See the
phase plan in [CLAUDE.md](CLAUDE.md).

| Phase | Scope | Status |
|---|---|---|
| 1 | Reverse-engineer the Delphi application into `SPEC.md` | Complete |
| 2 | Project skeleton: solution, layering, domain models, `.eng` round-trip, shell window | Complete |
| 3 | Remaining file formats, an expression evaluator to replace `TAdCalc`, the engine Edit form | Complete |
| 4 | Simulation core, validated against the two reference engines | Not started |
| 5 | Charts, the multi-run grid, PVT and manifold text exports | Not started |
| 6 | Packaging and distribution | Not started |

## Tech stack

C# on .NET 10, [Avalonia](https://avaloniaui.net/) for the UI (Windows is the
primary target; Avalonia keeps a cross-platform port open), CommunityToolkit.Mvvm
for observable properties and commands, Microsoft.Extensions.Hosting for
dependency injection, xUnit for tests, and ScottPlot for the charts in phase 5.

---

## Build and run on Windows

### 1. Install the .NET 10 SDK

`global.json` pins `10.0.100` with `rollForward: latestFeature`, so any 10.0.1xx
SDK will do.

```powershell
winget install Microsoft.DotNet.SDK.10
```

Or use the x64 installer from <https://dotnet.microsoft.com/download/dotnet/10.0>.
Open a **new** terminal afterwards so `PATH` picks it up, then confirm:

```powershell
dotnet --version    # expect 10.0.1xx
```

### 2. Clone

```powershell
git clone https://github.com/pangtuwi/ESA.NET.git
cd ESA.NET
```

### 3. Build, test, run

```powershell
dotnet build ESA.NET.slnx -c Release    # expect 0 warnings, 0 errors
dotnet test  ESA.NET.slnx               # expect 106 passed
dotnet run   --project src\App.Ui       # opens the shell window
```

`App.Ui` is a `WinExe`, so no console window tags along, and `app.manifest` gives
it per-monitor DPI awareness.

### 4. Or use an IDE

- **Visual Studio 2022 17.13+ / VS 2026** — open `ESA.NET.slnx` directly. Older
  17.x releases cannot read `.slnx`; if yours refuses, use *File → Open → Folder*,
  or run `dotnet sln ESA.NET.slnx migrate` to emit a classic `.sln` alongside it
  (don't commit that).
- **JetBrains Rider** — opens `.slnx` natively.
- **VS Code** — install the C# Dev Kit extension and open the folder.

Set `App.Ui` as the startup project and <kbd>F5</kbd> works.

### 5. Publish a standalone executable

```powershell
dotnet publish src\App.Ui\App.Ui.csproj -c Release -r win-x64 --self-contained
```

The result lands in `src\App.Ui\bin\Release\net10.0\win-x64\publish\App.Ui.exe`.
Drop `--self-contained` if the target machine already has the .NET 10 runtime.

### A note on line endings

Git for Windows defaults to `core.autocrlf=true`, so the 65 `.eng` files under
`legacy\ESA\Data\` will arrive on disk as CRLF. This is harmless: the INI reader
records each line's own terminator and re-emits it, so the byte-exact round-trip
test still passes, and `IniDocumentTests.ArbitraryTerminatorsRoundTrip` covers
CRLF, LF, mixed endings and a missing final newline explicitly. `.gitattributes`
additionally freezes the five test fixtures in `legacy/samples/` as LF so nothing
can drift.

## Build and run on Linux or macOS

The same three commands work unchanged:

```bash
dotnet build ESA.NET.slnx -c Release
dotnet test  ESA.NET.slnx
dotnet run   --project src/App.Ui
```

On Ubuntu, install the SDK with `sudo apt-get install -y dotnet-sdk-10.0`. On a
headless box, `xvfb-run -a dotnet run --project src/App.Ui` starts the app; the
`MenuStructureTests` also exercise the window through `Avalonia.Headless` with no
display at all.

## Verified on

What has actually been run, as opposed to what should work. Worth knowing if you
hit a difference and want a known-good reference point.

| Platform | Toolchain | Exercised |
|---|---|---|
| Windows 10 | VS Code with the C# Dev Kit, .NET SDK 10.0.400 | Build and run |
| Ubuntu 24.04 | .NET SDK 10.0.111, command line | Release build (0 warnings), 106 tests, run under Xvfb |

Not yet exercised anywhere: the `dotnet publish` step, and opening `ESA.NET.slnx`
in Visual Studio or Rider.

---

## Repository layout

```
src/App.Core          Domain models, business rules, interfaces. No UI framework, ever.
src/App.Persistence   Legacy file and INI readers/writers implementing Core interfaces.
src/App.Ui            Avalonia views and view models. Binding only, no logic.
tests/App.Tests       xUnit tests.
legacy/               The original Delphi source, untouched. Reference material.
legacy/samples/       Frozen .eng fixtures used by the round-trip test.
SPEC.md               Reverse-engineered specification of the Delphi application.
CLAUDE.md             Layering rules, naming conventions, port caveats, phase plan.
archive/              Working notes that produced SPEC.md.
```

The layering table above is enforced, not merely documented:
`tests/App.Tests/LayeringTests.cs` fails the build if `App.Core` or
`App.Persistence` ever picks up a reference to a UI framework.

## Tests

`dotnet test ESA.NET.slnx` runs 106 tests. The ones that matter most guard user
data and the ported semantics:

- `EngRoundTripTests`, `TableRoundTripTests`, `EditEngineViewModelTests` — every
  legacy `.eng`, `.maf` and `.vcd` file must read and write back **byte for byte
  identically**, and opening an engine in the editor and pressing OK must not
  restyle a single byte. If any goes red, something has started reformatting
  user data.
- `ExpressionCorpusTests` — every expression in every shipped `.eng` file must
  parse and evaluate.
- `ExpressionEvaluatorTests` — pins the AdCalc semantics recovered from the
  Delphi source, notably that `^` is left-associative.
- `LayeringTests` — `App.Core` and `App.Persistence` must never reference a UI
  framework.

## Credits

The original ESA was written by Paul Williams. The manifold pressure-wave solver
originates with Christie M. van Vuuren, and the equilibrium combustion model with
the author credited as "Arthur" in `legacy/ESA/Eqbm.pas`. The Delphi source in
`legacy/` is included as reference material for the port and is not modified.

`legacy/ESA/Components/adcalc41_paid/` contains the third-party AdCalc expression
evaluator under its own licence; see `LICENSE.TXT` in that directory. It is
reference material only: phase 3 replaced it with a native implementation in
`src/App.Core/Expressions`, and no AdCalc code is compiled into this port.
