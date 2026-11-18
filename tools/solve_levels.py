"""Solver luật lõi Buzzy Bus trên level JSON gốc (kiểm luật lõi giải được).

Luật (teardown.md mục 2-3, design-decisions.md mục 2):
- 3 ô bến, 3 cột xe; xe đầu mỗi cột vào ô cùng chỉ số; cột hết xe -> ô bị rào.
- Chạm màu c: cần xe cùng màu c trong ô với còn >0; gửi tối đa 'còn' pixel màu c (mọi pixel của màu đều lấy được,
  người dùng chốt 2026-11-18), thứ tự gần ô bến trước.
- Xe đủ -> rời, xe kế của cột xuống. Hết pixel = thắng. Bế tắc = thua.
Chỉ mô phỏng level thuần (không Walls, Spawners, Keys, Frozen, Surprise, xe khóa / 2 màu).
Dùng: python tools/solve_levels.py [--src <thư mục Levels>] [--tries 200] [--levels 1-59]
"""
import argparse
import glob
import json
import os
import random
import sys

SRC = "C:/Users/PC/Contacts/Downloads/Buzzy Bus Resource/Buzzy Bus/Levels"


def pure(ld):
    p = ld["PixelImageData"]
    if p.get("Walls") or p.get("Spawners") or p.get("Keys") or p.get("FrozenAreas") or p.get("Elevators"):
        return False
    if p.get("SurprisePixels", {}).get("Pixels"):
        return False
    if ld.get("QueueBoardGatePairs"):
        return False
    for q in ld["CarQueueGroup"]["CarQueues"]:
        for c in q["Cars"]:
            if c["IsLocked"] or c["SecondSeats"] or c["SwapPairId"] or c["HammerId"]:
                return False
    return True


def load(path):
    d = json.load(open(path, encoding="utf-8"))
    ld = d["LevelData"]
    p = ld["PixelImageData"]
    board = {(q["x"], q["y"]): q["ColorCode"] for q in p["Pixels"]}
    queues = [[(c["ColorCode"], c["Seats"]) for c in q["Cars"]] for q in ld["CarQueueGroup"]["CarQueues"]]
    return d["levelIndex"], p["Width"], p["Height"], board, queues, pure(ld)


def exposed(board, W, H):
    """Pixel lấy được theo màu. Luật chốt 2026-11-18: mọi pixel của màu đều lấy được (không chỉ pixel lộ);
    lớp ngoài + gần xe chỉ là thứ tự. Chặn (Frozen, Wall...) thêm ở M3."""
    out = {}
    for (x, y), c in board.items():
        out.setdefault(c, []).append((x, y))
    return out


def simulate(W, H, board0, queues0, rng, mode):
    board = dict(board0)
    queues = [list(q) for q in queues0]
    slots = [None, None, None]  # [color, remaining]
    nslots = len(queues)
    for i in range(nslots):
        if queues[i]:
            c, s = queues[i].pop(0)
            slots[i] = [c, s]
    steps = 0
    while board:
        ex = exposed(board, W, H)
        opts = [(i, s) for i, s in enumerate(slots) if s and s[1] > 0 and s[0] in ex]
        if not opts:
            return False, steps  # bế tắc
        if mode == "greedy":
            i, s = max(opts, key=lambda t: t[1][1])
        else:
            i, s = rng.choice(opts)
        c = s[0]
        sx = (i + 0.5) * (W + 1) / nslots
        cand = sorted(ex[c], key=lambda p: (abs(p[0] - sx), -p[1]))
        for p in cand[: s[1]]:
            del board[p]
        took = min(len(cand), s[1])
        s[1] -= took
        steps += 1
        if s[1] == 0:
            if queues[i]:
                nc, ns = queues[i].pop(0)
                slots[i] = [nc, ns]
            else:
                slots[i] = None
    return True, steps


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=SRC)
    ap.add_argument("--tries", type=int, default=200)
    ap.add_argument("--levels", default="")
    a = ap.parse_args()
    lo, hi = 1, 10 ** 6
    if a.levels:
        lo, hi = (int(x) for x in a.levels.split("-"))
    files = sorted(glob.glob(os.path.join(a.src, "level_*.json")))
    rng = random.Random(1)
    n_pure = n_ok = n_skip = 0
    bad = []
    rows = []
    for f in files:
        idx, W, H, board, queues, ok_pure = load(f)
        if not (lo <= idx <= hi):
            continue
        if not ok_pure:
            n_skip += 1
            continue
        n_pure += 1
        tot_seats = sum(s for q in queues for _, s in q)
        won, steps = simulate(W, H, board, queues, rng, "greedy")
        wins = 1 if won else 0
        tries = 1
        if not won:
            for _ in range(a.tries):
                tries += 1
                w, st = simulate(W, H, board, queues, rng, "random")
                wins += 1 if w else 0
                if w:
                    won = True
                    break
        if won:
            n_ok += 1
        else:
            bad.append(idx)
        rows.append((idx, len(board), tot_seats, won, steps, tries))
    print(f"level thuần mô phỏng: {n_pure} · thắng: {n_ok} · bế tắc mọi lần thử: {len(bad)} · bỏ qua (có mechanic phụ): {n_skip}")
    if bad:
        print("level không giải được:", bad)
    print("idx pixel seats thắng bước lần-thử")
    for r in rows[:60]:
        print(*r)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
