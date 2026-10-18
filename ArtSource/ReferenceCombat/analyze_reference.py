"""Extract timestamped contact sheets from the local combat reference (no source upload)."""
import argparse
import json
from pathlib import Path

import cv2
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument('video')
parser.add_argument('--start', type=float, default=0)
parser.add_argument('--end', type=float)
parser.add_argument('--step', type=float, default=6)
parser.add_argument('--label', default='overview')
args = parser.parse_args()
cap = cv2.VideoCapture(args.video)
fps = cap.get(cv2.CAP_PROP_FPS)
duration = cap.get(cv2.CAP_PROP_FRAME_COUNT) / fps
folder = Path('Library/ReferenceCombat')
folder.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 17)
tiles = []
time = args.start
while time < min(duration, args.end if args.end is not None else duration):
    cap.set(cv2.CAP_PROP_POS_MSEC, time * 1000)
    ok, frame = cap.read()
    if ok:
        picture = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)).resize((320, 180))
        tile = Image.new('RGB', (320, 206), '#161b23')
        tile.paste(picture, (0, 26))
        ImageDraw.Draw(tile).text((8, 2), f'{int(time // 60):02}:{time % 60:05.2f} / {time:.2f}s', font=font, fill='white')
        tiles.append(tile)
    time += args.step
cap.release()
for page in range((len(tiles) + 19) // 20):
    sheet = Image.new('RGB', (1600, 824), '#161b23')
    for index, tile in enumerate(tiles[page * 20:(page + 1) * 20]):
        sheet.paste(tile, ((index % 5) * 320, (index // 5) * 206))
    path = folder / f'{args.label}_{page + 1:02}.jpg'
    sheet.save(path, quality=90)
    print(path)
print(json.dumps({'duration': duration, 'fps': fps, 'samples': len(tiles)}))
