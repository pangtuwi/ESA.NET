#!/usr/bin/env python3
"""Regenerates the application icons from the original ESA icon.

The source is legacy/ESA/FERRARI.ICO, the 32x32 icon compiled into ESA.exe as
MAINICON. It is pixel art, so sizes above 32 are scaled up nearest-neighbour to
keep the pixels square and sharp; sizes below are averaged down.

Writes:
    src/App.Ui/Assets/ESA.ico    window and taskbar icon, and the Windows .exe icon
    packaging/macos/ESA.icns     the ESA.app bundle icon

Needs Pillow (pip install pillow). Run from anywhere:
    python3 packaging/icon/make-icons.py
"""

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "legacy" / "ESA" / "FERRARI.ICO"


def scaled(source: Image.Image, size: int) -> Image.Image:
    if size >= source.width:
        return source.resize((size, size), Image.NEAREST)
    return source.resize((size, size), Image.BOX)


def main() -> None:
    source = Image.open(SOURCE).convert("RGBA")

    ico_sizes = [16, 24, 32, 48, 64, 128, 256]
    ico = [scaled(source, size) for size in ico_sizes]
    ico[-1].save(
        ROOT / "src" / "App.Ui" / "Assets" / "ESA.ico",
        sizes=[(size, size) for size in ico_sizes],
        append_images=ico[:-1],
    )

    icns_sizes = [16, 32, 64, 128, 256, 512, 1024]
    icns = [scaled(source, size) for size in icns_sizes]
    icns[-1].save(ROOT / "packaging" / "macos" / "ESA.icns", append_images=icns[:-1])


if __name__ == "__main__":
    main()
