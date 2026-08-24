Read SPEC.md, CLAUDE.md and **BASELINE.md** in full before doing anything,
and read the phase 2 and phase 3 code — the domain models in App.Core, the
expression evaluator, the table readers, and EngineLoader. Phase 4 runs on
top of all of it and replaces none of it.

Phase 4: the simulation core. This is the phase the whole port exists
for, and the largest by a wide margin. An engine already loads
completely; now make it run a cycle and produce numbers that match the
Delphi original.

Scope:

  1. The RKF5 integrator and the crank-angle state machine.
  2. The two-zone gas model and the twelve-species equilibrium model.
  3. The one-dimensional manifold CFD solver.
  4. Performance and the energy balance.
  5. Validation against Example1 and Example2.

Out of scope: charts, the multi-run grid, the PVT export form. Those are
phase 5. Write the manifold text output files, because the solver
produces them and they are the best debugging aid you will have.

--------------------------------------------------------------------
Start with validation, not with code
--------------------------------------------------------------------

SPEC.md section 6 says the numerical tolerances "must be measured from
legacy reference runs before automated .NET acceptance tests are
finalized". That is still true and it is the first thing to settle. Do
not write the integrator and hope to check it later; you will have no way
to tell a porting mistake from a floating-point difference.

**The reference run now exists.** `data/baseline/` holds a complete,
deliberately captured run of the original Delphi application: the
`A2China.eng` engine, all ten side files it needs, screenshots of all
eight settings tabs, the simulation dialog, the results screen, two extra
result charts, and `A2China.txt` — a full-cycle 720-row PVT trace.
**BASELINE.md documents the whole set** and is the primary acceptance
target. Read it before planning anything.

Three things in it will save you real time:

  - **The derivation chain is closed.** Every headline number — IMEP,
    PMEP, FMEP, BMEP, torque, power — is reproduced from the trace alone
    in BASELINE.md, each step checkable on its own. When your solver
    starts producing numbers, the first link that misses tells you where
    the fault is without bisecting the physics. `BaselineDataTests`
    already pins that chain against the reference data.
  - **The accumulators reset at inlet valve closing**, CA −100 for this
    engine. The cycle-complete `WWork`, `PWork` and `htLoss` are the
    values just before that, not the ones on the trace's last row.
    Sampling the wrong point makes PMEP look wrong by a factor of four.
  - **The run converged rather than completing.** Six cycles were
    requested at 1 mg; it met 0.3 mg on cycle 4. Converging on cycle 4 is
    part of the expected result, not an incidental detail.

The baseline already loads through the phase 3 code with zero unresolved
files, despite every path in it being a dead `C:\CAEEng\...` absolute
path, so you can start comparing numbers immediately.

Older recorded output is also scattered through `legacy/ESA/Data/`. It is
worth knowing about but is much weaker evidence than the baseline, for
reasons set out below. Survey it, but do not build acceptance tests on it
without checking BASELINE.md first:

  Lastcyc.txt     A full cycle PVT trace: 720 rows, one per crank angle,
                  29 named columns (CA plus the 28 captured values).
                  Header names map onto TCAPoint's properties. This is by
                  far the most useful reference you have, because it lets
                  you compare per crank angle instead of arguing about an
                  aggregate at the end.
  Pcyl.txt        Crank angle and cylinder pressure, 720 rows.
  Tcyl.txt        Crank angle, temperature and volume.
  Inlet.txt       Crank angle then pressure and velocity at the pipe
  Exhaust.txt     start, midpoint and valve end.
  MassFlow.txt    Crank angle, inlet mass, exhaust mass, scaled by 1e6.
  Pressure.dat    720 rows of 21 columns: a pipe pressure field, one
                  column per grid point.
  *Dat.txt        Performance per speed: Speed, IMEP, PMEP, FMEP, BMEP,
                  MEff, VEff, ThEff, Torque, Power, mf, SFC, TMass,
                  MassIn, MassOut, Lambda, Spark, BackP and the five
                  energy balance terms.

Four things about this data will bite you if you assume otherwise:

  - **Provenance is mostly unrecorded.** Only six engines name a
    PerfDataSave file, and most results landed in Simuldat.txt, which is
    the Delphi default name. Nothing in the file says which engine,
    speed or cycle count produced it.
  - **The performance files accumulate.** Rows are appended per run, so
    one file can hold several unrelated runs. Example1/Simuldat.txt has
    two 5000 rpm rows with different values.
  - **The header format changed.** DefaultDat.txt writes MassIn/MassOut
    and BackP where Simuldat.txt writes MIn/MOut and ExhP. Two versions
    of the same writer.
  - **The manifold traces came from the eight engines with
    SaveManfData=1**, and only from the final cycle. Nissan2.eng and
    ChinaBoraVVT1.eng are among them.

So treat the shipped output as corroboration of magnitude and shape, not
as golden bytes. `data/baseline/` is the set that carries its own
provenance, and is the one to hold yourself to.

**If you need more reference runs, ask.** ESA.exe in legacy/ESA is a
32-bit Windows GUI binary; there is no Wine in the Linux dev container,
but I run Windows 10 and produced the existing baseline that way. A
second engine, another speed, or a run with SaveManfData set so you get
the manifold traces are all cheap for me to generate — but I need the
exact engine, speed, cycle count and settings from you. Propose the list
as soon as you know what you need, rather than a week later.

One request worth making early: the current baseline was run with
SaveManfData off, so there are no manifold pressure or velocity traces.
The CFD solver is the largest and least observable part of phase 4, and a
run with that box ticked would give you a per-crank-angle view inside the
pipes. Ask for it before you start the solver, not after it misbehaves.
BASELINE.md has the section "Getting the manifold traces", covering where
the switch is and the two ways it can appear not to work.

Decide and tell me the tolerance policy: which quantities are compared,
at what relative tolerance, and why. A per-crank-angle pressure trace and
an end-of-run SFC do not deserve the same number.

--------------------------------------------------------------------
What I already know about the physics. Verify it, don't trust it.
--------------------------------------------------------------------

### The integrator is simpler than its name

RKf5.pas, TRKF.IntegrateRKF. It computes the six Fehlberg stages and
takes the fifth-order step with weights 16/135, 0, 6656/12825,
28561/56430, -9/50, 2/55. There is **no error estimate, no fourth-order
comparison and no adaptive step control** despite the Fehlberg name. The
step is whatever dCA is. Do not "improve" this into an adaptive solver:
it would change every number in the reference runs.

Integrator 1 is plain Euler and exists as a quick alternative.

### The ODE system is four equations with fixed meanings

MaxN = 4 in RKf5.pas, and the state machine assigns fn[1..4] per state in
TEngine2z.Run. The vector is y[1] = Vb, y[2] = P, y[3] = Tb, y[4] = Tu.

Per state, from Run:

  Compression   Zero, dpdthetaUB, Zero, dTudThetaUB
  Combustion    dVbdThetaB, dPdThetaB, dTbdThetaB, dTudThetaB
  Expansion     Zero, dpdThetaBd, dTbdThetaBd, Zero
  Overlap       Zero, dPdTheta1z, Zero, Zero
  Intake        Zero, dPdThetaUB, Zero, dTudThetaUB

Note that Combustion falls back to the Compression set while Cyl.mb is
still zero, and that Overlap uses the simplified single-zone pressure
equation. SPEC.md section 5 records the commented-out gas-exchange
assignments as intentionally not restored; leave them alone.

**A correction you must make:** phase 2 wrote
`EsaLimits.MaxEquations = 10`. The Delphi constant is 4. The larger array
is harmless at runtime because NEqns drives every loop, but the constant
is wrong and the y indices have specific meanings that deserve naming.
Fix it and give y[1..4] readable accessors.

### Performance is short and complete

TEngine2z.Performance is thirty lines and ports directly. Two things in
it look like defects and must be reproduced anyway:

  - FMEP is TFMEP - PMEP and BMEP is IMEP - PMEP - FMEP, so PMEP cancels
    and BMEP is just IMEP - TFMEP. The intermediate FMEP is still
    reported, so keep both.
  - mf is `Cyl.Fuel.m * 2 * Nrpm * 60` and the ThEff denominator is
    `Q * m * 2 * Nrpm / 60`. Neither mentions the cylinder count, and
    both are correct **only for a four-cylinder engine** — the physically
    right factor is `NCyl * N / 2`, which equals `2 * N` only at
    `NCyl == 4`. Every one of the 71 shipped engines is `NoCyls=4`, which
    is why it was never caught. Both reproduce the baseline exactly. Port
    them verbatim, pin the behaviour in a test, and treat it as a known
    defect: SFC derives from mf, so fuel flow, SFC and thermal efficiency
    all go wrong by `4 / NCyl` for any other engine. BASELINE.md has the
    full derivation.

Friction is a Chen-Flynn style correlation in TFMEP:
`1.0e5 * (0.97 + 0.15*N/1000 + 0.05*(N/1000)^2)`.

### The manifold solver is the bulk of the work

Manifolds.pas is 3172 lines, about half the ported physics. Main_Prog
drives it, with roughly twenty-five supporting routines: characteristic
line calculations, INTERNAL_PIPE, the four valve boundary combinations,
INFLOW_INLET_PIPE, OUTFLOW_EXHAUST_PIPE, sonic and subsonic velocity
solvers with reverse-flow variants, Fanning friction and a critical
pressure solve. Iteration stops on tolerance or after 1000 iterations.

Budget for this properly. It is more code than the rest of phase 4 put
together, and it is where a per-crank-angle reference trace earns its
keep.

The grid sizing is already done: GridSizeCalculator evaluates the stored
expression and enforces NI = 68 and NE = 38, and TPipe.Length falls out
of the .maf table as `Index[xcount] / 1000`. Manifold pressure and
velocity fields hang off PipeGrid, which already exists.

CLAUDE.md lists the quirks that are already known and must survive:
the zero cliff past the end of an area table, the reversed y arguments in
the Cd lookup, the crossed exhaust Cd tables, and the hard-coded IVF line
at or below 1000 rpm that overrides the .eng expression.

### Equilibrium is where precision will hurt

Eqbm.pas computes twelve species with Olikara and Borman numbering and is
written in `Extended` throughout — 80-bit, 64-bit mantissa. .NET has no
equivalent and CLAUDE.md already flags this as the first thing to suspect
when numbers drift. It matters more here than anywhere else because the
solver is a Newton iteration with tolerances: a mantissa eleven bits
shorter can change an iteration count, not just a last digit.

Do not paper over this. Port it in double, then measure: compare species
against a reference run and report where the two diverge and by how much.
If double genuinely cannot hold the model together, say so with evidence
and we will discuss options rather than you quietly loosening a tolerance.

GASPROPS.PAS (1162 lines, TProp) sits between the gas model and the
equilibrium solver and is ported alongside it.

The burnt-volume clamp `if Vb > Vgas then Vb := Vgas` in Gasses2Z.pas is
an intentional safeguard (SPEC.md section 5). Keep it.

### Convergence and cycle handling

The run stops when `abs(TotalMInIV - TotalMOutEV) * 1e6 < MassBalance`,
with the tolerance in micrograms and three cycles a valid minimum. The
first No1zCycles cycles may run one-zone before switching to two. Carry
end-of-cycle gas state forward as the next cycle's initial condition, and
reset only the per-cycle totals at their state initialisation, following
the field-level resets in Run and InitVars rather than rebuilding the
engine object.

--------------------------------------------------------------------
Constraints
--------------------------------------------------------------------

The rules from phases 2 and 3 still hold:

  - App.Core references no UI framework. LayeringTests enforces it.
  - No static mutable state. The Delphi original leaned hard on globals
    here — Engine2z in ICEngine2z.pas and Choice, QI, QE and W in
    Manifolds.pas. Those become instance state on the solver.
  - Nullable on, warnings as errors, zero warnings in a Release build.
  - The round-trip gates stay green. Nothing in phase 4 should touch
    persistence, and if EngRoundTripTests or TableRoundTripTests goes
    red you have broken something you were not working on.

The simulation must be runnable headlessly, without an Avalonia window,
so that tests can drive it. Keep the solver free of any dependency on the
UI, and let the shell observe progress through an interface rather than
the solver reaching into a view model.

--------------------------------------------------------------------
Deliverables
--------------------------------------------------------------------

  1. A tolerance policy, agreed with me, and the reference runs it is
     based on.
  2. The integrator, state machine, gas and equilibrium models, manifold
     solver and performance calculations, ported and unit tested.
  3. A headless run of `data/baseline/A2China.eng` at 4000 rpm
     reproducing the trace and the aggregates in BASELINE.md within the
     agreed tolerance, as an automated test. Example1 and Example2 as
     further cases if the weaker reference data supports it.
  4. The nine manifold output files, written on the final cycle when
     SaveManfData is set, with the documented columns and units.
  5. CLAUDE.md updated: phase 4 complete, phase 5 next, new caveats
     added, and the MaxEquations correction noted.

Work in that order. Items 1 and 2 are independently testable long before
the solver runs end to end, and if the manifold CFD slips, everything
else should still land.

Then stop and give me a summary of what you built, what in SPEC.md turned
out wrong or underspecified, where the ported numbers diverge from the
reference runs and why you think they do, and what you would tackle first
in phase 5. Don't start phase 5.
