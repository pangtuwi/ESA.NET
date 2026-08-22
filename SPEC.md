<!-- Superseded by SPEC2.md. Retained unchanged as the original reverse-engineering snapshot. -->

# ESA Legacy Application Specification

This document describes the Delphi application in `Original_ESA/ESA`. It is a reverse-engineering record for a compatible .NET implementation. The legacy source and forms were not modified.

## 1. Form inventory

### `Main.pas` / `Main.dfm` — `TFMain`

The main window owns application startup, simulation execution, result display, and menus. Its layout contains a main menu, status bar, memo, three TeeChart charts, and a large collection of labels/panels for engine and performance results.

Charts and displayed data:

- `Chart1`: manifold gas-flow traces, including pressure, velocity, and mass-related series.
- `Chart2`: cylinder pressure and P-V traces.
- `Chart3`: in-cylinder properties.
- Status panels show file/engine name, run time, cycle count, mass balance, speed, torque, power, volumetric efficiency, fuel consumption, SFC, cylinder mass, work, heat loss, pumping work, friction, exhaust/fuel energy, IMEP/BMEP/FMEP/PMEP, and valve event crank angles.

Important handlers:

- `FormCreate`: initializes application state and reads INI settings through `IniValues`.
- `FormShow`: displays `FWelcome`; the welcome form closes automatically after its timer interval.
- `FormResize`: recalculates the display layout.
- `FormClose`: performs application shutdown/cleanup.
- `SinglePointSimulation1Click`: displays `FSimulateOptions`; after acceptance, calls the simulation routine.
- `MultiPointSimulation1Click`: displays `FMultiRun`, then runs each accepted grid row and adds results to `PerformanceData`.
- `QuickRunClick`: runs using the current/default settings without the options dialog.
- `STOP1Click`: sets `Running := FALSE`; the simulation loop observes this flag.
- `Pause1Click`: toggles `Paused`. While paused, the message loop continues but the engine step is skipped.
- `Load1Click`, `SaveAs1Click`, and `LoadDefault1Click`: load/save `.eng` engine definitions using INI text files.
- `Edit1Click`: displays `FEdit`.
- `PVTTrace1Click`: displays `FPVTData`.
- `Options1Click`: displays `FGraphOptions`.
- `ShowTorqueCurve1Click`: displays `FTorqueCurve`.
- `ValveOpening1Click`: displays `FValveLift`.
- `HeatLoss1Click`: displays `FEnergyBalance`.
- `Timer1Timer`: refreshes status, charts, and performance labels.
- `Chart2Zoom`: adjusts P-V chart scaling.

The central simulation loop is effectively:

```pascal
for i := 1 to NoCycles do
  with Engine2z do
  begin
    if (i >= No1zCycles + 1) and (FEdit.RB2Zone.Checked = TRUE) then
      NoZones := 2
    else
      NoZones := 1;

    if abs(TotalMInIV-TotalMOutEV)*1e6 < MassBalance then
      NoCycles := i;
      Running := FALSE;

    CA := Manifold.IV.C;
    repeat
      Application.ProcessMessages;
      if not Paused then
      begin
        Engine2z.Run;
        CurrCAList.UpdateCAPoint;
        if (abs(round(CA)) mod 5) < 1 then
          UpdateGraphs(CA);
        FPVTData.UpDateList(CA);
        CA := CA + dCA;
        if CA > 360 then CA := CA - 720;
      end;
    until Abs(CA-Manifold.IV.C) < dCA;
  end;
```

The exact statement grouping and formatting must be checked against the source when porting; the important behavior is the cycle loop, message processing, pause/stop flags, mass-balance convergence test, zone switch, and crank-angle traversal.

### `Edit.pas` / `Edit.dfm` — `TFEdit`

The engine-definition editor is organized into tabs for general cylinder geometry, exhaust, cams, valves, heat transfer/model settings, manifold files/functions, and calculation settings.

Controls represent:

- engine name, cylinder count, bore, stroke, compression ratio, and connecting-rod length;
- inlet/exhaust manifold area files and grid functions;
- IVO, IVC, EVO, EVC, lift values, and cam profile files;
- valve counts, diameters, and four discharge-coefficient tables;
- wall-temperature file and Woshini heat-transfer coefficient;
- fuel burn angle, spark schedule, AFR, fuel temperature, fuel energy, and lambda;
- atmospheric/oil conditions;
- variable-gamma, manifold-save, integrator, and performance-output options.

Handlers:

- `FormCreate`: initializes controls.
- `FormShow`: loads the current engine definition through `LoadTextFile`.
- `BOKClick`: calls `ReadFromEdits`, validating and applying values to `Engine2z`.
- `BLoadClick` and `BSaveClick`: load/save INI-format engine files.
- `ECCChanged`: recalculates capacity using `Cyl * Pi/4 * Bore^2 * Stroke / 1000`.
- Browse/edit buttons open profile, manifold-area, discharge-coefficient, wall-temperature, and exhaust-data editors or file dialogs.

Conversions and validation are performed in the editor before values reach the engine model. `BOKClick` contains a conversion exception handler that swallows `EConvertError` without presenting a corrective message.

### `formsimul.pas` / `formsimul.dfm` — `FSimulateOptions`

The simulation-options dialog edits RPM, cycle count, mass-balance tolerance, and graph selections. It enforces an RPM range of approximately 1250-7000 and a minimum cycle count of 3. Radio options support all graphs on, all off, or individual selection. On close/accept it updates `NoCycles`, `MassBalance`, and `Engine2z.Nrpm`.

### `MultiRun.pas` / `MultiRun.dfm` — `FMultiRun`

A `TStringGrid` (`SG1`) stores up to 100 parameter rows. The columns are:

`No`, `Speed`, `Iters`, `IManfFile`, `EManfFile`, `ICamFile`, `ECamFile`, `IVO`, `IVC`, `EVO`, `EVC`, `IValveLift`, `EValveLift`, `Spark °BTDC`, and `Burn Angle`.

`GetMultiRunVar` and `GetMultiRunStr` interpret cells, with `-` acting as a null value. `SaveGrid` and `LoadGrid` persist the grid as delimited text. `BOkClick` finds the first row containing `-`, sets `NoRuns`, and enables the multi-run operation through `OkToMultiRun`.

### `PVTDataForm.pas` / `PVTDataForm.dfm` — `FPVTData`

Displays a 29-column `TStringGrid`: crank angle plus the 28 values maintained by `TCAList`. `BSaveAsClick` exports the current cycle through `CurrCAList.SendToFile()`.

Columns 1-28 are: cylinder volume, cylinder pressure, cylinder mass, burnt mass, unburnt mass, inlet mass, outlet mass, burnt volume, unburnt volume, burnt temperature, unburnt temperature, burnt heat, unburnt heat, gamma, fuel mass, inlet-valve area, exhaust-valve area, inlet velocity, exhaust velocity, inlet pressure, exhaust pressure, work, pump work, CO, NO, CO2, HC, and heat loss.

### Other forms

- `AboutBoxUnit.pas` / `.dfm` — `TAboutBox`: static product/version/about information.
- `Welcome.pas` / `.dfm` — `TFWelcome`: splash screen; timer closes it after approximately three seconds.
- `TorqueCurve.pas` / `.dfm` — `FTorqueCurve`: plots torque, power, and volumetric efficiency against RPM from `PerformanceData`.
- `TCurveOptions.pas` / `.dfm` — `FTorqCurveOptions`: edits RPM, torque/power, and efficiency axis limits.
- `FflowGraphOptions.pas` / `.dfm` — `TFGraphOptions`: selects manifold pressure/velocity/mass mode, P-V display mode, and in-cylinder display mode, with optional Y-axis limits.
- `Flowgraph.pas` / `.dfm` — `TFFlowGraph`: displays valve discharge coefficient as a 3D surface over pressure ratio and lift ratio.
- `FManfA.pas` / `.dfm` — `TFManfArea`: edits and plots a one-dimensional manifold area-versus-length table, up to 50 points.
- `IPolTab.pas` / `.dfm` — `TFIpol`: edits a two-dimensional discharge-coefficient table, up to 20 by 20 points, and uses bilinear interpolation.
- `GHeatLoss.pas` / `.dfm` — `TFEnergyBalance`: plots heat loss, work, and pump work against crank angle.
- `GValveLift.pas` / `.dfm` — `TFValveLift`: plots inlet and exhaust lift profiles using `TValve.Lift()`.

## 2. Data structures

No `packed record` or `file of record` declaration was identified in the application units reviewed. The important types are classes, arrays, pointer-linked records, and text-file tables.

### Engine and thermodynamic classes

`TEngine2z` in `ICEngine2Z.pas` derives from `TRKF` and owns the simulation state. Its fields include:

- `Name: ShortString`;
- `NoZones`, `State`, `OldState`, `NCyl`, `NEqns`, `NCycles: Integer`;
- `INIT2Z`, `TWOZOVERLAP`, `SAVEMANFDATA`, `VariableGamma: Boolean`;
- crank state: `CA`, `dCA`, `Nrpm`, `wcrank`;
- geometry: `Bore`, `Stroke`, `CR`, `ConrodLength`;
- cylinder-at-IVC values: `PCylIVC`, `TCylIVC`, `VCylIVC`;
- gas objects: `Plenum`, `Exh`, `Cyl`, `Atm: TGas2z`;
- `Manifold: TManifolds`, `WallTemp: TWallTemps`, `SparkAngle: TVarSpeedList`, `FireOrder: ShortString`;
- flow/mass values including `MIn`, `Mout`, `dPMass`, inlet/exhaust pressure and velocity;
- `Emmissions: EqSpecArray`;
- performance values including `FMEP`, `IMEP`, `PMEP`, `BMEP`, torque, power, SFC, efficiencies, peak pressure/temperature, and energy-balance values;
- convergence values `TotalMInIV`, `TotalMOutEV`, `TotalMass`, and `ResidialFraction`;
- inherited integration state `y: yarray` and `fn: array[1..MaxN] of dxdyFunction`.

`TGas2Z` in `Gasses2Z.pas` contains pressure, mass, burnt/unburnt fractions and masses, volumes, volume derivative, temperatures, energies, gas constant, mass derivatives, enthalpy, gamma, fuel, burnt/unburnt property objects, derivatives, and spark angle.

`TFuel` in `Fuel.pas` contains `Q`, `T`, `AFRatio`, `Lambda`, `BurnAngle`, and `m` as `Double`, plus elemental composition `C`, `H`, `O`, and `N` as `Integer`.

`TProp` in `GASPROPS.PAS` owns an equilibrium calculator and species arrays, fuel composition/type fields, and thermodynamic lookup/calculation state.

`TEqbm` in `Eqbm.pas` contains species fractions and derivatives as `EqSpecArray`, an error code, frozen flag, and equilibrium coefficient tables. `EqSpecArray` is `array[1..12] of Extended`, ordered as H, O, N, H2, OH, CO, NO, O2, H2O, CO2, N2, and Ar.

### Integration

`TRKF` in `RKf5.pas` defines:

```pascal
type
  yarray = array[1..MaxN] of Double;
  dxdyFunction = function(x: Double; y: yarray): Double;
```

It stores `NEqns`, `Integrator`, `x`, `dx`, `y`, and four function pointers. Integrator 0 is RKF5; integrator 1 is Euler.

### Valves, profiles, pipes, and manifolds

`TValve` contains valve count, open/close crank angles, diameter, maximum lift, a `TProfile`, and forward/reverse `TCdValve` tables.

`TPoint` in `Profiles.pas` is a pointer-linked record:

```pascal
type
  PPoint = ^TPoint;
  TPoint = record
    x, y: Double;
    next: PPoint;
  end;
```

`TProfile` stores point count, spacing/lift/duration, linked-list pointers (`First`, `Current`, `OldFirst`, `OldCurrent`), status flags, bounds, and filename. This is heap-linked runtime state, not a stable serialized layout.

`TAManf` stores `Cell` and `Index` arrays sized `[1..maxx]`, a point count, and filename. `TCdValve` stores `Cell`, `xIndex`, `yIndex`, counts, and filename; the table capacity is `[1..maxxy, 1..maxxy]` with `maxxy = 20`.

`TPipe` owns an area table and insertion position/length values. `TManifolds` owns inlet/exhaust valves and pipes, exhaust pressure/temperature data, a plenum-pressure function, inlet/exhaust grid functions, flow functions, and fixed-capacity calculation arrays. The source constants are `NI = 68` and `NE = 38`.

### Captured cycle data

`TCAPoint` contains `Value: array[1..28] of Double` and exposes properties for the tracked quantities. `TCAList` contains `CaVar: array[-359..360] of TCAPoint`, plus column names, decimal counts, and display scale factors. This represents 720 crank-angle positions in memory.

### Lookup and performance classes

`TWallTemps`, `TExhaustPandT`, and `TVarSpeedList` use dynamic arrays of doubles and linear interpolation. The first stores RPM, head, piston, upper-liner, and lower-liner temperatures. The second stores RPM, exhaust pressure, exhaust temperature, and `PAtm`. The third stores RPM/value pairs.

`TPerfPoint` stores speed, torque, power, and volumetric efficiency. `TPerfData` stores up to `MaxNoPoints = 100` points.

`TDoubFunc` and `TGridSize` store expression strings, an `TAdCalc` expression evaluator, and the controls/function state required to evaluate user-defined functions.

### On-disk compatibility flags

- `ShortString` is length-prefixed in Delphi: one length byte followed by up to 255 characters. It is used for engine name/fire-order fields, but the engine definition itself is text INI, not a raw object dump.
- The pointer-linked `TPoint` records, dynamic arrays, Delphi objects, and `Extended` values are runtime representations and must not be serialized by copying memory.
- No binary record layout was found in the reviewed application source. The `SAVEMANFDATA` path needs confirmation because its complete writer was not established.

## 3. Persistence

### Engine definitions

`.eng` files are text INI files read and written with `TIniFile`-style sections. The source uses sections equivalent to:

```ini
[Cylinders]
Name=...
NoCyls=...
Bore=...
Stroke=...
CR=...
ConrodLength=...

[HeatTransfer]
TempFile=...
CWoshini=...

[Inlet]
AreaFile=...
FPlenumP=...
InletGrid=...
IVRFn=...
IVFFn=...
IVFRFn=...

[Exhaust]
AreaFile=...
ExhBackFile=...
ExhaustGrid=...
EVRFn=...
EVFFn=...
EVFRFn=...

[Cams]
IVO=...
IVC=...
EVO=...
EVC=...
IVLift=...
EVLift=...
IVProfile=...
EVProfile=...

[Valves]
IVNo=...
EVNo=...
IVDiam=...
EVDiam=...
CdIVIn=...
CdIVOut=...
CdEVIn=...
CdEVOut=...

[Fuel]
BurnAngle=...
SparkAngle=...
AFRatio=...
TFuel=...
QFuel=...
Lambda=...

[Conditions]
TAtm=...
PAtm=...
vOil=...

[Calculation]
VariableGamma=...
SaveManfData=...
Integrator=...
PerfDataSave=...
```

Values are stored as text. There is no packed-record byte layout for `.eng` files.

### Application INI

`ESA.ini` stores application defaults and simulation settings. The observed keys include:

```ini
[DefaultFiles]
ErrorLog=ESA2z1z.err
TextSave=Lastcyc.txt
Engine=Default.eng

[Simulation]
EngineSpeed=4000
Nocycles=6
No1zcycles=1
MassBalance=1
```

`IniValues.SaveIniValues` exists but is empty; settings are primarily written through the relevant form/engine save paths.

### External data tables

The following are text files loaded into interpolation structures:

- `.maf`: manifold position/area points, loaded into `TAManf.Index` and `Cell`, with linear interpolation.
- `.vcd`: valve discharge-coefficient two-dimensional tables, loaded into `TCdValve` and interpolated bilinearly.
- `.cam`: cam profile points, loaded into the linked list in `TProfile`.
- `.spk`: RPM/spark-angle pairs, loaded into `TVarSpeedList`.
- `.cwt`: RPM and wall-temperature columns, loaded into `TWallTemps`.
- `.exh`: RPM, exhaust pressure, and exhaust temperature columns, loaded into `TExhaustPandT`.

The exact delimiters and headers should be treated as defined by the individual `Load` routines, rather than inferred from sample data. All indexed tables use linear interpolation between neighboring rows.

### Runtime exports and logs

- PVT export is delimited text/CSV, with crank angle and 28 columns from `TCAList.SendToFile()`; display scale factors are applied.
- Multi-run grids are persisted as delimited text by `FMultiRun.SaveGrid` and restored by `LoadGrid`.
- `ErrorLog.pas` appends textual errors/messages to a log file.
- No database, BDE, ADO, or registry access was identified.
- No confirmed binary file-of-record format was identified.

## 4. External dependencies

### Delphi/VCL

The application uses standard Delphi VCL forms and controls: `TForm`, panels, labels, edits, memos, buttons, menus, grids, timers, dialogs, radio groups, and checkboxes. Direct .NET equivalents are Windows Forms or WPF controls; `TStringGrid` maps most directly to `DataGridView`.

`TIniFile`, Delphi file I/O (`AssignFile`, `Reset`, `Rewrite`), `Application.ProcessMessages`, and the VCL message loop require .NET replacements using an INI parser, `System.IO`, and a UI dispatcher/message-pump-safe design.

### TeeChart

`TChart`, `TFastLineSeries`, `TLineSeries`, and `TSurfaceSeries` are used for the plots. Options for a port include OxyPlot, LiveCharts, the WinForms chart control, or another charting library. The surface chart requires a library with 3D/surface support or a deliberate replacement.

### AdCalc

`Components/adcalc41_paid` supplies `TAdCalc`, used by `TDoubFunc` and `TGridSize` to evaluate expressions involving `N` (engine speed). This proprietary component is a direct third-party dependency and should not be assumed available to a .NET port. It needs a compatible expression parser/evaluator or a licensed wrapper.

### Win32 and system APIs

No explicit Win32 API call was identified. Windows behavior is reached through VCL. `Printers` is imported by `Main.pas`, but no implemented print workflow was found.

### .NET mapping summary

| Delphi dependency | Porting direction |
|---|---|
| VCL forms/controls | Windows Forms or WPF |
| TeeChart | OxyPlot, LiveCharts, WinForms charting, or replacement renderer |
| AdCalc | Compatible expression parser/evaluator |
| TIniFile | INI parser or dedicated configuration class |
| Delphi file I/O | `System.IO` |
| `Math` routines | `System.Math` |
| Pointer-linked profile | managed linked objects or list |

## 5. Business rules

### Crank-angle state machine

`TEngine2z.GetState` divides the cycle into six states:

1. Compression
2. Combustion
3. Expansion
4. Exhaust
5. Overlap
6. Intake

The source logic is equivalent to:

```pascal
if Theta < Manifold.EV.C then
  GetState := Overlap
else if Theta < Manifold.IV.C then
  GetState := Intake
else if Theta < Cyl.ThetaSpark then
  GetState := Compression
else if Theta < Cyl.ThetaSpark+Cyl.Fuel.Burnangle then
  GetState := Combustion
else if Theta < Manifold.EV.O then
  GetState := Expansion
else if Theta < Manifold.IV.O then
  GetState := Exhaust
else
  GetState := Overlap;
```

State changes select different ODE functions and initialize/reset state-specific quantities. Combustion couples burnt and unburnt zone volume, pressure, and temperatures. Expansion treats the charge as burned. Exhaust resets exhaust-flow accumulation. Intake absorbs residual gases. Overlap uses frozen equilibrium and simplified equations.

### One-zone/two-zone switching

`Main.Simulate` runs the first `No1zCycles` cycles as one-zone when configured, then switches to two-zone for later cycles. The switch is controlled by the editor's one-zone/two-zone radio buttons and the cycle number.

### Convergence

The run stops early when:

```pascal
if abs(TotalMInIV-TotalMOutEV)*1e6 < MassBalance then
  NoCycles := i;
  Running := FALSE;
```

The intended unit is micrograms after multiplying the kilogram difference by $10^6$. The default INI value is 1. The simulation also supports user stop and pause flags; `Application.ProcessMessages` keeps the UI responsive during calculation.

### ODE integration

The two-zone state vector has four values:

- `y[1]`: burnt-zone volume;
- `y[2]`: cylinder pressure;
- `y[3]`: burnt-zone temperature;
- `y[4]`: unburnt-zone temperature.

RKF5 is the normal integrator, with Euler as an alternate setting. The manifold solver uses fixed-capacity inlet/exhaust arrays (`NI = 68`, `NE = 38`) and user-selected grid functions.

### Heat transfer

`TEngine2z.hWoshini` uses a state-dependent coefficient and an empirical Woshini-style correlation. The source includes logic equivalent to:

```pascal
case State of
  Compression, Combustion, Expansion: C1 := 2.28;
  Exhaust, Overlap, Intake: C1 := 6.18;
end;

hwoshini := WoshiniCoeff * Pwr(Bore, -0.2)
  * Pwr(P/101325, 0.8)
  * Pwr(T, -0.53)
  * Pwr(w, 0.8);
```

`WoshiniCoeff` is configured through the editor, with the observed default around 131.

### Fuel and combustion

Fuel mass, burn duration, spark timing, AFR, lambda, and elemental composition feed the equilibrium/two-zone model. `TEqbm` calculates a 12-species equilibrium mixture. The implementation contains frozen-equilibrium behavior around low temperature, but the exact active threshold and all call sites must be preserved from the source rather than normalized during a port.

### Performance and energy

The application calculates IMEP, FMEP, PMEP, BMEP, torque, brake/indicated power, SFC, mechanical/overall/thermal efficiency, peak pressure/temperature, volumetric efficiency, and heat/work/exhaust/fuel energy values. Cycle traces expose work, pump work, emissions, and heat loss. Multi-run execution stores up to 100 points for the torque curve.

### Interpolation and validation

Manifold areas and RPM-keyed schedules use linear interpolation. Discharge-coefficient tables use bilinear interpolation. The editor recalculates capacity from geometry and validates numeric fields. The multi-run grid uses `-` as a missing-value marker and stops at the first incomplete row.

## 6. Dead code and suspicious paths

These items should be retained as observations, not silently removed during a compatibility port:

- `IniValues.SaveIniValues` is declared but empty and appears unused.
- `Main.pas` imports `Printers`, but no printing handler/menu workflow was found.
- `DoubleFunc.pas` contains commented-out string-list allocation/cleanup.
- `Edit.pas` catches `EConvertError` without displaying a corrective error.
- The two-zone `Overlap` path contains commented-out gas-exchange ODE assignments and uses simplified equations.
- `Gasses2Z.pas` contains a suspicious burnt-volume clamp: `if Vb > Vgas then Vb := Vgas`.
- `TExhaustPandT.PAtm` is used as a pressure baseline but appears not to be assigned by `Load`; this may make the effective baseline zero.
- The simulation does not visibly reset every cumulative output at the beginning of each cycle; determine which values are intentionally cumulative before porting.
- Profile points are manually allocated through `New`; repeated profile loads need confirmation that old linked lists are disposed.
- The editor allows grid settings while manifold arrays have fixed capacities of 68 and 38; confirm whether the configured values are limits, active counts, or a potential mismatch.

## Questions for Paul

1. Is the burnt-volume clamp in `Gasses2Z.pas` an intended numerical safeguard, or should exceeding cylinder volume be reported as an error?
2. What operating conditions and expected cycle count normally satisfy the 1 microgram mass-balance tolerance? Is three cycles always a valid minimum?
3. Is the simplified, frozen-equilibrium overlap model intentional, or should the commented gas-exchange equations be restored in a compatible implementation?
4. Is the combustion fuel-mass formula's `AFRatio + 1` intentional?
5. Are the state-specific Woshini constants empirically validated calibration values?
6. Does AdCalc parse/compile expression strings once or on every evaluation? This affects a .NET replacement's performance design.
7. Should equilibrium emissions freeze below a particular temperature, and if so what threshold is authoritative?
8. Which engine values are intended to accumulate across cycles and which should reset each cycle?
9. Are old `TProfile` linked lists freed when a new engine definition is loaded?
10. How do user-configured inlet/exhaust grid sizes relate to the fixed capacities 68 and 38?
11. Can multi-run count exceed `MaxNoPoints = 100`, and what user-visible behavior is expected?
12. Does `SAVEMANFDATA` produce a file? If so, what filename, encoding, and record/column layout must remain compatible?
13. Are the sample `.maf`, `.vcd`, `.cam`, `.spk`, `.cwt`, and `.exh` files the complete authoritative format examples, or are there additional legacy variants?
14. Should `.eng` and exported text files remain ANSI-compatible with the original Delphi `TIniFile` and file readers, or may the .NET implementation standardize on UTF-8?
