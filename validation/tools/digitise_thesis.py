"""Writes data/thesis/figures/Figure6_NN.csv: every series the thesis plots in sections
6.4 to 6.6, read from the PDF's vector graphics.

    python3 validation/tools/digitise_thesis.py     (needs pypdf and pdftotext)

Each CSV has the columns series, role, x, y. `role` is `measured` for dynamometer data
and `thesis_model` for what ESA predicted when the thesis was written. The x and y units
are the figure's own axes; validation/suite.json names them.

The values are the plotted coordinates, calibrated from each chart's tick marks and tick
labels, so they are good to the chart's plotting precision. Re-running this script must
reproduce the committed files exactly; Figure 6.12 is also checked against
data/thesis/Figure6_12.csv, which was extracted independently.
"""
import csv
import os
import sys

from figures import charts, data

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
PDF = os.path.join(ROOT, 'legacy', 'Thesis 02-11-11_03.pdf')
OUT = os.path.join(ROOT, 'data', 'thesis', 'figures')

# figure: (page, chart index from the top of the page, [(legend label, role, points, axis)])
# `points` is 'auto' (markers if drawn, else the curve's vertices), 'line' or 'every'.
FIGURES = {
    '6_05': (109, 0, [
        ('210mm Test', 'measured', 'auto', 'y'),
        ('310mm Test', 'measured', 'auto', 'y'),
        ('410mm Test', 'measured', 'auto', 'y'),
        ('210mm Model', 'thesis_model', 'auto', 'y'),
        ('310mm Model', 'thesis_model', 'auto', 'y'),
        ('410mm Model', 'thesis_model', 'auto', 'y'),
    ]),
    '6_06': (109, 1, [
        ('Std. Cam Tested', 'measured', 'auto', 'y'),
        ('12°Adv Cam Tested', 'measured', 'auto', 'y'),
        ('Std. Cam Modelled', 'thesis_model', 'auto', 'y'),
        ('12°Adv Cam Modelled', 'thesis_model', 'auto', 'y'),
    ]),
    '6_07': (110, 0, [
        ('Measured', 'measured', 'auto', 'y'),
        ('Modelled', 'thesis_model', 'auto', 'y'),
    ]),
    '6_08': (111, 0, [
        ('Test', 'measured', 'every', 'y'),
        ('Model', 'thesis_model', 'every', 'y'),
    ]),
    '6_10': (114, 0, [
        ('Measured Torque', 'measured', 'auto', 'y'),
        ('Measured Power', 'measured', 'auto', 'y2'),
        ('Modelled Torque', 'thesis_model', 'auto', 'y'),
        ('Modelled Power', 'thesis_model', 'auto', 'y2'),
    ]),
    '6_11': (115, 0, [
        ('Dynamometer Measured', 'measured', 'auto', 'y'),
        ('Model Predicted', 'thesis_model', 'auto', 'y'),
    ]),
    '6_12': (118, 0, [
        ('Test Results', 'measured', 'auto', 'y'),
        ('Simulated Performance', 'thesis_model', 'auto', 'y'),
    ]),
    '6_13': (119, 0, [
        ('9.3 Measured', 'measured', 'markers', 'y'),
        ('10.3 Measured (103RON)', 'measured', 'auto', 'y'),
        ('9.3 Simulated', 'thesis_model', 'auto', 'y'),
        ('10.3 Simulated', 'thesis_model', 'line', 'y'),
    ]),
    '6_14': (120, 0, [
        ('Dynamometer Measured: 35.5mm', 'measured', 'auto', 'y'),
        ('Dynamometer Measured: 41mm', 'measured', 'auto', 'y'),
        ('ESA Modeled: 35.5mm', 'thesis_model', 'auto', 'y'),
        ('ESA Modeled 41mm', 'thesis_model', 'auto', 'y'),
    ]),
    '6_15': (120, 1, [
        ('Dynamometer Measured Torque', 'measured', 'auto', 'y'),
        ('Dynamometer Measured Power', 'measured', 'auto', 'y2'),
        ('Modeled Torque', 'thesis_model', 'auto', 'y'),
        ('Modeled Power', 'thesis_model', 'auto', 'y2'),
    ]),
}

# Legend labels as printed are kept in the CSV, except where the PDF text is clumsy.
NAMES = {'ESA Modeled 41mm': 'ESA Modeled: 41mm'}


def series_points(chart, label, which, axis):
    matches = [s for s in chart.series if s.label == label]
    if not matches:
        raise SystemExit(f'no series labelled {label!r}; have {[s.label for s in chart.series]}')
    # Excel sometimes draws a series' line and markers in two colours; both carry the label.
    for want in ([which] if which != 'auto' else ['markers', 'line']):
        for s in matches:
            pts = data(chart, s, want, axis)
            if pts:
                return pts
    raise SystemExit(f'{label!r} has no points')


def main():
    os.makedirs(OUT, exist_ok=True)
    pages = {}
    for fig, (page, index, series) in FIGURES.items():
        if page not in pages:
            pages[page] = sorted(charts(PDF, page), key=lambda c: -c.frame[1])
        chart = pages[page][index]
        rows = []
        for label, role, which, axis in series:
            for x, y in series_points(chart, label, which, axis):
                rows.append((NAMES.get(label, label), role, f'{x:.2f}', f'{y:.3f}'))
        path = os.path.join(OUT, f'Figure{fig}.csv')
        with open(path, 'w', newline='') as f:
            w = csv.writer(f, lineterminator='\n')
            w.writerow(['series', 'role', 'x', 'y'])
            w.writerows(rows)
        print(f'{path}: {len(rows)} points; axis residuals x {chart.x.residual():.3g}, '
              f'y {chart.y.residual():.3g}' + (f', y2 {chart.y2.residual():.3g}' if chart.y2 else ''))
    check_6_12()


def check_6_12():
    """The independent extraction of Figure 6.12 must agree to its own rounding."""
    ref = list(csv.DictReader(open(os.path.join(ROOT, 'data', 'thesis', 'Figure6_12.csv'))))
    got = list(csv.DictReader(open(os.path.join(OUT, 'Figure6_12.csv'))))
    worst = 0.0
    for role, col in (('measured', 'measured_nm'), ('thesis_model', 'thesis_simulated_nm')):
        pts = [(float(r['x']), float(r['y'])) for r in got if r['role'] == role]
        for r in ref:
            if not r[col]:
                continue
            x, y = min(pts, key=lambda p: abs(p[0] - float(r['rpm'])))
            if abs(x - float(r['rpm'])) > 30:
                sys.exit(f'Figure 6.12 {role}: no point near {r["rpm"]} rpm')
            worst = max(worst, abs(y - float(r[col])))
    if worst > 0.051:
        sys.exit(f'Figure 6.12 disagrees with Figure6_12.csv by {worst:.3f} Nm')
    print(f'Figure 6.12 agrees with Figure6_12.csv to {worst:.3f} Nm')


if __name__ == '__main__':
    main()
