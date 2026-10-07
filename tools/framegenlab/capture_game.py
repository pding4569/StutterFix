"""Install section-12 probe for one fresh game, preserve disk backups, restore in finally."""
import argparse
import csv
import json
import shutil
from pathlib import Path
from measure_maps import sf

HERE = Path(__file__).resolve().parent


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--map", type=Path, required=True)
    p.add_argument("--seconds", type=float, default=15)
    p.add_argument("--out", type=Path, required=True)
    p.add_argument("--ui", action="store_true", help="One temporary overlay-to-UI-camera render, restored immediately")
    args = p.parse_args()
    if not args.map.is_file() or not 5 <= args.seconds <= 120 or sf.game_running():
        raise RuntimeError("Existing map, 5..120 seconds and closed game required")
    mod = Path(sf.MOD_DIR)
    args.out.mkdir(parents=True, exist_ok=False)
    original_dll = (mod / "StutterFix.dll").read_bytes()
    original_settings = (mod / "Settings.xml").read_bytes()
    (args.out / "installed-original.dll").write_bytes(original_dll)
    (args.out / "Settings.xml.original").write_bytes(original_settings)
    destination = mod / "framegen-capture"
    ui_flag = mod / "framegen-ui-experiment.txt"
    old_flag = ui_flag.read_bytes() if ui_flag.exists() else None
    if destination.exists():
        raise RuntimeError("Preserve/move previous framegen-capture before a new run")
    try:
        if args.ui:
            ui_flag.write_text("one diagnostic overlay UI render", encoding="utf-8")
        elif ui_flag.exists():
            ui_flag.unlink()
        (mod / "StutterFix.dll").write_bytes((HERE / "out" / "capture" / "StutterFix.dll").read_bytes())
        summary, _ = sf.sf_run(["game " + str(args.map), "auto on", "press",
                                "wait " + str(args.seconds), "quit"],
                               settings={"FrameStats": False, "LowHalfRender": False},
                               tag="framegen-capture", timeout_min=6)
        log = sf.read_text(sf.PLAYER_LOG)
        (args.out / "game.log").write_text(log, encoding="utf-8")
        (args.out / "run.txt").write_text(summary, encoding="utf-8")
        if "시간 초과로 끔" in summary or "단계 실패" in log or "[안정성] 안전 모드로 켬" in log:
            raise RuntimeError("Failed game run; do not interpret capture")
        shutil.copytree(destination, args.out / "capture")
        with (destination / "frames.csv").open(newline="", encoding="utf-8-sig") as f:
            rows = list(csv.DictReader(f))
        gaps = sum(int(b["frame"]) != int(a["frame"]) + 1 for a, b in zip(rows, rows[1:]))
        result = dict(frames=len(rows), frame_gaps=gaps,
                      rendered_frame_matches=sum(r["frame"] == r["main_render_frame"] for r in rows),
                      overlay_before_main=sum(0 < int(r["overlay_order"]) < int(r["main_order"]) for r in rows),
                      ranges={key: [min(float(r[key]) for r in rows), max(float(r[key]) for r in rows)]
                              for key in ["camera_x", "camera_y", "q_z", "ortho_size", "red_x", "red_y", "blue_x", "blue_y"]},
                      sample=[rows[i] for i in [0, len(rows)//2, len(rows)-1]])
        (args.out / "summary.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        print(json.dumps({k: v for k, v in result.items() if k != "sample"}, indent=2), flush=True)
    finally:
        if sf.game_running():
            sf.sf_quit()
        if sf.game_running():
            raise RuntimeError("Game still running; disk backups preserved; restore DLL/settings after closing normally")
        (mod / "StutterFix.dll").write_bytes(original_dll)
        (mod / "Settings.xml").write_bytes(original_settings)
        if old_flag is not None:
            ui_flag.write_bytes(old_flag)
        elif ui_flag.exists():
            ui_flag.unlink()
        # Only our new diagnostic folder, copied to the requested result folder.
        if destination.exists() and (args.out / "capture" / "frames.csv").is_file():
            if destination.resolve().parent != mod.resolve() or destination.resolve().name != "framegen-capture":
                raise RuntimeError("Unexpected diagnostic cleanup target")
            shutil.rmtree(destination)
        print("Installed DLL and settings restored", flush=True)


if __name__ == "__main__":
    main()
