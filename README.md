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
| 4 | Simulation core, validated against a captured run of the original app ([BASELINE.md](BASELINE.md)) | Not started |
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
dotnet test  ESA.NET.slnx               # expect 417 passed
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

## Build, run and test on Linux Mint

Everything below was run on a Mint 22 / Ubuntu 24.04 base with .NET SDK 10.0.111.
Two routes: the [command line](#variant-1--command-line), and
[VS Code](#variant-2--vs-code). The command-line route comes first because the
VS Code route depends on the SDK it installs.

### Which Ubuntu is your Mint?

Every Mint release tracks an Ubuntu LTS, and it is the Ubuntu codename — not the
Mint version — that decides whether the SDK is one `apt install` away:

```bash
cat /etc/upstream-release/lsb-release   # Mint-only file; names the Ubuntu base
```

| Mint | Ubuntu base | .NET 10 SDK in the Ubuntu repos? |
|---|---|---|
| 22.x (Wilma, Xia, Zara) | 24.04 `noble` | **Yes** — `noble-updates/universe` |
| 21.x (Vanessa … Virginia) | 22.04 `jammy` | No — use the install script below |
| LMDE 6/7 | Debian, not Ubuntu | No — use the install script below |

---

### Variant 1 — command line

#### 1. Install the .NET 10 SDK

**Mint 22.x** — Ubuntu ships it, and Mint enables `universe` out of the box:

```bash
sudo apt update
sudo apt install -y dotnet-sdk-10.0
```

**Mint 21.x, LMDE, or if that package is not found** — use Microsoft's install
script, which drops a private SDK in `~/.dotnet` and needs no root:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"

# put it on PATH for this shell and every future one
export PATH="$HOME/.dotnet:$PATH"
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc
```

> **Do not mix the two.** Installing both `dotnet-sdk-10.0` from Ubuntu *and* the
> `packages.microsoft.com` feed is the classic way to end up with two `dotnet`
> hosts that disagree about which runtimes exist. Pick one. If you already have
> the Microsoft feed configured, keep using it and skip the Ubuntu package.

Confirm, in a **new** terminal:

```bash
dotnet --version      # expect 10.0.1xx
dotnet --list-sdks
```

`global.json` pins `10.0.100` with `rollForward: latestFeature`, so any 10.0.1xx
SDK satisfies it — 10.0.111 from the Ubuntu repo is fine.

#### 2. Native libraries

On a normal Mint desktop (Cinnamon, MATE or Xfce) there is **nothing to install**:
Avalonia needs X11, fontconfig and ICU, and a Mint desktop already has all three.
Cinnamon is an X11 session, so no Wayland caveats apply.

Only on a minimal or server install do you need them spelled out:

```bash
sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1 libicu74
```

#### 3. Clone

```bash
git clone https://github.com/pangtuwi/ESA.NET.git
cd ESA.NET
```

#### 4. Build

```bash
dotnet build ESA.NET.slnx -c Release
```

Expect **0 warnings, 0 errors** — `Directory.Build.props` sets
`TreatWarningsAsErrors`, so a warning is a failed build. The first run restores
NuGet packages and takes about a minute; later ones are seconds.

#### 5. Test

```bash
dotnet test ESA.NET.slnx -c Release
```

Expect **417 passed, 0 failed**, in roughly 45 seconds.

You do **not** need a display for this. The Avalonia tests
(`MenuStructureTests`, `ChartRenderingTests`) run through `Avalonia.Headless`,
which draws into memory and never opens an X connection — so the suite passes
unchanged over SSH.

Narrowing the run while working on one area:

```bash
dotnet test ESA.NET.slnx --filter "FullyQualifiedName~EngRoundTripTests"
dotnet test ESA.NET.slnx --filter "FullyQualifiedName~CycleSolver"
dotnet test ESA.NET.slnx -v n                 # per-test names as they run
```

The tests worth knowing by name are listed under [Tests](#tests) below.

#### 6. Run

```bash
dotnet run --project src/App.Ui
```

The shell window opens. `App.Ui` is an `OutputType=WinExe`, which on Linux simply
means the process detaches from the console — it is not a Windows-only setting,
and the app runs natively with no Wine or Mono involved.

On a headless box — a VM, a container, or SSH without X forwarding — wrap it:

```bash
sudo apt install -y xvfb
xvfb-run -a dotnet run --project src/App.Ui
```

#### 7. Smoke-test it against real data

The repo carries its own engine files, so you can confirm a working install
end to end without supplying any input of your own:

1. **File → Load…**, and open `data/baseline/A2China.eng` — the engine behind
   the phase 4 reference run documented in [BASELINE.md](BASELINE.md).
   `legacy/ESA/Data/Example1/Nissan1.eng` is a second option.
2. **File → Edit** (<kbd>Ctrl</kbd>+<kbd>E</kbd>) opens the eight-tab editor.
   Pressing OK rewrites the file **byte for byte** unless you changed a value —
   that is a guarantee the round-trip tests enforce, not an aspiration.
3. **Run → Single Point Simulation** (<kbd>Ctrl</kbd>+<kbd>R</kbd>) opens the
   **Single Speed Simulation** dialog — engine speed, total cycles, mass balance
   and which charts to draw, the same four things the original asks for before
   every run. Press **Run** and it integrates the cycle, with the charts updating
   live. There are no run controls on the main window; the original has none
   either.
4. **Text → PVT Trace** exports the crank-angle trace, the same format
   `data/baseline/` holds a reference copy of.

Side-file paths inside `.eng` files are a mix of bare names, backslash-relative
paths and absolute paths to long-dead drive letters. `LegacyPathResolver` copes
with all three, which is the only reason any of this resolves on Linux at all.

#### 8. Publish a standalone binary

```bash
dotnet publish src/App.Ui/App.Ui.csproj -c Release -r linux-x64 --self-contained
```

The result is an executable at
`src/App.Ui/bin/Release/net10.0/linux-x64/publish/App.Ui`, which carries its own
runtime and needs no SDK on the target. Drop `--self-contained` if the target
already has the .NET 10 runtime.

#### 9. When it goes wrong

| Symptom | Cause and fix |
|---|---|
| `dotnet: command not found` after the install script | `~/.dotnet` is not on `PATH`. Open a new terminal, or `export PATH="$HOME/.dotnet:$PATH"`. |
| `Unable to fetch some archives … 404 Not Found` | A stale apt index pointing at a superseded version. `sudo apt update`, then retry. |
| `A compatible .NET SDK was not found` | An SDK older than 10.0.100. Check `dotnet --list-sdks` against `global.json`. |
| `Unable to connect to X server` / `XOpenDisplay failed` | No display. Use `xvfb-run -a` as above. Tests never need this; only the app does. |
| Build fails on a warning | Intended — warnings are errors here. Fix the warning. |
| Text renders as boxes | A minimal install with no fonts: `sudo apt install -y fonts-dejavu-core`. |

---

### Variant 2 — VS Code

#### 1. Install VS Code

Prefer Microsoft's `.deb` over the Flatpak in Mint's Software Manager. The
Flatpak build is sandboxed and cannot see the `dotnet` you installed on the host
without extra permission juggling; the `.deb` just works:

```bash
sudo apt install -y wget gpg
wget -qO- https://packages.microsoft.com/keys/microsoft.asc \
  | gpg --dearmor | sudo tee /usr/share/keyrings/microsoft.gpg > /dev/null
echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft.gpg] \
https://packages.microsoft.com/repos/code stable main" \
  | sudo tee /etc/apt/sources.list.d/vscode.list
sudo apt update && sudo apt install -y code
```

This repo adds only VS Code, not the .NET feed, so it does not create the
mixed-source problem warned about above.

#### 2. Install the extensions

```bash
code --install-extension ms-dotnettools.csdevkit
code --install-extension ms-dotnettools.csharp
```

**C# Dev Kit** brings the solution explorer and the Test Explorer; it pulls in
the **C#** extension, which supplies the language server and debugger. Dev Kit is
free for individuals and for open-source work, but it is not itself open source
and it asks you to sign in — if you would rather not, install only
`ms-dotnettools.csharp`. Everything below still works apart from the Test
Explorer UI, which is Dev Kit's.

Use a recent Dev Kit: `ESA.NET.slnx` is the SDK 10 solution format, and older
releases cannot parse it. If your solution explorer comes up empty, that is the
reason — update the extension, or use *File → Open Folder*, which reads the
projects directly and does not care about the solution format.

#### 3. Open the folder

```bash
cd ESA.NET
code .
```

Open a C# file once and wait for the language server to finish loading — the
status bar stops showing project-loading activity. Until it does, Go To
Definition and IntelliSense will be patchy.

#### 4. Add the build and test tasks

The repo ships no `.vscode/` directory. Create `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": ["build", "${workspaceFolder}/ESA.NET.slnx", "-c", "Debug"],
      "problemMatcher": "$msCompile",
      "group": { "kind": "build", "isDefault": true }
    },
    {
      "label": "build release",
      "command": "dotnet",
      "type": "process",
      "args": ["build", "${workspaceFolder}/ESA.NET.slnx", "-c", "Release"],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "test",
      "command": "dotnet",
      "type": "process",
      "args": ["test", "${workspaceFolder}/ESA.NET.slnx"],
      "problemMatcher": "$msCompile",
      "group": { "kind": "test", "isDefault": true }
    }
  ]
}
```

<kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>B</kbd> builds. Compiler errors become
clickable entries in the Problems panel via the `$msCompile` matcher.

#### 5. Add the launch configuration

Create `.vscode/launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Run App.Ui",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/App.Ui/bin/Debug/net10.0/App.Ui.dll",
      "cwd": "${workspaceFolder}/src/App.Ui",
      "console": "internalConsole",
      "stopAtEntry": false
    }
  ]
}
```

<kbd>F5</kbd> builds and launches under the debugger; <kbd>Ctrl</kbd>+<kbd>F5</kbd>
runs without it. Breakpoints in view models and in the simulation core are hit
normally.

`cwd` is deliberately the project directory rather than the workspace root:
`ESA.ini` is looked for beside the executable, exactly as it sat beside `ESA.EXE`.
A missing `ESA.ini` is not an error — the store returns the same defaults Delphi's
`TIniFile` would — so this only matters once you have one.

> Both files are yours, not the repo's: `.vscode/` is **not** in `.gitignore`, so
> add it there first if you would rather not commit these.

#### 6. Run the tests from the editor

With C# Dev Kit installed, the beaker icon in the activity bar lists all 417
tests by class. Run or debug any one from there, or from the ▷ gutter icon beside
a `[Fact]`. Debugging a single test is the fastest way into the ported physics —
put a breakpoint in `CycleSolver` and run one case from `CycleSolverTests`.

Without Dev Kit, use the `test` task
(<kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> → *Run Test Task*) or the terminal.

#### 7. A headless VS Code

Over SSH or in a container, the tests run unchanged — they need no display. Only
<kbd>F5</kbd> does, and for that either forward X (`ssh -X`) or launch the app
from the integrated terminal under `xvfb-run -a` as in variant 1.

## Build and run on macOS

The three commands are the same as on Linux:

```bash
dotnet build ESA.NET.slnx -c Release
dotnet test  ESA.NET.slnx
dotnet run   --project src/App.Ui
```

Install the SDK with `brew install --cask dotnet-sdk` or the installer from
<https://dotnet.microsoft.com/download/dotnet/10.0>. Not exercised — see below.

## Verified on

What has actually been run, as opposed to what should work. Worth knowing if you
hit a difference and want a known-good reference point.

| Platform | Toolchain | Exercised |
|---|---|---|
| Windows 10 | VS Code with the C# Dev Kit, .NET SDK 10.0.400 | Build and run |
| Ubuntu 24.04 (the Mint 22 base) | .NET SDK 10.0.111 from `noble-updates/universe`, command line | Release build (0 warnings), 417 tests passing, app run under Xvfb, `dotnet publish -r linux-x64 --self-contained` and the resulting binary run |

Everything in the Ubuntu row applies to Mint 22, which is the same base with the
same `universe` repo enabled. Not yet exercised anywhere: Mint 21 / LMDE and the
`dotnet-install.sh` fallback, the VS Code route end to end on Linux, macOS, and
opening `ESA.NET.slnx` in Visual Studio or Rider.

---

## Where your data goes

The application keeps its own data folder, `Documents/ESA`, created the first
time you run a simulation:

```
Documents/ESA/
  README.txt                      what the folders are for
  Engines/                        your .eng files and the side files they name
  Runs/
    2026-08-28_141530_A2China/    one folder per run, newest last
      run.txt                     what was run, and what came out
      inputs/                     copies of the .eng and every side file it read
      SimulDat.txt                the performance row
      Lastcyc.txt                 the full-cycle PVT trace, 720 rows
      Inlet.txt Exhaust.txt Pcyl.txt Tcyl.txt MassFlow.txt
      InlPress.m InlVel.m ExhPress.m ExhVel.m
```

A multi-point sweep puts every row in one folder, each row in a
`Row01_4000rpm` subfolder of its own, with a single `SimulDat.txt` at the top
carrying all of them.

Nothing in `Runs` is ever read back, so old runs can be deleted or moved
freely. Nothing is written next to your engine files.

To put the folder somewhere else, set `Data` under `[Folders]` in `ESA.ini`
beside the executable:

```ini
[Folders]
Data=D:\Engine Work
```

or set the `ESA_DATA_ROOT` environment variable, which wins over both:

```bash
ESA_DATA_ROOT=/tmp/esa dotnet run --project src/App.Ui
```

The original had none of this: it opened its output files under bare relative
names, so they landed in whatever the working directory happened to be and the
next run overwrote them — see `ISSUES.md` C4.

## Repository layout

```
src/App.Core          Domain models, business rules, interfaces. No UI framework, ever.
src/App.Persistence   Legacy file and INI readers/writers implementing Core interfaces.
src/App.Ui            Avalonia views and view models. Binding only, no logic.
tests/App.Tests       xUnit tests.
legacy/               The original Delphi source, untouched. Reference material.
legacy/samples/       Frozen .eng fixtures used by the round-trip test.
data/baseline/        A captured reference run of the original app, for phase 4.
SPEC.md               Reverse-engineered specification of the Delphi application.
BASELINE.md           What the reference run contains and how to validate against it.
ISSUES.md             Known issues: port defects, reproduced legacy defects, SPEC errors.
CLAUDE.md             Layering rules, naming conventions, port caveats, phase plan.
archive/              Working notes that produced SPEC.md.
```

The layering table above is enforced, not merely documented:
`tests/App.Tests/LayeringTests.cs` fails the build if `App.Core` or
`App.Persistence` ever picks up a reference to a UI framework.

## Tests

`dotnet test ESA.NET.slnx` runs 444 tests. The ones that matter most guard user
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
- `RunArchiveTests`, `RunFolderWiringTests` — every run leaves a folder holding
  its results and byte-identical copies of the files it read.

## Credits

The original ESA was written by Paul Williams. The manifold pressure-wave solver
originates with Christie M. van Vuuren, and the equilibrium combustion model with
the author credited as "Arthur" in `legacy/ESA/Eqbm.pas`. The Delphi source in
`legacy/` is included as reference material for the port and is not modified.

`legacy/ESA/Components/adcalc41_paid/` contains the third-party AdCalc expression
evaluator under its own licence; see `LICENSE.TXT` in that directory. It is
reference material only: phase 3 replaced it with a native implementation in
`src/App.Core/Expressions`, and no AdCalc code is compiled into this port.
