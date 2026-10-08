# Thesis validation suite

Re-runs the dynamometer correlations of the thesis (`legacy/Thesis 02-11-11_03.pdf`,
sections 6.4 to 6.6, Figures 6.5 to 6.15) on the current ESA.NET physics, so a change to
the simulation can be judged against the engines on the test bed and not only against
the original program. `ThesisCorrelationTests` gates one of these figures, 6.12; this
suite covers all ten and draws them as the thesis did.

| Path | What it is |
|---|---|
| `suite.json` | Figures, series, the case and grid behind each series, axes, assumptions |
| `cases/<Case>/` | One engine configuration: its `.eng`, every side file it names, and `.msr` grids of run points |
| `../data/thesis/figures/` | What the thesis plotted: measured and 2002-model series per figure (`data/thesis/README.md`) |
| `tools/build_cases.py` | Writes `cases/` from the legacy files |
| `tools/digitise_thesis.py` | Writes `data/thesis/figures/` from the PDF |

Both scripts are deterministic: re-running them must leave `git status` clean. They need
Python 3, `pypdf` and poppler's `pdftotext`, and only when a source changes.
`tests/App.Tests/ThesisSuiteTests.cs` checks without running a simulation that every case
loads with every side file resolved, that every grid reads, and that every series in
`suite.json` names data that exists.

## Running the suite

```bash
dotnet run --project tools/App.Validation -c Release -- [options]
```

| Option | Effect |
|---|---|
| `--mode corrected` / `--mode legacy` | Physics mode. Corrected is the default; Legacy is for diagnosis |
| `--set B4=1` | Override one correction, as in `ESA.ini` (repeatable) |
| `--figure 6.12` | Run only one figure (repeatable) |
| `--previous <dir>` | Compare with an earlier report folder: dotted curves on every chart, plus a change-in-rms column |
| `--out <dir>` | Output folder. The default is `validation-reports/<date>_<time>_<mode>`, which git ignores |

A full run takes about half a minute. Grid rows run in parallel, and a row that fails is
reported rather than aborting the run. The folder holds:

- **`report.html`**: one self-contained page. It opens with the commit, its dirty flag,
  the mode and the corrections. A summary table follows, then one section per figure with
  the chart drawn after the thesis figure, the comparison, the runs that did not finish, a
  data table, and the assumptions that figure rests on.
- **`results.csv`**: every simulated point, as `figure,series,x,y`.
- **`failures.csv`**: every run that did not finish, with its message.
- **`run.json`**: what was run and when. `--previous` reads it along with `results.csv`.

The charts mark where each curve came from. Dots are the dynamometer, a dashed line with
open squares is the thesis's 2002 model, a solid line is ESA.NET now, and a dotted line
is the previous run. Colour marks the series. Each comparison is the rms and the bias of
one curve against another, taken at the reference curve's own points, in percent of the
reference (bar for Figure 6.8, Nm for Figure 6.11). A simulated curve is interpolated
only across gaps under 1.6 times its median point spacing, so a failed row leaves a gap
instead of a line drawn through it. Figures 6.10 and 6.12 are compared from 2500 rpm up,
as the thesis does.

Reproducing `ThesisCorrelationTests` is a check that the runner is wired the same way as
that test: `--figure 6.12 --mode legacy --set B4=1` gives 5.3 % rms, a bias of -3.3 Nm and
151.8 Nm at 4000 rpm.

## Cases

Every file in a case is a byte copy of a legacy file, except the `.eng` of a case that
changes one value. That file is derived from its source and opens with a `;` comment
naming the source and the change. Side-file paths are left as the legacy files wrote
them; `LegacyPathResolver` finds each one beside the `.eng`.

| Case | Engine | Source | Grids | Figures |
|---|---|---|---|---|
| `Nissan_290` | Nissan Z24/NA20, 290 mm inlet | `legacy/ESA/Data/Example1/Nissan/Nissan6.eng` | `torque` 2000-5000 rpm | 6.5, 6.6 |
| `Nissan_390` | 390 mm inlet | `.../Nissan5.eng` | `torque`; `pulse5000` | 6.5, 6.8 |
| `Nissan_490` | 490 mm inlet | `.../Nissan4.eng` | `torque` | 6.5 |
| `Nissan_290_Adv12` | 290 mm, cam advanced 12° | `.../Nissan7.eng` | `torque`; `timing2000` spark 10-30° | 6.6, 6.7 |
| `VW8V_A4LowCost` | VW 1.6 L eight-valve | `legacy/CAEEng/A4LowCost.eng`, older schema restated (V1) | `torque` 1500-6000; `camA`, `camB` | 6.10, 6.11, **does not run** |
| `A2China_Baseline` | VW 1.6 L five-valve baseline | `data/baseline/A2China.eng` | `torque` 1500-6250 | 6.12 |
| `ChinaBora_CR93` | five-valve, CR 9.3 | `legacy/ESA/Data/Example2/ChinaBora92.eng`, CR changed | `timing4000` spark 8-24° | 6.13 |
| `ChinaBora_CR103` | five-valve, CR 10.3 | the same, CR changed | `timing4000` | 6.13 |
| `ChinaBora98_Dia355` | 35.5 mm runners | `.../ChinaBora98.eng`, inlet `.maf` changed | `torque` 1500-6500 | 6.14 |
| `ChinaBora98_Dia41` | 41 mm runners | the same | `torque` | 6.14 |
| `ChinaBora98_Final` | final prototype | `.../ChinaBora98.eng` | `torque` | 6.15 |

Every grid row runs 8 cycles. Spark advance comes from the engine's `.spk` map unless the
grid sets it (column 13, timing loops); cam timing and lift come from the `.eng` unless
the grid sets them (columns 7 to 12, Figure 6.11).

**Manifold variants are separate cases, not grid rows.** In the port a grid row's file
override (columns 3 to 6, `IManfFile` to `ECamFile`) changes only the file name, after
`EngineLoader` has read the tables, so the row runs on the base engine's manifold. The
original reloaded them in `InitVars` (`ICEngine2Z.pas:994-1009`). Until that is fixed,
the suite keeps one manifold per `.eng`.

## What does not run

**`VW8V_A4LowCost` fails in its first cycle at every speed**, so Figures 6.10 and 6.11
show only the measured and 2002-model curves, with the failure. Two causes were isolated,
by moving its inputs onto the working `A2China_Baseline` one group at a time:

- **λ below about 0.97.** `A2China_Baseline` with only `Lambda` changed runs at 0.98 and
  fails at 0.95 and 0.92 with "Matrixsolver Returned Insufficient Resolution", in Legacy
  and Corrected alike. The engine file says 0.92, and the dynamometer ran at 0.88 to 0.93.
- **The 1999 exhaust area profile.** `A4LowCostEx.maf` (611 to 1452 mm² in steps) stops
  the wave solver ("Pressure negative in cThermo") even on `A2China_Baseline`, while a
  uniform pipe of the same length runs at any of those areas.

At λ 1.0, with its own exhaust, it still stops at 1500-4500 and 6000 rpm and makes an
implausible 209 Nm at 5000. Nothing in the suite substitutes inputs to make it run,
because that would be fitting. Both causes are open defects to raise against the solver.

## Assumptions

The thesis text leaves some inputs open. Each case uses the reading the evidence supports
best (the source spreadsheets in `data/thesis/source/`, the legacy file names and dates,
the thesis text, in that order), and nothing is adjusted to bring a result nearer the
measured curve. Each figure in `suite.json` lists the entries it depends on.

| Id | Assumption | Evidence |
|---|---|---|
| N1 | The Nissan cases are `Example1/Nissan/Nissan4.eng` to `Nissan7.eng`, unchanged | They carry Table 6.2 exactly (bore 89, stroke 96, CR 8.3, valves 38/34 mm, 20/40/60/30°). Their `.spk` maps equal the spark advance at every point of `ModellingPerformance_Final.xls`. The spreadsheet's tests ran with cylinder 1's inlet open to atmosphere and the custom exhaust, which are tests 4 to 7 of Table 6.1; the model has no cylinder interaction either way |
| N2 | The legend's 210/310/410 mm are tract lengths; the cases' `NissanInlet_290/390/490.maf` are total flow lengths including the 80 mm port | Section 6.4.2 and Table 6.1 |
| N3 | The 2002 model ran at the engine file's own settings, including `Lambda=1.05` | The spreadsheet's Lambda column reads 1.05 throughout |
| N4 | The timing loop is at 2000 rpm, 290 mm inlet, cam advanced 12° | The measured loop in `ModellingPerformance_Final.xls` (sheet TestPerf2: 2000 rpm, spark 15.5/19.7/24.0°) is Test 7 |
| N5 | Point A is the inlet pipe's grid point at the valve. The thesis's 0-720° axis counts from firing top dead centre, so the report shifts ESA.NET's trace, which counts from intake top dead centre, by 360° | "As close as possible to the cylinder head". The thesis's measured and model traces have their ram peak just before 580°, which is inlet valve closing (40° ABDC) counted from firing TDC. ESA's recorder puts inlet valve closing at IVC + 360 = 220° (`ManifoldCaptureWindow`), and that is where its own peak falls. The recorder keeps only the original's capture window, so ESA.NET has no trace from 580° to 720° |
| V1 | The eight-valve engine is `legacy/CAEEng/A4LowCost.eng`, including its fixed 15° spark, λ 0.92, and constant 20 kPa, 400 °C back pressure. The derived file restates the three values the port misreads in the older schema: plenum pressure in pascals, grid point counts under `[Inlet]`/`[Exhaust]`, and wall temperatures in kelvin | It is the only eight-valve definition that survives, named after the project and dated within it. Its cam profile 050 109 113 B is the "050 B" of the Kick-Up spreadsheet. The translations follow the predecessor's own `Edit.pas` and `GridSizes.pas` (CAEEng backup), not a guess |
| V2 | The long production manifold `06A133205G long.maf` (658 mm) stands in for both the final prototype manifold of Figure 6.10 and the 630 mm aluminium manifold of the cam study | Neither manifold file survives |
| V3 | Figure 6.10 is compared as shape, not level | The case is the pre-development engine; the measured curve is the final one |
| V4 | Cam A and cam B are Table 6.3's timings and lifts on the `050109113B` profile shape, the same lift for both valves | The cams' own profiles (026 109 113B and 113D) are not in the backup. `China Cam Effect Modelling.xls` identifies cam A as 026 109 101M and cam B as 101L |
| C1 | Figure 6.12 is `data/baseline/A2China.eng`, the reference run's engine | `China_5V Baseline Modelling.xls` names the same engine, manifold and spark map; `ThesisCorrelationTests` uses it |
| C2 | The compression ratio cases are `ChinaBora92.eng` with only `CR` changed, to the legend's 9.3 and 10.3 | `ChinaBora92/98/103.eng` are the compression ratio series; 92 says 9.2 where the figure says 9.3. `ChinaBora103.eng` was not used: it names a `ChinaBora103.spk` that does not exist and a different manifold, so it would change two things at once |
| C3 | The timing loops are at 4000 rpm | Not recorded anywhere that survives. The measured torque (143-153 Nm) is the engine's level at its 4000 rpm peak |
| C4 | Figure 6.14 is `ChinaBora98.eng` (CR 9.72) with `A3TumbleInlet_M770_355.maf` and `A3TumbleInlet_M770_41.maf`, on `ChinaBora98.spk` | `China_5V Model vs Test Diameters.xls` names exactly these, and the engine tested measured CR 9.715. The thesis text's 686 mm and 10.3:1 describe the project, not this run |
| C5 | Figure 6.15 is `ChinaBora98.eng` with its own `A3TumbleInlet_M770_36.maf` | `China_5V Final Model vs Test.xls`, whose first model sheet is `Model_770_36`, could not be saved (`data/thesis/README.md`). `ChinaBora103N.eng` (CR 10.3, the same manifold) is the alternative |
