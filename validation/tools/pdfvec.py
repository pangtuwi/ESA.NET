"""Low-level reader for the vector charts in the thesis PDF.

Walks a page's content stream and returns what it draws: text runs with their
positions, and stroked or filled paths with their colours, all in page
coordinates (points, origin bottom left). The thesis charts are Word/Excel vector
graphics, so the plotted values are recoverable exactly, to the chart's own
plotting precision - see data/thesis/README.md.
"""
from dataclasses import dataclass, field
from pypdf import PdfReader
from pypdf.generic import ContentStream


def _mul(a, b):
    # 2-D affine matrices as [a b c d e f]; returns a x b (apply a then b).
    return [a[0]*b[0] + a[1]*b[2], a[0]*b[1] + a[1]*b[3],
            a[2]*b[0] + a[3]*b[2], a[2]*b[1] + a[3]*b[3],
            a[4]*b[0] + a[5]*b[2] + b[4], a[4]*b[1] + a[5]*b[3] + b[5]]


def _apply(m, x, y):
    return (m[0]*x + m[2]*y + m[4], m[1]*x + m[3]*y + m[5])


@dataclass
class Path:
    subpaths: list            # list of lists of (x, y)
    stroke: tuple | None      # rgb if stroked
    fill: tuple | None        # rgb if filled
    width: float
    dash: tuple

    def points(self):
        return [p for sp in self.subpaths for p in sp]

    def bbox(self):
        pts = self.points()
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        return min(xs), min(ys), max(xs), max(ys)


@dataclass
class Text:
    x: float
    y: float
    text: str


@dataclass
class Page:
    paths: list = field(default_factory=list)
    texts: list = field(default_factory=list)


def read_page(pdf_path, page_number):
    """page_number is 1-based, as pdftotext and a PDF viewer count."""
    reader = PdfReader(pdf_path)
    page = reader.pages[page_number - 1]
    ops = ContentStream(page.get_contents(), reader).operations
    out = Page()
    ctm = [1, 0, 0, 1, 0, 0]
    stack = []
    stroke = (0, 0, 0); fill = (0, 0, 0); width = 1.0; dash = ()
    subpaths = []; current = None
    tm = [1, 0, 0, 1, 0, 0]; tlm = [1, 0, 0, 1, 0, 0]

    def num(v):
        return float(v)

    for operands, op in ops:
        op = op.decode() if isinstance(op, bytes) else op
        if op == 'q':
            stack.append((ctm[:], stroke, fill, width, dash))
        elif op == 'Q':
            ctm, stroke, fill, width, dash = stack.pop()
        elif op == 'cm':
            ctm = _mul([num(v) for v in operands], ctm)
        elif op in ('RG', 'SCN', 'SC') and len(operands) == 3:
            stroke = tuple(num(v) for v in operands)
        elif op in ('rg', 'scn', 'sc') and len(operands) == 3:
            fill = tuple(num(v) for v in operands)
        elif op in ('G',):
            g = num(operands[0]); stroke = (g, g, g)
        elif op in ('g',):
            g = num(operands[0]); fill = (g, g, g)
        elif op == 'w':
            width = num(operands[0])
        elif op == 'd':
            dash = tuple(num(v) for v in operands[0])
        elif op == 'm':
            current = [_apply(ctm, num(operands[0]), num(operands[1]))]
            subpaths.append(current)
        elif op == 'l':
            if current is None:
                current = []; subpaths.append(current)
            current.append(_apply(ctm, num(operands[0]), num(operands[1])))
        elif op == 'c':
            current.append(_apply(ctm, num(operands[4]), num(operands[5])))
        elif op in ('v', 'y'):
            current.append(_apply(ctm, num(operands[2]), num(operands[3])))
        elif op == 're':
            x, y, w, h = (num(v) for v in operands)
            current = [_apply(ctm, x, y), _apply(ctm, x + w, y), _apply(ctm, x + w, y + h),
                       _apply(ctm, x, y + h), _apply(ctm, x, y)]
            subpaths.append(current)
        elif op == 'h':
            if current:
                current.append(current[0])
        elif op in ('S', 's', 'f', 'F', 'f*', 'B', 'B*', 'b', 'b*'):
            if subpaths:
                st = stroke if op in ('S', 's', 'B', 'B*', 'b', 'b*') else None
                fl = fill if op in ('f', 'F', 'f*', 'B', 'B*', 'b', 'b*') else None
                out.paths.append(Path([sp for sp in subpaths if sp], st, fl, width, dash))
            subpaths = []; current = None
        elif op == 'n':
            subpaths = []; current = None
        elif op == 'BT':
            tm = [1, 0, 0, 1, 0, 0]; tlm = tm[:]
        elif op == 'Tm':
            tm = [num(v) for v in operands]; tlm = tm[:]
        elif op in ('Td', 'TD'):
            tlm = _mul([1, 0, 0, 1, num(operands[0]), num(operands[1])], tlm); tm = tlm[:]
        elif op in ('Tj', "'", '"', 'TJ'):
            if op == 'TJ':
                s = ''.join(str(t) for t in operands[0] if not isinstance(t, (int, float)) and not hasattr(t, 'as_numeric'))
            else:
                s = str(operands[-1])
            x, y = _apply(_mul(tm, ctm), 0, 0)
            out.texts.append(Text(x, y, s))
    return out
