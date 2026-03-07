#!/usr/bin/env python3
"""Trim light-colored borders from specific bible icon PNGs.

Detects the content bounding box by finding where pixels transition
from light border to actual content, then crops and saves in-place.
"""

import os
import sys
import numpy as np
from PIL import Image

IMG_DIR = os.path.join(os.path.dirname(__file__), '..', 'docs', 'assets', 'img', 'bible')

# Only process these — identified by visual inspection
TARGETS = [
    'electrical_hazard.png',
    'factory_efficiency.png',
    'honest_almost_systems.png',
    'scavenger_trader.png',
    'scrap_helmet.png',
    'signal_decoder.png',
    'splitter_t1.png',
]

LIGHT_THRESHOLD = 200  # pixel brightness above this = "border"


def find_content_box(arr, threshold=LIGHT_THRESHOLD):
    """Find bounding box of non-light content."""
    gray = arr[:, :, :3].mean(axis=2)
    mask = gray < threshold

    rows = np.any(mask, axis=1)
    cols = np.any(mask, axis=0)

    if not rows.any() or not cols.any():
        return 0, 0, arr.shape[1], arr.shape[0]

    top = np.argmax(rows)
    bottom = len(rows) - np.argmax(rows[::-1])
    left = np.argmax(cols)
    right = len(cols) - np.argmax(cols[::-1])

    return left, top, right, bottom


def crop_to_square(img):
    """Center-crop a non-square image to square."""
    w, h = img.size
    if w == h:
        return img
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    return img.crop((left, top, left + side, top + side))


def process(filename):
    path = os.path.join(IMG_DIR, filename)
    if not os.path.exists(path):
        print(f'  SKIP {filename} — not found')
        return False

    img = Image.open(path)
    orig_size = img.size
    arr = np.array(img)

    left, top, right, bottom = find_content_box(arr)

    # Add a small margin (2px) so we don't clip content
    h, w = arr.shape[:2]
    left = max(0, left - 2)
    top = max(0, top - 2)
    right = min(w, right + 2)
    bottom = min(h, bottom + 2)

    cropped = img.crop((left, top, right, bottom))
    cropped = crop_to_square(cropped)

    # Resize back to 1024x1024 for consistency
    cropped = cropped.resize((1024, 1024), Image.LANCZOS)

    cropped.save(path)
    print(f'  {filename}: {orig_size[0]}x{orig_size[1]} -> crop({left},{top},{right},{bottom}) -> 1024x1024')
    return True


def main():
    print(f'Cropping {len(TARGETS)} bible images...\n')

    count = 0
    for filename in TARGETS:
        if process(filename):
            count += 1

    print(f'\nDone: {count} images cropped')


if __name__ == '__main__':
    main()
