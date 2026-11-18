"""Kiểm 300 level bằng LevelValidator (C# thuần, cùng luật lõi với game) — M4.

Trước đây file này là solver Python riêng (chỉ level thuần). Từ M4 nó chỉ gọi test ValidatorTests (dotnet, không cần Unity)
rồi in báo cáo; luật mechanic nằm một chỗ ở Buzzy.Core, không viết lại bằng Python để khỏi lệch.
Dùng: python tools/solve_levels.py [--src <thư mục Levels>] [--dotnet <dotnet.exe>]
Báo cáo đầy đủ: CoreTests/bin/.../level-report.md (và .game/research/level-report.md khi chạy tool Unity Validate Levels).
"""
import argparse
import glob
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", help="thư mục level_*.json (mặc định: bộ gốc trong Downloads)")
    ap.add_argument("--dotnet", default=os.environ.get("DOTNET_EXE", r"C:/Users/PC/.dotnet/dotnet.exe"))
    a = ap.parse_args()
    env = dict(os.environ)
    if a.src:
        env["BUZZY_LEVELS"] = a.src
    cmd = [a.dotnet, "test", os.path.join(ROOT, "CoreTests"), "--filter", "FullyQualifiedName~ValidatorTests", "--logger", "console;verbosity=normal"]
    r = subprocess.run(cmd, env=env, capture_output=True, text=True, encoding="utf-8", errors="replace")
    tail = (r.stdout or "").strip().splitlines()[-6:]
    print("\n".join(tail))
    reps = glob.glob(os.path.join(ROOT, "CoreTests", "bin", "**", "level-report.md"), recursive=True)
    if reps:
        rep = max(reps, key=os.path.getmtime)
        print("Báo cáo:", rep.replace("\\", "/"))
        with open(rep, encoding="utf-8") as f:
            print("".join(f.readlines()[:14]))
    return r.returncode


if __name__ == "__main__":
    sys.exit(main())
