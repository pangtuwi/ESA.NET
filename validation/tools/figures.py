"""Recovers the plotted series of the thesis charts from the PDF's vector graphics.

For each chart on a page: the frame (the chart's outer rectangle), the axes calibrated
from their tick marks and tick labels, and every coloured series with its legend label,
as data coordinates. Nothing is digitised by eye; see data/thesis/README.md.
"""
import re
from dataclasses import dataclass, field
from pdfvec import read_page

BLACK = (0.0, 0.0, 0.0)
WHITE = (1.0, 1.0, 1.0)


def _inside(bb, frame, pad=1.0):
    return (bb[0] >= frame[0] - pad and bb[1] >= frame[1] - pad
            and bb[2] <= frame[2] + pad and bb[3] <= frame[3] + pad)


@dataclass
class Axis:
    ticks: list      # page coordinate of each tick
    values: list     # data value of each tick

    def to_data(self, p):
        # Linear least squares through the (tick, value) pairs.
        n = len(self.ticks)
        mx = sum(self.ticks) / n; my = sum(self.values) / n
        sxx = sum((t - mx) ** 2 for t in self.ticks)
        sxy = sum((t - mx) * (v - my) for t, v in zip(self.ticks, self.values))
        b = sxy / sxx
        return my + b * (p - mx)

    def residual(self):
        return max(abs(self.to_data(t) - v) for t, v in zip(self.ticks, self.values))


@dataclass
class Series:
    colour: tuple
    label: str | None
    line: list = field(default_factory=list)      # polyline vertices, page coords
    markers: list = field(default_factory=list)   # marker centres, page coords
    every: list = field(default_factory=list)     # every segment end, for dense traces
    dashed: bool = False


@dataclass
class Chart:
    frame: tuple
    x: Axis
    y: Axis
    y2: Axis | None
    series: list
    texts: list


def _is_rect(pts):
    return len(pts) == 5 and len({round(p[0], 1) for p in pts}) == 2 and len({round(p[1], 1) for p in pts}) == 2


def _dedupe(points, tol=0.3):
    out = []
    for p in points:
        if not any(abs(p[0] - q[0]) < tol and abs(p[1] - q[1]) < tol for q in out):
            out.append(p)
    return out


def _has_label(texts, x, y):
    return any(abs(ty - y) < 4 and 0 < x0 - x < 25 for x0, x1, ty, s in texts)


def _label(texts, at):
    lx, ly = at
    line = sorted((x0, x1, s) for x0, x1, y, s in texts if abs(y - ly) < 4 and 0 < x0 - lx < 200)
    label, last = [], None
    for x0, x1, s in line:
        if last is None and x0 - lx > 25:
            break
        if last is not None and x0 - last > 8:
            break
        label.append(s); last = x1
    return ' '.join(label) or None


def _num(s):
    s = s.replace(',', '.').replace('−', '-')
    return float(s) if re.fullmatch(r'-?\d+(\.\d+)?', s) else None


def _words(pdf, page_number):
    """Word boxes from pdftotext, as (centre x, centre y, text) in PDF coordinates.

    pdftotext positions every glyph run; the raw content stream does not carry an
    explicit position for each Tj, so its text is not used.
    """
    import subprocess, html
    out = subprocess.run(['pdftotext', '-f', str(page_number), '-l', str(page_number), '-bbox', pdf, '-'],
                         capture_output=True, text=True, check=True).stdout
    height = float(re.search(r'<page width="[\d.]+" height="([\d.]+)"', out).group(1))
    words = []
    for m in re.finditer(r'<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">(.*?)</word>', out):
        x0, y0, x1, y1 = (float(m.group(i)) for i in range(1, 5))
        words.append((x0, x1, height - (y0 + y1) / 2, html.unescape(m.group(5))))
    return words


def charts(pdf, page_number):
    page = read_page(pdf, page_number)
    words = _words(pdf, page_number)
    frames = [p.bbox() for p in page.paths
              if p.stroke == BLACK and not p.dash and len(p.subpaths) == 1 and len(p.points()) == 5
              and p.bbox()[2] - p.bbox()[0] > 250 and p.bbox()[3] - p.bbox()[1] > 150]
    # One frame per chart; the same rectangle is often drawn twice.
    unique = []
    for f in frames:
        if not any(all(abs(a - b) < 1 for a, b in zip(f, u)) for u in unique):
            unique.append(f)
    unique = [f for f in unique if not any(g != f and g[0] <= f[0] + 1 and g[1] <= f[1] + 1
                                           and g[2] >= f[2] - 1 and g[3] >= f[3] - 1 for g in unique)]
    result = []
    for frame in unique:
        paths = [p for p in page.paths if _inside(p.bbox(), frame)]
        texts = [w for w in words if frame[0] - 1 <= w[0] and w[1] <= frame[2] + 1 and frame[1] - 1 <= w[2] <= frame[3] + 1]
        result.append(_chart(frame, paths, texts))
    return result


def _chart(frame, paths, texts):
    # Tick stubs: black, undashed two-point segments shorter than 5 pt.
    hticks, vticks = [], []
    for p in paths:
        if p.stroke != BLACK or p.dash:
            continue
        for sp in p.subpaths:
            if len(sp) != 2:
                continue
            (x0, y0), (x1, y1) = sp
            if abs(y0 - y1) < 0.01 and 0 < abs(x0 - x1) < 6:
                hticks.append((min(x0, x1), max(x0, x1), y0))
            elif abs(x0 - x1) < 0.01 and 0 < abs(y0 - y1) < 6:
                vticks.append((x0, min(y0, y1), max(y0, y1)))
    nums = [((x0 + x1) / 2, y, _num(s), x0, x1) for x0, x1, y, s in texts if _num(s) is not None]

    def pair(ticks, labels):
        # Each tick to the label centred nearest it; unlabelled (minor) ticks drop out.
        ts, vs = [], []
        for t in ticks:
            near = min(labels, key=lambda l: abs(l[0] - t), default=None)
            if near and abs(near[0] - t) < 6:
                ts.append(t); vs.append(near[1])
        if len(ts) < 2:
            raise ValueError(f'axis: ticks {ticks} labels {labels}')
        return Axis(ts, vs)

    def y_axis(side):
        if not hticks:
            return None
        xs = sorted({round(t[1] if side == 'left' else t[0], 1) for t in hticks})
        axis_x = xs[0] if side == 'left' else xs[-1]
        if side == 'right' and len(xs) == 1:
            return None
        ts = sorted({round(t[2], 2) for t in hticks
                     if abs((t[1] if side == 'left' else t[0]) - axis_x) < 0.5})
        if side == 'left':
            labels = [(y, v) for cx, y, v, x0, x1 in nums if axis_x - 45 < x1 < axis_x]
        else:
            labels = [(y, v) for cx, y, v, x0, x1 in nums if axis_x < x0 < axis_x + 40]
        return pair(ts, labels)

    def x_axis():
        axis_y = min(round(t[2], 1) for t in vticks)
        ts = sorted({round(t[0], 2) for t in vticks if abs(t[2] - axis_y) < 0.5})
        labels = [(cx, v) for cx, y, v, x0, x1 in nums if axis_y - 18 < y < axis_y - 2]
        return pair(ts, labels)

    x = x_axis(); y = y_axis('left')
    try:
        y2 = y_axis('right')
    except ValueError:
        y2 = None

    # Series by colour, from segments rather than paths: Excel splits one curve
    # across several paths and draws markers as small closed shapes or as stars of
    # short strokes from a centre.
    segs, closed = {}, {}
    for p in paths:
        c = p.stroke or p.fill
        if c == WHITE:
            continue
        if c == BLACK:
            # Black is the axes and grid, except a polyline of many points, which is
            # a series drawn in black, and the two-point legend sample beside it.
            if p.dash:
                continue
            if len(p.subpaths) == 1 and len(p.points()) > 6 and not _is_rect(p.points()):
                segs.setdefault(c, []).extend(zip(p.points(), p.points()[1:]))
            elif len(p.subpaths) == 1 and len(p.points()) == 2:
                (a, b) = p.points()
                if abs(a[1] - b[1]) < 0.01 and 9 < abs(a[0] - b[0]) < 15:
                    segs.setdefault(c, []).append((a, b))
            continue
        for sp in p.subpaths:
            xs = [q[0] for q in sp]; ys = [q[1] for q in sp]
            small = max(xs) - min(xs) < 8 and max(ys) - min(ys) < 8
            if len(sp) > 3 and small:
                closed.setdefault(c, []).append(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2))
            else:
                for a, b in zip(sp, sp[1:]):
                    segs.setdefault(c, []).append((a, b))
    result = []
    for c in set(segs) | set(closed):
        s = Series(c, None)
        markers = list(closed.get(c, []))
        stars = {}
        long_pts = []
        legend = None
        for a, b in segs.get(c, []):
            length = ((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5
            if length < 4:
                stars.setdefault((round(a[0], 1), round(a[1], 1)), 0)
                stars[(round(a[0], 1), round(a[1], 1))] += 1
            elif abs(a[1] - b[1]) < 0.01 and 9 < length < 15 and _has_label(texts, max(a[0], b[0]), a[1]):
                legend = ((a[0] + b[0]) / 2, a[1])
            else:
                long_pts += [a, b]
        markers += [k for k, n in stars.items() if n >= 3]
        if legend is None:
            # A legend drawn as a marker alone, with no sample line.
            for m in markers:
                if _has_label(texts, m[0] + 3, m[1]) and not any(abs(m[0] - q[0]) < 1 for q in long_pts):
                    legend = m
        if legend:
            s.label = _label(texts, legend)
            markers = [m for m in markers if abs(m[0] - legend[0]) > 1 or abs(m[1] - legend[1]) > 1]
        s.markers = _dedupe(markers)
        s.every = sorted(_dedupe([q for a, b in segs.get(c, []) for q in (a, b)
                                  if not legend or abs(q[1] - legend[1]) > 1]))
        s.line = sorted(_dedupe(long_pts))
        result.append(s)
    return Chart(frame, x, y, y2, result, texts)


def data(chart, series, which='auto', axis='y'):
    """The series in data units: markers if it has them, else the polyline vertices."""
    if which == 'every':
        pts = series.every
    else:
        pts = series.markers if (which == 'markers' or (which == 'auto' and series.markers)) else series.line
    ya = chart.y2 if axis == 'y2' else chart.y
    return sorted((chart.x.to_data(px), ya.to_data(py)) for px, py in pts)
