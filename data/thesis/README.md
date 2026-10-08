# Thesis correlation data

Measured dynamometer data from the thesis that produced ESA
(`legacy/Thesis 02-11-11_03.pdf`), for checking the simulation against an engine on a test
bed rather than against itself. `data/baseline/` checks the port against the original
program; this folder checks both against the engine. Treat every file in it as read-only.

## `Figure6_12.csv`

**Source:** page 118, Figure 6.12, "Simulated and measured baseline engine performance",
from section 6.6 (design project 2). The engine is the baseline 1.6 L five-valve
four-cylinder before development, the engine `data/baseline/A2China.eng` describes ("A2
China Jetta 1.6L 5V Baseline").

| Column | Meaning |
|---|---|
| `rpm` | Engine speed |
| `measured_nm` | "Test Results": the dynamometer's brake torque |
| `thesis_simulated_nm` | "Simulated Performance": what ESA predicted when the thesis was written |

20 measured speeds from 1500 to 6250 rpm, every 250 rpm. The simulated series also has a
point at 5800 rpm and no measured one there.

**How it was read.** The chart is vector graphics, not an image, so the values are the
plotted coordinates themselves, not a digitisation by eye:

- The page's content stream (object 689) was decompressed and the two series' path
  operators read off: a red polyline with square markers and a blue one with triangle
  markers.
- The axes were calibrated from the tick-label positions in the same stream. The plot area
  runs from x = 141.96 at 1000 rpm to 468.00 at 7000 rpm, and from y = 419.36 at 80 Nm to
  595.10 at 160 Nm, giving `rpm = 1000 + (x - 141.96) / 326.04 * 6000` and
  `torque = 80 + (y - 419.36) / 175.74 * 80`.
- The legend lists "Test Results" first, and its marker on the upper legend line is the red
  square, so red is measured and blue is simulated.

The values are good to the chart's own plotting precision, about 0.1 Nm and 1 rpm. The rpm
column is rounded to the nearest 50.

**What the thesis says about it.** The model "had captured the primary aspects of the
engine operation". The deviation below 3000 rpm is put down to the fixed crank-angle step,
which makes the manifold model's time step too large at low speed (section 7.3 recommends
fixing it). So the correlation is meant from about 2500 rpm upward.

## `figures/`: every correlation in sections 6.4 to 6.6

`figures/Figure6_NN.csv` holds every series the thesis plots against the dynamometer, one
file per figure, written by `validation/tools/digitise_thesis.py` from the PDF's vector
graphics by the method above. The script calibrates each chart's axes from its tick marks
and tick labels instead of by hand, and re-running it reproduces the files exactly. Its
`Figure6_12.csv` agrees with the hand extraction above to 0.05 Nm.

Columns: `series` (the legend label as printed), `role` (`measured` for dynamometer data,
`thesis_model` for what ESA predicted in 2002), `x`, `y`. The units are the figure's own
axes; `validation/suite.json` names them.

| Figure | Page | x | y | Engine |
|---|---|---|---|---|
| 6.5 | 109 | rpm | torque, Nm | 2.4 L Nissan test engine, three inlet lengths |
| 6.6 | 109 | rpm | torque, Nm | the same, standard and 12° advanced cam |
| 6.7 | 110 | spark advance, °BTDC | torque, Nm | the same, timing loop |
| 6.8 | 111 | crank angle, ° | inlet pressure at point A, bar | the same, 5000 rpm, 310 mm |
| 6.10 | 114 | rpm | torque, Nm, and power, kW | VW 1.6 L eight-valve, final manifold |
| 6.11 | 115 | rpm | torque change, Nm | the same, cam A to cam B |
| 6.12 | 118 | rpm | torque, Nm | VW 1.6 L five-valve baseline |
| 6.13 | 119 | spark advance, °BTDC | torque, Nm | the same, CR 9.3 and 10.3 |
| 6.14 | 120 | rpm | torque, Nm | the same, 35.5 and 41 mm runners |
| 6.15 | 120 | rpm | torque, Nm, and power, kW | the same, final prototype |

Power series (6.10, 6.15) are read against the right-hand axis. Figure 6.8's two traces are
every vertex of the plotted polyline, about 680 each. Figure 6.13's "10.3 Simulated" is a
smoothed curve, so its vertices are not model points; every other model series is the
model's own points.

## `source/`: the spreadsheets the figures were drawn from

The author's working spreadsheets, recovered from his backup (Google Drive,
`Data6_Testing/`) in October 2026 and committed unchanged. Every plotted value checked
against them agrees to the chart's precision, and they settle questions the thesis text
leaves open.

| File | Figure | What it adds |
|---|---|---|
| `Test Config Data.xls` | Table 6.1, 6.2 | The test matrix and the Nissan engine data, as in the thesis |
| `ModellingPerformance_Final.xls` | 6.5, 6.6, 6.7 | Model and test tables per inlet length. The tests had cylinder 1's inlet open to atmosphere and the custom exhaust, so they are tests 4 to 7 of Table 6.1, not 1 to 3. The model's spark advance at each speed equals the `.spk` maps of `legacy/ESA/Data/Example1/Nissan/Nissan4.eng` to `Nissan7.eng`, which fixes the mapping of cases to engine files. The measured timing loop is at 2000 rpm |
| `China Torque Kick Up Investigation.xls` | 6.10 | Predicted and measured torque and power, 1500-6000 rpm, and the model's crank-angle traces at each speed. Cam profile "050 B" |
| `China Cam Effect Modelling.xls` | 6.11 | Model and test runs of four camshafts on the 630 mm aluminium manifold. Figure 6.11 is camshaft 026 109 101L minus 026 109 101M: cam A is 101M (profile 026 109 113B), cam B is 101L (profile 026 109 113D). The engine files it names are not in the backup |
| `China_5V Baseline Modelling.xls` | 6.12 | The model run behind Figure 6.12: `A2China.eng`, `A2ChinaInlet_M758.maf`, `A2ChinaVar.spk`, and the measured curve with the spark advance used at each speed |
| `China_5V Model vs Test Diameters.xls` | 6.14 | The model was `ChinaBora98.eng` (CR 9.72) with `A3TumbleInlet_M770_355.maf` and `A3TumbleInlet_M770_41.maf`, on `ChinaBora98.spk`; the engine tested measured CR 9.715 on 95 RON. The thesis text's "686 mm" and "10.3:1" describe the project, not this comparison |

Two more spreadsheets exist in the backup and are not here, because the connector returned
them only as text that could not be saved byte for byte: `China_5V Compression Ratio and
Timing.xls` (Figure 6.13; its sheets are 9.3:1 and 9.8:1, with series 9.3, 9.8 and 10.3
Simulated) and `China_5V Final Model vs Test.xls` (Figure 6.15; its model sheets are
`Model_770_36`, `Model_770_37` and `Model_760_37`). Figures 6.13 and 6.15 are taken from
the PDF alone.
