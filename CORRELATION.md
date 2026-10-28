# Correlation plan

How to close Corrected's gap to the dynamometer without fitting it. This is the working plan
behind `ISSUES.md` F8 and F9. `CORRECTIONS.md` governs any physics change it leads to, and
`validation/README.md` describes the suite it measures with.

## Where Corrected stands

After B92, the validation suite in Corrected gives (rms against the dynamometer, bias in brackets):

| Figure | Engine | Corrected | 2002 model |
|---|---|---|---|
| 6.5 to 6.7 | 2.4 L Nissan | 13 to 14 % (+12 to +14) | 0.4 to 2 % |
| 6.10 | 1.6 L eight-valve | 5.6 % (−0.1) | 2.4 % |
| 6.12 | 1.6 L five-valve, baseline | 16.7 % (+13.4) | 6.6 % |
| 6.13 | the same, CR 9.3 and 10.3, 4000 rpm timing loops | 19.8 and 18.2 % | 1.4 and 0.6 % |
| 6.14 | the same, 35.5 and 41 mm runners | 18.0 and 16.1 % | 4.9 and 5.0 % |
| 6.15 | the same, final build | 14.8 % | 5.8 % |
| T1, T2 | work per kilogram of air | +5.4, +8.4 and +2.2 % bias | |

Step 2 (A40) moved these away from the dynamometer by removing a late burn that had offset a
conversion excess. Step 3's combustion efficiency (B92) brought back about 3.5 points of it,
from physics the model lacked.

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

### 3. Losses the model does not have — *combustion efficiency done; friction next*

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
- **Friction at speed — next.** The excess still grows above 4500 rpm. The test implies 0.7 to
  1.7 bar more friction there than B88 gives, and B88's bearing sizes and ring factors are
  unsourced defaults. The check is against measured motored friction (Heywood ch. 13, or
  Sandoval's thesis, which B88 is built on).
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
| 3. Missing losses | Combustion efficiency done (B92); friction at speed next |
| 4. Charge error | Not started |
| 5. Correction convention | Not started |
| 6. Correctness fixes | Not started |
