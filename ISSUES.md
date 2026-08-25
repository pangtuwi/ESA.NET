# Known issues

Everything found so far that is wrong, surprising, or deliberately reproduced,
in one place. Findings were previously scattered across `CLAUDE.md`,
`BASELINE.md`, the phase task files and code comments, which made them easy to
miss.

Sections A and F are work. B, C, D and E are reference: they exist so nobody
"fixes" something that is load-bearing, or rediscovers it the expensive way.

---

## A. Defects in this port

Ours, and ours to fix.

| # | Issue | Status |
|---|---|---|
| A1 | `EsaLimits.MaxEquations` was 10; Delphi's `MaxN` is 4 | **Fixed** |
| A2 | Edit form ordered the fuel composition boxes C H N O; the original orders them C H O N | **Fixed** |
| A3 | Edit form showed capacity as 1595.4; the original displays whole cc, 1595 | **Fixed** |
| A4 | Cams tab is missing the read-only **Duration** field the original shows, computed as `Open + 180 + Close` (279 °CA inlet, 281 exhaust on the baseline engine) | Open |
| A5 | Cylinders tab: in the original, *No Cylinders* and *Capacity* look greyed out. Capacity is derived so read-only is right, but the port lets cylinder count be edited. Needs confirming against the running app before changing | Open, unconfirmed |

## B. Legacy defects reproduced on purpose

**Do not fix these.** The reference run in `data/baseline/` was produced by them,
so "correcting" any one puts the port out of agreement with the thing it is being
measured against. Each is pinned by a test.

| # | Behaviour | Where |
|---|---|---|
| B1 | `mf`, `ThEff` and therefore `SFC` hard-code four cylinders: the factor is `2 * Nrpm` where the physics needs `NCyl * Nrpm / 2`. All 71 shipped engines are `NoCyls=4`, so it was never exercised. Wrong by `4 / NCyl` for any other engine | `TEngine2z.Performance` |
| B2 | `FMEP := TFMEP - PMEP` then `BMEP := IMEP - PMEP - FMEP`, so PMEP cancels and BMEP is really `IMEP - TFMEP`. The reported FMEP is the intermediate, not the friction correlation | `TEngine2z.Performance` |
| B3 | The exhaust valve's discharge tables are crossed: `EV.CdForward` comes from `CdEvOut`, `EV.CdReverse` from `CdEvIn`, because forward flow through an exhaust valve is outward. The inlet valve is wired the obvious way | `ICEngine2Z.pas:998-1005` |
| B4 | `TAManf.GetValue` returns **zero** past the end of the area table, not the last area. A cliff, not a clamp | `FManfA.pas` |
| B5 | `TCdValve.GetValue` passes its y arguments in the reverse order to its x ones | `IPolTab.pas` |
| B6 | The `IVFFn` expression is ignored at or below 1000 rpm, replaced by a hard-coded line. Not yet ported; belongs with the solver | `Manifolds.pas:2739-2742` |
| B7 | `^` is **left**-associative, so `2^3^2` is 64. Unary minus binds looser, so `-2^2` is −4. A sign is legal only at the start of an expression or a bracket, so `3*-2` is an error | `ADCALC.PAS:2555-2620` |
| B8 | `^` must not use `Math.Pow`: Delphi routes integer exponents through `IntPower`'s repeated squaring. The paths differ in the last bits, and a grid size is the `Round` of an expression in `N^6` | `DelphiMath.Power` |
| B9 | Delphi `Round` is round-half-to-even. `Math.Round` matches; a cast or `Floor(x + 0.5)` would not | throughout |
| B10 | The burnt-volume clamp `if Vb > Vgas then Vb := Vgas` is an intentional safeguard | `Gasses2Z.pas` |
| B11 | Fewer than three cycles is silently raised to three | `TFMain.Simulate` |
| B12 | RKF5 here is **fixed-step**: six Fehlberg stages, no error estimate, no adaptive control, despite the name. Do not "improve" it | `RKf5.pas` |
| B13 | Delphi's 80-bit `Extended` becomes `double`. Unavoidable, and the first thing to suspect if phase 4 numbers drift — it matters most in the equilibrium model's Newton iteration | throughout |
| B14 | **The RKF5 tableau carries a transposed digit.** `RKf5.pas:76` reads `854/4104` where Fehlberg published `845/4104`, so the fifth stage's coefficients sum to 455/456 instead of the 1 its node requires. Measured effect: the method converges at **first order, not fifth** — halving the step halves the error rather than dividing it by 32. At 40 steps over a unit interval it is seven orders of magnitude less accurate than the method it claims to be. ESA offers it to the user as "Runga Kutte Felberg (accurate)" against "Euler (fast)" | `RKf5.pas:76` |
| B15 | **The equilibrium derivatives use the wrong pressure units.** `go2` builds C1 to C10 from pressure in atmospheres (`p := Pres/101325`, Eqbm.pas:117) but `Partial_dxd` rebuilds their temperature derivatives from pressure in pascals (`p := Pres`, Eqbm.pas:294). Every `dC/dT` is off by `sqrt(101325)` = 318.3, and `dx/dT` lands 260 to 360 times the true derivative. The pressure derivatives escape it, because `dC/dPres = -0.5*C/Pres` holds whatever constant factor sits inside C. These feed `dudT` and so reach the ODEs. **Measured consequence**: `ReturnProps` inflates `Cp` and `DuDt` for burnt gas by about 11x at 1800 K, 92x at 2400 K and 164x at 2800 K against the frozen specific heat, which is itself physically sound at 1400 to 1500 J/(kg.K). `Get_gamma` escapes it by passing a zero derivative array, which is why gamma still matches the baseline trace exactly | `Eqbm.pas:294` |

## C. Legacy behaviour that catches out the operator

Not port defects, but things that make the original behave in ways that look like
breakage.

**C1 — Manifold output needs three conditions, not one.** The gate is
`(CA = 359) and (tStep = NoCycles-1) and (DataWrite = TRUE)`
(`Manifolds.pas:3022`). Ticking *Save Manifold Data* only satisfies the third.
`NoCycles` is `Engine2z.NCycles`, the **requested** count, fixed at
`Main.pas:895` and never updated by convergence. So the files appear only if the
run reaches the final requested cycle — and a run that converges early exits
before it. Requesting 6 cycles produced nothing; requesting 4 produced all nine.

**C2 — The checkbox is read off the Edit form, not the engine.**
`TFMain.Simulate` tests `FEdit.CBSaveManfData.Checked`, so the Edit window must
have been opened at least once in the session for it to reflect the loaded engine.

**C3 — And it latches.** That same line only ever assigns `TRUE`, so once set it
stays set until the application restarts. Unticking does not turn output off.

**C4 — Manifold files land in the working directory**, not beside the `.eng`.
They are opened with bare relative names. Look where `SimulDat.txt` appears.

**C5 — "4 / 4" does not mean four cycles ran.** The convergence test sits at the
top of the loop body, before the `repeat` that runs the cycle, and exits outright.
Stopping at `i = 4` means cycles 1 to 3 ran and cycle 4 never did.

**C6 — The performance data file accumulates.** Rows are appended per run, so one
file can hold several unrelated runs. `Example1/Simuldat.txt` has two 5000 rpm
rows with different values.

**C7 — Switching the gas-flow chart to velocities after a run shows stale data.**
The panel title changes but the plot does not: the mode is read on refresh, and
the refresh timer has stopped. The axis still reads Pressure [bar].

**C8 — `Ctrl+Q` is assigned to both Exit and QuickRun.** In Avalonia
`MenuItem.InputGesture` is display-only, so nothing clashes yet. Resolve it when
those commands get real behaviour.

**C9 — `BOKClick` swallows `EConvertError` and tells the user nothing**, so one
bad numeric field silently discards the whole edit. The port does not reproduce
this: every field validates and OK is disabled while anything is invalid.

**C10 — `IniValues.SaveIniValues` is empty**, so the original never wrote
`ESA.ini` back. The port implements it properly.

## D. Errors and gaps in SPEC.md

`SPEC.md` is the phase 1 reverse-engineering output. Where it disagrees with the
source or the data, the source and the data win.

| # | Section | Problem |
|---|---|---|
| D1 | §3 | `.exh` columns are given as RPM, pressure, temperature. They are RPM, **temperature**, **pressure** — the loader reads `ATExh` before `APExh` and the shipped header row reads `SPEED / TEMP[C] / P[kPa]` |
| D2 | §3 | The `.eng` section list omits the older `[InManifold]` / `[ExManifold]` schema used by five `Example1` engines |
| D3 | §3 | Does not state that `.eng` keys must match **case-insensitively**. `Edit.pas` reads `CdIvIn`; every shipped file writes `CdIVIn` |
| D4 | §3 | Describes `.spk`, `.cwt` and `.exh` as simple column pairs. They have a row-count line and a discarded heading line first |
| D5 | §3 | The quoted `ESA.ini` does not match the shipped file: `CAEEng.err` not `ESA2z1z.err`, `MassBalance=0.5` not `1`, and no trailing newline |
| D6 | §2 | Calls `TCAPoint` a record; it is a Delphi `class` |
| D7 | §1 | Says to reproduce the main menu but never lists it, and treats `Main.dfm` as readable when it is **binary** DFM needing string extraction |
| D8 | §4 | Recommends WPF and OxyPlot. Superseded by `TECHSTACK.md`, which requires Avalonia and ScottPlot |
| D9 | §6 | Says tolerances "must be measured from legacy reference runs" before acceptance tests are written. Now satisfied — see `BASELINE.md` |

## E. Dead data

Present in the repository, referenced by nothing.

| # | What | Evidence |
|---|---|---|
| E1 | `Example1/Inlet.grd`, `Example1/Exhaust.grd` | No Delphi unit references `.grd`. They hold Pascal fragments, not data |
| E2 | `.eng` keys `IVMinA` and `EVMinA` | Never read by any Delphi unit. Present in `A2China.eng` and the older `Example1` engines; round-tripped, never used |
| E3 | `.msr` files | Saved **multi-run grids**, i.e. inputs, not results. Easy to mistake for recorded output |
| E4 | `Example1/Pressure.dat` | 721 rows of 21 columns; almost certainly a renamed `InlPress.m` |

## F. Open questions

| # | Question | Why it matters |
|---|---|---|
| F1 | The manifold files and `A2China.txt` come from **adjacent** cycles — which is which? The closed period agrees to 0.0001 bar, gas exchange diverges up to 0.07 bar | Phase 4 needs to know which cycle it is reproducing when comparing gas-exchange data |
| F2 | `A2China.eng` carries both `PlenumP=98.0` (kPa) and `FPlenumP=(99000)` (Pa). The Inlet tab shows `(99000)`, so `FPlenumP` wins — but 98 kPa and 99 kPa are not the same number, so this is not a stale unit conversion | The plenum pressure feeds the inlet boundary condition; the wrong one shifts every intake result |
| F3 | Is *No Cylinders* genuinely read-only in the original, or just rendered flat? | See A5 |
| F4 | What tolerance should each compared quantity carry? | Nothing is a pass/fail gate until this is agreed — see `BASELINE.md` |

---

## Where the detail lives

- `BASELINE.md` — the reference run, the derivation chain from trace to
  headline numbers, and the manifold output mechanics (C1 to C5).
- `CLAUDE.md` — the port caveats that affect day-to-day work (most of B).
- `task-phase4.md` — what phase 4 must do about all of it.
- `tests/App.Tests/BaselineDataTests.cs` — B1, B2, B7, B8, B9 and the grid-size
  chain pinned against the original's own output.
- `tests/App.Tests/EquilibriumSolverTests.cs` — B15, pinned by comparing the
  analytic derivative against a finite difference of the solver itself.
- `tests/App.Tests/GasPropertyModelTests.cs` — gamma against the baseline trace in
  both the burnt and unburnt branches, and B15's downstream effect on Cp and DuDt.
- `tests/App.Tests/Rkf5IntegratorTests.cs` — B12 and B14, including a measured
  order-of-convergence test that fails loudly if the transposed coefficient is
  ever "corrected".
