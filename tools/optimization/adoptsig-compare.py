"""Compare complete SF adopt signatures. Standard library only; bounded streaming memory.

Position/pivot tolerance: 0.01 screen pixel (per-row projection factors).
Scale: relative 1e-4; color/opacity: 1/255. Rotation/parallax: absolute 1e-4.
One neighbor frame per decoration must satisfy ALL channels together.
"""
import argparse
import array
from collections import deque
import gzip
import heapq
import json
import math
import struct
import sys

NAMES = ["positionX", "positionY", "pivotX", "pivotY", "rotation", "scaleX", "scaleY",
         "red", "green", "blue", "alpha", "opacity", "parallaxX", "parallaxY",
         "parallaxOffsetX", "parallaxOffsetY", "screenX", "screenY", "pixelPerWorldX", "pixelPerWorldY",
         "corner00X", "corner00Y", "corner10X", "corner10Y", "corner01X", "corner01Y",
         "rendererRed", "rendererGreen", "rendererBlue", "rendererAlpha", "rendererEnabled",
         "rendererForceOff", "rendererActive", "spritePresent", "rendererPresent"]
CHANNELS = 35
COMPARED = list(range(18)) + list(range(20, CHANNELS))


class Signature:
    def __init__(self, path):
        self.file = gzip.open(path, "rb")
        self.header = json.loads(self.file.readline(8_000_000))
        h = self.header
        if h.get("schema") != 8 or h.get("channels") != CHANNELS or not h.get("indices"):
            raise ValueError("invalid signature schema/empty ownership")
        if not 20 <= h["hz"] <= 1000 or len(h["indices"]) != len(set(h["indices"])):
            raise ValueError("invalid Hz/duplicate decoration index")
        self.n = len(h["indices"])

    def exact(self, count):
        data = self.file.read(count)
        if len(data) != count:
            raise ValueError("truncated signature")
        return data

    def frames(self):
        count, high, heap = 0, -math.inf, []
        while True:
            marker = self.exact(1)
            if marker == b"\0":
                complete, expected = struct.unpack("<?i", self.exact(5))
                if not complete or count == 0 or count != expected or self.file.read(1):
                    raise ValueError("incomplete/empty/invalid signature footer")
                break
            if marker != b"\1" or count >= 2000:
                raise ValueError("invalid frame marker/frame limit")
            time, tb, frame = struct.unpack("<ddi", self.exact(20))
            if not math.isfinite(time) or not math.isfinite(tb) or time < high - .05:
                raise ValueError("nonfinite clock or seek inside capture")
            values = array.array("f")
            values.frombytes(self.exact(self.n * CHANNELS * 4))
            if sys.byteorder != "little":
                values.byteswap()
            if not all(map(math.isfinite, values)):
                raise ValueError("nonfinite captured value")
            high = max(high, time)
            heapq.heappush(heap, (time, count, values))
            count += 1
            # Reorder audio-clock jitter, bounded to 50 ms.
            while heap and heap[0][0] < high - .05:
                yield heapq.heappop(heap)
        while heap:
            yield heapq.heappop(heap)

    def close(self):
        self.file.close()


def ratio(a, b, offset, c):
    x, y = a[offset+c], b[offset+c]
    if c in (0, 1, 2, 3):
        axis = 18 + (c & 1)
        pixels = max(a[offset+axis], b[offset+axis])
        if pixels <= 0:
            raise ValueError("invalid world-to-pixel factor")
        tolerance = .01 / pixels
    elif c in (5, 6):
        tolerance = 1e-4 * max(abs(x), abs(y), 1e-8)
    elif 7 <= c <= 11 or 26 <= c <= 29:
        tolerance = 1/255
    elif c in (16, 17) or 20 <= c <= 25:
        tolerance = .01
    elif c >= 30:
        return 0 if x == y else math.inf
    else:
        tolerance = 1e-4
    return abs(x-y) / tolerance


def compare_pass(left_path, right_path, direction, worst, start=-math.inf, end=math.inf):
    left, right = Signature(left_path), Signature(right_path)
    try:
        for key in ("schema", "channels", "fingerprint", "indices", "hz"):
            if left.header[key] != right.header[key]:
                raise ValueError("signature contract differs: " + key)
        if left.header.get("adopt") == right.header.get("adopt"):
            raise ValueError("need one adopt-off and one adopt-on capture")
        neighbors, iterator = deque(), iter(right.frames())
        future = next(iterator, None)
        period = 1 / left.header["hz"]
        frames = matched = failures = 0
        for time, serial, values in left.frames():
            if time < start or time > end:
                continue
            frames += 1
            while future is not None and future[0] <= time + period + 1e-9:
                neighbors.append(future)
                future = next(iterator, None)
            while neighbors and neighbors[0][0] < time - period - 1e-9:
                neighbors.popleft()
            if not neighbors:
                failures += left.n * len(COMPARED)
                item = (math.inf, direction, time, -1, "time-coverage", time, math.nan, math.nan)
                heapq.heappush(worst, item)
                if len(worst) > 20:
                    heapq.heappop(worst)
                continue
            matched += 1
            for d, decoration in enumerate(left.header["indices"]):
                offset, best, chosen = d*CHANNELS, math.inf, None
                for neighbor in neighbors:
                    scores = [ratio(values, neighbor[2], offset, c) for c in COMPARED]
                    score = max(scores)
                    if chosen is None or score < best:
                        best, chosen = score, (neighbor, scores)
                if best <= 1:
                    continue
                neighbor, scores = chosen
                for c, score in zip(COMPARED, scores):
                    if score <= 1:
                        continue
                    failures += 1
                    item = (score, direction, time, decoration, NAMES[c], neighbor[0],
                            values[offset+c], neighbor[2][offset+c])
                    heapq.heappush(worst, item)
                    if len(worst) > 20:
                        heapq.heappop(worst)
        # Consume footer and reject corruption even beyond the overlap.
        for _ in iterator:
            pass
        if frames == 0 or matched == 0:
            raise ValueError("no aligned sample frames")
        print(f"{direction}: frames={frames} aligned={matched} O={left.n} channelFailures={failures} window=±{period*1000:.6g}ms")
        return failures
    finally:
        left.close()
        right.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("off")
    parser.add_argument("on")
    parser.add_argument("--start", type=float, default=-math.inf, help="compare only this song-time interval; still validate both complete files")
    parser.add_argument("--end", type=float, default=math.inf)
    args = parser.parse_args()
    worst = []
    try:
        if math.isnan(args.start) or math.isnan(args.end) or args.start >= args.end:
            raise ValueError("invalid comparison interval")
        failures = compare_pass(args.off, args.on, "off->on", worst, args.start, args.end)
        failures += compare_pass(args.on, args.off, "on->off", worst, args.start, args.end)
        for score, direction, time, d, channel, nearby, x, y in sorted(worst, reverse=True):
            print(f"{direction} t={time:.9g} near={nearby:.9g} decoration={d} {channel}: {x:.9g} / {y:.9g}, toleranceRatio={score:.6g}")
        print("PASS" if failures == 0 else "FAIL")
        return int(failures != 0)
    except (ValueError, KeyError, EOFError, OSError, struct.error) as error:
        print("INVALID: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
