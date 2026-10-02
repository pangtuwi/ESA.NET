# Corrections

How to work through the legacy defects in `ISSUES.md` section B now that the port
reproduces the original end to end.

**Status: proposal, awaiting a decision.** Nothing here has been implemented. Section 5
lists what needs deciding.

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
| [B48](https://github.com/pangtuwi/ESA.NET/issues/60) | Make `cThermo`'s zero result deliberate | The port already returns 0; this documents it as a decision |
| [B51](https://github.com/pangtuwi/ESA.NET/issues/63) | Cap the characteristic foot loops and report when the cap is hit | Uncapped, a non-converging foot hangs, so no completed run has hit it |
| [B52](https://github.com/pangtuwi/ESA.NET/issues/64) | Count the grid points where the outer iteration gave up | Counting changes nothing computed |
| [B53](https://github.com/pangtuwi/ESA.NET/issues/65) | Report a negative pressure or density at a foot as a diagnostic | The port already drops the dialog; this adds the report |
| [B57](https://github.com/pangtuwi/ESA.NET/issues/69) | Make the seated-valve zero deliberate | The port already returns 0 |

Two more need only a register update: **[B26](https://github.com/pangtuwi/ESA.NET/issues/38)**
(`ValveMotion.Lift` already takes the evident intent) and
**[B67](https://github.com/pangtuwi/ESA.NET/issues/79)** (`CrankAngleTraceWriter` already
indexes the last column correctly). Both can be closed as they stand.

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
| Lookups | [B4](https://github.com/pangtuwi/ESA.NET/issues/16) (area cliff — `AreaGradient`'s end-of-pipe branch depends on it), [B5](https://github.com/pangtuwi/ESA.NET/issues/17) (Cd axes crossed) |
| Gas properties | [B16](https://github.com/pangtuwi/ESA.NET/issues/28) (analytic `dudp` thrown away), [B18](https://github.com/pangtuwi/ESA.NET/issues/30) (first-call transient) |
| Heat transfer | [B31](https://github.com/pangtuwi/ESA.NET/issues/43) (`Pwr` zero for a negative base), [B32](https://github.com/pangtuwi/ESA.NET/issues/44) (motored volume at the wrong angle), [B33](https://github.com/pangtuwi/ESA.NET/issues/45) (swept volume), [B38](https://github.com/pangtuwi/ESA.NET/issues/50) (IVC conditions never updated) |
| Cylinder equations | [B35](https://github.com/pangtuwi/ESA.NET/issues/47) (gamma fixed at 1.4), [B36](https://github.com/pangtuwi/ESA.NET/issues/48) (transfer enthalpy either side of the update), [B37](https://github.com/pangtuwi/ESA.NET/issues/49) (no gas-exchange equations), [B46](https://github.com/pangtuwi/ESA.NET/issues/58) (stale mass-flow derivatives) |
| Wave solver | [B50](https://github.com/pangtuwi/ESA.NET/issues/62), [B54](https://github.com/pangtuwi/ESA.NET/issues/66)–[B56](https://github.com/pangtuwi/ESA.NET/issues/68), [B59](https://github.com/pangtuwi/ESA.NET/issues/71)–[B62](https://github.com/pangtuwi/ESA.NET/issues/74), [B64](https://github.com/pangtuwi/ESA.NET/issues/76), [B65](https://github.com/pangtuwi/ESA.NET/issues/77) |

## 3. Tier 3: a switch, not a new baseline

### Recommendation

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
  *Corrected*. Per-flag control stays in `ESA.ini` for whoever is working on the physics.

### Why not re-baseline

A fresh reference run of the original on Windows has every one of these defects in it,
so it cannot say whether a correction is right. Nothing produced by the original can.
The evidence for a corrected result has to come from physics, so each tier 3 fix carries
its own check:

| Kind of check | Used for |
|---|---|
| Order of convergence on an analytic problem (already in `Rkf5IntegratorTests`) | B14 |
| Mass conservation through the cylinder over a cycle | B37, B46 |
| No flow terms in a closed cylinder | B46 |
| Energy-balance closure (heat + work + exhaust + pumping + friction against fuel energy) | B31–B33, B35, B38 |
| Wave-solver invariants: a stagnant uniform pipe stays put, symmetric boundaries stay symmetric (`CharacteristicSolverTests`) | B50, B54–B56, B59–B65 |

### Measure every correction

When a flag lands, record its effect on the baseline engine in its B entry: torque, IMEP,
volumetric efficiency, SFC and peak pressure, with the flag alone and with every flag so
far. B14 already has its figure: the whole-cycle rms against the reference goes from
0.225 % to 0.190 %, so the corrected integrator fits the reference slightly *better*.
This keeps a running account of how far "Corrected" sits from "Legacy", and stops any
single flag from moving results by surprise.

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

## 5. What needs deciding

1. **A switch, or a new baseline?** This document recommends the switch. A new baseline
   would mean accepting the corrected results on physical evidence alone and retiring
   `data/baseline/` as the reference.
2. **Should Corrected become the default** once tier 3 is complete, or stay opt-in for
   good? The original's results can always be reproduced by switching back.
3. **Should the operator see per-flag control** at all, or only Legacy / Corrected?

## 6. Register housekeeping found while writing this

- **B69**'s verdict cell still begins "**Fix**". The entry was fixed by #133, and its
  text says so.
- **B26** and **B67** are already correct in the port (tier 1 above), and their entries
  can say so.
