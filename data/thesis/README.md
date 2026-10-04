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

## Not used

- **Figure 6.10** (page 114) is development engine 1, a VW 1.6 L EA113 eight-valve engine.
  Its input files are on the thesis's CD and not in this repository.
- **Figures 6.13 to 6.15** (pages 119-120) are later prototypes of the five-valve engine:
  a compression-ratio change, two inlet diameters and the final engine. They may match
  some of the `legacy/ESA/Data/Example2` engines, but nothing records which.
