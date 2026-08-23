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

From `Screen_Capture_Results.JPG`:

| Setting | Value |
|---|---|
| Engine | A2 China Jetta 1.6L 5V Baseline |
| Speed | 4000 rpm |
| Cycles | 4 / 4 |
| Mass balance achieved | 0.3 mg |
| Combustion model | 2 zone |
| Integrator | Runge-Kutta-Fehlberg (`Integrator=0`) |
| Variable gamma | on |
| Save manifold data | off |
| Run time | 5 s |

**On the cycle count.** The screen reads `4 / 4`, and the achieved mass balance
of 0.3 mg is below the 0.5 mg tolerance in the shipped `ESA.ini`. The convergence
rule in `TEngine2z.Run` sets `NoCycles := i` when the balance is met, so a run
that converged on cycle 4 of a longer request would display exactly this. Whether
4 cycles were requested or 4 were reached by convergence is therefore ambiguous
from the screenshot alone. **Confirm with the repository owner before treating
the cycle count as an input rather than an outcome.**

## Expected results

The numbers a correct port must reproduce at 4000 rpm.

| Quantity | Value | Unit |
|---|---|---|
| Torque | 151.3 | Nm |
| Power | 63.4 | kW |
| Volumetric efficiency | 109.7 | % |
| IMEP | 14.291 | bar |
| BMEP | 11.921 | bar |
| FMEP | 2.762 | bar |
| PMEP | −0.392 | bar |
| SFC | 273.6 | g/kW.hr |
| Fuel consumption | 17.3 | kg/hr |
| Cylinder mass | 580.1 | mg |

Energy balance, as percentages of fuel energy:

| Term | Value |
|---|---|
| Work | 30.6 % |
| Heat loss | 24.3 % |
| Pumping | −1.0 % |
| Friction | 7.1 % |
| Exhaust | 39.1 % |
| Fuel | 100 % |

Note that IMEP − FMEP = 14.291 − 2.762 = 11.529, which is *not* BMEP. The
identity that holds is BMEP = IMEP − PMEP − FMEP. This is worth checking against
`TEngine2z.Performance`, where `FMEP := TFMEP - PMEP` makes PMEP cancel
algebraically; the reported FMEP is the intermediate, not `TFMEP`.

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

### Screenshots

`A2China_Cylinders.JPG`, `A2China_HeatTrans.JPG`, `A2China_Inlet.JPG`,
`A2China_Exhaust.JPG`, `A2China_Cams.JPG`, `A2China_Valves.JPG`,
`A2China_Fuel.JPG`, `A2China_Model.JPG` — the eight Edit Engine Data tabs after
loading `A2China.eng`.

`Screen_Capture_Results.JPG` — the main window after the run, showing the result
panel, the P-V diagram, the gas-flow pressure traces and the in-cylinder
properties chart.

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

**A duration field the port does not have.** The Cams tab shows a read-only
Duration per cam — 279 °CA inlet, 281 °CA exhaust — computed as
`Open + 180 + Close`. Worth adding to the Edit form.

## Using this in phase 4

1. Load `data/baseline/A2China.eng`. It must resolve all ten side files with no
   problems reported.
2. Run at 4000 rpm, two-zone, RKF5, variable gamma, to a mass balance of 0.5 mg,
   having first settled the cycle-count question above.
3. Compare per crank angle against `A2China.txt`, remembering the scale factors.
   A per-angle trace localises a fault far better than an end-of-run aggregate:
   a pressure trace that diverges at inlet valve closing points somewhere quite
   different from one that diverges during combustion.
4. Compare the aggregates against the expected results table.
5. Agree the tolerances before treating any of this as a pass/fail gate. A
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
