# ESA Legacy Application Specification (Revision 2)

This document supersedes `SPEC.md` and incorporates the decisions in `answers.md` plus additional source review. The Delphi source and forms remain unchanged. The intended .NET implementation should preserve the behavior described here unless a remaining question is explicitly resolved.

## 1. Form inventory

### `Main.pas` / `Main.dfm` - `TFMain`

The main form owns startup, simulation execution, result display, and menus. It contains a main menu, status bar, memo, three TeeChart charts, and labels/panels for engine and performance results.

- `Chart1` displays manifold gas-flow traces.
- `Chart2` displays cylinder pressure and the P-V diagram.
- `Chart3` displays in-cylinder properties.
- Status fields display file and engine name, run time, cycle count, mass balance, speed, torque, power, volumetric efficiency, fuel consumption, SFC, cylinder mass, work, heat loss, pumping work, friction, exhaust/fuel energy, IMEP/BMEP/FMEP/PMEP, and valve event crank angles.

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
- `Edit1Click`, `PVTTrace1Click`, `Options1Click`, `ShowTorqueCurve1Click`, `ValveOpening1Click`, and `HeatLoss1Click` show the corresponding child forms.
- `Timer1Timer` refreshes status, charts, and result labels.
- `Chart2Zoom` adjusts P-V chart scaling.

The run advances crank angle in steps, processes UI messages, invokes `Engine2z.Run`, captures PVT data, and checks mass balance between cycles. The one-zone/two-zone switch occurs after `No1zCycles` when two-zone mode is selected. The run may terminate early when the mass-balance error is below the configured tolerance. Three cycles is an accepted minimum.

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

- `formsimul.pas` / `.dfm` - `FSimulateOptions`: RPM, cycle count, mass-balance tolerance, and graph selection. RPM is constrained to approximately 1250-7000 and the minimum cycle count is 3.
- `MultiRun.pas` / `.dfm` - `FMultiRun`: a 15-column `TStringGrid` for speed, iterations, file names, valve timing/lift, spark, and burn angle. It supports up to 100 rows and uses `-` for missing values.
- `PVTDataForm.pas` / `.dfm` - `FPVTData`: a 29-column grid containing crank angle plus 28 captured quantities; saves through `TCAList.SendToFile`.
- `AboutBoxUnit.pas` / `.dfm` - static about dialog.
- `Welcome.pas` / `.dfm` - splash screen with timed close.
- `TorqueCurve.pas` / `.dfm` - torque, power, and volumetric-efficiency charts from `PerformanceData`.
- `TCurveOptions.pas` / `.dfm` - torque-curve axis limits.
- `FflowGraphOptions.pas` / `.dfm` - manifold pressure/velocity/mass mode, P-V mode, in-cylinder mode, and Y-axis limits.
- `Flowgraph.pas` / `.dfm` - discharge-coefficient surface plot over pressure ratio and lift ratio.
- `FManfA.pas` / `.dfm` - one-dimensional manifold area-versus-length table, up to 50 points.
- `IPolTab.pas` / `.dfm` - two-dimensional discharge-coefficient table, up to 20 by 20, with bilinear interpolation.
- `GHeatLoss.pas` / `.dfm` - heat loss, work, and pump-work chart.
- `GValveLift.pas` / `.dfm` - inlet and exhaust valve-lift charts.

## 2. Data structures

No `packed record` or `file of record` declaration was identified in the application units. Runtime data is represented by Delphi objects, arrays, and pointer-linked records. These representations must not be serialized by copying object memory.

### Engine and thermodynamic model

`TEngine2z` in `ICEngine2Z.pas` derives from `TRKF`. Important fields include:

- `Name: ShortString`, `FireOrder: ShortString`;
- `NoZones`, `State`, `OldState`, `NCyl`, `NEqns`, `NCycles`, and `tstep`;
- `INIT2Z`, `TWOZOVERLAP`, `SAVEMANFDATA`, and `VariableGamma`;
- crank state `CA`, `dCA`, `Nrpm`, `wcrank`;
- geometry `Bore`, `Stroke`, `CR`, `ConrodLength`, and displacement `Vd`;
- gas objects `Plenum`, `Exh`, `Cyl`, and `Atm: TGas2z`;
- `Manifold: TManifolds`, `WallTemp: TWallTemps`, and `SparkAngle: TVarSpeedList`;
- pressure, temperature, mass, flow, EGR, and valve-pressure state;
- `Emmissions: EqSpecArray`;
- performance values including work, MEPs, torque, power, SFC, efficiencies, peak pressure/temperature, and energy balance;
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

`TPoint` in `Profiles.pas` is a pointer-linked record:

```pascal
type
  PPoint = ^TPoint;
  TPoint = record
    x, y: Double;
    next: PPoint;
  end;
```

`TProfile` stores point count, spacing, linked-list pointers (`First`, `Current`, `OldFirst`, `OldCurrent`), status flags, limits, lift, duration, and filename. `AddPoint` allocates nodes with `New`. `Clear` disposes the complete list, `Destroy` calls `Clear`, and `LoadText` calls `Clear` before loading a replacement profile. Therefore replacement loads are intended to release the old linked list.

`TAManf` stores position and area arrays up to `maxx` points. `TCdValve` stores a two-dimensional table and axes up to `maxxy = 20`. `TPipe` owns an area-versus-length table and insertion values.

`TManifolds` owns inlet/exhaust valves and pipes, exhaust pressure/temperature data, a plenum-pressure function, grid functions, valve-flow functions, throat values, and fixed-capacity flow arrays. The exact declarations are:

```pascal
const
  NI = 68;
  NE = 38;

type
  TInletCalcArray = array[1..NI] of Double;
  TExhaustCalcArray = array[1..NE] of Double;
```

It stores `X`, velocity `u`, pressure `P`, density `R`, speed of sound `c`, and temperature arrays for both pipes, plus inlet/exhaust gamma, boundary temperatures, discharge coefficients, and throat velocity/speed-of-sound/density values.

The configured grid functions calculate active counts `QI` and `QE` at the first timestep. Counts above 68 or 38 raise `ECFDError`. The fixed capacities are intentional design limits retained for the new software, although variable capacities would be preferable in a future redesign.

### Captured and performance data

`TCAPoint` contains `Value: array[1..28] of Double`. `TCAList` contains `CaVar: array[-359..360] of TCAPoint`, column names, decimal counts, and display scale factors.

`TPerfPoint` stores speed, torque, power, and volumetric efficiency. `TPerfData` has `MaxNoPoints = 100`. `AddDataPoint` refuses additional points after 100 and displays `Max No Of Stored Datapoints reached... This point will not be stored.` This is the user-visible behavior, rather than an unreported failure.

`TWallTemps`, `TExhaustPandT`, and `TVarSpeedList` store dynamic arrays of doubles and perform RPM-keyed linear interpolation. `TDoubFunc` and `TGridSize` own an `TAdCalc` evaluator and expression strings.

### Compatibility flags

- Delphi `ShortString` is length-prefixed: one length byte followed by up to 255 characters. It is runtime/string compatibility information, not the layout of `.eng` files.
- Pointer-linked records, dynamic arrays, Delphi objects, and `Extended` values are runtime layouts and must not be persisted by raw memory copy.
- No packed records or binary file-of-record formats were identified.

## 3. Persistence and file formats

### Engine and application INI files

`.eng` files are text INI files with sections equivalent to `[Cylinders]`, `[HeatTransfer]`, `[Inlet]`, `[Exhaust]`, `[Cams]`, `[Valves]`, `[Fuel]`, `[Conditions]`, and `[Calculation]`. They contain geometry, file names, timing, valve data, fuel/condition data, and calculation flags including `VariableGamma`, `SaveManfData`, `Integrator`, and `PerfDataSave`.

`ESA.ini` contains defaults such as:

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

The new implementation may standardize `.eng`, INI, and exported text files on UTF-8. ANSI compatibility with Delphi is not required by the owner. There is no BDE, ADO, database, or registry access identified.

### Input tables

The sample `.maf`, `.vcd`, `.cam`, `.spk`, `.cwt`, and `.exh` files are the authoritative format examples.

- `.maf`: manifold position/area text points, loaded into `TAManf` and linearly interpolated.
- `.vcd`: discharge-coefficient grids, loaded into `TCdValve` and bilinearly interpolated.
- `.cam`: two-column profile points, loaded into the `TProfile` linked list.
- `.spk`: RPM/spark-angle pairs, loaded into `TVarSpeedList`.
- `.cwt`: RPM and wall-temperature columns, loaded into `TWallTemps`.
- `.exh`: RPM, exhaust pressure, and exhaust temperature columns, loaded into `TExhaustPandT`.

### PVT and multi-run exports

PVT export is delimited text containing crank angle plus the 28 captured values. Multi-run grids are delimited text managed by `SaveGrid` and `LoadGrid`. Error logs are appended text.

### Manifold output

`TManifolds.Main_Prog` honors `SaveManifoldData`. On the final cycle it creates and writes these UTF-8-compatible text outputs in the legacy Delphi text-file style:

- `Inlet.txt`: crank angle, inlet pressure/velocity at pipe start, midpoint, and valve end;
- `Exhaust.txt`: corresponding exhaust values at pipe end, midpoint, and valve end;
- `Pcyl.txt`: crank angle and cylinder pressure;
- `Tcyl.txt`: crank angle, cylinder temperature, and cylinder volume;
- `MassFlow.txt`: crank angle, inlet mass, and exhaust mass, scaled by $10^6$;
- `InlPress.m`: one row of inlet pressures per crank-angle output;
- `InlVel.m`: one row of inlet velocities;
- `ExhPress.m`: one row of exhaust pressures;
- `ExhVel.m`: one row of exhaust velocities.

The text layout is whitespace-delimited and uses formatted fixed-width numeric fields. The legacy writer uses `Rewrite` at the start of the output cycle, `Append` for rows, and `CloseFile` at the end. The owner has requested a new output aligned with the other outputs; the .NET replacement should use UTF-8 text with equivalent file names, columns, units, and final-cycle selection unless a new output contract is deliberately approved.

## 4. External dependencies

The VCL controls map to Windows Forms or WPF. `TStringGrid` maps most directly to `DataGridView`. `TIniFile` and Delphi file I/O map to an INI parser and `System.IO`. The VCL message loop and `Application.ProcessMessages` require an equivalent UI-dispatch strategy.

TeeChart types (`TChart`, `TFastLineSeries`, `TLineSeries`, `TSurfaceSeries`) require a .NET charting replacement such as OxyPlot, LiveCharts, a WinForms chart control, or a surface-capable renderer.

`Components/adcalc41_paid/ADCALC.PAS` supplies the proprietary `TAdCalc` expression evaluator used by `TDoubFunc` and `TGridSize`. The evaluator supports arithmetic, logical, string, comparison, and function expressions. It has both compiled-parser APIs (`CompileText`/`ExecuteExtended`) and immediate APIs (`GetExtendedResult`). ESA uses the immediate API.

The relevant code is:

```pascal
Func.RegVariable('N', EtExtended, 'EngineSpeed');
Func.SetExtendedVarValue('N', N);
Func.GetExtendedResult(FuncStrings, FRes, 1);
```

`GetExtendedResult` constructs a new `TParser` with `cNo`, evaluates, and destroys it. It does not reuse a compiled parser. `TGridSize.GridSize` behaves the same way for `L` and `N`. Consequently the legacy behavior reparses the expression on every call. A .NET implementation should preserve expression semantics but may compile/cache safely after verifying numerical equivalence.

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

For two-zone mode, state transitions select the corresponding ODE functions. Compression initializes the two-zone unburnt model; Combustion uses burnt-zone volume/pressure/temperature equations; Expansion makes the charge burned; Exhaust removes burned mass; Intake adds unburnt mass and carries residual gas state; Overlap uses the simplified frozen-equilibrium single-zone pressure equations. This simplified overlap treatment is intentional and must be retained.

At every step `Cyl.mgas := Cyl.mgas + Min - Mout`. Negative gas mass raises `EEngineError`. State-specific totals are handled as follows:

- intake mass contributes to `TotalMInIV`;
- exhaust mass contributes to `TotalMOutEV`;
- overlap tracks burned/unburned reverse flow with `MbOutInlet` and `MuOutExhaust` corrections;
- at compression initialization, the previous cycle's inlet total becomes `NewAirMass`, then inlet/outlet totals are reset for the new cycle;
- at exhaust initialization, exhaust total is reset.

The intended cycle-to-cycle behavior is to carry the engine's end-of-cycle gas values forward as the next cycle's initial condition, while cycle-specific totals and performance accumulators are reset at the appropriate state initialization. A .NET port should follow the field-level resets in `Run` and `InitVars`, not reset the whole engine object between cycles.

### Convergence and two-zone switching

The main loop stops when:

```pascal
if abs(TotalMInIV-TotalMOutEV)*1e6 < MassBalance then
  NoCycles := i;
  Running := FALSE;
```

The tolerance is expressed in micrograms. Three cycles is a valid minimum. The first `No1zCycles` cycles may establish a one-zone state before a two-zone run.

### Manifold solver

`Main_Prog` computes `dt := (1/(Speed/60*360))*dCrankA`, initializes fixed-size pipe arrays on `tStep = 0`, and then advances the two pipes with characteristic-line calculations. Boundary routines are selected by valve status:

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

The burnt-volume clamp in `Gasses2Z.pas` is an intentional numerical safeguard:

```pascal
if Vb > Vgas then Vb := Vgas;
```

It should remain in a compatible implementation.

### Heat transfer and combustion

The Woshini constants `C1 = 2.28` for compression/combustion/expansion and `C1 = 6.18` for exhaust/overlap/intake are empirically validated calibration values. Do not replace them with generic constants.

Fuel mass uses:

```pascal
Cyl.Fuel.M := (1/Cyl.Fuel.Lambda) * TotalMInIV /
              (Cyl.Fuel.AFRatio + 1);
```

The `+ 1` is intentional: AFR is represented as an X:1 air-to-fuel ratio, so total mixture mass is X+1 parts.

The equilibrium model calculates 12 species. The owner indicates that SI-engine chemistry freezes approximately at 1800-2000 K for NOx and 1500-1700 K for CO. These ranges are engineering guidance, not yet a single source-level threshold. The existing code's exact `TEqbm` behavior must be preserved until a specific freeze policy is approved.

### Performance

`Performance` computes:

- `IMEP := WWork / Vd`;
- `PMEP := PWork / Vd`;
- friction MEP using `TFMEP`;
- `BMEP := IMEP - PMEP - FMEP`;
- torque from BMEP, displacement, cylinder count, and four-stroke geometry;
- brake/indicated/heat power, volumetric efficiency, mechanical/thermal efficiency, SFC, and fuel/heat/work/pump/friction/exhaust energy balance.

## 6. Dead code and retained legacy behavior

- `IniValues.SaveIniValues` is declared but empty and appears unused.
- `Main.pas` imports `Printers`, but no print workflow exists.
- `DoubleFunc.pas` contains commented-out string-list allocation/cleanup.
- `Edit.pas` catches conversion errors without a corrective message.
- The commented gas-exchange overlap ODE assignments are intentionally not restored.
- `TProfile.LoadText` clears old profile nodes before loading; `TProfile.Destroy` also clears them.
- Fixed grid capacities of 68 inlet and 38 exhaust points are intentional legacy limits for the new software.
- `PerformanceData.AddDataPoint` explicitly warns and discards points beyond 100.
- Manifold output is enabled by `SAVEMANFDATA` and consists of the nine text files listed above.

## Remaining Questions for Paul

1. The AdCalc call path is now established as reparsing each expression on every `Result`/`GridSize` call. Should the .NET implementation intentionally preserve that performance characteristic, or may it compile/cache expressions while preserving results and error behavior?
2. The NOx and CO freeze ranges supplied are engineering ranges rather than one authoritative threshold. Should the .NET model use separate thresholds (for example NOx 1800 K and CO 1500 K), retain the Delphi equilibrium behavior, or use another calibrated policy?
3. For the new UTF-8 manifold outputs, should the legacy `.m` files remain exactly whitespace-compatible with MATLAB-style consumers, or may the .NET implementation emit named headers while retaining the same numeric columns?
4. Should the new manifold output be written only on the final simulated cycle exactly as the Delphi code does, or should the .NET application expose a user-selectable cycle/output policy?
5. Are there calibration test cases and expected numerical tolerances for validating the one-zone/two-zone transition, manifold flow, emissions, and performance outputs against the legacy executable?
