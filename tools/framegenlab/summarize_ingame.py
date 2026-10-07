"""Create section-13 public numeric evidence without game paths or raw logs."""
import json
from pathlib import Path

HERE=Path(__file__).resolve().parent
cases=[("HELLO 2026",0,"hello-off"),("HELLO 2026",2,"hello-2x-hdr"),("HELLO 2026",4,"hello-4x-hdr"),
       ("Arche",0,"arche-off"),("Arche",2,"arche-2x-hdr"),("Arche",4,"arche-4x-hdr")]


def main():
    results=[]
    for label,mode,stem in cases:
        result=json.loads((HERE/"results"/("ingame-"+stem)/"summary.json").read_text(encoding="utf-8"))
        n=result["native"]
        if result["visual_smoke"] or n["seconds"]<39.9 or n["song_start"]>5.05 or n["song_end"]<44.95:
            raise RuntimeError("Not a complete common music window")
        if n["generated_frames"]!=n["gpu_coverage"]:
            raise RuntimeError("Missing GPU query")
        results.append(dict(map=label,mode=mode,**n))
    public=dict(date="2026-10-07",branch="codex/framegen",engine="6000.3.21f1",resolution=[3440,1440],monitor_hz=144,
                sync=0,color_space="Gamma",song_window_seconds=[5,45],fresh_process_per_run=True,trials_per_condition=1,
                real_fps_definition="Full world rendering, not all gameplay updates",logic_updates="Every Unity/output tick",
                physical_display_fps="Not verified",gpu_scope="Two HDR planet passes plus native reprojection and frozen UI composite",
                off_path_unchanged_by_hdr_buffer_revision=True,measurements=results,
                ui_capture_checks=[dict(map="HELLO 2026",selected_pixel_fraction=.05119751291989664,selected_rgb_max_error=0),
                                   dict(map="Arche",selected_pixel_fraction=.13603359173126614,selected_rgb_max_error=0,white_hdr_channel_max=1.224609375)])
    (HERE/"validation-13-2026-10-07.json").write_text(json.dumps(public,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    for n in results:
        gpu=n["generated_gpu_mean_ms"]
        print(f"{n['map']} {n['mode'] or 'off'}: source {n['real_fps']:.1f}, output {n['output_fps']:.1f}, GPU {gpu if gpu is not None else '-'}, output worst {n['output_worst_ms']:.1f}ms")


if __name__=="__main__": main()
