"""Writes data/thesis/tests/: the dynamometer test sheets and 2002 model runs held in the
thesis's source workbooks, for the validation report's measured channels beyond torque.

    python3 validation/tools/extract_tests.py     (needs xlrd 1.2, which reads .xls)

Two kinds of file:

- One table per test sheet (FiveValve_355.csv, FiveValve_41.csv, EightValve_101M.csv,
  EightValve_101L.csv): a row per speed, the sheet's own values under common column names,
  blank where the sheet has no such column.
- One chart file per test figure of validation/suite.json (T1.csv, T2.csv), in the columns
  of data/thesis/figures/ (series, role, x, y): air flow and brake work per kilogram of air,
  measured and as the 2002 model ran them.

Air flow, measured, is fuel flow x lambda x 14.5, the AFRatio every case's .eng declares. The
2002 model's is its mixture inducted (MIn) less its fuel, which is how ESA divides a charge;
its runs were at lambda 1. Re-running this script must reproduce the committed files exactly.
See CORRELATION.md, step 1, and data/thesis/README.md.
"""
import csv
import math
import os

import xlrd

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
SOURCE = os.path.join(ROOT, 'data', 'thesis', 'source')
OUT = os.path.join(ROOT, 'data', 'thesis', 'tests')

AFR = 14.5
CYLINDERS = 4

COLUMNS = ['rpm', 'torque_nm', 'torque_corrected_nm', 'power_kw', 'fuel_kg_h', 'sfc_g_kwh',
           'lambda', 'spark_btdc', 'knock_limit_btdc', 'exhaust_t_c', 'exhaust_p_mbar', 'blowby']

# file: (workbook, sheet, header row, first data row, {our column: the sheet's})
SHEETS = {
    'FiveValve_355': ('China_5V Model vs Test Diameters.xls', '35_5 Test Data', 15, 16, {
        'rpm': 'Speed', 'torque_nm': 'Torque', 'torque_corrected_nm': 'TorqueDI',
        'power_kw': 'Power.DI', 'fuel_kg_h': 'Fuel Cons', 'sfc_g_kwh': 'SFC', 'lambda': 'Lambda',
        'spark_btdc': 'MBT', 'exhaust_t_c': 'TExh', 'exhaust_p_mbar': 'PExh'}),
    'FiveValve_41': ('China_5V Model vs Test Diameters.xls', '41 Test Data', 15, 16, {
        'rpm': 'Speed', 'torque_nm': 'Torque', 'torque_corrected_nm': 'Torque ECE',
        'power_kw': 'Power ECE', 'lambda': 'Lambda', 'spark_btdc': 'MBT',
        'knock_limit_btdc': 'KLSA', 'exhaust_t_c': 'TExh', 'exhaust_p_mbar': 'PExh',
        'blowby': 'Blowby6.'}),
    'EightValve_101M': ('China Cam Effect Modelling.xls', '101M', 5, 7, {
        'rpm': 'Speed', 'torque_nm': 'Torque', 'power_kw': 'Power', 'fuel_kg_h': 'Flow',
        'sfc_g_kwh': 'S.F.C.', 'lambda': 'Lambda'}),
    'EightValve_101L': ('China Cam Effect Modelling.xls', '101L', 5, 7, {
        'rpm': 'Speed', 'torque_nm': 'Torque', 'power_kw': 'Power', 'fuel_kg_h': 'Flow',
        'sfc_g_kwh': 'S.F.C.', 'lambda': 'Lambda'}),
}

# 2002 model runs: (workbook, sheet, header row, first data row, last data row)
MODELS = {
    '355': ('China_5V Model vs Test Diameters.xls', 'Model_770_355', 11, 12, 35),
    '101M': ('China Cam Effect Modelling.xls', '026109101M', 5, 6, 18),
    '101L': ('China Cam Effect Modelling.xls', '026109101L', 5, 6, 18),
}

# chart file: [(series, measured table, model run)]
CHARTS = {
    'T1': [('35.5 mm', 'FiveValve_355', '355')],
    'T2': [('Cam A', 'EightValve_101M', '101M'), ('Cam B', 'EightValve_101L', '101L')],
}


def fmt(v):
    return '' if v is None else '%.10g' % v


def read_table(workbook, sheet, header_row, first_row, columns):
    s = xlrd.open_workbook(os.path.join(SOURCE, workbook)).sheet_by_name(sheet)
    header = [str(c.value).strip() for c in s.row(header_row)]
    rows = []
    for r in range(first_row, s.nrows):
        cells = s.row(r)
        speed = cells[header.index(columns['rpm'])].value
        # Each sheet ends in placeholder rows of zeros.
        if not isinstance(speed, float) or speed <= 0:
            continue
        row = {}
        for ours, theirs in columns.items():
            value = cells[header.index(theirs)].value
            row[ours] = value if isinstance(value, float) else None
        rows.append(row)
    return sorted(rows, key=lambda row: row['rpm'])


def read_model(workbook, sheet, header_row, first_row, last_row):
    s = xlrd.open_workbook(os.path.join(SOURCE, workbook)).sheet_by_name(sheet)
    header = [str(c.value).strip() for c in s.row(header_row)]
    rows = []
    for r in range(first_row, last_row + 1):
        cells = {h: s.cell_value(r, i) for i, h in enumerate(header) if h}
        rpm = cells['Speed']
        mixture = cells['MIn'] * 1e-6 * CYLINDERS * rpm / 2 * 60   # kg/h through the inlet valves
        air = mixture - cells['mf']
        rows.append((rpm, air, cells['Power'] / (air / 3600)))
    return rows


def main():
    os.makedirs(OUT, exist_ok=True)
    tables = {}
    for name, (workbook, sheet, header_row, first_row, columns) in SHEETS.items():
        rows = read_table(workbook, sheet, header_row, first_row, columns)
        tables[name] = rows
        with open(os.path.join(OUT, name + '.csv'), 'w', newline='') as f:
            w = csv.writer(f, lineterminator='\n')
            w.writerow(COLUMNS)
            for row in rows:
                w.writerow([fmt(row.get(c)) for c in COLUMNS])

    for chart, series in CHARTS.items():
        lines = []
        for label, table, model in series:
            for row in tables[table]:
                if row.get('fuel_kg_h') is None or row.get('lambda') is None:
                    continue
                air = row['fuel_kg_h'] * row['lambda'] * AFR
                power = row['power_kw'] if table.startswith('EightValve') \
                    else row['torque_nm'] * row['rpm'] * 2 * math.pi / 60 / 1000
                lines.append((label + ' air flow', 'measured', row['rpm'], air))
                lines.append((label + ' work per air', 'measured', row['rpm'], power / (air / 3600)))
            for rpm, air, work in read_model(*MODELS[model]):
                lines.append((label + ' air flow', 'thesis_model', rpm, air))
                lines.append((label + ' work per air', 'thesis_model', rpm, work))
        with open(os.path.join(OUT, chart + '.csv'), 'w', newline='') as f:
            w = csv.writer(f, lineterminator='\n')
            w.writerow(['series', 'role', 'x', 'y'])
            for s, role, x, y in lines:
                w.writerow([s, role, fmt(x), f'{y:.4f}'])
        print(chart, len(lines), 'points')


if __name__ == '__main__':
    main()
