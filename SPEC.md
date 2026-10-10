# ESA Legacy Application Specification (Revision 3)

This document supersedes `archive/SPEC2.md` for the .NET port. It incorporates the answers in `archive/answers2.md`. The Delphi source and forms remain unchanged.

Corrected on 2026-10-10 against `ISSUES.md` section D, which lists where this phase 1 document disagreed with the source and the shipped data. Each correction is marked with the D entry it comes from; where they disagree, the source and the data win.

## 1. Form inventory

### `Main.pas` / `Main.dfm` - `TFMain`

The main form owns startup, simulation execution, result display, and menus. It contains a main menu, status bar, memo, three TeeChart charts, and labels/panels for engine and performance results.

The `.dfm` files are **binary** DFM, not the text form: their component names, captions and event handlers have to be recovered by string extraction. The main menu, as recovered from `Main.dfm` (the port's `MenuStructureTests` pins it):

- **File**: Load..., Save As..., Edit, Load Default, Exit.
- **Run**: Single Point Simulation, Multi Point Simulation, Pause, STOP, QuickRun. The original assigned `Ctrl+Q` to both Exit and QuickRun (`ISSUES.md` C8).
- **Graph**: Run-Time Graph Options, Torque Curve, Valve Opening, Energy Balance. The original drew its P-V, in-cylinder and gas-flow charts inside the main form, switched by the Run-Time Graph Options dialog, not by menu items.
- **Text**: PVT Trace.
- **Help**: User Manual, About.

(Corrected: `ISSUES.md` D7.)

- `Chart1`: manifold gas-flow traces.
- `Chart2`: cylinder pressure and P-V diagram.
- `Chart3`: in-cylinder properties.
- Status fields: file/engine name, run time, cycle count, mass balance, speed, torque, power, volumetric efficiency, fuel consumption, SFC, cylinder mass, work, heat loss, pumping work, friction, exhaust/fuel energy, IMEP/BMEP/FMEP/PMEP, and valve event crank angles.

Important handlers:

- `FormCreate` initializes application state and reads INI settings through `IniValues`.
- `FormShow` displays `FWelcome`; its timer closes the splash screen.
- `FormResize` recalculates the display layout.
- `FormClose` performs shutdown/cleanup.
- `SinglePointSimulation1Click` displays `FSimulateOptions` and starts a simulation after acceptance.
- `MultiPointSimulation1Click` displays `FMultiRun`, then processes accepted rows.
- `QuickRunClick` starts a simulation with current settings.
- `STOP1Click` sets `Running := FALSE`; the simulation loop observes this flag.
- `Pause1Click` toggles `Paused`; message processing continues while engine steps are skipped.
- `Load1Click`, `SaveAs1Click`, and `LoadDefault1Click` load/save `.eng` INI text files.
- `Edit1Click`, `PVTTrace1Click`, `Options1Click`, `ShowTorqueCurve1Click`, `ValveOpening1Click`, and `HeatLoss1Click` show their child forms.
- `Timer1Timer` refreshes status, charts, and result labels.
- `Chart2Zoom` adjusts P-V chart scaling.

The run advances crank angle in steps, processes UI messages, invokes `Engine2z.Run`, captures PVT data, and checks mass balance between cycles. One-zone/two-zone switching occurs after `No1zCycles` when two-zone mode is selected. The run may terminate early when mass-balance error is below the configured tolerance. Three cycles is a valid minimum.

### `Edit.pas` / `Edit.dfm` - `TFEdit`

The editor contains tabs for cylinder geometry, exhaust, cams, valves, heat transfer/model settings, manifold files/functions, and calculation settings. It edits engine name, cylinder count, bore, stroke, compression ratio, connecting-rod length, manifold area and grid functions, valve timing/lift/profile files, valve counts/diameters, discharge-coefficient tables, wall temperatures, Woshini coefficient, fuel data, atmospheric/oil conditions, variable-gamma, manifold-save, integrator, and performance-output settings.

- `FormCreate` initializes controls.
- `FormShow` loads the current engine definition through `LoadTextFile`.
- `BOKClick` calls `ReadFromEdits` and applies validated values to `Engine2z`.
- `BLoadClick` and `BSaveClick` load/save INI-format engine files.
- `ECCChanged` recalculates capacity as `Cyl * Pi/4 * Bore^2 * Stroke / 1000` in the editor's displayed units.
- Browse/edit buttons open profile, manifold-area, discharge-coefficient, wall-temperature, and exhaust-data editors or dialogs.

`BOKClick` catches `EConvertError` without displaying a corrective message.

### Other forms

- `formsimul.pas` / `.dfm` - `FSimulateOptions`: RPM, cycle count, mass-balance tolerance, and graph selection. RPM is approximately 1250-7000; minimum cycle count is 3.
- `MultiRun.pas` / `.dfm` - `FMultiRun`: 15-column `TStringGrid` for speed, iterations, file names, valve timing/lift, spark, and burn angle. It supports up to 100 rows and uses `-` for missing values.
- `PVTDataForm.pas` / `.dfm` - `FPVTData`: 29-column grid containing crank angle plus 28 captured quantities; saves through `TCAList.SendToFile`.
- `AboutBoxUnit.pas` / `.dfm` - static about dialog.
- `Welcome.pas` / `.dfm` - timed splash screen.
- `TorqueCurve.pas` / `.dfm` - torque, power, and volumetric-efficiency charts from `PerformanceData`.
- `TCurveOptions.pas` / `.dfm` - torque-curve axis limits.
- `FflowGraphOptions.pas` / `.dfm` - manifold pressure/velocity/mass mode, P-V mode, in-cylinder mode, and Y-axis limits.
- `Flowgraph.pas` / `.dfm` - discharge-coefficient surface plot.
- `FManfA.pas` / `.dfm` - one-dimensional manifold area-versus-length table, up to 50 points.
- `IPolTab.pas` / `.dfm` - two-dimensional discharge-coefficient table, up to 20 by 20, with bilinear interpolation.
- `GHeatLoss.pas` / `.dfm` - heat loss, work, and pump-work chart.
- `GValveLift.pas` / `.dfm` - inlet and exhaust valve-lift charts.

## 2. Data structures

No `packed record` or `file of record` declaration was identified. Runtime data uses Delphi objects, arrays, and pointer-linked records; these must not be serialized by copying object memory.

### Engine and thermodynamic model

`TEngine2z` in `ICEngine2Z.pas` derives from `TRKF`. Important fields include:

- `Name: ShortString`, `FireOrder: ShortString`;
- `NoZones`, `State`, `OldState`, `NCyl`, `NEqns`, `NCycles`, and `tstep`;
- `INIT2Z`, `TWOZOVERLAP`, `SAVEMANFDATA`, and `VariableGamma`;
- `CA`, `dCA`, `Nrpm`, `wcrank`;
- `Bore`, `Stroke`, `CR`, `ConrodLength`, and `Vd`;
- gas objects `Plenum`, `Exh`, `Cyl`, and `Atm: TGas2z`;
- `Manifold: TManifolds`, `WallTemp: TWallTemps`, and `SparkAngle: TVarSpeedList`;
- pressure, temperature, mass, flow, EGR, and valve-pressure state;
- `Emmissions: EqSpecArray`;
- work, MEP, torque, power, SFC, efficiency, peak, and energy-balance fields;
- `TotalMInIV`, `TotalMOutEV`, `TotalMass`, `MbOutInlet`, `MuOutExhaust`, and `ResidialFraction`;
- inherited `y: yarray` and four ODE function pointers.

`TGas2Z` stores pressure, mass, burnt/unburnt mass and volume, temperatures, energies, gas constants, mass derivatives, enthalpy, gamma, fuel, property calculators, and spark angle. `TFuel` stores `Q`, `T`, `AFRatio`, `Lambda`, `BurnAngle`, and `m` as `Double`, with elemental composition `C`, `H`, `O`, and `N` as `Integer`.

`TEqbm` stores 12-species arrays and derivatives. `EqSpecArray` is `array[1..12] of Extended`, ordered H, O, N, H2, OH, CO, NO, O2, H2O, CO2, N2, Ar. `TProp` owns equilibrium and thermodynamic property state.

### Integration

```pascal
type
  yarray = array[1..MaxN] of Double;
  dxdyFunction = function(x: Double; y: yarray): Double;
```

`TRKF` stores `NEqns`, `Integrator`, `x`, `dx`, `y`, and four function pointers. Integrator 0 is RKF5 and integrator 1 is Euler.

### Valves, profiles, pipes, and manifolds

`TValve` contains valve count, open/close crank angles, diameter, maximum lift, a `TProfile`, and forward/reverse `TCdValve` tables.

`TPoint` in `Profiles.pas` is:

```pascal
type
  PPoint = ^TPoint;
  TPoint = record
    x, y: Double;
    next: PPoint;
  end;
```

`TProfile` stores point count, spacing, linked-list pointers (`First`, `Current`, `OldFirst`, `OldCurrent`), status flags, limits, lift, duration, and filename. `AddPoint` allocates nodes with `New`. `Clear` disposes the complete list, `Destroy` calls `Clear`, and `LoadText` calls `Clear` before loading a replacement profile. Old profile lists are therefore intended to be released when definitions are reloaded.

`TAManf` stores position and area arrays up to `maxx` points. `TCdValve` stores a two-dimensional table and axes up to `maxxy = 20`. `TPipe` owns an area-versus-length table and insertion values.

`TManifolds` owns inlet/exhaust valves and pipes, exhaust pressure/temperature data, a plenum-pressure function, grid functions, valve-flow functions, throat values, and fixed-capacity flow arrays:

```pascal
const
  NI = 68;
  NE = 38;

type
  TInletCalcArray = array[1..NI] of Double;
  TExhaustCalcArray = array[1..NE] of Double;
```

It stores X, velocity `u`, pressure `P`, density `R`, speed of sound `c`, and temperature arrays for both pipes, plus inlet/exhaust gamma, boundary temperatures, discharge coefficients, and throat velocity/speed-of-sound/density values.

Configured grid functions calculate active counts `QI` and `QE` at the first timestep. Counts above 68 or 38 raise `ECFDError`. These fixed capacities are intentional legacy design limits retained for the new software, although variable capacities would be preferable in a future redesign.

### Captured and performance data

`TCAPoint` is a Delphi **class**, not a record (corrected: `ISSUES.md` D6), holding `Value: array[1..28] of Double`. `TCAList` contains `CaVar: array[-359..360] of TCAPoint`, column names, decimal counts, and display scale factors.

`TPerfPoint` stores speed, torque, power, and volumetric efficiency. `TPerfData` has `MaxNoPoints = 100`. `AddDataPoint` refuses additional points after 100 and displays `Max No Of Stored Datapoints reached... This point will not be stored.`

`TWallTemps`, `TExhaustPandT`, and `TVarSpeedList` store dynamic arrays of doubles and use RPM-keyed linear interpolation. `TDoubFunc` and `TGridSize` own an `TAdCalc` evaluator and expression strings.

### Compatibility flags

- Delphi `ShortString` is length-prefixed: one length byte followed by up to 255 characters. This is runtime/string compatibility information, not the layout of `.eng` files.
- Pointer-linked records, dynamic arrays, Delphi objects, and `Extended` values are runtime layouts and must not be persisted by raw memory copy.
- No packed records or binary file-of-record formats were identified.

## 3. Persistence and file formats

### Engine and application INI files

`.eng` files are text INI files with sections equivalent to `[Cylinders]`, `[HeatTransfer]`, `[Inlet]`, `[Exhaust]`, `[Cams]`, `[Valves]`, `[Fuel]`, `[Conditions]`, and `[Calculation]`. They contain geometry, file names, timing, valve data, fuel/condition data, and calculation flags including `VariableGamma`, `SaveManfData`, `Integrator`, and `PerfDataSave`.

Keys must be matched **case-insensitively**, as Delphi's `TIniFile` matched them: `Edit.pas` reads `CdIvIn` while every shipped file writes `CdIVIn` (corrected: `ISSUES.md` D3).

An older, undocumented schema survives in five `Example1` engines and `legacy/CAEEng/A4LowCost.eng`, written for the 1998-99 predecessor program. It keeps the manifolds under `[InManifold]` and `[ExManifold]`, and its units differ from the current schema's: `PlenumP` is kPa rather than Pa, the wall temperatures (`THead` and the others) are Celsius rather than kelvin, and grid sizes are fixed counts under `[Calculation]` rather than expressions. Where a file carries both schemas, the current keys win (corrected: `ISSUES.md` D2; see A31-A35).

The shipped `ESA.ini` (`legacy/ESA/ESA.ini`) reads, with no trailing newline:

```ini
[DefaultFiles]
ErrorLog=CAEEng.err
TextSave=Lastcyc.txt
Engine=Default.eng

[Simulation]
EngineSpeed=4000
Nocycles=6
No1zcycles=1
MassBalance=0.5
```

(Corrected: `ISSUES.md` D5. This section earlier quoted `ErrorLog=ESA2z1z.err` and `MassBalance=1`.)

The .NET implementation may standardize `.eng`, INI, and exported text files on UTF-8. ANSI compatibility with Delphi is not required. No BDE, ADO, database, or registry access was identified.

### Input tables

The sample `.maf`, `.vcd`, `.cam`, `.spk`, `.cwt`, and `.exh` files in the data folders are the authoritative format examples.

- `.maf`: manifold position/area text points, loaded into `TAManf` and linearly interpolated.
- `.vcd`: discharge-coefficient grids, loaded into `TCdValve` and bilinearly interpolated.
- `.cam`: two-column profile points, loaded into the `TProfile` linked list.
- `.spk`: RPM/spark-angle pairs, loaded into `TVarSpeedList`.
- `.cwt`: RPM and wall-temperature columns, loaded into `TWallTemps`.
- `.exh`: RPM, exhaust **temperature**, and exhaust **pressure** columns, in that order, loaded into `TExhaustPandT`. The loader reads `ATExh` before `APExh`, and the shipped heading row reads `SPEED / TEMP[C] / P[kPa]`. Despite that heading the temperature is used as kelvin (`ISSUES.md` B66), and the pressure is gauge kPa, to which atmospheric pressure is added (A9). (Corrected: `ISSUES.md` D1.)

`.spk`, `.cwt` and `.exh` are not bare column pairs: each starts with a row-count line, then a heading line the loader discards, then the data rows (corrected: `ISSUES.md` D4).

### PVT and multi-run exports

PVT export is delimited text containing crank angle plus the 28 captured values. Multi-run grids are delimited text managed by `SaveGrid` and `LoadGrid`. Error logs are appended text.

### Manifold output

`TManifolds.Main_Prog` honors `SaveManifoldData`. On the final cycle it creates and writes:

- `Inlet.txt`: crank angle, inlet pressure/velocity at pipe start, midpoint, and valve end;
- `Exhaust.txt`: corresponding exhaust values at pipe end, midpoint, and valve end;
- `Pcyl.txt`: crank angle and cylinder pressure;
- `Tcyl.txt`: crank angle, cylinder temperature, and cylinder volume;
- `MassFlow.txt`: crank angle, inlet mass, and exhaust mass, scaled by $10^6$;
- `InlPress.m`: one row of inlet pressures per crank-angle output;
- `InlVel.m`: one row of inlet velocities;
- `ExhPress.m`: one row of exhaust pressures;
- `ExhVel.m`: one row of exhaust velocities.

The legacy layout is whitespace-delimited with fixed-width numeric formatting. The .NET implementation should use a standard UTF-8 .NET text-output implementation rather than requiring byte-for-byte MATLAB/Delphi formatting. It should retain the equivalent file names, numeric columns, units, and final-cycle-only behavior. Headers and other standard .NET formatting are permitted, provided downstream consumers and the documented columns are preserved.

## 4. External dependencies

**Superseded** by `TECHSTACK.md`, which requires Avalonia and ScottPlot; the two paragraphs below are kept as phase 1 wrote them (corrected: `ISSUES.md` D8).

VCL controls map to Windows Forms or WPF. `TStringGrid` maps most directly to `DataGridView`. `TIniFile` and Delphi file I/O map to an INI parser and `System.IO`. The VCL message loop and `Application.ProcessMessages` require an equivalent UI-dispatch strategy.

TeeChart types (`TChart`, `TFastLineSeries`, `TLineSeries`, `TSurfaceSeries`) require a .NET charting replacement such as OxyPlot, LiveCharts, a WinForms chart control, or a surface-capable renderer.

`Components/adcalc41_paid/ADCALC.PAS` supplies the proprietary `TAdCalc` expression evaluator used by `TDoubFunc` and `TGridSize`. It supports arithmetic, logical, string, comparison, and function expressions and has both compiled-parser APIs (`CompileText`/`ExecuteExtended`) and immediate APIs (`GetExtendedResult`). ESA uses the immediate API:

```pascal
Func.RegVariable('N', EtExtended, 'EngineSpeed');
Func.SetExtendedVarValue('N', N);
Func.GetExtendedResult(FuncStrings, FRes, 1);
```

`GetExtendedResult` constructs a new parser with `cNo`, evaluates, and destroys it. The .NET replacement is explicitly permitted to compile/cache expressions, provided expression semantics, numerical results, and error behavior remain compatible. `TGridSize.GridSize` may cache its expressions in the same way for `L` and `N`.

No explicit Win32 API call was identified. `Printers` is imported but no print workflow was found.

## 5. Business rules and calculations

### State machine

The six crank-angle states are Compression, Combustion, Expansion, Exhaust, Overlap, and Intake. `GetState` is equivalent to:

```pascal
if Theta < Manifold.EV.C then Getstate := Overlap
else if Theta < Manifold.IV.C then Getstate := Intake
else if Theta < Cyl.ThetaSpark then Getstate := Compression
else if Theta < Cyl.ThetaSpark+Cyl.Fuel.Burnangle then Getstate := Combustion
else if Theta < Manifold.EV.O then Getstate := Expansion
else if Theta < Manifold.IV.O then Getstate := Exhaust
else Getstate := Overlap;
```

For two-zone mode, state transitions select the corresponding ODE functions. Compression initializes the two-zone unburnt model; Combustion uses burnt-zone volume/pressure/temperature equations; Expansion makes the charge burned; Exhaust removes burned mass; Intake adds unburnt mass and carries residual gas state; Overlap uses the simplified frozen-equilibrium single-zone pressure equations. This overlap treatment is intentional and must be retained.

At every step `Cyl.mgas := Cyl.mgas + Min - Mout`. Negative gas mass raises `EEngineError`. State-specific totals are handled as follows:

- intake mass contributes to `TotalMInIV`;
- exhaust mass contributes to `TotalMOutEV`;
- overlap tracks burned/unburned reverse flow with `MbOutInlet` and `MuOutExhaust` corrections;
- at compression initialization, the previous cycle's inlet total becomes `NewAirMass`, then inlet/outlet totals are reset for the new cycle;
- at exhaust initialization, exhaust total is reset.

The intended cycle-to-cycle behavior is to carry engine end-of-cycle gas values forward as the next cycle's initial condition, while cycle-specific totals and performance accumulators reset at their state initialization. A .NET port should follow the field-level resets in `Run` and `InitVars`, not reset the entire engine object between cycles.

### Convergence and two-zone switching

The main loop stops when:

```pascal
if abs(TotalMInIV-TotalMOutEV)*1e6 < MassBalance then
  NoCycles := i;
  Running := FALSE;
```

The tolerance is expressed in micrograms. Three cycles is a valid minimum. The first `No1zCycles` cycles may establish a one-zone state before a two-zone run.

### Manifold solver

`Main_Prog` computes `dt := (1/(Speed/60*360))*dCrankA`, initializes fixed-size pipe arrays on `tStep = 0`, and advances both pipes with characteristic-line calculations. Boundary routines are selected by valve status:

- both valves closed: `INFLOW_INLET_PIPE`, `INLET_VALVE_CLOSED`, `EXHAUST_VALVE_CLOSED`, `OUTFLOW_EXHAUST_PIPE`;
- inlet closed/exhaust open: inlet closed routines plus `EXHAUST_VALVE_OPEN`;
- both open: `INLET_VALVE_OPEN` and `EXHAUST_VALVE_OPEN`;
- inlet open/exhaust closed: `INLET_VALVE_OPEN` and `EXHAUST_VALVE_CLOSED`.

Internal pipe points use `INTERNAL_PIPE` with characteristic variables, area-gradient terms, Fanning friction, and convergence checks. Valve routines distinguish sonic and subsonic flow and include reverse-flow paths. Negative pressure or density displays an error message; solver iterations stop on configured tolerances or after 1000 iterations.

`MassFlow` calculates:

```pascal
MassIn := Iut*IRt*(ICd*IValveArea)*dt;
MassOut := Eut*ERt*(ECd*EValveArea)*dt;
dPMass := (sqr(cStag)*MassIn - sqr(cCyl)*MassOut)/CylVol;
```

The burnt-volume clamp in `Gasses2Z.pas` is an intentional numerical safeguard and must remain:

```pascal
if Vb > Vgas then Vb := Vgas;
```

### Heat transfer and combustion

The Woshini constants `C1 = 2.28` for compression/combustion/expansion and `C1 = 6.18` for exhaust/overlap/intake are empirically validated calibration values.

Fuel mass uses:

```pascal
Cyl.Fuel.M := (1/Cyl.Fuel.Lambda) * TotalMInIV /
              (Cyl.Fuel.AFRatio + 1);
```

The `+ 1` is intentional: AFR is represented as an X:1 air-to-fuel ratio, so total mixture mass is X+1 parts.

The equilibrium model calculates 12 species. The Delphi equilibrium behavior is authoritative for the port. The supplied NOx/CO temperature ranges are background engineering information only; do not introduce new separate freeze thresholds in the .NET implementation unless the Delphi behavior is first shown to require it. Preserve the existing `TEqbm` behavior and validate it against the reference cases.

### Performance

`Performance` computes IMEP, PMEP, friction MEP, BMEP, torque, brake/indicated/heat power, volumetric efficiency, mechanical/thermal efficiency, SFC, and fuel/heat/work/pump/friction/exhaust energy balance. `PerformanceData` stores up to 100 points and explicitly warns and discards additional points.

## 6. Dead code, retained behavior, and validation

- `IniValues.SaveIniValues` is declared but empty and appears unused.
- `Main.pas` imports `Printers`, but no print workflow exists.
- `DoubleFunc.pas` contains commented-out string-list allocation/cleanup.
- `Edit.pas` catches conversion errors without a corrective message.
- The commented gas-exchange overlap ODE assignments are intentionally not restored.
- `TProfile.LoadText` clears old profile nodes before loading; `TProfile.Destroy` also clears them.
- Fixed grid capacities of 68 inlet and 38 exhaust points are intentional legacy limits for the new software.
- Manifold output is enabled by `SAVEMANFDATA`, is written only for the final simulated cycle, and comprises the nine files listed above.
- AdCalc expressions may be compiled/cached in the .NET implementation, subject to compatibility testing.

The calibration/reference cases are the two examples in `legacy/ESA/Data/Example1` and `legacy/ESA/Data/Example2`. They should be used to validate the one-zone/two-zone transition, manifold flow, emissions, performance outputs, convergence behavior, and the retained Delphi chemistry. Expected numerical tolerances are not specified in the answers; the test harness should record legacy outputs first and use those as the comparison baseline. **Done**: `data/baseline/` holds a complete reference run of the original, every input and output recorded, and `BASELINE.md` documents it and the agreement measured against it (corrected: `ISSUES.md` D9).

## Further questions

No original open questions remain. The one implementation detail this section left for the engineering test plan - measuring the numerical comparison tolerances from legacy reference runs before the acceptance tests were finalized - has been done: see `BASELINE.md` (corrected: `ISSUES.md` D9). The questions still open are `ISSUES.md` section F.
