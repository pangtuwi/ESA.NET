# Correlation plan

How to close Corrected's gap to the dynamometer without fitting it. This is the working plan
behind `ISSUES.md` F8 and F9. `CORRECTIONS.md` governs any physics change it leads to, and
`validation/README.md` describes the suite it measures with.

## Where Corrected stands

After B94 (and B92, with B34 out of Corrected), the validation suite in Corrected gives (rms against the dynamometer, bias in brackets):

| Figure | Engine | Corrected | 2002 model |
|---|---|---|---|
| 6.5 to 6.7 | 2.4 L Nissan | 10.4 to 15.4 % (+3 to +15) | 0.4 to 2 % |
| 6.10 | 1.6 L eight-valve | 3.8 % (−0.8) | 2.4 % |
| 6.12 | 1.6 L five-valve, baseline | 18.5 % (+17.6) | 6.6 % |
| 6.13 | the same, CR 9.3 and 10.3, 4000 rpm timing loops | 22.2 and 20.8 % | 1.4 and 0.6 % |
| 6.14 | the same, 35.5 and 41 mm runners | 16.7 and 17.9 % | 4.9 and 5.0 % |
| 6.15 | the same, final build | 17.3 % | 5.8 % |
| T1, T2 | work per kilogram of air | +5.5, +9.3 and +2.8 % bias | |

Step 2 (A40) moved these away from the dynamometer by removing a late burn that had offset a
conversion excess. Step 3's combustion efficiency (B92) brought back about 3.5 points of it,
from physics the model lacked. Taking B34 out of Corrected, because its "defect" is the
thesis's own flame-geometry wall split (Table 2.1), gave about 1 point back.

Legacy, the original's physics, is 5.3 % on 6.12. It sits close to the dynamometer only because
its defects and its calibration offset each other (F6). Corrected removes the defects, so its
remaining gap is physics the model lacks or gets slightly wrong.

## Working assumption: many small errors, not one large one

Nothing found so far accounts for more than a few per cent. The corrections since B87 have each
moved the bias by 1 to 12 points, in both directions. A single torque figure cannot tell
several small errors apart, because they cancel and compound. So each candidate below is judged
against **its own measured quantity**, never against torque, and is fixed only when a physical
source says what the right answer is (`CORRECTIONS.md`). Torque is the sum, checked last.

## What the measurements already say

Against the five-valve engine's own test sheet (35.5 mm runners, at the test's spark and λ;
`ISSUES.md` F8):

| Speed | Air | Brake efficiency | Torque |
|---|---|---|---|
| below 3000 rpm | −5 to −16 % | +2 to +13 % | about −5 % |
| 3250 to 4250 rpm | +3 to +14 % | −7 to +5 % | about +6 % |
| 4500 rpm and above | +6 to +15 % | +8 to +28 %, rising with speed | about +27 % |

The eight-valve engine is about 14 % short of air at every speed. Three ways out are closed:
- **Heat transfer.** It is already above Woschni's own correlation, and neither `CWoshini` nor
  the wall temperatures can close the gap without implausible values (F9).
- **Mixture strength.** λ moves torque by under 0.5 % (F8).
- **Spark.** The 6.12 spark map is the test's own, and 6.14 now runs its tests' spark.

So two separate errors remain:
1. A **charge error that changes sign with speed**.
2. An **efficiency error that grows with speed**.

## The steps

Each step names its oracle. A step that changes physics goes behind the Legacy/Corrected switch
with that oracle as its test.

### 1. Measured channels beyond torque — *done*

The workbooks in `data/thesis/source/` hold, per speed:
- fuel flow, SFC and λ for the 35.5 mm five-valve test and for both eight-valve camshafts;
- spark advance (MBT, and the knock limit on the 41 mm test);
- exhaust temperature and pressure;
- blow-by.

None of it reaches the report. Extract it reproducibly into `data/thesis/tests/` and add two
channels to the validation report:
- **Air flow**, kg/h. Measured as fuel flow × λ × the files' `AFRatio`; simulated from the
  model's fuel flow in the same way.
- **Brake work per kilogram of air**, kJ/kg, which is brake power ÷ air flow. This is the
  efficiency channel. It is used instead of SFC because it barely depends on λ: within 1 % from λ 1
  to 0.92, measured in F8. SFC, by contrast, moves with how much excess fuel is burnt.

Torque ≈ air × work per air, so the two channels split the torque gap into breathing and
conversion at every speed of every test that has the data. *Done when the report shows both
channels for the 35.5 mm five-valve case and both eight-valve cams, against the test and, where
the workbooks have it, the 2002 model.*

**Done.** `validation/tools/extract_tests.py` writes `data/thesis/tests/`, and the report has
test figures T1 (five-valve, 35.5 mm) and T2 (eight-valve, cams A and B). On `main` after B34,
in Corrected:

| Channel | rms (bias) | 2002 model | Shape |
|---|---|---|---|
| T1 air flow | 9.9 % (+3.7) | 7.4 % (+4.0) | 5 to 16 % short below 3000 rpm, 3 to 15 % over above |
| T1 work per air | 9.7 % (+4.0) | 8.7 % (−7.9) | within 3 % to 3250 rpm; 4 to 9 % low at 3500 to 4250, where the test ran 13 to 19° of advance; 6 % high at 4500 rising to 26 % at 6500, where it ran 30 to 41° |
| T2 cam A air flow | 13.6 % (−12.5) | 7.8 % (+2.8) | short throughout |
| T2 cam A work per air | 5.7 % (−3.0) | 3.1 % (−0.6) | |
| T2 cam B air flow | 11.6 % (−8.0) | 13.3 % (+8.1) | |
| T2 cam B work per air | 10.1 % (−8.4) | 8.5 % (−7.1) | |

So 6.10's deficit is breathing, not conversion, and on the five-valve engine the conversion
error changes sign with the spark the test ran. The model loses more than the engine at a small
advance and less at a large one, which points step 2 at the burn's duration and how it scales with
speed before friction.

### 2. Where the high-speed efficiency excess comes from: MBT — *done*

The test needs 13 to 19° of advance at 4000 to 4500 rpm and 30 to 41° above 5000. The model
burns over a fixed 55°, starting at Heywood's flame-development angle (B90). Two candidates fit
an efficiency excess that grows with speed:
- **A burn too short in crank angle at high speed.** The engine loses less to a late burn in the
  model than it does in reality.
- **Too little mechanical friction at speed.** The test implies 2.2 to 3.6 bar above 4500 rpm,
  against B88's 1.5 to 1.9.

**Oracle**: the model's MBT spark at each speed, found by a spark sweep, against the test's
(the 41 mm sheet records MBT and the knock limit separately; the 6.7 and 6.13 timing loops
measure MBT too). If the model's MBT is earlier or later than the engine's, the burn rate is
wrong. If it matches, the remaining excess is friction, to be checked against Sandoval's
measured motoring friction (the source of B88).

**Measured** (`ISSUES.md` A40). Spark was swept from 4 to 56° at every speed on the 41 mm case,
over 8 cycles:

| | Model's MBT to 5000 rpm | Torque lost at the test's spark | Spark to 50 % burnt |
|---|---|---|---|
| The engine | 14 to 21° | — | about 22 to 31°, taking 50 % burnt at 7 to 10° ATDC at MBT (US 6,609,497; US 7,086,382) |
| Corrected | 27 to 40° | 4 to 18 % | 36 to 51° |
| Corrected with `B90=0` | within about 3° of the engine's, 2000 to 5000 rpm | under 2 % | 28° |
| Legacy | within 2 to 4° | — | 27.5° |

The timing loops of 6.7 and 6.13 agree: the engines peak at 15 to 20°, and Corrected is still
rising at the top of each sweep. So the first candidate was right but turned the other way.
**Corrected's burn is too slow, not too short.** B90's flame-development delay comes from a
part-load measurement and is far too long at full throttle. Its late burn has been costing torque
at the test's spark, which hid about half of the real conversion excess.

With `B90=0` the picture is simpler (validation suite, Corrected):
- work per air is 6 to 13 % high on both engines (T1 +8.8 %, cam A +12.6 %, cam B +6.0 %),
  roughly uniform below 5000 rpm;
- torque is +15 to +24 % on most figures;
- 6.10 is +3.6 %.

**Above 5250 rpm** the engine needs 31 to 39° to the model's 24 to 25°. The real burn lengthens
at high speed and the fixed 55° does not. That is a second, smaller burn-duration error, and no
full-load source has been found for it yet.

**Decided**: B90 is off in Corrected and on only by name (`B90=1`), recorded as `ISSUES.md` A40.
Running without it exposed A41: a NaN equilibrium solve was being accepted as resolved. Now
fixed, so every row runs. Steps 3 and the friction check face a conversion excess of about
10 %, uniform with speed below 5000 rpm. That shape suits a loss proportional to the fuel burnt,
such as crevices and unburnt fuel, more than friction, which grows with speed.

### 3. Losses the model does not have — *done*

After step 2 the conversion excess was about +5 to +10 % of brake work per kilogram of air,
roughly constant to 4500 rpm on both engines, plus 5 to 10 points more above it.

- **Combustion efficiency — done (`ISSUES.md` B92).** Heywood (1988, §3.5.5, Fig. 3-9, p. 82):
  a spark-ignition engine's combustion efficiency is "usually in the range 95 to 98 percent" with
  excess air, and is "little affected by other engine operating and design variables". The
  model's equilibrium burnt gas already has the fall on the rich side; what it lacked was that 2
  to 5 % shortfall. The burn now releases 96.5 % of the fuel's energy, the midpoint of the range,
  and the rest reaches the exhaust. It accounts for about 3.5 points of the constant part. Work
  per kilogram of air is now T1 +5.4 %, cam A +8.4 % and cam B +2.2 %; 6.10 is 5.6 % rms with a
  bias of −0.1.
- **Friction at speed — checked, not the cause; no change.** Heywood's Fig. 13-14a (p. 726, from
  Kovach, Tsakiris and Wong, SAE 820085) breaks down the motored friction of a 1.6 L
  four-cylinder spark-ignition engine, pumping excluded: read off the figure, about 69, 83, 108
  and 143 kPa at 1000, 2000, 3000 and 4000 rpm, of which crankshaft and seals 6 to 22, pistons,
  rings, pins and rods 27 to 70, valve train 24 to 22, oil pump about 4 to 6, water pump and
  alternator 8 to 18. B88 on the baseline engine, a 1.6 L four at wide-open throttle, gives 89,
  100, 116 and 134 kPa (crankshaft 21, reciprocating and ring gas loading 63, valvetrain 27,
  auxiliary 24 at 4000 rpm): within about 6 per cent of the measured total at 4000 rpm, the
  components each within a few kPa. The 8-valve case gives 81 to 125 kPa. Heywood's accessory
  figures (§13.7: water pump under 7 kPa, oil pump 4 to 10, alternator 7 to 10 at 1500 rpm) and
  its Eq. 13.6, the Barnes-Moss curve as a motored total *with* pumping, raise no objection.
  Against that, 6.12's excess is not a friction shape: about +2 bar of BMEP from 3250 rpm up
  (2.2 at 4000, 1.9 to 2.4 from 4500 to 6000), and within ±0.8 bar below 3000. To close 2 bar at
  4000 rpm friction would have to be two and a half times Kovach's measured 1.6 L engine. The
  figure stops at 4000 rpm; extrapolating its curve quadratically puts it 30 and 70 kPa above
  B88 at 5000 and 6000, worth 4 and 9 Nm, but that is extrapolation, not data, so it is no
  ground for a change. The step in the excess at 3250 rpm, where the inlet tuning comes in,
  points at the charge (step 4).
- **Crevices** are part of the combustion efficiency above; Namazian and Heywood (SAE 820088) is
  not needed separately.
- **Blow-by.** The thesis left it out as negligible for an engine in good condition (§2.7.5). The
  41 mm sheet measures it, in units it does not state, so it stays out until they are known.

### 4. The charge error

Corrected is short of air at low speed and over at high speed, and its torque curve swings
harder than the engine's (6.12 jumps 21 % at 3250 rpm). That points at wave dynamics that are
too strong or wrongly phased rather than at a uniform loss. In order of cost:
- **Inlet pressure trace.** Figure 6.8: Corrected 0.113 bar rms, the 2002 model 0.096.
- **Measured air flow.** From step 1.
- **Courant number per run (F5).** To rule numerical damping in or out.

Suspects:
- the valve discharge coefficients at high lift and speed;
- the inlet charge temperature (B65: without it, low-speed air goes from short to over);
- pipe friction (B49).

**Measured air flow, by speed** (Corrected after B34, against the test sheets): the five-valve
engine 6 to 16 % short from 1750 to 2750 rpm, then 3 to 16 % over from 3250 up; the eight-valve
14 to 22 % short above 4000 rpm, with work per kilogram of air 6 to 15 % over. So 6.10's good
torque was two errors cancelling, and the two engines breathed wrong in opposite directions.

**Which correction moves it.** The suite was run with each gas-exchange correction off in turn
(B4, B37, B46, B50, B54-B56, B59, B61, B62, B64, B65, B77, B79, B81-B85, B91). Only B65 moves
air flow materially: off, the five-valve goes from -10 to +5 % below 3100 rpm and the eight-valve
from -1 to +15 %. With B65 the charge comes in too hot, without it too cold, so the question was
the temperature of the inflowing charge.

**Found: mass created at the valves (`ISSUES.md` B93).** The inlet pipe is adiabatic, yet over a
converged cycle the stagnation temperature it delivered was 9 to 38 K above what it drew from the
plenum. Following the mass rather than the energy: the cylinder took up to 1.8 times what the
pipe's last node passed, because the boundaries solve with the valve area capped at the port's
and the mass flow used the whole valve area. Every validation engine's valves are larger than
their ports, inlet and exhaust. Fixed behind the switch; the cylinder now takes exactly what the
pipe delivered. Nissan 6.5 and 6.6 fall from 14 to 16 % to 10 to 12 %, 6.10 from 5.5 to 3.8 %;
the five-valve engine now over-breathes from 2500 to 4000 rpm (up to +27 % at 3000), which moves
6.12 to 18.9 % (+18.0). What remains there is a tuning resonance too strong, not a conservation
error.

**Found: blowdown carries too little into the exhaust (`ISSUES.md` B94).** While the exhaust flow
is choked, the pipe's first node received a third to a half of what the cylinder discharges: the
choked branch took its velocity from a momentum balance and its pressure from the characteristic,
and continuity gave. Fixed behind the switch, with continuity and the characteristic, the throat
unchoking when momentum asks for more than `p*`. The exhaust balances to 0.0 % on every engine;
the suite moves by 0.4 points or less (6.12 18.5 %, +17.6), so the blowdown pulse was not where
the five-valve's mid-speed over-breathing comes from.

**Next in this step.** With mass conserved at both valves, the five-valve engine still breathes 5
to 27 % too much from 2500 to 4000 rpm and the eight-valve 10 to 22 % too little from 3500 to
5000: a resonance too strong on one, too weak or mistimed on the other.

**The inlet pressure trace (Figure 6.8: Nissan, 310 mm runner, 5000 rpm, point A at the valve).**
Over the window ESA.NET records (0 to 579°) every model has the measured mean, 0.93 to 0.94 bar,
and the phase, to within about 10°, but not the amplitude. Standard deviation against the
measured trace's 0.145 bar: the 2002 model 1.39 times, Legacy 1.53, Corrected 1.71 (rms 0.076,
0.094 and 0.132 bar). Run with each gas-exchange correction off in turn, only B93 moves it (1.58
without), and physically: once the cylinder no longer gets mass for nothing, it draws harder on
the runner. So the waves are under-damped in the model's structure, the thesis's own included,
and conserving mass made that show.

**What damps them, checked.**
- The open end already follows Benson: inflow isentropic from the reservoir, outflow at its
  static pressure, so a wave reflecting there loses energy.
- Viscosity is air's (18.5 μPa·s at 300 K).
- The friction factor above Re = 10⁵, `0.04 Re^-0.16`, jumps 42 % above the Blasius branch it
  replaces (`ISSUES.md` B49). Runner flow is above 10⁵ much of the time, but the jump adds damping.
- The Courant number (F5) averages 0.72 to 0.85 on every validation case at 2000 to 6000 rpm,
  peaking at 1.02 to 1.09 for a few steps of peak flow: little numerical damping, and not
  where the excess comes from.

The pipe's only loss is quasi-steady wall friction, and it has no heat transfer. Adding damping
needs a published unsteady-friction or pipe heat-transfer model to cite, not a factor fitted to
6.8. That is the next decision in this step.

### 5. The dynamometer's correction convention

The figures plot torque corrected to a standard atmosphere (the sheets' `CorrFact` is 0.98 to
0.985). Each case runs at its own ambient (25 °C, 101.325 kPa). Establish the standard's
reference conditions from a source and run the cases at them. Likely 1 to 2 %, systematic, and
cheap to settle.

### 6. Correctness fixes that will not move the correlation

- **B66.** The `.exh` temperatures are the dynamometer's measured exhaust temperatures in
  Celsius: `ChinaBora98.exh` matches the 35.5 mm sheet's TExh, 674 at 3250 rpm in both. The
  original reads them as kelvin. Reading them as Celsius moves 6.10, 6.12 and 6.14 by under half
  a point of bias each, measured. Fix it behind the switch for correctness.
- **`validation/README.md`.** "A bias of −3.3 Nm" should read −3.3 %.

### Not on the path

- A8: about 0.1 % of IMEP.
- F7: the eight-valve cam B engine files are lost.
- F2: plenum pressure, about 1 %.

## Status

| Step | State |
|---|---|
| 1. Measured channels | Done: T1, T2 |
| 2. MBT | Done: B90 off in Corrected (A40); A41 fixed on the way |
| 3. Missing losses | Done: combustion efficiency (B92); friction checked against Heywood Fig. 13-14a, not the cause |
| 4. Charge error | In progress: B93 and B94 fixed (mass at the valves now conserved); waves 1.7 times too strong, damping model needs a source |
| 5. Correction convention | Not started |
| 6. Correctness fixes | Not started |
