# Known issues

Everything found so far that is wrong, surprising, or deliberately reproduced,
in one place. Findings were previously scattered across `CLAUDE.md`,
`BASELINE.md`, the phase task files and code comments, which made them easy to
miss.

Sections A and F are work now. B, C, D and E are reference during the port: they
exist so nobody "fixes" something that is load-bearing, or rediscovers it the
expensive way.

**After the port reproduces the original end to end, section B becomes the
improvement backlog.** Each entry carries a verdict — Fix, Keep, Moot or Stuck —
so the list can be worked through without re-deriving why each thing is there.

The ones that change results, roughly in order of how much:

| Entry | What fixing it buys |
|---|---|
| B14 | The integrator converges at first order instead of fifth; fixing it makes results far less sensitive to the crank-angle step |
| B1 | Fuel flow, SFC and thermal efficiency are wrong by `4 / NCyl` for any engine that is not a four-cylinder |
| B6 | A hard-coded line silently overrides the user's `IVFFn` at or below 1000 rpm |
| B18 | The unburnt mixture depends on call history, so the first evaluation of a state differs from later ones |
| B4, B5 | A lookup that falls to zero past its table, and a bilinear interpolation with its axes crossed |

Fixing any of these will move the numbers away from `data/baseline/`, so expect to
re-baseline against a fresh reference run, or to keep both behaviours behind a
switch.

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
| A6 | **`Engine` carries the `.eng` file's units, not SI.** Bore, stroke, conrod length, valve lift and valve diameter are millimetres; atmospheric and plenum pressures are kilopascals; ambient temperature is Celsius; valve timings are degrees before or after a dead centre rather than signed crank angles. Delphi converted all of these on the way out of the edit form (`Edit.pas:412-419, 448-466`), so its engine object was SI throughout and the simulation could assume it. The port's loader is persistence-shaped and stores what the file says, so every conversion happens at the simulation boundary instead: `CylinderGeometry.FromEngine`, `ValveMotion.Inlet`/`Exhaust`, `CrankAngleStateMap.FromEngine` and `CycleSolver.Initialise`. Each is pinned by a test. Consolidating them behind one runtime view of the engine would be tidier and is worth doing before phase 5 adds more callers | Open, by design for now |
| A7 | **The burnt-zone temperature derivative was ~100x too large.** `EquilibriumSolver.Solve` computed the atmospheres value `p` for the equilibrium constants, then passed the raw pascals `pressure` to `PartialDerivatives`, where `go2` passes `p`. Every `dC/dT` came out `sqrt(101325)` too large, and that reached `dudT` through `MixdhdT` and `MixdRdT`, leaving `dudTb` near 172,000 J/(kg K) against a sound 1,660. It sits in the denominator of the burnt temperature equation, so `dTb/dtheta` was 99 per cent short. **Fixed** by passing `p`. The equilibrium temperature derivatives now agree with a finite difference of the solver to seven significant figures, where they were 250 to 380 times out; per-crank-angle cylinder pressure improved from 0.77 to 0.25 per cent through expansion and from 1.26 to 0.72 per cent through combustion. Found only because the baseline trace disagreed - `Gamma` matched throughout, because `Get_gamma` passes a zero derivative array and never touches this path | **Fixed** |
| A8 | **Cylinder pressure through combustion carries a smooth bias**, -0.72 per cent at the spark, crossing zero around 5 degrees before top dead centre and settling near +0.2 per cent for the rest of the burn. Not a transient: the one-step-ahead harness reloads the reference state before every step, so nothing propagates and this is a systematic error in the burning equations. Ruled out so far - the burnt fraction and both zone masses match the trace exactly at every step, 0.01 clamp included; the `dudp` finite-difference band matches the original digit for digit; and `dudT` is sound since A7. The sign change tracks the burnt fraction, so the unburnt-weighted and burnt-weighted halves of `dPdThetaB` appear to be in error in opposite directions. Compression (0.081 per cent) and expansion (0.036 per cent) are unaffected | Open, small |

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
| B1 | `mf`, `ThEff` and therefore `SFC` hard-code four cylinders: the factor is `2 * Nrpm` where the physics needs `NCyl * Nrpm / 2`. All 71 shipped engines are `NoCyls=4`, so it was never exercised. Wrong by `4 / NCyl` for any other engine | `TEngine2z.Performance` | **Fix** — silently wrong for any engine that is not a four-cylinder |
| B2 | `FMEP := TFMEP - PMEP` then `BMEP := IMEP - PMEP - FMEP`, so PMEP cancels and BMEP is really `IMEP - TFMEP`. The reported FMEP is the intermediate, not the friction correlation | `TEngine2z.Performance` | Keep — algebraically equivalent, only the reported FMEP is unconventional |
| B3 | The exhaust valve's discharge tables are crossed: `EV.CdForward` comes from `CdEvOut`, `EV.CdReverse` from `CdEvIn`, because forward flow through an exhaust valve is outward. The inlet valve is wired the obvious way | `ICEngine2Z.pas:998-1005` | Keep — correct physics, just surprising |
| B4 | `TAManf.GetValue` returns **zero** past the end of the area table, not the last area. A cliff, not a clamp | `FManfA.pas` | **Fix** — a discontinuity in a lookup is almost certainly unintended |
| B5 | `TCdValve.GetValue` passes its y arguments in the reverse order to its x ones | `IPolTab.pas` | **Fix** — reversed arguments look like a slip |
| B6 | The `IVFFn` expression is ignored at or below 1000 rpm, replaced by a hard-coded line. Not yet ported; belongs with the solver | `Manifolds.pas:2739-2742` | **Fix** — a hard-coded line silently overriding user data |
| B7 | `^` is **left**-associative, so `2^3^2` is 64. Unary minus binds looser, so `-2^2` is −4. A sign is legal only at the start of an expression or a bracket, so `3*-2` is an error | `ADCALC.PAS:2555-2620` | Keep — matches the shipped expressions, which were written for it |
| B8 | `^` must not use `Math.Pow`: Delphi routes integer exponents through `IntPower`'s repeated squaring. The paths differ in the last bits, and a grid size is the `Round` of an expression in `N^6` | `DelphiMath.Power` | Keep — required for bit-agreement with the original |
| B9 | Delphi `Round` is round-half-to-even. `Math.Round` matches; a cast or `Floor(x + 0.5)` would not | throughout | Keep — correct rounding |
| B10 | The burnt-volume clamp `if Vb > Vgas then Vb := Vgas` is an intentional safeguard. **Measured**: it fires on every step of the burn. The baseline trace carries `Vb = Vcyl` and `Vu = 0` for every crank angle from −20 to +30, so the integrated `y[1]` exceeds the cylinder volume from the first combustion step onwards and the clamp is what the two-zone volume split actually rests on | `Gasses2Z.pas` | Keep — the author marked it a safeguard, and the trace shows it load-bearing |
| B11 | Fewer than three cycles is silently raised to three | `TFMain.Simulate` | Keep — a sensible floor |
| B12 | RKF5 here is **fixed-step**: six Fehlberg stages, no error estimate, no adaptive control, despite the name. Do not "improve" it | `RKf5.pas` | Keep — but see B14 |
| B13 | Delphi's 80-bit `Extended` becomes `double`. Unavoidable, and the first thing to suspect if phase 4 numbers drift — it matters most in the equilibrium model's Newton iteration | throughout | **Stuck** — .NET has no 80-bit float; mitigate only where measured |
| B14 | **The RKF5 tableau carries a transposed digit.** `RKf5.pas:76` reads `854/4104` where Fehlberg published `845/4104`, so the fifth stage's coefficients sum to 455/456 instead of the 1 its node requires. Measured effect: the method converges at **first order, not fifth** — halving the step halves the error rather than dividing it by 32. At 40 steps over a unit interval it is seven orders of magnitude less accurate than the method it claims to be. ESA offers it to the user as "Runga Kutte Felberg (accurate)" against "Euler (fast)" | `RKf5.pas:76` | **Fix** — restores the fifth-order convergence the name promises |
| B15 | **RETRACTED - this was never a legacy defect.** The entry claimed `go2` builds the equilibrium constants from pressure in atmospheres while `Partial_dxd` differentiates them in pascals, making every `dC/dT` wrong by `sqrt(101325)`. `Partial_dxd`'s first parameter is indeed named `Pres`, but `go2` passes it the local `p`, already divided by 101325 (`Eqbm.pas:117, 137`). The units agree. The 318-fold error was in this port, which passed pascals: see A7. Kept as a numbered entry rather than removed, because the numbering is stable and because the misreading is worth remembering - a parameter named for the wrong quantity, and a "defect" confirmed by measuring the port against itself instead of against the reference data | `Eqbm.pas:117, 137, 284-296` | Moot - there is nothing here to fix |
| B16 | **The analytic `dudp` is computed and then thrown away.** `ReturnProps` calls `Mixdudp` and immediately overwrites the result with a central difference of `u` across a 0.05 per cent pressure band, marked in the source with a bare `//#`. It costs two extra equilibrium solves, so one burnt `ReturnProps` runs the solver three times. `Get_dudp` does the same with a 0.5 per cent band — a tenfold different step for the same quantity — and there the whole analytic block is commented out | `GASPROPS.PAS:325-331, 612-618` | **Fix** — read as a workaround for B15, but B15 is retracted, so the reason for it is now unknown |
| B17 | **`MixdRdp(R, M, dMdp)` passes the wrong argument.** Every sibling call passes `MolWt`, the mixture molecular weight of about 29. This one passes `M`, which Pascal resolves case-insensitively to the private field `m` — hydrogen atoms per fuel molecule, 17 for C7H17 | `GASPROPS.PAS:325` | Moot — feeds only the `dudp` that B16 overwrites |
| B18 | **The unburnt mixture is a successive substitution, not a closed form.** `FuelAirResConcs` takes the products' molecular weight from the mixture array it is about to overwrite rather than from the residual. The first call therefore sees zeros, drives the residual mass fraction to one and yields pure residual; later calls converge gradually, agreeing to about nine significant figures rather than exactly | `GASPROPS.PAS:1070` | **Fix** — first-call transient means results depend on call history |
| B19 | **`Get_gamma` deliberately ignores the composition derivatives.** The burnt branch computes `dMdT`, then passes a zero array and a zero `dMdT` to `MixdhdT` anyway, with the real arguments left commented out beside them. Gamma is therefore the frozen ratio. This is why gamma matches the baseline exactly while `Cp` from `ReturnProps` does not | `GASPROPS.PAS:509` | Keep — and it is what makes gamma trustworthy today |
| B20 | **The species curve fits clamp rather than extrapolate**, and the lower guard was widened from 300 K to 260 K with the comment "to avoid error messages for now" | `GASPROPS.PAS:735, 772` | **Fix** — a silent clamp hides out-of-range states |
| B21 | **`KEquilib`'s temperature clamp is dead code.** It calls `error(6, ...)` for a temperature outside 600 K to 4000 K, and `error` raises, so the clamp on the next line never runs. Out of range is fatal, not clamped | `Eqbm.pas:415-419` | **Fix** — the intent was clearly to clamp |
| B22 | **`TEqbm.Error`'s counters are all unreachable.** Every branch raises before incrementing, so `errcount`, `err2count` and friends stay zero, the "report only once" guards never engage, and the `errcode` assignment at the end never executes. Every occurrence throws | `Eqbm.pas:453-510` | **Fix** — restores the intended suppression |
| B23 | **The `x[8] := 99` sentinel is unreachable.** `go2` tests for it and `ReturnProps` branches on it, but the only path that sets it requires `error(2, ...)` to return, and it raises instead | `Eqbm.pas:206` | Moot — dead branch either way |
| B24 | **The negative-mole-fraction guard and its clamps disagree.** The guard fires on `< 0` but each clamp inside tests `<= 0`, so an exact zero is only corrected when some other species happens to be negative | `Eqbm.pas:253-259` | **Fix** — an exact zero divides by zero downstream |
| B25 | **`FuelThermo` carries a commented-out row** between rows 1 and 2, so the library fuel indices are only correct if that row stays commented. The port carries the active rows only | `GASPROPS.PAS:81` | Keep — documented, and the indices are right |
| B26 | **`TValve.Lift` can read an uninitialised variable.** `CNew` is assigned only inside `if O > C`, but is read unconditionally on the next branch. Every sane cam has `O > C` after the angle conversion, so the path is not reached in practice. Not yet ported | `Valves.pas` | **Fix** — initialise it when step 4 ports this |
| B27 | **`TGas2z.Tgas` is a function that writes.** It recomputes `xb` from the zone masses and stores it before weighting the two temperatures, so reading the gas temperature mutates the gas. The callers in `ICEngine2Z.pas` read it at points where nothing else has refreshed `xb`, so the write is load-bearing rather than incidental | `Gasses2Z.pas:43` | **Fix** — split the refresh out and call it deliberately |
| B28 | **The zone-mass guard in `UpdateB` is always true.** `if (xb>0) and (xb<1)` sits immediately below the two clamps that pin `xb` into [0.01, 0.99], so the burnt and unburnt masses are assigned on every call and the guard tests nothing | `Gasses2Z.pas:64` | Moot — the condition it guards cannot be false |
| B29 | **`UpdateGE` leaves both zone volumes at zero** while `Vgas` takes the full cylinder volume, so `Vu + Vb <> Vgas` throughout valve overlap. It also accepts `Vb1` and `Tb1` and ignores both, taking both zone temperatures from `Tu1`; `UpdateUB` and `UpdateBD` likewise accept and ignore `Vb1` | `Gasses2Z.pas:127` | Moot — nothing reads the zone volumes in this state, though a chart or export that did would show zeros |
| B30 | **Gasses2Z declares a unit-level `var err : Integer`** in its implementation section, commented "GasProps Error". Every method that writes `err` resolves it to the class field of the same name, which is in a nearer scope, so the unit variable is never read or written | `Gasses2Z.pas:37` | Moot — dead declaration |
| B31 | **`Pwr` is not `Power`, and it returns zero for a non-positive base.** `PNTWMath.pas`'s `Pwr` always evaluates `exp(j*ln(i))` and answers `0` when `i <= 0` rather than raising. `hWoshini` raises the characteristic velocity `w` to 0.8 through it, and `w` goes negative whenever cylinder pressure falls far enough below the motored pressure, at which point the heat-transfer coefficient collapses to exactly zero instead of following the correlation | `PNTWMath.pas:50-56` | **Fix** — a silent discontinuity in the middle of a correlation |
| B32 | **`hWoshini` computes the motored volume from the solver's current crank angle, not the one it was called with.** `Vmot := VCyl(x)` reads the `TRKF` field `x`, while the derivative that called it is evaluating a trial state at `x1`. Five of RKF5's six stages have `x1 <> x`, so the motored pressure lags the state it is being compared against | `ICEngine2Z.pas:183` | **Fix** — pass the angle through; it is almost certainly a slip |
| B33 | **The swept volume in `hWoshini` is wrong.** `Vswept := VCyl(Pi) * CR/(CR+1)`, where the swept volume of a cylinder whose volume at bottom dead centre is `VCyl(pi)` is `VCyl(pi) * (CR-1)/CR`. At CR 9.2 the two differ by a factor of 1.13, which scales the pressure-rise term of the Woschni velocity | `ICEngine2Z.pas:186` | **Fix** — but it is partly absorbed by the calibrated `CWoshini` |
| B34 | **The two combustion heat-loss branches are asymmetric.** `dQbdtheta` scales the piston and head term by `Vb/Vgas` and drops the liner term entirely; `dQudtheta`'s matching branch scales the piston and head term by `Vu/Vgas` and keeps the liner term at full area. Defensible for a flame kernel near the head, but stated nowhere | `ICEngine2Z.pas:236-247, 264-268` | Keep — plausibly deliberate; confirm before changing |
| B35 | **`dPdTheta1z` hard-codes gamma to 1.4.** Both the `VariableGamma` test and the `Cyl.Gamma` assignment sit immediately above the literal, commented out. This is the equation the single-zone model integrates and, because of B37, the one **two-zone** overlap uses too, so the engine's own computed gamma never reaches the pressure equation in either mode. See also C11 | `ICEngine2Z.pas:320-334` | **Fix** — honour the flag, or remove the flag |
| B36 | **The two unburnt equations choose the transfer enthalpy on opposite sides of the update.** `dPdThetaUB` calls `Cyl.updateUB` and then reads `Cyl.hu`; `dTudThetaUB` reads `Cyl.hu` and then calls `updateUB`. When `MIn > 0` both take the plenum's enthalpy and agree; otherwise they use values one update apart | `ICEngine2Z.pas:340-377` | **Fix** — one of the two is wrong, and they cannot both be right |
| B37 | **The four gas-exchange equations are unreachable.** `dVbdThetaGE`, `dPdThetaGE`, `dTbdThetaGE` and `dTudThetaGE` are referenced in exactly one place, `ICEngine2Z.pas:725-728`, and that block is commented out. Overlap installs `dPdTheta1z` and leaves the other three components at `Zero`, so valve overlap runs a single-zone constant-gamma pressure equation in a two-zone simulation. The free function `dQldTheta` is dead for the same reason. Ported anyway, so restoring the block is a one-line change | `ICEngine2Z.pas:515-633, 719-728` | **Fix** — the two-zone model has no gas-exchange equations in the loop |
| B38 | **`PCylIVC`, `TCylIVC` and `VCylIVC` are not conditions at inlet valve closing.** `InitVars` sets them once, from the plenum pressure expression, the ambient temperature and the cylinder volume at the IVC crank angle, and nothing revises them for the rest of the run. Woschni's motored-pressure reference is therefore fixed at the initial guess rather than tracking each cycle | `ICEngine2Z.pas:1015-1017` | **Fix** — update them at IVC, which is what the names promise |
| B39 | **`FEnergy` accumulates with degrees where radians are expected.** `FEnergy := FEnergy + ... * Cyl.dxdTheta(CA) * dx` passes `CA` in degrees to a function whose first act is `Theta := ThetaRad*180/pi`; every other caller passes `x`. The burn window is missed almost entirely. Harmless in practice: `FEnergy` is written in two places and read in none — the energy balance takes `QFuel` from `Cyl.fuel.Q * Cyl.Fuel.M` instead | `ICEngine2Z.pas:920` | Moot — the field is write-only, but fix the units if it is ever wired up |
| B40 | **The exhaust state never assigns an equation set.** Every other state in `Run`'s two-zone switch sets `fn[1..4]`; `Exhaust` only zeroes `TotalMoutEV`, so exhaust integrates with the equations expansion left installed. That happens to be the right pair — `dPdThetaBD` and `dTbdThetaBD` — so it works, but by inheritance rather than intent | `ICEngine2Z.pas:721` | Keep — correct as written; worth a comment, not a change |
| B41 | **`TProfile.Gety` returns -1 for an unusable profile.** A profile that failed to load or holds fewer than two points answers `-1` rather than raising, and `TValve.Lift` multiplies that by the maximum lift, so a missing cam file yields a negative lift and then a negative flow area rather than an error. `ProfileOk` is checked when the engine loads but not here | `Profiles.pas:114` | **Fix** — raise, or refuse to run with an unusable profile |
| B42 | **`TValve.Lift` declares `ONew` and never uses it**, alongside the `CNew` of B26. Whatever symmetric handling of the opening angle was intended was never written | `Valves.pas:60` | Moot — dead local |
| B43 | **Both stagnation branches in `MassFlow` compute the same expression.** `if Iut > 0 then cStag := sqrt(sqr(Ict) + (gam-1)/2*sqr(Iut)) else cStag := sqrt(sqr(Ict) + (gam-1)/2*sqr(abs(Iut)))` differ only by an `abs` inside a square. The same holds for `cCyl` just below. The alternatives the tests were meant to select - `cStag := cCyl` and `cCyl := cCyl` - are commented out on the lines between, so both conditionals are no-ops | `Manifolds.pas:428-434` | Keep — decide with the rest of the CFD in phase 4b |
| B44 | **The atmosphere's `hin` is copied before it exists.** `InitVars` does `With Atm do ... hin := hu`, but no `ReturnProps` call is ever made on the atmosphere gas, so `hu` is still zero and `hin` copies that zero. `Cyl.hin` gets the same treatment. Only the unreachable gas-exchange equations of B37 read `hin`, so nothing observable depends on it | `ICEngine2Z.pas:957, 981` | Moot — feeds dead code only, but would bite if B37 were restored |
| B45 | **`PMax` is initialised to 1e7 Pa, not zero.** `InitVars` sets the running peak-pressure maximum to 10,000,000, which no cycle exceeds, so it could never fall to a real value. Harmless only because the first state entry - always Compression, since cycles start at inlet valve closing - resets it to zero before any comparison happens | `ICEngine2Z.pas:1046` | Moot — overwritten before it is ever read |
| B46 | **The mass-flow derivatives are stale throughout the closed period.** `Run`'s two-zone mass block has a case for Exhaust, Intake and Overlap only, so from inlet valve closing to exhaust valve opening `Cyl.dmindtheta` and `Cyl.dmoutdtheta` keep whatever gas exchange last left on them — the final intake step of this cycle, and the final exhaust step of the cycle **before**. The inlet one is harmless on any normal cam, because the valve is nearly shut at IVC and the value is already near zero; the exhaust one is not. On the baseline engine it carries 6.3e-5 kg/rad into expansion, where `dPdThetaBD` and `dTbdThetaBD` both use it as though gas were still leaving the cylinder. **Measured**: treating it as zero instead leaves `dTb/dtheta` 7 per cent short at the start of expansion and 13 per cent short by the end, because the stale term stays constant while `-P dV/dtheta` shrinks. Reproducing it brings expansion pressure from 0.25 per cent to 0.036 per cent. Note also that the two are assigned with opposite sign conventions — `dmindtheta := -Min/dx` against `dmoutdtheta := Mout/dx` | `ICEngine2Z.pas:822-844` | **Fix** — zero them on entry to compression; the physics wants no flow term in a closed cylinder |

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

**C11 — The *Variable Gamma* checkbox does nothing.** It is read from the
`.eng` file, written back, enabled and disabled with the zone count, and assigned
to `Engine2z.VariableGamma` — and then never tested. The only occurrence of the
field outside the edit form is inside a comment (`ICEngine2Z.pas:324`), directly
above the `LocalGamma := 1.4` that replaced it. An operator who unticks it sees no
change in the results, because there was nothing to turn off. See B35.

**C12 — `InitialTb` calls `Halt` when it fails to converge.** A thousand
iterations of the isenthalpic estimate without settling terminates the process
outright, losing whatever the operator had loaded, after a single message box
(`ICEngine2Z.pas:296-300`). The port throws instead, the same trade
`TWallTemps.Load` gets.


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
- `tests/App.Tests/EquilibriumSolverTests.cs` — the derivatives, checked by comparing the
  analytic derivative against a finite difference of the solver itself.
- `tests/App.Tests/GasPropertyModelTests.cs` — gamma against the baseline trace in
  both the burnt and unburnt branches, and the equilibrium specific heat.
- `tests/App.Tests/Rkf5IntegratorTests.cs` — B12 and B14, including a measured
  order-of-convergence test that fails loudly if the transposed coefficient is
  ever "corrected".
