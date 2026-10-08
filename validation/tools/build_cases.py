"""Builds validation/cases/: one self-contained folder per engine configuration.

    python3 validation/tools/build_cases.py

Every legacy file is copied byte for byte. Where a case needs a different value - a
compression ratio, an inlet manifold - a derived .eng is written beside the copy, with a
leading comment naming its source and the change; nothing else in it moves. The run
points are .msr multi-run grids, in the format the application reads and writes.

The cases and the reasoning behind each are in validation/README.md. Re-running this
script must reproduce the committed folder exactly.
"""
import os
import re
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'validation', 'cases')

NISSAN = 'legacy/ESA/Data/Example1/Nissan'
EX2 = 'legacy/ESA/Data/Example2'
BASELINE = 'data/baseline'
CAEENG = 'legacy/CAEEng'

SIDE_KEYS = ('TempFile', 'AreaFile', 'ExhBackFile', 'IVProfile', 'EVProfile',
             'CdIVIn', 'CdEVIn', 'CdIVOut', 'CdEVOut', 'SparkAngle')

CYCLES = 8


def rpm_rows(speeds, **cells):
    return [dict(speed=s, **cells) for s in speeds]


# A grid row: speed, and optionally spark (°BTDC), ivo/ivc/evo/evc (the .eng's own
# convention), ilift/elift (mm). Columns follow MultiRunGrid.ColumnNames.
COLUMNS = ['speed', 'cycles', 'imanf', 'emanf', 'icam', 'ecam', 'ivo', 'ivc', 'evo', 'evc',
           'ilift', 'elift', 'spark', 'burn']

CAM_A = dict(ivo=23, ivc=66, evo=51, evc=38, ilift=8.6, elift=8.6)    # Table 6.3, 026 109 101M
CAM_B = dict(ivo=29, ivc=75, evo=71, evc=33, ilift=10, elift=10)      # Table 6.3, 026 109 101L
CAM_SPEEDS = [1500, 2000, 2500, 2800, 3000, 3500, 4000, 4500, 5000, 5300, 5500]

CASES = {
    # 6.4: the Nissan test engine. Tests 4 to 7 of Table 6.1.
    'Nissan_290': dict(source=f'{NISSAN}/Nissan6.eng', grids={
        'torque': rpm_rows([2000, 3000, 4000, 5000])}),
    'Nissan_390': dict(source=f'{NISSAN}/Nissan5.eng', grids={
        'torque': rpm_rows([2000, 3000, 4000, 5000]),
        'pulse5000': rpm_rows([5000])}),
    'Nissan_490': dict(source=f'{NISSAN}/Nissan4.eng', grids={
        'torque': rpm_rows([2000, 3000, 4000, 5000])}),
    'Nissan_290_Adv12': dict(source=f'{NISSAN}/Nissan7.eng', grids={
        'torque': rpm_rows([2000, 3000, 4000, 5000]),
        'timing2000': [dict(speed=2000, spark=a) for a in range(10, 31, 2)]}),
    # 6.5: the VW eight-valve engine, from the 1999 CAEEng folder, unchanged.
    # The 1999 file is in the older schema, whose plenum pressure (kPa), wall
    # temperatures (Celsius) and fixed grid point counts ([Calculation]) the loader
    # translates as the predecessor's Edit.pas did (ISSUES.md A31-A33).
    'VW8V_A4LowCost': dict(source=f'{CAEENG}/A4LowCost.eng',
        grids={
        'torque': rpm_rows(range(1500, 6001, 500)),
        'camA': rpm_rows(CAM_SPEEDS, **CAM_A),
        'camB': rpm_rows(CAM_SPEEDS, **CAM_B)}),
    # 6.6: the VW five-valve engine.
    'A2China_Baseline': dict(source=f'{BASELINE}/A2China.eng', grids={
        'torque': rpm_rows(range(1500, 6251, 250))}),
    'ChinaBora_CR93': dict(source=f'{EX2}/ChinaBora92.eng', derive={('Cylinders', 'CR'): '9.3'},
                           grids={'timing4000': [dict(speed=4000, spark=a) for a in range(8, 25, 2)]}),
    'ChinaBora_CR103': dict(source=f'{EX2}/ChinaBora92.eng', derive={('Cylinders', 'CR'): '10.3'},
                            grids={'timing4000': [dict(speed=4000, spark=a) for a in range(8, 25, 2)]}),
    'ChinaBora98_Dia355': dict(source=f'{EX2}/ChinaBora98.eng',
                               derive={('Inlet', 'AreaFile'): 'A3TumbleInlet_M770_355.maf'},
                               grids={'torque': rpm_rows(range(1500, 6501, 250))}),
    'ChinaBora98_Dia41': dict(source=f'{EX2}/ChinaBora98.eng',
                              derive={('Inlet', 'AreaFile'): 'A3TumbleInlet_M770_41.maf'},
                              grids={'torque': rpm_rows(range(1500, 6501, 250))}),
    'ChinaBora98_Final': dict(source=f'{EX2}/ChinaBora98.eng', grids={
        'torque': rpm_rows(range(1500, 6501, 250))}),
}


def read_lines(path):
    with open(path, 'rb') as f:
        data = f.read()
    eol = b'\r\n' if b'\r\n' in data else b'\n'
    return data, eol


def entries(data):
    """(section, key, value) for every key=value line."""
    section = None
    for raw in data.decode('latin-1').splitlines():
        line = raw.strip()
        if line.startswith('[') and line.endswith(']'):
            section = line[1:-1]
        elif '=' in line and not line.startswith(';'):
            k, v = line.split('=', 1)
            yield section, k.strip(), v.strip()


def find_side_file(source_dir, stored):
    """The legacy resolver's rule: as written, relative, or by bare name beneath the
    engine's folder - here also beneath its parent data folder."""
    name = re.split(r'[\\/]', stored)[-1]
    rel = os.path.join(source_dir, *re.split(r'[\\/]', stored))
    if os.path.isfile(rel):
        return rel
    for base in (source_dir, os.path.dirname(source_dir)):
        for dirpath, _, files in sorted(os.walk(base)):
            if name in files:
                return os.path.join(dirpath, name)
    raise SystemExit(f'{stored} not found for {source_dir}')


def derive(data, eol, changes, source_rel, additions=None):
    text = data.decode('latin-1')
    lines = text.split(eol.decode())
    section = None
    done = set()
    for i, line in enumerate(lines):
        s = line.strip()
        if s.startswith('[') and s.endswith(']'):
            section = s[1:-1]
        elif '=' in s:
            k = s.split('=', 1)[0].strip()
            for (sec, key), value in changes.items():
                if sec == section and k.lower() == key.lower() and (sec, key) not in done:
                    lines[i] = f'{k}={value}'
                    done.add((sec, key))
    missing = set(changes) - done
    if missing:
        raise SystemExit(f'{source_rel}: no {missing}')
    additions = additions or {}
    while lines and lines[-1] == '':
        lines.pop()
    for sec in dict.fromkeys(s for s, _ in additions):
        lines += ['', f'[{sec}]'] + [f'{k}={v}' for (s, k), v in additions.items() if s == sec]
    lines.append('')
    what = ', '.join(f'[{s}] {k}={v}' for (s, k), v in {**changes, **additions}.items())
    header = f'; Validation case derived from {source_rel}: {what}. Nothing else differs.'
    return (header + eol.decode() + eol.decode().join(lines)).encode('latin-1')


def grid_text(rows):
    out = []
    for n in range(100):
        cells = ['-'] * 14
        if n < len(rows):
            row = dict(cycles=CYCLES, **rows[n])
            for col, value in row.items():
                cells[COLUMNS.index(col)] = f'{value:g}' if isinstance(value, (int, float)) else value
        out.append(','.join([str(n + 1)] + cells))
    return ('\r\n'.join(out) + '\r\n').encode('ascii')


def main():
    if os.path.isdir(OUT):
        shutil.rmtree(OUT)
    for name, case in CASES.items():
        folder = os.path.join(OUT, name)
        os.makedirs(folder)
        source = os.path.join(ROOT, case['source'])
        source_dir = os.path.dirname(source)
        data, eol = read_lines(source)
        names = []
        changes = case.get('derive', {})
        additions = case.get('add', {})
        replaced = {(sec.lower(), key.lower()) for sec, key in changes}
        for section, key, value in entries(data):
            if ((section or '').lower(), key.lower()) in replaced:
                continue
            if key in SIDE_KEYS and value and not re.fullmatch(r'-?[\d.]+', value):
                names.append(value)
        names += [v for (s, k), v in changes.items() if k in SIDE_KEYS]
        for stored in names:
            found = find_side_file(source_dir, stored)
            shutil.copyfile(found, os.path.join(folder, os.path.basename(found)))
        derived = bool(changes or additions)
        engine_name = name + '.eng' if derived else os.path.basename(source)
        with open(os.path.join(folder, engine_name), 'wb') as f:
            f.write(derive(data, eol, changes, case['source'], additions) if derived else data)
        for grid, rows in case['grids'].items():
            with open(os.path.join(folder, grid + '.msr'), 'wb') as f:
                f.write(grid_text(rows))
        print(name, engine_name, sorted(os.listdir(folder)))


if __name__ == '__main__':
    main()
