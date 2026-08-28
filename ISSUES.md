# Known issues

Everything found so far that is wrong, surprising, or deliberately reproduced,
in one place. Findings were previously scattered across `CLAUDE.md`,
`BASELINE.md`, the phase task files and code comments, which made them easy to
miss.

**Every entry below is mirrored as a GitHub issue**, linked from its number.
The issues carry the same text; this file stays the place the entries are written
and revised. Entries already resolved — A1, A2, A3, A7, A9, B68 and C13 — have
their issues closed.

Sections A and F are work now. B, C, D and E are reference during the port: they
exist so nobody "fixes" something that is load-bearing, or rediscovers it the
expensive way.

**After the port reproduces the original end to end, section B becomes the
improvement backlog.** Each entry carries a verdict — Fix, Keep, Moot or Stuck —
so the list can be worked through without re-deriving why each thing is there.

The ones that change results, roughly in order of how much:

| Entry | What fixing it buys |
|---|---|
| [B14](https://github.com/pangtuwi/ESA.NET/issues/26) | The integrator converges at first order instead of fifth; fixing it makes results far less sensitive to the crank-angle step |
| [B1](https://github.com/pangtuwi/ESA.NET/issues/13) | Fuel flow, SFC and thermal efficiency are wrong by `4 / NCyl` for any engine that is not a four-cylinder |
| [B6](https://github.com/pangtuwi/ESA.NET/issues/18) | A hard-coded line silently overrides the user's `IVFFn` at or below 1000 rpm |
| [B18](https://github.com/pangtuwi/ESA.NET/issues/30) | The unburnt mixture depends on call history, so the first evaluation of a state differs from later ones |
| [B4](https://github.com/pangtuwi/ESA.NET/issues/16), [B5](https://github.com/pangtuwi/ESA.NET/issues/17) | A lookup that falls to zero past its table, and a bilinear interpolation with its axes crossed |

Fixing any of these will move the numbers away from `data/baseline/`, so expect to
re-baseline against a fresh reference run, or to keep both behaviours behind a
switch.

---

## A. Defects in this port

Ours, and ours to fix.

| # | Issue | Status |
|---|---|---|
| A1 ([#3](https://github.com/pangtuwi/ESA.NET/issues/3)) | `EsaLimits.MaxEquations` was 10; Delphi's `MaxN` is 4 | **Fixed** |
| A2 ([#4](https://github.com/pangtuwi/ESA.NET/issues/4)) | Edit form ordered the fuel composition boxes C H N O; the original orders them C H O N | **Fixed** |
| A3 ([#5](https://github.com/pangtuwi/ESA.NET/issues/5)) | Edit form showed capacity as 1595.4; the original displays whole cc, 1595 | **Fixed** |
| A4 ([#6](https://github.com/pangtuwi/ESA.NET/issues/6)) | Cams tab is missing the read-only **Duration** field the original shows, computed as `Open + 180 + Close` (279 °CA inlet, 281 exhaust on the baseline engine) | Open |
| A5 ([#7](https://github.com/pangtuwi/ESA.NET/issues/7)) | Cylinders tab: in the original, *No Cylinders* and *Capacity* look greyed out. Capacity is derived so read-only is right, but the port lets cylinder count be edited. Needs confirming against the running app before changing | Open, unconfirmed |
| A6 ([#8](https://github.com/pangtuwi/ESA.NET/issues/8)) | **`Engine` carries the `.eng` file's units, not SI.** Bore, stroke, conrod length, valve lift and valve diameter are millimetres; atmospheric and plenum pressures are kilopascals; ambient temperature is Celsius; valve timings are degrees before or after a dead centre rather than signed crank angles. Delphi converted all of these on the way out of the edit form (`Edit.pas:412-419, 448-466`), so its engine object was SI throughout and the simulation could assume it. The port's loader is persistence-shaped and stores what the file says, so every conversion happens at the simulation boundary instead: `CylinderGeometry.FromEngine`, `ValveMotion.Inlet`/`Exhaust`, `CrankAngleStateMap.FromEngine` and `CycleSolver.Initialise`. Each is pinned by a test. Consolidating them behind one runtime view of the engine would be tidier and is worth doing before phase 5 adds more callers | Open, by design for now |
| A7 ([#9](https://github.com/pangtuwi/ESA.NET/issues/9)) | **The burnt-zone temperature derivative was ~100x too large.** `EquilibriumSolver.Solve` computed the atmospheres value `p` for the equilibrium constants, then passed the raw pascals `pressure` to `PartialDerivatives`, where `go2` passes `p`. Every `dC/dT` came out `sqrt(101325)` too large, and that reached `dudT` through `MixdhdT` and `MixdRdT`, leaving `dudTb` near 172,000 J/(kg K) against a sound 1,660. It sits in the denominator of the burnt temperature equation, so `dTb/dtheta` was 99 per cent short. **Fixed** by passing `p`. The equilibrium temperature derivatives now agree with a finite difference of the solver to seven significant figures, where they were 250 to 380 times out; per-crank-angle cylinder pressure improved from 0.77 to 0.25 per cent through expansion and from 1.26 to 0.72 per cent through combustion. Found only because the baseline trace disagreed - `Gamma` matched throughout, because `Get_gamma` passes a zero derivative array and never touches this path | **Fixed** |
| A8 ([#10](https://github.com/pangtuwi/ESA.NET/issues/10)) | **Cylinder pressure through combustion carries a smooth bias**, -0.72 per cent at the spark, crossing zero around 5 degrees before top dead centre and settling near +0.2 per cent for the rest of the burn. Not a transient: the one-step-ahead harness reloads the reference state before every step, so nothing propagates and this is a systematic error in the burning equations themselves. **Ruled out**: the burnt fraction and both zone masses match the trace exactly at every step, 0.01 clamp included; the `dudp` finite-difference band matches the original digit for digit; `dudT` is sound since A7; and the stale mass-flow derivatives of B46 are not read by these equations. **Where to look next**: the bias changes sign as the burnt fraction passes about 0.2, so weigh the unburnt-side terms of `dPdThetaB` against the burnt-side ones - `Q` and `H` carry `mu` and `Ru`, `F` and `R` carry `mb`, `Rb` and `dudTb`. Note that `Vu` is zero throughout the burn because of the B10 clamp, which kills `L`, `J` and part of `F` and `S`, so the unburnt zone reaches the answer only through `Q`, `B`, `D` and `H`. Contributes roughly 0.1 per cent to IMEP, which is at the edge of the phase 4 aggregate tolerance. Compression (0.081 per cent) and expansion (0.036 per cent) are unaffected | Open, small |
| A9 ([#11](https://github.com/pangtuwi/ESA.NET/issues/11)) | **Exhaust back pressure was read as absolute and its temperature converted from Celsius; both were wrong.** `TExhaustPandT.Pres` returns `TempP*1000 + PAtm` (`ExhBackPandT.pas:72`), so the `.exh` table's kPa figure is **gauge** and atmospheric has to be added; the port used it as absolute, leaving the whole exhaust pipe at 17.8 kPa instead of 119.1 kPa. `TExhaustPandT.Temp` returns its value **raw**, so the port's `+ 273.15` was also wrong — see B66. Together these emptied the cylinder through an exhaust pipe that was effectively a vacuum: cylinder pressure fell to 12 kPa where the reference has 65 kPa, and the whole exhaust stroke was 30 to 90 per cent low. **Fixed**; the converged whole-cycle comparison went from unusable to inside 0.33 per cent at every crank angle | **Fixed** |
| A10 ([#12](https://github.com/pangtuwi/ESA.NET/issues/12)) | **The exhaust wave field is an order of magnitude less accurate than the inlet one.** Measured against the original's own field files from a converged run: inlet pipe pressure agrees to 0.006 bar and inlet velocity to 8 m/s, but exhaust pipe pressure only to 0.098 bar and exhaust velocity to 36 m/s. Cylinder pressure, temperature and volume in the same files agree to 0.42 bar, 3.9 K and exactly. Part of the exhaust gap is not the port's: F1 records that the manifold files and the PVT trace come from adjacent cycles and that gas exchange differs by up to 0.07 bar between them, which is the same order as the discrepancy — so this sits near the resolution limit of the reference data and cannot be pinned down further without a fresh reference run capturing both on the same cycle. Worth revisiting if one is ever produced | Open, bounded by the reference data |
| A11 ([#119](https://github.com/pangtuwi/ESA.NET/issues/119)) | **The manifold file bounds were measured from physics the app never runs.** `ManifoldTraceWriterTests.RunAndWrite` counts cycles with `solver.RunCycles`, which sets `ZoneCount` to 1 and then 2 as the original does, then records from a second pass of bare `RunOneCycle` calls that never set `ZoneCount` at all — it stays at 0, its uninitialised value, so every cycle of the recording pass is single-zone. `SimulationRunner` runs cycle 1 single-zone and the rest two-zone, so the bounds in `TheValuesAgreeWithTheOriginalsToTheMeasuredBounds` describe a run the application never performs. **Measured**: routing `RunAndWrite` through `SimulationRunner` — same engine, settings, three cycles and 620-row window — moves `MassFlow.txt` column 1 to 0.466 against its recorded bound of 0.25, everything else staying inside. The wrong physics passes and the right physics does not, which is either a real loss of accuracy in the two-zone gas-exchange path (B37 has overlap running a single-zone constant-gamma pressure equation, exactly where a mass-flow discrepancy would come from) or a bound that needs re-measuring against the real path. Not established which. Found while wiring up C1 and left alone there rather than re-measure a documented bound as a side effect | Open |
| A12 ([#120](https://github.com/pangtuwi/ESA.NET/issues/120)) | **A multi-run sweep writes no manifold data at all.** `MultiRunner.RunRow` calls `_runner.Run` without a `manifoldRecorder` (`MultiRunner.cs:99`), so `SaveManifoldData` is ignored for every row, where a single-point run of the same engine writes all nine files. The original is not much better: `DataWrite := SaveManifoldData` sits inside `Main_Prog` (`Manifolds.pas:2701`), which every row runs, and the files are opened with bare relative names (C4) — so each row overwrites the last and what survives is one unlabelled row, and under C1's gate not even the last one but the last that reached its final requested cycle. Left out of the C1 fix rather than guessed at, because it needs a destination and a naming decision: a subdirectory or a filename prefix per row, whether every row is written at all (100 rows × 9 files is 900 of them), and how the row's speed and overrides get recorded when the filenames cannot carry them | Open |
| A13 ([#121](https://github.com/pangtuwi/ESA.NET/issues/121)) | **The editor's changes never reached the engine the simulation reads.** `EngineLoadResult` carries an `Engine` and an `EngineDefinition` that are two snapshots taken at load time; `EditEngine` handed the editor the definition, `Apply` wrote to the definition, and the run read the engine, with nothing reconciling them. Bore, stroke, cam and manifold file names, valve timing and `Save Manifold Data` alike reached the definition and stopped there — the only way to get an edit into a run was to save the file and reopen it. The original has no such split: `Edit.pas`'s OK handler converts and assigns straight onto `Engine2z` (`Edit.pas:412-419, 448-466`); the port grew the split when it separated the format-preserving INI model from the domain model, and nothing was put back to bridge them. **Fixed** by `IEngineLoader.Rebuild` plus the editor's `Applied` event, which the shell rebuilds `CurrentEngine` on. Found while working on C2, where it surfaced narrowly as the checkbox having no effect | **Fixed** |

## B. Legacy defects reproduced on purpose

**Do not fix these while the port is being brought up.** The reference run in
`data/baseline/` was produced by them, so correcting any one puts the port out of
agreement with the thing it is being measured against. Each is pinned by a test.

Once the port reproduces the original end to end, this becomes the improvement
backlog. The **Later?** column is the verdict on each:

- **Fix** — a genuine defect worth correcting once fidelity is no longer the goal.
- **Keep** — deliberate, or correct as written; leave alone.
- **Moot** — dead or overwritten code, so changing it alters nothing.
- **Stuck** — cannot be fixed, only mitigated.

Numbering is stable: entries are referenced from code comments, so nothing is ever
renumbered.

| # | Behaviour | Where | Later? |
|---|---|---|---|
| B1 ([#13](https://github.com/pangtuwi/ESA.NET/issues/13)) | `mf`, `ThEff` and therefore `SFC` hard-code four cylinders: the factor is `2 * Nrpm` where the physics needs `NCyl * Nrpm / 2`. All 71 shipped engines are `NoCyls=4`, so it was never exercised. Wrong by `4 / NCyl` for any other engine | `TEngine2z.Performance` | **Fix** — silently wrong for any engine that is not a four-cylinder |
| B2 ([#14](https://github.com/pangtuwi/ESA.NET/issues/14)) | `FMEP := TFMEP - PMEP` then `BMEP := IMEP - PMEP - FMEP`, so PMEP cancels and BMEP is really `IMEP - TFMEP`. The reported FMEP is the intermediate, not the friction correlation | `TEngine2z.Performance` | Keep — algebraically equivalent, only the reported FMEP is unconventional |
| B3 ([#15](https://github.com/pangtuwi/ESA.NET/issues/15)) | The exhaust valve's discharge tables are crossed: `EV.CdForward` comes from `CdEvOut`, `EV.CdReverse` from `CdEvIn`, because forward flow through an exhaust valve is outward. The inlet valve is wired the obvious way | `ICEngine2Z.pas:998-1005` | Keep — correct physics, just surprising |
| B4 ([#16](https://github.com/pangtuwi/ESA.NET/issues/16)) | `TAManf.GetValue` returns **zero** past the end of the area table, not the last area. A cliff, not a clamp | `FManfA.pas` | **Fix** — a discontinuity in a lookup is almost certainly unintended |
| B5 ([#17](https://github.com/pangtuwi/ESA.NET/issues/17)) | `TCdValve.GetValue` passes its y arguments in the reverse order to its x ones | `IPolTab.pas` | **Fix** — reversed arguments look like a slip |
| B6 ([#18](https://github.com/pangtuwi/ESA.NET/issues/18)) | The `IVFFn` expression is ignored at or below 1000 rpm, replaced by a hard-coded line. Not yet ported; belongs with the solver | `Manifolds.pas:2739-2742` | **Fix** — a hard-coded line silently overriding user data |
| B7 ([#19](https://github.com/pangtuwi/ESA.NET/issues/19)) | `^` is **left**-associative, so `2^3^2` is 64. Unary minus binds looser, so `-2^2` is −4. A sign is legal only at the start of an expression or a bracket, so `3*-2` is an error | `ADCALC.PAS:2555-2620` | Keep — matches the shipped expressions, which were written for it |
| B8 ([#20](https://github.com/pangtuwi/ESA.NET/issues/20)) | `^` must not use `Math.Pow`: Delphi routes integer exponents through `IntPower`'s repeated squaring. The paths differ in the last bits, and a grid size is the `Round` of an expression in `N^6` | `DelphiMath.Power` | Keep — required for bit-agreement with the original |
| B9 ([#21](https://github.com/pangtuwi/ESA.NET/issues/21)) | Delphi `Round` is round-half-to-even. `Math.Round` matches; a cast or `Floor(x + 0.5)` would not | throughout | Keep — correct rounding |
| B10 ([#22](https://github.com/pangtuwi/ESA.NET/issues/22)) | The burnt-volume clamp `if Vb > Vgas then Vb := Vgas` is an intentional safeguard. **Measured**: it fires on every step of the burn. The baseline trace carries `Vb = Vcyl` and `Vu = 0` for every crank angle from −20 to +30, so the integrated `y[1]` exceeds the cylinder volume from the first combustion step onwards and the clamp is what the two-zone volume split actually rests on | `Gasses2Z.pas` | Keep — the author marked it a safeguard, and the trace shows it load-bearing |
| B11 ([#23](https://github.com/pangtuwi/ESA.NET/issues/23)) | Fewer than three cycles is silently raised to three | `TFMain.Simulate` | Keep — a sensible floor |
| B12 ([#24](https://github.com/pangtuwi/ESA.NET/issues/24)) | RKF5 here is **fixed-step**: six Fehlberg stages, no error estimate, no adaptive control, despite the name. Do not "improve" it | `RKf5.pas` | Keep — but see B14 |
| B13 ([#25](https://github.com/pangtuwi/ESA.NET/issues/25)) | Delphi's 80-bit `Extended` becomes `double`. Unavoidable, and the first thing to suspect if phase 4 numbers drift — it matters most in the equilibrium model's Newton iteration | throughout | **Stuck** — .NET has no 80-bit float; mitigate only where measured |
| B14 ([#26](https://github.com/pangtuwi/ESA.NET/issues/26)) | **The RKF5 tableau carries a transposed digit.** `RKf5.pas:76` reads `854/4104` where Fehlberg published `845/4104`, so the fifth stage's coefficients sum to 455/456 instead of the 1 its node requires. That much is arithmetic, not interpretation, and the port reproduces the text. On an analytic test problem the method then converges at **first order, not fifth**. **But the baseline cannot tell the two apart.** Measured on a converged whole-cycle run at the reference crank-angle step: `854` gives a worst cylinder-pressure error of 0.416 per cent and an rms of 0.225; `845` gives 0.407 and 0.190. Both sit inside the A8 combustion bias, and the two solutions differ from each other by about 0.1 per cent — so the reference run would be reproduced roughly as well either way, and if anything `845` fits it marginally better. The earlier justification for reproducing this, that `data/baseline/` was produced by it, is therefore **not supported by measurement**; the entry rests on the source text alone. The practical claim that results are unusually step-sensitive is also unverified in the application, since order of convergence is an asymptotic property and the engine runs at a fixed one-degree step | `RKf5.pas:76` | **Fix** — restores the fifth-order convergence the name promises, and costs nothing measurable against the reference |
| B15 ([#27](https://github.com/pangtuwi/ESA.NET/issues/27)) | **RETRACTED - this was never a legacy defect.** The entry claimed `go2` builds the equilibrium constants from pressure in atmospheres while `Partial_dxd` differentiates them in pascals, making every `dC/dT` wrong by `sqrt(101325)`. `Partial_dxd`'s first parameter is indeed named `Pres`, but `go2` passes it the local `p`, already divided by 101325 (`Eqbm.pas:117, 137`). The units agree. The 318-fold error was in this port, which passed pascals: see A7. Kept as a numbered entry rather than removed, because the numbering is stable and because the misreading is worth remembering - a parameter named for the wrong quantity, and a "defect" confirmed by measuring the port against itself instead of against the reference data | `Eqbm.pas:117, 137, 284-296` | Moot - there is nothing here to fix |
| B16 ([#28](https://github.com/pangtuwi/ESA.NET/issues/28)) | **The analytic `dudp` is computed and then thrown away.** `ReturnProps` calls `Mixdudp` and immediately overwrites the result with a central difference of `u` across a 0.05 per cent pressure band, marked in the source with a bare `//#`. It costs two extra equilibrium solves, so one burnt `ReturnProps` runs the solver three times. `Get_dudp` does the same with a 0.5 per cent band — a tenfold different step for the same quantity — and there the whole analytic block is commented out | `GASPROPS.PAS:325-331, 612-618` | **Fix** — read as a workaround for B15, but B15 is retracted, so the reason for it is now unknown |
| B17 ([#29](https://github.com/pangtuwi/ESA.NET/issues/29)) | **`MixdRdp(R, M, dMdp)` passes the wrong argument.** Every sibling call passes `MolWt`, the mixture molecular weight of about 29. This one passes `M`, which Pascal resolves case-insensitively to the private field `m` — hydrogen atoms per fuel molecule, 17 for C7H17 | `GASPROPS.PAS:325` | Moot — feeds only the `dudp` that B16 overwrites |
| B18 ([#30](https://github.com/pangtuwi/ESA.NET/issues/30)) | **The unburnt mixture is a successive substitution, not a closed form.** `FuelAirResConcs` takes the products' molecular weight from the mixture array it is about to overwrite rather than from the residual. The first call therefore sees zeros, drives the residual mass fraction to one and yields pure residual; later calls converge gradually, agreeing to about nine significant figures rather than exactly | `GASPROPS.PAS:1070` | **Fix** — first-call transient means results depend on call history |
| B19 ([#31](https://github.com/pangtuwi/ESA.NET/issues/31)) | **`Get_gamma` deliberately ignores the composition derivatives.** The burnt branch computes `dMdT`, then passes a zero array and a zero `dMdT` to `MixdhdT` anyway, with the real arguments left commented out beside them. Gamma is therefore the frozen ratio. This is why gamma matches the baseline exactly while `Cp` from `ReturnProps` does not | `GASPROPS.PAS:509` | Keep — and it is what makes gamma trustworthy today |
| B20 ([#32](https://github.com/pangtuwi/ESA.NET/issues/32)) | **The species curve fits clamp rather than extrapolate**, and the lower guard was widened from 300 K to 260 K with the comment "to avoid error messages for now" | `GASPROPS.PAS:735, 772` | **Fix** — a silent clamp hides out-of-range states |
| B21 ([#33](https://github.com/pangtuwi/ESA.NET/issues/33)) | **`KEquilib`'s temperature clamp is dead code.** It calls `error(6, ...)` for a temperature outside 600 K to 4000 K, and `error` raises, so the clamp on the next line never runs. Out of range is fatal, not clamped | `Eqbm.pas:415-419` | **Fix** — the intent was clearly to clamp |
| B22 ([#34](https://github.com/pangtuwi/ESA.NET/issues/34)) | **`TEqbm.Error`'s counters are all unreachable.** Every branch raises before incrementing, so `errcount`, `err2count` and friends stay zero, the "report only once" guards never engage, and the `errcode` assignment at the end never executes. Every occurrence throws | `Eqbm.pas:453-510` | **Fix** — restores the intended suppression |
| B23 ([#35](https://github.com/pangtuwi/ESA.NET/issues/35)) | **The `x[8] := 99` sentinel is unreachable.** `go2` tests for it and `ReturnProps` branches on it, but the only path that sets it requires `error(2, ...)` to return, and it raises instead | `Eqbm.pas:206` | Moot — dead branch either way |
| B24 ([#36](https://github.com/pangtuwi/ESA.NET/issues/36)) | **The negative-mole-fraction guard and its clamps disagree.** The guard fires on `< 0` but each clamp inside tests `<= 0`, so an exact zero is only corrected when some other species happens to be negative | `Eqbm.pas:253-259` | **Fix** — an exact zero divides by zero downstream |
| B25 ([#37](https://github.com/pangtuwi/ESA.NET/issues/37)) | **`FuelThermo` carries a commented-out row** between rows 1 and 2, so the library fuel indices are only correct if that row stays commented. The port carries the active rows only | `GASPROPS.PAS:81` | Keep — documented, and the indices are right |
| B26 ([#38](https://github.com/pangtuwi/ESA.NET/issues/38)) | **`TValve.Lift` can read an uninitialised variable.** `CNew` is assigned only inside `if O > C`, but is read unconditionally on the next branch. Every sane cam has `O > C` after the angle conversion, so the path is not reached in practice. Not yet ported | `Valves.pas` | **Fix** — initialise it when step 4 ports this |
| B27 ([#39](https://github.com/pangtuwi/ESA.NET/issues/39)) | **`TGas2z.Tgas` is a function that writes.** It recomputes `xb` from the zone masses and stores it before weighting the two temperatures, so reading the gas temperature mutates the gas. The callers in `ICEngine2Z.pas` read it at points where nothing else has refreshed `xb`, so the write is load-bearing rather than incidental | `Gasses2Z.pas:43` | **Fix** — split the refresh out and call it deliberately |
| B28 ([#40](https://github.com/pangtuwi/ESA.NET/issues/40)) | **The zone-mass guard in `UpdateB` is always true.** `if (xb>0) and (xb<1)` sits immediately below the two clamps that pin `xb` into [0.01, 0.99], so the burnt and unburnt masses are assigned on every call and the guard tests nothing | `Gasses2Z.pas:64` | Moot — the condition it guards cannot be false |
| B29 ([#41](https://github.com/pangtuwi/ESA.NET/issues/41)) | **`UpdateGE` leaves both zone volumes at zero** while `Vgas` takes the full cylinder volume, so `Vu + Vb <> Vgas` throughout valve overlap. It also accepts `Vb1` and `Tb1` and ignores both, taking both zone temperatures from `Tu1`; `UpdateUB` and `UpdateBD` likewise accept and ignore `Vb1` | `Gasses2Z.pas:127` | Moot — nothing reads the zone volumes in this state, though a chart or export that did would show zeros |
| B30 ([#42](https://github.com/pangtuwi/ESA.NET/issues/42)) | **Gasses2Z declares a unit-level `var err : Integer`** in its implementation section, commented "GasProps Error". Every method that writes `err` resolves it to the class field of the same name, which is in a nearer scope, so the unit variable is never read or written | `Gasses2Z.pas:37` | Moot — dead declaration |
| B31 ([#43](https://github.com/pangtuwi/ESA.NET/issues/43)) | **`Pwr` is not `Power`, and it returns zero for a non-positive base.** `PNTWMath.pas`'s `Pwr` always evaluates `exp(j*ln(i))` and answers `0` when `i <= 0` rather than raising. `hWoshini` raises the characteristic velocity `w` to 0.8 through it, and `w` goes negative whenever cylinder pressure falls far enough below the motored pressure, at which point the heat-transfer coefficient collapses to exactly zero instead of following the correlation | `PNTWMath.pas:50-56` | **Fix** — a silent discontinuity in the middle of a correlation |
| B32 ([#44](https://github.com/pangtuwi/ESA.NET/issues/44)) | **`hWoshini` computes the motored volume from the solver's current crank angle, not the one it was called with.** `Vmot := VCyl(x)` reads the `TRKF` field `x`, while the derivative that called it is evaluating a trial state at `x1`. Five of RKF5's six stages have `x1 <> x`, so the motored pressure lags the state it is being compared against | `ICEngine2Z.pas:183` | **Fix** — pass the angle through; it is almost certainly a slip |
| B33 ([#45](https://github.com/pangtuwi/ESA.NET/issues/45)) | **The swept volume in `hWoshini` is wrong.** `Vswept := VCyl(Pi) * CR/(CR+1)`, where the swept volume of a cylinder whose volume at bottom dead centre is `VCyl(pi)` is `VCyl(pi) * (CR-1)/CR`. At CR 9.2 the two differ by a factor of 1.13, which scales the pressure-rise term of the Woschni velocity | `ICEngine2Z.pas:186` | **Fix** — but it is partly absorbed by the calibrated `CWoshini` |
| B34 ([#46](https://github.com/pangtuwi/ESA.NET/issues/46)) | **The two combustion heat-loss branches are asymmetric.** `dQbdtheta` scales the piston and head term by `Vb/Vgas` and drops the liner term entirely; `dQudtheta`'s matching branch scales the piston and head term by `Vu/Vgas` and keeps the liner term at full area. Defensible for a flame kernel near the head, but stated nowhere | `ICEngine2Z.pas:236-247, 264-268` | Keep — plausibly deliberate; confirm before changing |
| B35 ([#47](https://github.com/pangtuwi/ESA.NET/issues/47)) | **`dPdTheta1z` hard-codes gamma to 1.4.** Both the `VariableGamma` test and the `Cyl.Gamma` assignment sit immediately above the literal, commented out. This is the equation the single-zone model integrates and, because of B37, the one **two-zone** overlap uses too, so the engine's own computed gamma never reaches the pressure equation in either mode. See also C11 | `ICEngine2Z.pas:320-334` | **Fix** — honour the flag, or remove the flag |
| B36 ([#48](https://github.com/pangtuwi/ESA.NET/issues/48)) | **The two unburnt equations choose the transfer enthalpy on opposite sides of the update.** `dPdThetaUB` calls `Cyl.updateUB` and then reads `Cyl.hu`; `dTudThetaUB` reads `Cyl.hu` and then calls `updateUB`. When `MIn > 0` both take the plenum's enthalpy and agree; otherwise they use values one update apart | `ICEngine2Z.pas:340-377` | **Fix** — one of the two is wrong, and they cannot both be right |
| B37 ([#49](https://github.com/pangtuwi/ESA.NET/issues/49)) | **The four gas-exchange equations are unreachable.** `dVbdThetaGE`, `dPdThetaGE`, `dTbdThetaGE` and `dTudThetaGE` are referenced in exactly one place, `ICEngine2Z.pas:725-728`, and that block is commented out. Overlap installs `dPdTheta1z` and leaves the other three components at `Zero`, so valve overlap runs a single-zone constant-gamma pressure equation in a two-zone simulation. The free function `dQldTheta` is dead for the same reason. Ported anyway, so restoring the block is a one-line change | `ICEngine2Z.pas:515-633, 719-728` | **Fix** — the two-zone model has no gas-exchange equations in the loop |
| B38 ([#50](https://github.com/pangtuwi/ESA.NET/issues/50)) | **`PCylIVC`, `TCylIVC` and `VCylIVC` are not conditions at inlet valve closing.** `InitVars` sets them once, from the plenum pressure expression, the ambient temperature and the cylinder volume at the IVC crank angle, and nothing revises them for the rest of the run. Woschni's motored-pressure reference is therefore fixed at the initial guess rather than tracking each cycle | `ICEngine2Z.pas:1015-1017` | **Fix** — update them at IVC, which is what the names promise |
| B39 ([#51](https://github.com/pangtuwi/ESA.NET/issues/51)) | **`FEnergy` accumulates with degrees where radians are expected.** `FEnergy := FEnergy + ... * Cyl.dxdTheta(CA) * dx` passes `CA` in degrees to a function whose first act is `Theta := ThetaRad*180/pi`; every other caller passes `x`. The burn window is missed almost entirely. Harmless in practice: `FEnergy` is written in two places and read in none — the energy balance takes `QFuel` from `Cyl.fuel.Q * Cyl.Fuel.M` instead | `ICEngine2Z.pas:920` | Moot — the field is write-only, but fix the units if it is ever wired up |
| B40 ([#52](https://github.com/pangtuwi/ESA.NET/issues/52)) | **The exhaust state never assigns an equation set.** Every other state in `Run`'s two-zone switch sets `fn[1..4]`; `Exhaust` only zeroes `TotalMoutEV`, so exhaust integrates with the equations expansion left installed. That happens to be the right pair — `dPdThetaBD` and `dTbdThetaBD` — so it works, but by inheritance rather than intent | `ICEngine2Z.pas:721` | Keep — correct as written; worth a comment, not a change |
| B41 ([#53](https://github.com/pangtuwi/ESA.NET/issues/53)) | **`TProfile.Gety` returns -1 for an unusable profile.** A profile that failed to load or holds fewer than two points answers `-1` rather than raising, and `TValve.Lift` multiplies that by the maximum lift, so a missing cam file yields a negative lift and then a negative flow area rather than an error. `ProfileOk` is checked when the engine loads but not here | `Profiles.pas:114` | **Fix** — raise, or refuse to run with an unusable profile |
| B42 ([#54](https://github.com/pangtuwi/ESA.NET/issues/54)) | **`TValve.Lift` declares `ONew` and never uses it**, alongside the `CNew` of B26. Whatever symmetric handling of the opening angle was intended was never written | `Valves.pas:60` | Moot — dead local |
| B43 ([#55](https://github.com/pangtuwi/ESA.NET/issues/55)) | **Both stagnation branches in `MassFlow` compute the same expression.** `if Iut > 0 then cStag := sqrt(sqr(Ict) + (gam-1)/2*sqr(Iut)) else cStag := sqrt(sqr(Ict) + (gam-1)/2*sqr(abs(Iut)))` differ only by an `abs` inside a square. The same holds for `cCyl` just below. The alternatives the tests were meant to select - `cStag := cCyl` and `cCyl := cCyl` - are commented out on the lines between, so both conditionals are no-ops | `Manifolds.pas:428-434` | Keep — decide with the rest of the CFD in phase 4b |
| B44 ([#56](https://github.com/pangtuwi/ESA.NET/issues/56)) | **The atmosphere's `hin` is copied before it exists.** `InitVars` does `With Atm do ... hin := hu`, but no `ReturnProps` call is ever made on the atmosphere gas, so `hu` is still zero and `hin` copies that zero. `Cyl.hin` gets the same treatment. Only the unreachable gas-exchange equations of B37 read `hin`, so nothing observable depends on it | `ICEngine2Z.pas:957, 981` | Moot — feeds dead code only, but would bite if B37 were restored |
| B45 ([#57](https://github.com/pangtuwi/ESA.NET/issues/57)) | **`PMax` is initialised to 1e7 Pa, not zero.** `InitVars` sets the running peak-pressure maximum to 10,000,000, which no cycle exceeds, so it could never fall to a real value. Harmless only because the first state entry - always Compression, since cycles start at inlet valve closing - resets it to zero before any comparison happens | `ICEngine2Z.pas:1046` | Moot — overwritten before it is ever read |
| B46 ([#58](https://github.com/pangtuwi/ESA.NET/issues/58)) | **The mass-flow derivatives are stale throughout the closed period.** `Run`'s two-zone mass block has a case for Exhaust, Intake and Overlap only, so from inlet valve closing to exhaust valve opening `Cyl.dmindtheta` and `Cyl.dmoutdtheta` keep whatever gas exchange last left on them — the final intake step of this cycle, and the final exhaust step of the cycle **before**. The inlet one is harmless on any normal cam, because the valve is nearly shut at IVC and the value is already near zero; the exhaust one is not. On the baseline engine it carries 6.3e-5 kg/rad into expansion, where `dPdThetaBD` and `dTbdThetaBD` both use it as though gas were still leaving the cylinder. **Measured**: treating it as zero instead leaves `dTb/dtheta` 7 per cent short at the start of expansion and 13 per cent short by the end, because the stale term stays constant while `-P dV/dtheta` shrinks. Reproducing it brings expansion pressure from 0.25 per cent to 0.036 per cent. Note also that the two are assigned with opposite sign conventions — `dmindtheta := -Min/dx` against `dmoutdtheta := Mout/dx` | `ICEngine2Z.pas:822-844` | **Fix** — zero them on entry to compression; the physics wants no flow term in a closed cylinder |
| B47 ([#59](https://github.com/pangtuwi/ESA.NET/issues/59)) | **`Manifolds.pas` declares a third power routine.** Its own `Power` agrees with neither `DelphiMath.Power` (which routes integer exponents through repeated squaring) nor `Pwr` (which answers zero for a non-positive base): this one **raises** on a base at or below zero or at or above 1e20, and otherwise always takes `exp(b*ln(a))`. Its `if (a = 0) and (b > 0)` branch is unreachable, because `a = 0` has already raised two lines above. Three routines with the same name and three different contracts, in one program | `Manifolds.pas:86-99` | Keep — the raising behaviour is load-bearing as a guard; but never substitute one for another |
| B48 ([#60](https://github.com/pangtuwi/ESA.NET/issues/60)) | **`cThermo` can return an unassigned result.** When `gam*pres/dens` is not positive it tests density and pressure for being *negative* and raises on either, but a pressure of exactly zero with a positive density satisfies neither test and falls out of the `else` without assigning the function result. Delphi returns whatever was in the result register. The port returns zero, which is the value the expression would have taken | `Manifolds.pas:103-113` | **Fix** — raise, or return zero deliberately |
| B49 ([#61](https://github.com/pangtuwi/ESA.NET/issues/61)) | **The transitional and turbulent friction bands are the same expression.** `FricFact` splits Reynolds number 2300 to 4000 from 4000 to 1e5 and evaluates `0.0791/Re^0.25` in both, so the boundary between them does nothing. Only the laminar branch below 2300 and the high-Reynolds branch above 1e5 differ | `Manifolds.pas:126-146` | Keep — harmless, but the split is misleading to read |
| B50 ([#62](https://github.com/pangtuwi/ESA.NET/issues/62)) | **The manifold solver hard-codes two gammas and discards the computed ones.** Every routine in `Manifolds.pas` opens by overwriting the `gam` parameter it was handed: 1.3994 in the inlet routines, 1.3 in the exhaust ones. Meanwhile `InitVars` computes `GammaIn` and `GammaEx` from the equilibrium property model, and `GammaCyl` is declared beside them — all three are written and **never read by anything**. So the wave solver runs on two constants while the equilibrium gamma it went to the trouble of computing goes nowhere, and `Main_Prog`'s own speed-of-sound calls (including the one feeding `MassFlow`) use 1.3994 even for exhaust gas | `Manifolds.pas:40-42, 486-2537`, `ICEngine2Z.pas:1011-1012` | **Fix** — use the computed gammas, or delete them |
| B51 ([#63](https://github.com/pangtuwi/ESA.NET/issues/63)) | **The characteristic foot-location loops have no iteration cap.** `INTERNAL_PIPE`'s three inner `REPEAT` loops run until the foot settles to within 0.1 mm, with nothing to stop them if it never does. Only the outer convergence loop is capped, at 1000. A foot that oscillates between two positions more than 0.1 mm apart hangs the application with no message | `Manifolds.pas:1966-2113` | **Fix** — cap them, and report rather than hang |
| B52 ([#64](https://github.com/pangtuwi/ESA.NET/issues/64)) | **The outer iteration gives up silently.** After 1000 passes `INTERNAL_PIPE` sets `stop := 1` and takes whatever the last pass produced as the answer, with no flag, no message and no record. A grid point that never converged is indistinguishable from one that did on the first pass | `Manifolds.pas:2129` | **Fix** — at minimum count them, as the equilibrium solver's diagnostics do |
| B53 ([#65](https://github.com/pangtuwi/ESA.NET/issues/65)) | **The CFD raises a dialog mid-solve.** A negative pressure or density at any of the three characteristic feet calls `ShowMessage` from inside the iteration — a modal message box per grid point per time step — and then carries on with the negative value regardless. The port has no UI in Core and drops the dialog; the underlying condition still passes through to `cThermo`, which raises on a negative argument | `Manifolds.pas:1980, 2028, 2076` | **Fix** — surface it as a diagnostic, not a dialog |
| B54 ([#66](https://github.com/pangtuwi/ESA.NET/issues/66)) | **The two closed-valve routines build their interpolants differently.** Both fit a straight line through the wall point and its interior neighbour, but `INLET_VALVE_CLOSED` takes the velocity stored at the wall while `EXHAUST_VALVE_CLOSED` substitutes the imposed wall velocity (`u[3] := uSOLID`) for it. The two agree only while the stored wall velocity is already zero — which it is not on the step after a valve shuts, when the point still carries the flow it had while open. They also form the slope in opposite directions and anchor the intercept on opposite points; that part is algebraically the same line, and is reproduced only because floating point makes it a different one | `Manifolds.pas:2405-2416, 2553-2568` | **Fix** — pick one; the exhaust form, which imposes the wall condition consistently, looks like the intended one |
| B55 ([#67](https://github.com/pangtuwi/ESA.NET/issues/67)) | **The two open-end routines converge on different quantities.** `OUTFLOW_EXHAUST_PIPE` requires velocity, density **and** pressure to settle; `INFLOW_INLET_PIPE` requires only velocity and pressure, so it can stop while density is still moving. Density at that boundary feeds the mass flow into the cylinder through `IRt`, so this is not merely cosmetic | `Manifolds.pas:1690-1693, 1863-1867` | **Fix** — test all three at both ends |
| B56 ([#68](https://github.com/pangtuwi/ESA.NET/issues/68)) | **The two subsonic throat solvers are the same routine twice.** `InlSubSonicVelSolve` and `ExhSubSonicVelSolve` have the same residual, the same brackets, the same tolerance and the same iteration cap; they differ only in the text of their two error messages. The sonic pair is nearly as close - same residual again, with the upper bracket at 0.6 of the throat velocity for the inlet and 0.8 for the exhaust, which means a root between those two makes the inlet version raise where the exhaust version succeeds | `Manifolds.pas:215-311` | **Fix** — one routine, and decide deliberately what the bracket should be |
| B57 ([#69](https://github.com/pangtuwi/ESA.NET/issues/69)) | **A seated valve returns an undefined discharge coefficient.** `TValve.FlowCoeff` assigns its result 0 when the lift ratio is zero, then falls through to an unconditional `FlowCoeff := Coeff` where `Coeff` is only ever assigned in the other branch. A shut valve therefore yields whatever was in that local. The port returns 0, which is what the live branch was plainly meant to give | `Valves.pas:41-46` | **Fix** — return zero, or refuse to be called with a seated valve |
| B58 ([#70](https://github.com/pangtuwi/ESA.NET/issues/70)) | **`INLET_VALVE_REVERSE` writes to the current-time-level arrays.** Every other boundary routine writes its answer into the `...New` arrays; this one and its exhaust twin overwrite `uInlet[Q]`, `PInlet[Q]` and `RInlet[Q]` in place. They are called from inside the corresponding `..._VALVE_OPEN` routine, which then continues from the mutated values, so the current arrays are half-updated partway through a step | `Manifolds.pas:672-675, 1280-1283` | Keep — load-bearing for how the open routines are staged; document rather than change |
| B59 ([#71](https://github.com/pangtuwi/ESA.NET/issues/71)) | **The no-flow branch of the reverse-flow routines is unreachable on the first pass.** `if Pcyl <= Pt then Pt := 0.999999*Pcyl` sits immediately above `if Pcyl <= Pt then <no flow>`, so the assignment guarantees the test fails. On later passes the guard carries an extra `u[4] < IVR` condition, which leaves the branch reachable only when the pipe-end velocity is at or above the tuning constant. When it is bypassed the routine runs the subsonic branch at a pressure ratio pinned just above 1, giving a throat velocity near zero, which puts the root outside the velocity solver's fixed bracket and stops the run with a CFD error | `Manifolds.pas:553-567` | **Fix** — the branch is evidently meant to catch this case and cannot |
| B60 ([#72](https://github.com/pangtuwi/ESA.NET/issues/72)) | **`CritPress` is used once, inverted, and the plain isentropic ratio it replaced is commented out beside it.** `INLET_VALVE_OPEN` sets `PRcr := 1/CritPress(gam,Cd,Aratio)` with `PRcr := power((gam+1)/2,gam/(gam-1))` commented out on the next line — and that commented form is exactly what `INLET_VALVE_REVERSE` uses for its own `PRcr`. So the forward and reverse halves of the same valve switch between choked and subsonic on **different criteria**: the forward one accounts for the discharge coefficient and area ratio through a hundred-thousand-iteration root find, the reverse one does not | `Manifolds.pas:474, 736-737` | **Fix** — one criterion, chosen deliberately; the reverse routine looks like the one left behind |
| B61 ([#73](https://github.com/pangtuwi/ESA.NET/issues/73)) | **The exhaust reverse routine's no-flow branch does not stop its loop; the inlet's does.** When the cylinder turns out to be the higher of the two, `EXHAUST_VALVE_REVERSE` substitutes back to normal outward flow and sets `stop := 0`, falling through to the convergence test, where `INLET_VALVE_REVERSE`'s equivalent branch sets `stop := 1` and exits outright. The consequence is that the exhaust branch runs **twice** - once on the pass with no convergence test, once more before it converges - so its throat relaxation `Pt := 0.5*Pcyl + 0.5*Pt` is applied twice and the throat lands three quarters of the way to cylinder pressure rather than half. Pinned by a test | `Manifolds.pas:1191, 601` | **Fix** — decide whether the relaxation is meant to compound |
| B62 ([#74](https://github.com/pangtuwi/ESA.NET/issues/74)) | **The exhaust reverse routine tests for choking on a different pressure than it builds the throat from.** The throat state comes from the pipe's stagnation pressure `Pstag`, but the sonic-versus-subsonic test is `P[4]/Pcyl >= PRcr` on the **static** pipe-end pressure. The author left `//???????????` on that exact line | `Manifolds.pas:1195` | **Fix** — the author flagged it himself; `Pstag/Pcyl` is the consistent form |
| B63 ([#75](https://github.com/pangtuwi/ESA.NET/issues/75)) | **`EXHAUST_VALVE_OPEN` writes the same throat guard twice and then twice does nothing.** At `iter = 0` it nests `if u[4] >= 0 then if (Pt >= Pcyl) and (u[4] > EVF) then Pt := 0.999999*Pcyl`, and the very next statement applies `if (Pt >= Pcyl) and (u[4] > EVF) then Pt := 0.999999*Pcyl` unconditionally on every pass — which subsumes the nested form entirely. Two literal `Pt := Pt` statements follow. The port keeps only the unconditional guard | `Manifolds.pas:1404-1420` | Moot — redundant, not wrong; drop the dead lines |
| B64 ([#76](https://github.com/pangtuwi/ESA.NET/issues/76)) | **The four valve routines do not agree on which way to probe the secant.** The first probe of the Mach-matching iteration steps the pipe-end pressure by `1.001` in `INLET_VALVE_REVERSE`, `INLET_VALVE_OPEN` and `EXHAUST_VALVE_REVERSE`, but by `0.99999` in `EXHAUST_VALVE_OPEN` — downward, and by a step two hundred times smaller. A secant started from a different probe converges by a different path, so this is not cosmetic even where both converge | `Manifolds.pas:1490` | **Fix** — one probe, chosen deliberately |
| B65 ([#77](https://github.com/pangtuwi/ESA.NET/issues/77)) | **The pipe temperature arrays are written once and never updated.** `TempInlet` and `TempExhaust` are filled at `tStep = 0` — the whole inlet at plenum temperature, the whole exhaust at back temperature — and nothing writes to them again for the rest of the run. `Main_Prog` then reports `InletT := TempInlet[QI]` every step, so the value `TEngine2z.Run` uses to refresh the plenum gas is permanently the starting temperature. The wave solver does carry temperature implicitly, through density and speed of sound, so the pipes themselves are not wrong; it is the reported boundary temperature that is frozen. `ExhaustT` gets the same treatment and is then **never read by anything** | `Manifolds.pas:2747-2749, 3017-3018` | **Fix** — report the temperature the solver actually holds, `c^2/(gamma*287)`, and delete `ExhaustT` |
| B66 ([#78](https://github.com/pangtuwi/ESA.NET/issues/78)) | **The `.exh` temperature column is labelled Celsius and used as kelvin.** The file header reads `SPEED TEMP[C] P[kPa]` and `TExhaustPandT.Temp` returns the value with no conversion, so both `InitVars` (`Exh.Tb`) and `Main_Prog` (`Tback`) treat it as an absolute temperature. On the baseline engine that is 820 at 4000 rpm: read as kelvin it is 547 C, read as the column says it is 820 C. Which the author intended cannot be settled from the source — but the whole-cycle comparison can settle it, and it comes out inside 0.33 per cent using the value raw, so the original's behaviour is reproduced and the label is what is wrong | `ExhBackPandT.pas:88`, `A2China.exh` | Keep — the reference run agrees with the raw reading; fix the column heading instead |
| B67 ([#79](https://github.com/pangtuwi/ESA.NET/issues/79)) | **The PVT export scales its last column with a stale loop counter.** `TCAList.SendToFile` writes the first 27 columns as `value[i]*k[i]` inside a `for i := 1 to NoVals-1` loop, then writes the 28th as `value[NoVals]*k[i]` — reusing `i` after the loop, where Pascal leaves it undefined. `k[27]` is 1000 and `k[28]` is 1, so the difference is not academic: heat loss would be reported a thousand times too large. The reference file shows it in joules, so the compiler evidently left `i` at 28. The port writes `k[NoVals]`, which is both the evident intent and what reproduces the file | `CAList2z.pas:156` | **Fix** — index the last column properly rather than relying on a value the language does not define |
| B68 ([#80](https://github.com/pangtuwi/ESA.NET/issues/80)) | **`LoadGrid` parses each `.msr` line backwards.** It walks from the end of the string splitting on commas and fills the grid from its rightmost column inwards, stopping when either runs out. Two consequences: the row number `SaveGrid` writes at the front of every line is never read back, and a line with too few fields fills the **right-hand** columns and leaves the left ones at their default instead of the other way round — which is what made C13 silent | `MultiRun.pas:156-200` | **Fixed** — `MultiRunGridStore.Read` parses forwards, discards the leading row number, fills from the left and refuses a line that is not in the format. Nothing in `data/baseline/` depends on it: the reference run is a single engine and reads no `.msr` at all, so this is the one section B entry that could be fixed without re-baselining. See C13 |
| B69 ([#81](https://github.com/pangtuwi/ESA.NET/issues/81)) | **The reported exhaust back pressure subtracts a hard-coded atmosphere.** `WriteRunFile` writes `Manifold.ExhBack.Pres(Nrpm)/1e3-101.325` to undo the `+ PAtm` that `TExhaustPandT.Pres` applied — but `Pres` adds the engine's own `Atm.PGas`, which the operator can set. Run an engine at any other ambient pressure and the `BackP` column is wrong by the difference, while everything downstream of the real `Pres` value stays right | `Main.pas:1234` | **Fix** — subtract the same atmospheric pressure that was added |
| B70 ([#82](https://github.com/pangtuwi/ESA.NET/issues/82)) | **The charts are drawn at two different point densities.** A static redraw walks every **even** crank angle (`RedrawGraphs`, 360 points of the 720), while a live run updates every **five** degrees (`Simulate`, roughly 144). So the same chart looks smoother after a run finishes than it did while the run was in progress, and neither shows every point that was computed. The port takes the stride as a parameter and defaults to the redraw's two | `Main.pas:329, 551` | Keep — a deliberate speed trade for live plotting, though the redraw could simply use every point |
| B71 ([#83](https://github.com/pangtuwi/ESA.NET/issues/83)) | **Unburnt hydrocarbons are recorded as a literal zero.** `UpdateCApoint` sets the HC column to `0` outright, while CO, NO and CO2 beside it come from the equilibrium solver. The column exists in the PVT grid, in the text export and in the emissions the results carry, and nothing anywhere computes a value for it — which the reference trace confirms, HC being 0.00 at every one of its 720 crank angles. A twelve-species equilibrium model has no unburnt fuel to report, so this is a placeholder for something never implemented rather than a calculation gone wrong | `CAList2z.pas:131` | Keep — but the column is misleading as it stands; either compute it or label it |
| B72 ([#84](https://github.com/pangtuwi/ESA.NET/issues/84)) | **Multi-run valve timings bypass the conversion the edit form applies.** Columns 7 to 10 are assigned straight onto `IV.O`, `IV.C`, `EV.O` and `EV.C`, where `Edit.pas:448-451` converts the same four quantities as `360 - IVO`, `-180 + IVC`, `180 - EVO` and `-360 + EVC`. A row saying IVO 19 — the value an operator would read off the Cams tab — therefore sets the opening angle to 19 rather than 341, and the engine runs on timing nothing asked for | `Main.pas:1334-1337` | **Fix** — convert as the form does |
| B73 ([#85](https://github.com/pangtuwi/ESA.NET/issues/85)) | **Overriding the inlet valve lift also overwrites the exhaust lift.** `if GetMultiRunVar(11,...) then begin IV.MaxLift := IV.MaxLift/1000; EV.MaxLift := IV.MaxLift; end` — the second assignment is inside the same block, so setting column 11 silently replaces the exhaust cam's lift with the inlet's | `Main.pas:1338-1343` | **Fix** — almost certainly a copy-paste slip |
| B74 ([#86](https://github.com/pangtuwi/ESA.NET/issues/86)) | **The multi-run exhaust lift is not converted to metres.** Column 11 divides the inlet lift by a thousand; column 12 assigns the exhaust lift raw. Set both and the inlet is in metres while the exhaust is in millimetres — a thousandfold difference between two valves on the same engine | `Main.pas:1344` | **Fix** — divide, as column 11 does |

## C. Legacy behaviour that catches out the operator

Not port defects, but things that make the original behave in ways that look like
breakage.

**C1 ([#87](https://github.com/pangtuwi/ESA.NET/issues/87)) — Manifold output needs three conditions, not one.** **Fixed.** The gate is
`(CA = 359) and (tStep = NoCycles-1) and (DataWrite = TRUE)`
(`Manifolds.pas:3022`). Ticking *Save Manifold Data* only satisfies the third.
`NoCycles` is `Engine2z.NCycles`, the **requested** count, fixed at
`Main.pas:895` and never updated by convergence. So the files appear only if the
run reaches the final requested cycle — and a run that converges early exits
before it. Requesting 6 cycles produced nothing; requesting 4 produced all nine.

**Fixed** in
[`24ccb0d`](https://github.com/pangtuwi/ESA.NET/commit/24ccb0d212b09e93f6185de059c89baa38fc6af0),
and so not reproduced: ticking the box is the whole gate in the port. `SimulationRunner`
takes an `IManifoldRecorder`, wraps it in `ManifoldCaptureWindow` — the same crank-angle
window the original writes, so the files hold the same rows — and resets it at each cycle
boundary, which leaves it holding the last cycle *actually* run rather than the last one
requested. A converged run therefore still produces all nine files, in one pass and with
no second run to find out how many cycles there will be. `SimulationResult` reports
`ManifoldDataCaptured` so the caller knows there is something to write. Pinned by
`ManifoldOutputGateTests`.

The files land beside the `.eng` rather than in the working directory, which also settles
C4 for the single-point run; with no engine path loaded there is nowhere better than the
working directory, which is what the original always did. C2 and C3 were dealt with
separately: the note that first stood here, that they "do not arise", was wrong — the port
read the flag off the engine as it says, but nothing refreshed the engine when the editor
changed it.

**A multi-run sweep still writes nothing** — see A12, which is where that now lives.

**C2 ([#88](https://github.com/pangtuwi/ESA.NET/issues/88)) — The checkbox is read off the Edit form, not the engine.** **Fixed.**
`TFMain.Simulate` tests `FEdit.CBSaveManfData.Checked`, so the Edit window must
have been opened at least once in the session for it to reflect the loaded engine.

The port reads `Engine.Manifold.SaveManifoldData`, which `EngineLoader` applies from the
`.eng` whether or not the editor is ever opened. **But that was not enough on its own, and
an earlier note here claiming C2 and C3 "do not arise" was wrong.** `EngineLoadResult`
carries an `Engine` and an `EngineDefinition` that are separate snapshots taken at load
time, and the editor writes only to the definition — so ticking the box, pressing OK and
running used the value the file was opened with. Not the original's defect, but the same
symptom and arguably worse: in the original the form at least fed the run, where here the
edit reached nothing. It was never confined to the checkbox either; every field the editor
writes went the same way — that broader defect is A13, which is where it is tracked.

**Fixed** in
[`902dc97`](https://github.com/pangtuwi/ESA.NET/commit/902dc97f0bfacc1bc607f4db2d55eebe9dba4206).
`IEngineLoader.Rebuild` now re-derives the engine from a definition already in hand, and
the shell calls it whenever the editor raises `Applied`, so OK puts the operator's values
where the simulation reads them — which is what `Edit.pas`'s OK handler did by assigning
onto `Engine2z` directly. Rebuilding also re-resolves the side files, so a renamed cam or
manifold file is picked up and the status line's problem count refreshes. The definition
instance is passed back out unchanged, so the open editor's reference stays live across the
rebuild. Pinned by `EngineEditRefreshTests`, three of whose five tests fail if the rebuild
is removed.

**C3 ([#89](https://github.com/pangtuwi/ESA.NET/issues/89)) — And it latches.** **Fixed.** That same line only ever assigns `TRUE`, so once set it
stays set until the application restarts. Unticking does not turn output off.

Not reproduced, and settled by the same change as C2
([`902dc97`](https://github.com/pangtuwi/ESA.NET/commit/902dc97f0bfacc1bc607f4db2d55eebe9dba4206)).
The port holds the flag on the engine rather than in form state, so there is nothing to
latch: unticking and pressing OK writes `false` through to the engine the same way ticking
writes `true`, and `UntickingItTurnsOutputOffAgain` pins the round trip.

**C4 ([#90](https://github.com/pangtuwi/ESA.NET/issues/90)) — Manifold files land in the working directory**, not beside the `.eng`.
They are opened with bare relative names. Look where `SimulDat.txt` appears.

**C5 ([#91](https://github.com/pangtuwi/ESA.NET/issues/91)) — "4 / 4" does not mean four cycles ran.** The convergence test sits at the
top of the loop body, before the `repeat` that runs the cycle, and exits outright.
Stopping at `i = 4` means cycles 1 to 3 ran and cycle 4 never did.

**C6 ([#92](https://github.com/pangtuwi/ESA.NET/issues/92)) — The performance data file accumulates.** Rows are appended per run, so one
file can hold several unrelated runs. `Example1/Simuldat.txt` has two 5000 rpm
rows with different values.

**C7 ([#93](https://github.com/pangtuwi/ESA.NET/issues/93)) — Switching the gas-flow chart to velocities after a run shows stale data.**
The panel title changes but the plot does not: the mode is read on refresh, and
the refresh timer has stopped. The axis still reads Pressure [bar].

**C8 ([#94](https://github.com/pangtuwi/ESA.NET/issues/94)) — `Ctrl+Q` is assigned to both Exit and QuickRun.** In Avalonia
`MenuItem.InputGesture` is display-only, so nothing clashes yet. Resolve it when
those commands get real behaviour.

**C9 ([#95](https://github.com/pangtuwi/ESA.NET/issues/95)) — `BOKClick` swallows `EConvertError` and tells the user nothing**, so one
bad numeric field silently discards the whole edit. The port does not reproduce
this: every field validates and OK is disabled while anything is invalid.

**C10 ([#96](https://github.com/pangtuwi/ESA.NET/issues/96)) — `IniValues.SaveIniValues` is empty**, so the original never wrote
`ESA.ini` back. The port implements it properly.

**C11 ([#97](https://github.com/pangtuwi/ESA.NET/issues/97)) — The *Variable Gamma* checkbox does nothing.** It is read from the
`.eng` file, written back, enabled and disabled with the zone count, and assigned
to `Engine2z.VariableGamma` — and then never tested. The only occurrence of the
field outside the edit form is inside a comment (`ICEngine2Z.pas:324`), directly
above the `LocalGamma := 1.4` that replaced it. An operator who unticks it sees no
change in the results, because there was nothing to turn off. See B35.

**C12 ([#98](https://github.com/pangtuwi/ESA.NET/issues/98)) — `InitialTb` calls `Halt` when it fails to converge.** A thousand
iterations of the isenthalpic estimate without settling terminates the process
outright, losing whatever the operator had loaded, after a single message box
(`ICEngine2Z.pas:296-300`). The port throws instead, the same trade
`TWallTemps.Load` gets.

**C13 ([#99](https://github.com/pangtuwi/ESA.NET/issues/99)) — 43 of the 49 shipped `.msr` files loaded with their columns shifted.** **Fixed.**
The multi-run grid has fourteen editable columns, so `SaveGrid` writes fifteen
comma-separated fields per line. Only six of the shipped files are in that format;
the other forty-three carry fourteen fields, a column short, from before **Burn
Angle** was added. Because `LoadGrid` fills from the right (B68) it does not notice:
every value lands one column over, and the row number itself ends up in the **Speed**
column. The giveaway is a speed column counting 1, 2, 3 up the grid. Loading one of
these and pressing OK would sweep an engine from 1 rpm upwards.

**Fixed** in [#2](https://github.com/pangtuwi/ESA.NET/pull/2), merged as
[`ae00484`](https://github.com/pangtuwi/ESA.NET/commit/ae00484c7e47ec3b801369356acdb426b307600c);
the loader no longer reproduces it. The doubt was whether a short line means "a
column was added since" or "the first column is deliberately blank", and the data
settles it.
`SaveGrid` has always written the row number first, and twenty-eight of the
forty-three short files — every one that overrides anything past Iters — carry an
inlet manifold `.maf` name in their fourth field, which is `IManfFile` under
left-alignment and `EManfFile` under the original's fill from the right. No short
file uses any field beyond the fourth, so nothing contradicts it. B68's forward
parse therefore discards the leading row number, fills from the left and leaves the
trailing column unset, and all forty-three now load the speeds they were written
with. Nothing about the simulation moves: no reference run reads a `.msr`.

`Write` still emits the current fifteen-field format, so a short file opened and
saved comes back a cell longer — the alternative being to silently drop any Burn
Angle the operator had typed. The editor says so when it loads one, rather than
letting the rewrite be a surprise.


**C14 ([#100](https://github.com/pangtuwi/ESA.NET/issues/100)) — a blank row in the multi-run grid silently discards everything below it.**
`BOkClick` counts runs by walking the Speed column until it finds a dash, so the
runs have to start at the first row and be contiguous (`MultiRun.pas:93-104`).
Fill in rows 1, 2, 4 and 5 and only the first two are swept; nothing says the other
two were dropped, and the results screen shows a two-point curve as though that is
what was asked for. Neither the grid nor the status bar mentions it.

The counting rule is reproduced — it is what decides which rows run — but the
editor now reports the consequence, saying how many further rows are filled in and
will not run.


**C15 ([#122](https://github.com/pangtuwi/ESA.NET/issues/122)) — The simulation dialog will not close while the speed is out of range, Cancel included.**
`TFSimulateOptions.FormClose` sets `Action := caNone` and rewrites the edit box to the
nearer limit whenever engine speed leaves 1250 to 7000 rev/min (`FormSimul.pas:66-88`).
`FormClose` runs on **any** close, whichever button was pressed, so an operator who types
8000 and then thinks better of the whole thing cannot leave by pressing Cancel either. The
window just stays put with no message. A second press does work — the box now reads 7000,
which passes — so it is a wasted press rather than a permanent dead end, but nothing says
so and the typed value has been silently replaced. Both limits are hard-coded in the
handler: they are not in `ESA.ini`, and the dialog names them nowhere.

Not reproduced. The port clamps on Run and names the range in the dialog as soon as the
typed value leaves it, so the limit is visible before anything is pressed and Cancel always
cancels. Pinned by `SimulateOptionsTests`.

Note a second trap in the same handler, left unrecorded for now: the whole body sits in a
`try ... Except end` whose except block is **empty**, and `NoCycles`, `MassBalance` and
`Nrpm` are assigned in that order — so a non-numeric *Total Cycles* throws on the first line,
the other two are never assigned, and the run uses the previous values for every field. The
clamp does not run either, being below the assignment that threw. It deserves its own entry
if anyone wants to reproduce or fix it.


## D. Errors and gaps in SPEC.md

`SPEC.md` is the phase 1 reverse-engineering output. Where it disagrees with the
source or the data, the source and the data win.

| # | Section | Problem |
|---|---|---|
| D1 ([#101](https://github.com/pangtuwi/ESA.NET/issues/101)) | §3 | `.exh` columns are given as RPM, pressure, temperature. They are RPM, **temperature**, **pressure** — the loader reads `ATExh` before `APExh` and the shipped header row reads `SPEED / TEMP[C] / P[kPa]` |
| D2 ([#102](https://github.com/pangtuwi/ESA.NET/issues/102)) | §3 | The `.eng` section list omits the older `[InManifold]` / `[ExManifold]` schema used by five `Example1` engines |
| D3 ([#103](https://github.com/pangtuwi/ESA.NET/issues/103)) | §3 | Does not state that `.eng` keys must match **case-insensitively**. `Edit.pas` reads `CdIvIn`; every shipped file writes `CdIVIn` |
| D4 ([#104](https://github.com/pangtuwi/ESA.NET/issues/104)) | §3 | Describes `.spk`, `.cwt` and `.exh` as simple column pairs. They have a row-count line and a discarded heading line first |
| D5 ([#105](https://github.com/pangtuwi/ESA.NET/issues/105)) | §3 | The quoted `ESA.ini` does not match the shipped file: `CAEEng.err` not `ESA2z1z.err`, `MassBalance=0.5` not `1`, and no trailing newline |
| D6 ([#106](https://github.com/pangtuwi/ESA.NET/issues/106)) | §2 | Calls `TCAPoint` a record; it is a Delphi `class` |
| D7 ([#107](https://github.com/pangtuwi/ESA.NET/issues/107)) | §1 | Says to reproduce the main menu but never lists it, and treats `Main.dfm` as readable when it is **binary** DFM needing string extraction |
| D8 ([#108](https://github.com/pangtuwi/ESA.NET/issues/108)) | §4 | Recommends WPF and OxyPlot. Superseded by `TECHSTACK.md`, which requires Avalonia and ScottPlot |
| D9 ([#109](https://github.com/pangtuwi/ESA.NET/issues/109)) | §6 | Says tolerances "must be measured from legacy reference runs" before acceptance tests are written. Now satisfied — see `BASELINE.md` |

## E. Dead data

Present in the repository, referenced by nothing.

| # | What | Evidence |
|---|---|---|
| E1 ([#110](https://github.com/pangtuwi/ESA.NET/issues/110)) | `Example1/Inlet.grd`, `Example1/Exhaust.grd` | No Delphi unit references `.grd`. They hold Pascal fragments, not data |
| E2 ([#111](https://github.com/pangtuwi/ESA.NET/issues/111)) | `.eng` keys `IVMinA` and `EVMinA` | Never read by any Delphi unit. Present in `A2China.eng` and the older `Example1` engines; round-tripped, never used |
| E3 ([#112](https://github.com/pangtuwi/ESA.NET/issues/112)) | `.msr` files | Saved **multi-run grids**, i.e. inputs, not results. Easy to mistake for recorded output |
| E4 ([#113](https://github.com/pangtuwi/ESA.NET/issues/113)) | `Example1/Pressure.dat` | 721 rows of 21 columns; almost certainly a renamed `InlPress.m` |

## F. Open questions

| # | Question | Why it matters |
|---|---|---|
| F1 ([#114](https://github.com/pangtuwi/ESA.NET/issues/114)) | The manifold files and `A2China.txt` come from **adjacent** cycles — which is which? The closed period agrees to 0.0001 bar, gas exchange diverges up to 0.07 bar | Phase 4 needs to know which cycle it is reproducing when comparing gas-exchange data |
| F2 ([#115](https://github.com/pangtuwi/ESA.NET/issues/115)) | `A2China.eng` carries both `PlenumP=98.0` (kPa) and `FPlenumP=(99000)` (Pa). The Inlet tab shows `(99000)`, so `FPlenumP` wins — but 98 kPa and 99 kPa are not the same number, so this is not a stale unit conversion | The plenum pressure feeds the inlet boundary condition; the wrong one shifts every intake result |
| F3 ([#116](https://github.com/pangtuwi/ESA.NET/issues/116)) | Is *No Cylinders* genuinely read-only in the original, or just rendered flat? | See A5 |
| F4 ([#117](https://github.com/pangtuwi/ESA.NET/issues/117)) | What tolerance should each compared quantity carry? | Nothing is a pass/fail gate until this is agreed — see `BASELINE.md` |
| F5 ([#118](https://github.com/pangtuwi/ESA.NET/issues/118)) | **The wave solver runs near a Courant number of 1, and nothing checks it.** Measured on the baseline inlet at one crank degree: a characteristic foot travels 14.4 mm against a grid spacing of 19.95 mm, so the Courant number is 0.72. The grid-size expressions in the `.eng` file are evidently tuned to land there, since they scale the point count by both pipe length and engine speed. But nothing in `Manifolds.pas` computes the Courant number or asserts anything about it, and three inputs move it independently: engine speed, pipe length, and the crank-angle step `dCA` that `InitVars` hard-codes to 1. Above 1 the piecewise-linear interpolant in `INTERNAL_PIPE` is extrapolating past its neighbouring point rather than interpolating between it, which degrades quietly rather than failing. A user-supplied grid expression tuned for a different pipe, or a different `dCA`, could cross that line with no warning | Phase 4b accuracy depends on it, and B14 already makes results unusually step-sensitive from the integrator side. Worth computing and reporting the Courant number per run, even if the original never did |

---

## Where the detail lives

- `BASELINE.md` — the reference run, the derivation chain from trace to
  headline numbers, and the manifold output mechanics (C1 to C5).
- `CLAUDE.md` — the port caveats that affect day-to-day work (most of B).
- `task-phase4.md` — what phase 4 must do about all of it.
- `tests/App.Tests/BaselineDataTests.cs` — B1, B2, B7, B8, B9 and the grid-size
  chain pinned against the original's own output.
- `tests/App.Tests/EquilibriumSolverTests.cs` — the derivatives, checked by comparing the
  analytic derivative against a finite difference of the solver itself.
- `tests/App.Tests/GasPropertyModelTests.cs` — gamma against the baseline trace in
  both the burnt and unburnt branches, and the equilibrium specific heat.
- `tests/App.Tests/ManifoldNumericsTests.cs` — B47 to B49, the three power routines
  kept apart and the friction bands.
- `tests/App.Tests/CharacteristicSolverTests.cs` — B50, and the invariants the
  interior-point update must satisfy: a uniform stagnant pipe preserved exactly over
  fifty steps, and the measured Courant number of F5.
- `tests/App.Tests/Rkf5IntegratorTests.cs` — B12 and B14, including a measured
  order-of-convergence test that fails loudly if the transposed coefficient is
  ever "corrected".
