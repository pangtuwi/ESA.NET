# Phase 4 validation baseline

`data/baseline/` holds a complete, self-contained reference run of the original
Delphi ESA 3.0: one engine, every side file it needs, screenshots of the settings
that produced it, and the full-cycle results the original computed.

This is the yardstick phase 4 is measured against. SPEC.md section 6 says the
numerical tolerances "must be measured from legacy reference runs before
automated .NET acceptance tests are finalized" — this is that reference run.

## Why this set exists

The recorded output already scattered through `legacy/ESA/Data/` cannot be
trusted as a baseline: its provenance is unrecorded, the performance files
accumulate rows across unrelated runs, two different header formats are in
circulation, and nothing states which engine or speed produced any given file.

This set has none of those problems. It was produced deliberately, in one
sitting, with every input captured alongside the output.

## Provenance

Run by the repository owner on the original **ESA 3.0 (1 October 2001)** Windows
binary, from `C:\CAEEng\A2China.eng`, as a **Single Point Simulation** using the
dialog's default values. The engine settings were photographed tab by tab before
the run, and the results screen and detailed results file captured after it.

The `.eng` file stores every side-file path as an absolute `C:\CAEEng\...` path
from that machine. The files themselves were copied here from
`legacy/ESA/Data/Example2/`, where each exists byte-identically (several appear
in more than one subfolder; all copies were verified identical before copying).

`LegacyPathResolver` resolves the dead absolute paths to the local copies by
falling back to the file name beside the `.eng`, so the set loads as-is with no
edits. That is verified — the engine loads with **zero** unresolved files.

## The run

Requested, from the Single Speed Simulation dialog in `A2China_Simulation.JPG`:

| Input | Value |
|---|---|
| Engine speed | 4000 rpm |
| Total cycles | **6** |
| Mass balance tolerance | **1 mg** |
| Graphs | on (gas flow, P-V, in-cylinder) |

Achieved, from `Screen_Capture_Results.JPG`:

| Outcome | Value |
|---|---|
| Engine | A2 China Jetta 1.6L 5V Baseline |
| Cycles | 4 / 4 |
| Mass balance | 0.3 mg |
| Combustion model | 2 zone |
| Integrator | Runge-Kutta-Fehlberg (`Integrator=0`) |
| Variable gamma | on |
| Save manifold data | off |
| Run time | 5 s |

**The run converged; it did not run to completion.** Six cycles were requested at
a 1 mg tolerance, and the mass balance reached 0.3 mg on cycle 4. `TEngine2z.Run`
sets `NoCycles := i` when the balance is met, and `TFMain.Simulate` renders the
caption as `i + ' / ' + NoCycles`, so both halves become 4. That is why the screen
reads `4 / 4` for a six-cycle request.

This matters for phase 4 in two ways. The cycle count is an **outcome, not an
input**: a correct port must also converge on cycle 4 at this tolerance, which is
itself a testable behaviour. And the tolerance to use is **1 mg**, not the 0.5 mg
in the shipped `legacy/ESA/ESA.ini` — the machine that produced this run had a
different `ESA.ini`.

Note also `if NoCycles < 3 then NoCycles := 3` in `TFMain.Simulate`: three cycles
is a floor applied silently, matching SPEC.md section 5.

## Expected results

The numbers a correct port must reproduce at 4000 rpm. These come from
`SimulDat.txt`, the performance data file the run wrote, which carries more
precision than the results screen and several quantities the screen never shows.

| Quantity | Value | Unit | Screen shows |
|---|---|---|---|
| Speed | 4000 | rpm | 4000 |
| IMEP | 14.291 | bar | 14.291 |
| PMEP | −0.392 | bar | −0.392 |
| FMEP | 2.762 | bar | 2.762 |
| BMEP | 11.921 | bar | 11.921 |
| Mechanical efficiency | 83.4 | % | — |
| Volumetric efficiency | 109.7 | % | 109.7 |
| Thermal efficiency | 30.6 | % | — |
| Torque | 151.34 | Nm | 151.3 |
| Power | 63.395 | kW | 63.4 |
| Fuel flow | 17.35 | kg/hr | 17.3 |
| SFC | 273.6 | g/kW.hr | 273.6 |
| Trapped mass | 580.11 | mg | 580.1 |
| Mass in | 560.11 | mg | — |
| Mass out | 560.38 | mg | — |
| Lambda | 1.00 | | — |
| Spark | 21.0 | °BTDC | — |
| Exhaust back pressure | 17.8 | kPa | — |

Energy balance, as percentages of fuel energy:

| Term | Value |
|---|---|
| Work | 30.6 % |
| Heat loss | 24.3 % |
| Pumping | −1.0 % |
| Friction | 7.1 % |
| Exhaust | 39.1 % |
| Fuel | 100 % |

Two of the new columns are useful checks in their own right. The mass balance the
screen reports as 0.3 mg is `|MassIn − MassOut|` = |560.11 − 560.38| = 0.27 mg,
confirming the convergence metric. And `Spark` 21.0 and `BackP` 17.8 are the
values interpolated out of `A2ChinaVar.spk` and `A2China.exh` at 4000 rpm, so
they check the speed-keyed table lookups without running any physics.

`SimulDat.txt` also holds **two identical data rows**, one per run — the original
capture and the re-run with manifold output enabled. Beyond confirming that the
file accumulates rows across runs, this shows the simulation is deterministic:
every digit of the two runs agrees.

### Every one of those numbers derives from the trace

The trace and the results screen form a closed chain. Each link can be checked
independently, so a phase 4 failure can be localised without running the physics
end to end.

Start from the geometry the trace itself carries: `Vcyl` runs 48.64 to 447.48 cc,
so the swept volume is **Vd = 398.84 cc per cylinder**.

The accumulators `WWork`, `PWork` and `htLoss` all reset to zero at **CA −100**,
which is inlet valve closing (IVC = 80 °ABDC, so −180 + 80). The cycle-complete
values are therefore the ones at **CA −101**, immediately before that reset — not
the ones on the trace's last row. Sampling the wrong point is an easy mistake and
makes PMEP appear to be out by a factor of four.

| Step | From | Value | Screen |
|---|---|---|---|
| IMEP | `WWork(−101)` 570.0 J ÷ Vd | 14.291 bar | 14.291 |
| PMEP | `PWork(−101)` −15.615 J ÷ Vd | −0.392 bar | −0.392 |
| TFMEP | `1e5(0.97 + 0.15N/1000 + 0.05(N/1000)²)` at 4000 | 2.370 bar | not shown |
| FMEP | TFMEP − PMEP | 2.762 bar | 2.762 |
| BMEP | IMEP − PMEP − FMEP | 11.921 bar | 11.921 |
| Torque | BMEP × Vd × NCyl ÷ 4π | 151.3 Nm | 151.3 |
| Power | Torque × N × 2π ÷ 60 | 63.4 kW | 63.4 |

All seven reproduce the displayed values exactly. `IMEP × Vd` recovers `WWork` to
three parts in a million, which independently confirms both the swept volume and
the trace's volume scaling.

Note that IMEP − FMEP = 11.529 bar, which is *not* BMEP. The identity that holds
is BMEP = IMEP − PMEP − FMEP. Algebraically that collapses to IMEP − TFMEP,
because `FMEP := TFMEP - PMEP` in `TEngine2z.Performance` makes PMEP cancel; the
reported FMEP is that intermediate, not the raw friction correlation. Both
readings give 11.921 bar here, so the odd-looking formula is confirmed rather
than merely tolerated.

## Files

### Inputs

| File | Role |
|---|---|
| `A2China.eng` | The engine definition. Note it carries **both** `PlenumP=98.0` and `FPlenumP=(99000)`; the Inlet tab shows `(99000)`, so `FPlenumP` wins. It also carries `IVMinA` / `EVMinA` from the older schema, and no `PerfDataSave` key, so the Model tab shows the `SimulDat.txt` default. |
| `A2ChinaInlet_M758.maf` | Inlet manifold area versus length, 17 points, 0.758 m long |
| `A2ChinaExhaust_M.maf` | Exhaust manifold area versus length, 13 points, 0.855 m long |
| `A2China Inlet Profile.cam` | Inlet cam, 100 points, normalised 0–1 on both axes |
| `A2China Exhaust Profile.cam` | Exhaust cam, 100 points, normalised |
| `A2China IVIn.vcd` | Inward-flow discharge coefficients, 6 × 8 |
| `A2China IVOut.vcd` | Outward-flow discharge coefficients, 6 × 8 |
| `A2ChinaVar.spk` | Spark map, 25 rows |
| `A2China.cwt` | Wall temperatures, 7 rows |
| `A2China.exh` | Exhaust back pressure and temperature, 21 rows |

### Output

| File | Role |
|---|---|
| `A2China.txt` | The detailed results: a full-cycle PVT trace, 720 rows |
| `SimulDat.txt` | The performance data file, one row per run. Full precision aggregates plus mechanical and thermal efficiency, mass in and out, spark and back pressure, which the results screen does not show. CRLF line endings, last line unterminated. |

### Screenshots

**Settings.** `A2China_Cylinders.JPG`, `A2China_HeatTrans.JPG`,
`A2China_Inlet.JPG`, `A2China_Exhaust.JPG`, `A2China_Cams.JPG`,
`A2China_Valves.JPG`, `A2China_Fuel.JPG`, `A2China_Model.JPG` — the eight Edit
Engine Data tabs after loading `A2China.eng`.

**The run.** `A2China_Simulation.JPG` — the Single Speed Simulation dialog,
carrying the requested speed, cycle count and mass balance tolerance.

**Results.** `Screen_Capture_Results.JPG` — the main window after the run: the
result panel, the P-V diagram, the gas-flow pressure traces and the in-cylinder
properties chart.

`Screen_Capture_Results_GasVel.JPG` — the same window with the gas-flow panel
switched to velocities. **The panel title changes but the plot does not**: the
y-axis still reads Pressure [bar] and the three curves are identical to the
pressure capture. The mode is read when the display refreshes, and the refresh
timer has stopped by the time the run ends, so this capture carries no velocity
data. It is kept as a record of the behaviour, not as reference data. The two
captures do usefully confirm that the aggregate results are stable across a
redraw — every displayed number is identical.

**Additional charts**, both reached from the Graph menu after a run:

`Energy_Balance.JPG` — heat loss, work done and pump work against crank angle
over −360 to 360. The visible discontinuity at −100° is the accumulator reset at
IVC, and the plateaus agree with the trace: work done settles near 570 J and heat
loss near −382 J, matching `WWork` and `htLoss`.

`Valve_Profile.JPG` — inlet and exhaust valve lift against crank angle. This is
the clearest confirmation of how cam data is assembled: the `.cam` files hold a
shape normalised to 0–1 on both axes, and the chart shows it scaled to the
`IVLift` 8.62 mm and `EVLift` 10.4 mm peaks and positioned by the timing angles.
Exhaust spans −244° to +37° (EVO 64 °BBDC to EVC 37 °ATDC, 281° duration), inlet
spans −19° to +260° (IVO 19 °BTDC to IVC 80 °ABDC, 279° duration), overlapping
for 56° around gas-exchange TDC.

### Crank angle origins differ between charts

Worth knowing before comparing anything against a chart:

| View | Zero is | Range |
|---|---|---|
| `A2China.txt`, in-cylinder, energy balance | firing TDC | −359 … 360 |
| Valve lift profile | gas-exchange TDC | −360 … 360 |
| Gas flow | firing TDC | 0 … 720 |

The trace and the energy balance agree: peak cylinder pressure sits at +14° and
peak burnt temperature at −7°, both around firing TDC.

## `A2China.txt` format

A header line then 720 data rows, comma separated, crank angle −359 to 360. The
columns are crank angle followed by the 28 captured values, and they match
`ColName` in `CAList2z.pas` exactly:

```
CA, Vcyl, PCyl, Mcyl, Mb, Mu, Min, Mout, Vb, Vu, Tb, Tu, Qb, Qu, Gamma,
FuelM, IV A, EV A, IV V, EV V, IV P, EV P, WWork, PWork, CO, NO, CO2, HC, htLoss
```

**The values are display-scaled, not SI.** `TCAList` applies a per-column factor
`k` before writing, so a ported trace must be scaled the same way before
comparison:

| Columns | Factor | Effect |
|---|---|---|
| `Vcyl`, `Vb`, `Vu` | 1e6 | m³ → cc |
| `Mcyl`, `Mb`, `Mu`, `Min`, `Mout` | 1e6 | kg → mg |
| `IV A`, `EV A` | 1e6 | m² → mm² |
| `CO`, `NO`, `CO2`, `HC` | 1e3 | |
| everything else | 1 | SI as computed |

Decimal places are also per column and are in `CAList2z.pas`; the file is written
with fixed-width formatting.

An older variant of this file exists as `legacy/ESA/Data/Example1/Lastcyc.txt`,
whose 16th column is headed `xb` rather than `FuelM`. `A2China.txt` matches the
shipped source and is the one to trust.

## What the baseline already confirms

Cross-checking the trace and the screenshots against the phase 3 port validated
several decisions that had been made from the Delphi source alone:

- **Fuel composition is C7H17.** The Fuel tab shows C 7, H 17, O 0, N 0 — the
  defaults phase 3 inferred from `Edit.dfm` and adopted for the optional
  `[Fuel]` keys. No `.eng` file stores composition, so this screenshot is the
  only record that the original ran on those numbers.
- **The exhaust discharge tables really are crossed.** The Valves tab shows the
  exhaust *Forward Flow Cd* box holding `A2China IVOut.vcd` and *Reverse Flow
  Cd* holding `A2China IVIn.vcd`, matching `ICEngine2Z.pas:998-1005` and the
  phase 3 wiring.
- **`FPlenumP` takes precedence over `PlenumP`** when a file carries both, as
  `EffectivePlenumPressure` assumes.
- **The capacity formula is right.** The form shows 1595 cc; the port computes
  1595.4 cc and the original displays it as an integer.
- **The tab set is right.** The original's eight tabs are Cylinders, Heat Trans,
  Inlet, Exhaust, Cams, Valves, Fuel, Model — the set phase 3 built.
- **The cam angle conventions are right.** IVO °BTDC, IVC °ABDC, EVO °BBDC, EVC
  °ATDC, exactly as carried over.

The trace also validates itself: `Vcyl` runs 48.64 to 447.48 cc, giving a swept
volume of 398.84 cc per cylinder — 1595.4 cc for four — and a ratio of 9.20,
matching `CR=9.2` in the `.eng`.

Two further cross-checks against the charts: peak cylinder pressure in the trace
is 70.1 bar at 14° ATDC, and the P-V diagram peaks just over 70 bar; peak burnt
temperature is 3015 K, and the in-cylinder chart tops out near 3000 K.

The valve lift chart adds two more: the cam timing angles place the profiles
exactly where the conventions say they should, and the normalised `.cam` shape is
scaled by `IVLift` / `EVLift`, which is how phase 3 read those files.

**A duration field the port does not have.** The Cams tab shows a read-only
Duration per cam — 279 °CA inlet, 281 °CA exhaust — computed as
`Open + 180 + Close`. Worth adding to the Edit form.

## A latent defect the baseline exposed

Reconciling `mf` and `ThEff` against `SimulDat.txt` turned up something that has
been sitting in the original for over twenty years.

`TEngine2z.Performance` computes fuel flow as:

```pascal
mf := Cyl.Fuel.m * 2 * Nrpm * 60;
```

`Cyl.Fuel.M` is the fuel mass for **one cylinder over one cycle**, so the
physically correct conversion to kg/hr is `m * NCyl * (N/2) * 60`. The two agree
only when `NCyl * N / 2 == 2 * N`, that is when **`NCyl == 4`**. The cylinder
count is not in the formula at all; the number 4 is baked into the constant.

`ThEff` has the same structure and the same assumption:

```pascal
ThEff := BPower / (Cyl.fuel.Q * Cyl.fuel.m * 2 * Nrpm / 60) * 100;
```

Both reproduce the baseline exactly — 17.35 kg/hr and 30.6 % — because this is a
four-cylinder engine. And **every one of the 71 engine files shipped with ESA is
`NoCyls=4`**, which is why nothing ever caught it. `SFC` derives from `mf`, so it
inherits the same limitation.

Port these verbatim: the baseline was produced by them, and changing them would
put the port out of agreement with its own reference. But pin the behaviour in a
test and treat it as a known defect, because the moment anyone models a three or
six cylinder engine, fuel flow, SFC and thermal efficiency will be silently wrong
by a factor of `4 / NCyl`.

## Getting the manifold traces

The current baseline was run with manifold output off, so there are no in-pipe
pressure or velocity traces. The CFD solver is the largest and least observable
part of phase 4, and having them would be worth a lot.

The switch is on the **Model tab** of the Edit Engine Data window: the *Save Data*
group, checkbox **Save Manifold Data**. It is visible, unticked, in
`A2China_Model.JPG`. Ticking it and pressing OK sets `[Calculation] SaveManfData`
in the `.eng` and `Engine2z.SaveManfData` for the run, after which the nine files
listed in SPEC.md section 3 — `Inlet.txt`, `Exhaust.txt`, `Pcyl.txt`, `Tcyl.txt`,
`MassFlow.txt`, `InlPress.m`, `InlVel.m`, `ExhPress.m`, `ExhVel.m` — are written
**on the final cycle only**, into the application's working directory rather than
next to the engine file.

Three traps if this is attempted:

- **The files are written to the working directory, not next to the engine.**
  `Manifolds.pas:3024-3041` opens them with bare relative names —
  `AssignFile(OutI, 'Inlet.txt')` and so on — so they land wherever the process
  was started from. That is the same place `SimulDat.txt` appears, since
  `PerfDataSave` is a bare name too, so look for them beside that file rather
  than beside the `.eng`.
- `TFMain.Simulate` reads `FEdit.CBSaveManfData.Checked`, the Edit form's
  checkbox, not the engine's field. The Edit window has to have been opened at
  least once in that session for the checkbox to reflect the loaded engine.
- The same line only ever assigns `TRUE`. Once set in a session the flag latches
  until the application restarts, so unticking the box does not turn output back
  off.

Note also that the files are written **only on the final simulated cycle**, so a
run that converges early still produces them, but a run that errors out before
the last cycle will not.

## Using this in phase 4

1. Load `data/baseline/A2China.eng`. It must resolve all ten side files with no
   problems reported.
2. Run at 4000 rpm, two-zone, RKF5, variable gamma, requesting **6 cycles** at a
   **1 mg** mass balance tolerance. Convergence on cycle 4 at about 0.3 mg is
   itself part of the expected result.
3. Work up the derivation chain before comparing anything else. Vd from the
   volume column, then `WWork` and `PWork` at CA −101, then IMEP, PMEP, TFMEP,
   FMEP, BMEP, torque and power. Each link is checkable on its own, so the first
   one that misses localises the fault.
4. Compare per crank angle against `A2China.txt`, remembering the scale factors.
   A per-angle trace localises a fault far better than an end-of-run aggregate:
   a pressure trace that diverges at inlet valve closing points somewhere quite
   different from one that diverges during combustion. Useful landmarks are the
   accumulator reset at −100°, peak pressure 70.1 bar at +14°, and peak burnt
   temperature 3015 K at −7°.
5. Compare the aggregates against the expected results table.
6. Agree the tolerances before treating any of this as a pass/fail gate. A
   per-crank-angle pressure and an end-of-run SFC do not deserve the same number,
   and the 80-bit `Extended` to `double` narrowing in the equilibrium model
   (CLAUDE.md) makes some divergence expected rather than a defect.

## Integrity

MD5 checksums at the time of capture, so drift can be detected:

```
dadb6f2655cc5dce1feb389bb742cfc9  A2China.eng
de65acfdd207f94848866210a7717709  A2ChinaInlet_M758.maf
1865cb0b475aa815e6b29ec0155e392c  A2ChinaExhaust_M.maf
f0de27939df325941ab66db467f15cc1  A2China Inlet Profile.cam
bd06715e331022cbe03d70f7aaa053ee  A2China Exhaust Profile.cam
2ab24170a47dfd9fc548d31466da073b  A2China IVIn.vcd
b1b6f0e92faea38a16b374af3cff2b0c  A2China IVOut.vcd
e0a7dcaca10a883ab3517f8259ddfe02  A2ChinaVar.spk
569c7cac48ae93c5be17f72187afee7a  A2China.cwt
4538bd602703ce5365854777808f785f  A2China.exh
faad1c0108a13806453d0301a3265c9b  A2China.txt
```

Treat every file here as read-only. Regenerating any of it means running the
original Delphi application again, which needs a Windows machine.
