# Corrections

How to work through the legacy defects in `ISSUES.md` section B now that the port
reproduces the original end to end.

**Status: agreed on 2026-10-02**, with the decisions recorded in section 5. Section 4 is
the order of work and section 7 tracks progress.

---

## 1. The problem

Section B records 74 behaviours of the Delphi original, reproduced on purpose so the port
could be validated against `data/baseline/`. 38 of them still carry a **Fix** verdict
(a 39th, B69, is done). That
reference run is the only one the port is measured against, and the original produced it
with every one of those defects in place. So fixing anything that touches the physics
moves the port away from the one thing that says it is right.

Treating all 44 the same way would be a mistake in either direction:
- fixing them all at once throws away the validation phase 4 built;
- fixing none leaves known-wrong results in the hands of anyone running an engine that
  isn't the baseline one.

They are not alike, though. Sorted by what each fix would do to the baseline, they fall
into three tiers - 12, 3 and 23 entries - and only the third needs any machinery at all.
B75, found while scoping B31, joined tier 3 later and made it 24; B60 and B5, closed on
analysis as not defects, left it again, so it is 22. B76, B77 and B78 were found while
fixing others, and B79 by B37's oracle; all four joined tier 3.

## 2. The three tiers

Issue numbers are `ISSUES.md`'s, and each is linked to its GitHub issue.

### Tier 1: cannot move any result — fix directly

These are robustness, diagnostics, refactors that preserve behaviour, and paths no
successful run reaches. Fixing them changes what happens when something goes wrong, not
what a converged run computes.

| Entry | Fix | Why it cannot move a result |
|---|---|---|
| [B21](https://github.com/pangtuwi/ESA.NET/issues/33) | Clamp `KEquilib`'s temperature as intended | Today out-of-range is fatal, so no completed run has ever been through it |
| [B22](https://github.com/pangtuwi/ESA.NET/issues/34) | Make the equilibrium error counters reachable | Same: every branch currently throws |
| [B24](https://github.com/pangtuwi/ESA.NET/issues/36) | Make the negative-mole-fraction guard and its clamps agree on exact zero | An exact zero divides by zero downstream, so no completed run contains one |
| [B27](https://github.com/pangtuwi/ESA.NET/issues/39) | Split `Tgas`'s hidden `xb` refresh into an explicit call | Refactor: the same refresh at the same points |
| [B41](https://github.com/pangtuwi/ESA.NET/issues/53) | Refuse an unusable cam profile instead of answering −1 | The loader already reports it; a run on it is meaningless |
| [B51](https://github.com/pangtuwi/ESA.NET/issues/63) | Cap the characteristic foot loops and report when the cap is hit | Uncapped, a non-converging foot hangs, so no completed run has hit it |
| [B52](https://github.com/pangtuwi/ESA.NET/issues/64) | Count the grid points where the outer iteration gave up | Counting changes nothing computed |
| [B53](https://github.com/pangtuwi/ESA.NET/issues/65) | Report a negative pressure or density at a foot as a diagnostic | The port already drops the dialog; this adds the report |

Four more needed only a register update, because the port already does the right thing:
- **[B26](https://github.com/pangtuwi/ESA.NET/issues/38)**: `ValveMotion.Lift` takes the
  evident intent;
- **[B48](https://github.com/pangtuwi/ESA.NET/issues/60)**: `cThermo`'s zero result is
  already deliberate and documented;
- **[B57](https://github.com/pangtuwi/ESA.NET/issues/69)**: so is the seated-valve zero;
- **[B67](https://github.com/pangtuwi/ESA.NET/issues/79)**: `CrankAngleTraceWriter` indexes
  the last column correctly.

All four are closed as they stand.

**Gate:** the full-precision fingerprint used for A6 must stay **bit-identical**. It
covers torque, IMEP, volumetric efficiency, SFC and trapped mass at 3000, 4000 and
5500 rpm, a hash of all 28 trace quantities at 720 crank angles, a hash of the nine
manifold files, and a multi-run row with every valve override. If an entry moves it, the
entry was in the wrong tier and goes to tier 3.

### Tier 2: changes results only outside the baseline case — fix directly

These are wrong for some engine or operating point, and the reference run happens not to
be one of them.

| Entry | Fix | Where it bites |
|---|---|---|
| [B1](https://github.com/pangtuwi/ESA.NET/issues/13) | Use `NCyl * Nrpm / 2` for fuel flow, thermal efficiency and SFC | Every engine that isn't a four-cylinder, by a factor of `4 / NCyl`. **Silently wrong for real engines today** |
| [B6](https://github.com/pangtuwi/ESA.NET/issues/18) | Evaluate the operator's `IVFFn` expression at every speed | At or below 1000 rpm a hard-coded line replaces it |
| [B20](https://github.com/pangtuwi/ESA.NET/issues/32) | Report a temperature outside the curve fits rather than substituting a value | Below 260 K the port answers for 300 K; above 5000 K, for 5000 K |

**Gate:** the baseline suite and the fingerprint stay unchanged, *and* there is a new test
on a case the fix does change: a six-cylinder engine for B1, 900 rpm for B6, and an
out-of-range state for B20.

### Tier 3: moves the baseline — fix behind a switch

These change what the reference engine computes, some by tenths of a percent, B37
possibly by more.

| Area | Entries |
|---|---|
| Integrator | [B14](https://github.com/pangtuwi/ESA.NET/issues/26) (the transposed RKF5 digit) |
| Lookups | [B4](https://github.com/pangtuwi/ESA.NET/issues/16) (area cliff — `AreaGradient`'s end-of-pipe branch depends on it), B5 closed as not a defect |
| Gas properties | [B16](https://github.com/pangtuwi/ESA.NET/issues/28) (analytic `dudp` thrown away; [B17](https://github.com/pangtuwi/ESA.NET/issues/29) resolved with it), [B18](https://github.com/pangtuwi/ESA.NET/issues/30) (first-call transient) |
| Heat transfer | [B31](https://github.com/pangtuwi/ESA.NET/issues/43) (`Pwr` zero for a negative base), [B32](https://github.com/pangtuwi/ESA.NET/issues/44) (motored volume at the wrong angle), [B33](https://github.com/pangtuwi/ESA.NET/issues/45) (swept volume), [B38](https://github.com/pangtuwi/ESA.NET/issues/50) (IVC conditions never updated), [B75](https://github.com/pangtuwi/ESA.NET/issues/157) (the pressure-rise term in every state) |
| Cylinder equations | [B78](https://github.com/pangtuwi/ESA.NET/issues/176) (burnt volume never reset at combustion entry: every converged burn creates energy, the largest of all), [B35](https://github.com/pangtuwi/ESA.NET/issues/47) (gamma fixed at 1.4) with [B76](https://github.com/pangtuwi/ESA.NET/issues/172) (single-zone state a step stale), found on the way and only right together; [B36](https://github.com/pangtuwi/ESA.NET/issues/48) (transfer enthalpy either side of the update) changed nothing and was fixed directly; [B37](https://github.com/pangtuwi/ESA.NET/issues/49) (no gas-exchange equations), [B46](https://github.com/pangtuwi/ESA.NET/issues/58) (stale mass-flow derivatives), [B77](https://github.com/pangtuwi/ESA.NET/issues/173) (the gas updated at the step's starting angle, with work a step out of phase), [B79](https://github.com/pangtuwi/ESA.NET/issues/182) (overlap's valve totals counting a crossing flow twice) |
| Wave solver | [B50](https://github.com/pangtuwi/ESA.NET/issues/62), [B54](https://github.com/pangtuwi/ESA.NET/issues/66)–[B56](https://github.com/pangtuwi/ESA.NET/issues/68), [B59](https://github.com/pangtuwi/ESA.NET/issues/71), [B61](https://github.com/pangtuwi/ESA.NET/issues/73), [B62](https://github.com/pangtuwi/ESA.NET/issues/74), [B64](https://github.com/pangtuwi/ESA.NET/issues/76), [B65](https://github.com/pangtuwi/ESA.NET/issues/77) |

## 3. Tier 3: a switch, not a new baseline

### The switch

Add a set of **physics corrections** to `SimulationSettings`, with one named flag per
tier 3 entry (`Rkf5Coefficient`, `StaleMassFlowDerivatives`, `WoschniSweptVolume`, …),
all **off** by default.

- **Off is today's behaviour, everything validated stays validated.** The baseline suite
  runs with every flag off and is not touched.
- **One correction per change.** Each lands behind its own flag, with its own test and
  its own measurement. Nothing is fixed in bulk, and a regression can be traced to a
  single flag.
- **Stored in `ESA.ini`, never in the `.eng`.** The corrections describe the simulator,
  not the engine, and the byte-exact `.eng` round trip is not disturbed.
- **Recorded with every run.** `run.txt` lists the flags that were on, so an archived
  result stays attributable after the defaults change.
- **One choice for the operator.** The Single Speed Simulation dialog offers *Legacy* or
  *Corrected*, and nothing finer. Per-flag control stays in `ESA.ini` for whoever is working
  on the physics.
- **Legacy stayed the default while tier 3 was in progress.** A half-corrected engine is
  neither the original nor the intended physics. With every tier 3 correction landed and
  measured, the default is now *Corrected*, and *Legacy* stays available for reproducing
  the original's numbers. See section 5.

### Why not re-baseline

A fresh reference run of the original on Windows has every one of these defects in it,
so it cannot say whether a correction is right. Nothing produced by the original can.
The evidence for a corrected result has to come from physics, so each tier 3 fix carries
its own check:

| Kind of check | Used for |
|---|---|
| Order of convergence on an analytic problem (already in `Rkf5IntegratorTests`) | B14 |
| Mass conservation through the cylinder over a cycle | B46 |
| Two zones on the ideal gas law filling the cylinder, and the open-system first law over overlap (`GasExchangeTests`) | B37 |
| Every flow through a valve counted once, in the valve's total or in what its port holds (`ValveTotalsTests`) | B79 |
| No flow terms in a closed cylinder | B46 |
| Energy-balance closure (heat + work + exhaust + pumping + friction against fuel energy) | B35 |
| The Woschni correlation's own terms: no combustion term at the motored pressure, the displacement in the pressure-rise term, no negative term, and the term only where Woschni published it (`CylinderHeatTransferTests`) | B31, B32, B33, B75 |
| The motored pressure tracking compression (`ClosedCylinderTests`) | B38 |
| Wave-solver invariants: a stagnant uniform pipe stays put, symmetric boundaries stay symmetric (`CharacteristicSolverTests`) | B50, B54–B56, B59–B65 |

### Measure every correction

When a flag lands, record its effect on the baseline engine in its B entry: torque, IMEP,
volumetric efficiency, SFC and peak pressure, with the flag alone and with every flag so
far. B14 has its figures: the whole-cycle rms against the reference goes from 0.225 % to
0.190 %, so the corrected integrator fits the reference slightly *better*, and torque at
4000 rpm moves −0.086 %. Measure with the cycle count fixed as well as at the reference
settings: a correction can tip the 1 mg convergence test onto a different cycle, and that
moves results further than most corrections do (B14's entry has the numbers).
This keeps a running account of how far "Corrected" sits from "Legacy", and stops any
single flag from moving results by surprise.

B46 is where Corrected leaves the reference behind. With it on, expansion pressure runs up
to 17 % above `A2China.txt` and torque rises about 6 %, because the reference reproduces
the defect. From B46 on, the reference run is evidence of what Legacy does and not of
what is right; each flag's physical oracle is what says it is right. B38 widened the gap
again: heat loss about 17 % lower, and with all seven corrections torque about 10 % higher.
B50 was the first correction whose effect changes sign with speed: it moves the inlet
tuning, so torque rises at 3000 rpm and falls at 6000. B55 was the first with no effect at
all on the baseline engine; it is kept behind the switch, and pinned to change nothing,
rather than being moved to tier 1 after the fact. B65 was the second largest after B46: the
charge had been drawn at the starting plenum temperature, and with the valve-end gas's own
temperature torque falls 15 % at 3000 rpm, taking the all-corrections figure there from
+8.8 % to −9.4 %.

### Check against the engine as well as the oracle

A correction's oracle says it is right; it does not say Corrected as a whole is. The
original's empirical inputs - the Woschni coefficient, the valve-flow tuning functions,
the burn angle - were calibrated, with every defect in place, against dynamometer data,
and removing defects one at a time can remove the ones that were cancelling each other.
`data/thesis/` holds the baseline engine's measured torque curve (thesis Figure 6.12), and
`ThesisCorrelationTests` gates Legacy against it and reports Corrected. Record a
correction's effect on that error alongside its oracle.

This is how B78 ([#176](https://github.com/pangtuwi/ESA.NET/issues/176)) was found (ISSUES.md F6, [#178](https://github.com/pangtuwi/ESA.NET/issues/178)). By B77, Corrected sat 18 % over the measured peak
with every correction passing its oracle. The cause was a defect none of them touched:
every converged burn creates about 15 % of the fuel energy (B78), and Legacy matched the
engine only because its plenum-referenced heat loss (B38) and phase-shifted work (B77) took
roughly that much back out. With B78 fixed, Corrected is back on the measured curve: 5.2 % rms
against Legacy's 5.3.

**Tier 3 is complete with B37** ([#49](https://github.com/pangtuwi/ESA.NET/issues/49)), the
last and most invasive entry: overlap now runs the burnt residual and the fresh charge as two
zones. Within Corrected it moves torque by about 1 % (2 % at 2000 rpm), and Corrected is 5.3 %
rms against the measured curve. B79, which B37's oracle found in the overlap mass
bookkeeping, followed it behind the switch. What remains is the change of default in
section 5, a single step of its own.

## 4. Suggested order

1. **Tier 1**, in one or two PRs, gated on the fingerprint. Close B26 and B67 at the same
   time.
2. **B1** on its own: it is the one defect that gives wrong numbers for ordinary engines
   today. Then B6 and B20.
3. **The switch**: the settings, the `ESA.ini` keys, the dialog choice and the `run.txt`
   line, with no corrections behind it yet. The baseline suite must be untouched.
4. **Tier 3, smallest and best-evidenced first:**
   - B14, which already has an oracle and a measurement;
   - B46, a closed-cylinder check;
   - B32 and B33, the Woschni inputs.

   Then work through the wave solver. Leave **B37** for last: it is the most invasive,
   and restoring the gas-exchange equations also brings B44 and B29 back into play.
5. **A8** (the combustion pressure bias) is not a section B entry, but some tier 3
   corrections touch the same equations. Re-measure it after each cylinder-equation flag.

## 5. Decisions

Agreed on 2026-10-02.

| Question | Decision |
|---|---|
| A switch, or a new baseline? | **A switch.** `data/baseline/` stays the reference for Legacy, and each correction is validated by its own physical check and measured against the baseline when it lands. |
| What becomes the default once tier 3 is complete? | **Corrected, with Legacy opt-in.** Legacy stays the default until then, and the change of default is a single step taken when the last tier 3 correction is in, not one flag at a time. **Done** once B37 and B79 were in: `PhysicsCorrections.DefaultMode` is Corrected. |
| How much control does the operator get? | **Legacy / Corrected only**, in the Single Speed Simulation dialog. Individual flags are set in `ESA.ini` and are not shown in the UI. |

## 6. Register housekeeping found while writing this

Both items were done with the first tier 1 change:
- **B69**'s verdict cell began "**Fix**", although #133 had fixed it.
- **B26**, **B48**, **B57** and **B67** were already right in the port.

## 7. Progress

| Step | Entries | State |
|---|---|---|
| Tier 1a: no new plumbing | B24, B27, B41 fixed; B26, B48, B57, B67 closed as already right | **Done**, with the fingerprint bit-identical |
| Tier 1b: a diagnostics channel | B21, B22, B51, B52, B53 fixed; A18 found and fixed on the way | **Done**, with the fingerprint bit-identical |
| Tier 2 | B1, B6, B20 fixed; A19 found and fixed on the way | **Done**, with the fingerprint bit-identical |
| The switch | — | **Done**, with nothing behind it yet; the fingerprint is bit-identical |
| Tier 3 | Every entry | B14, B46, B32, B33, B31, B38, B75, B50, B54, B55, B56, B59, B61, B62, B64, B65, B4, B16, B18, B35, B76, B77, B78, B37 and B79 **done**, B17 resolved with B16; B36 fixed directly, bit-identical everywhere; B60 and B5 closed as not defects — the fingerprint bit-identical under Legacy throughout. Corrected matches the thesis's dynamometer curve as closely as Legacy (5.3 against 5.3 % rms, [F6](https://github.com/pangtuwi/ESA.NET/issues/178)) |
| The default | — | **Corrected**, with Legacy opt-in (section 5); the fingerprint bit-identical with Legacy named |

Tier 1b was grouped because all five entries needed the same new piece: somewhere for a solver
to **report** what it used to throw, hang or pop a dialog over. That meant a per-run counter
of clamped equilibrium temperatures, suppressed equilibrium errors, capped foot loops,
abandoned outer iterations and negative foot states. As built:
- `EquilibriumDiagnostics` already existed, one per solver, and gained the new counters.
- `ManifoldDiagnostics` is new, owned by `ManifoldSolver` and passed into the interior-point
  update.
- `RunDiagnostics` gathers both onto `SimulationResult`.
- `run.txt` always carries a *Solver diagnostics* section, so a clean run is visibly clean,
  and the status line names anything counted.

Errors 2, 3 and 5 stay fatal, decided on 2026-10-02 (B22).

### The switch, as built

- **Model:** `PhysicsMode` (Legacy, Corrected) and `PhysicsCorrections` on
  `SimulationSettings.Physics`, in `App.Core/Model/PhysicsCorrections.cs`. That file also
  holds the `CorrectionCatalogue` of corrections, which was **empty** until the first tier 3
  correction landed; until then Corrected computed exactly what Legacy does, and said so.
  `PhysicsCorrections.DefaultMode` is the default, Corrected since tier 3 was completed: a
  new `PhysicsCorrections`, and so a new `SimulationSettings`, is Corrected. A solver handed
  no physics at all (`physics: null`) still runs with every correction off.
- **Resolving a flag:** `IsOn(correction)` returns the correction's override if it has one,
  and otherwise the mode. Overrides are keyed by `ISSUES.md` entry and matched
  case-insensitively. An unknown entry is kept, so an `ESA.ini` written by a later version
  survives being read by this one.
- **`ESA.ini`:** a `[Physics]` section with `Mode=Legacy|Corrected` and overrides such as
  `B14=1` or `B14=0`. It is written only once it says something, and only where its meaning
  changed, so an existing file keeps its bytes. Legacy has to be named: a missing or
  unreadable mode reads as the default, Corrected, so an `ESA.ini` written before the switch
  runs Corrected and keeps its bytes until the operator chooses Legacy, which writes
  `Mode=Legacy`. An override that is neither 0 nor 1 is ignored.
- **The operator:** the Single Speed Simulation dialog has a *Physics* group, *Legacy (as
  the original)* or *Corrected*, opening on the settings' mode. A note under it says when
  no corrections exist yet. A multi-point sweep runs on the mode last chosen, or the one
  in `ESA.ini`, and every row gets a copy.
- **The record:** `SimulationResult.Physics` is a snapshot of what the run used, and
  `run.txt` has a *Physics* line for single runs and sweeps alike.

**Adding a tier 3 correction** then means:
1. Add its `Correction` to the catalogue.
2. Read `settings.Physics.IsOn(...)` where the behaviour lives, passing the settings down
   as far as that code.
3. Give it a physical-oracle test.
4. Record its measured effect on the baseline engine in its B entry.

The baseline suite runs on Legacy throughout, and must not change. Since the default became
Corrected, a test about Legacy says so - `new PhysicsCorrections { Mode = PhysicsMode.Legacy }`
on its settings - or hands its solvers no physics at all.
