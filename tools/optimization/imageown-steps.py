# 이미지 장식(약 4,976개) GPU 소유 + draw 통합 시험 계획 생성기.
# 템플릿(drawcollapse-safe-readyon-steps.json: 재계획과 켜기를 붙인 ABBA + 픽셀)에서 맵 경로를 바꾸고 `gpudeco images own on` 을 넣는다.
import argparse, json, pathlib
ap = argparse.ArgumentParser(); ap.add_argument("--chart", required=True); ap.add_argument("--output", required=True); a = ap.parse_args()
tpl = json.loads((pathlib.Path(__file__).parent / "out" / "drawcollapse-safe-readyon-steps.json").read_text(encoding="utf-8"))
steps = []
for s in tpl["steps"]:
    if s.startswith("open "): steps.append("gpudeco images own on"); steps.append("open " + a.chart); continue
    steps.append(s)
tpl["steps"] = steps; tpl["tag"] = "imageown-260-270"
pathlib.Path(a.output).write_text(json.dumps(tpl, ensure_ascii=False, indent=1), encoding="utf-8")
