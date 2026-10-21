# 키씹 시험: python run_keydrop.py <이름> <TimerHalfMs true|false> [반복=2]
import sys, subprocess, json, time
sys.path.insert(0, r"E:\StutterFixWork"); sys.stdout.reconfigure(encoding="utf-8")
from gamelock import GameLock
name, timer = sys.argv[1], sys.argv[2].lower() == "true"; reps = sys.argv[3] if len(sys.argv) > 3 else "2"
steps = {"steps": ["timeout 300", "auto off", "asyncinput on", r"game D:\얼불춤 맵 파일\Cry\Cry.adofai", "nofail on", "press", "wait 8",
                   "inputprobe begin", "wait 70", "inputprobe end", "quit"],
         "settings": {"TimerHalfMs": timer}, "timeout_min": 8, "tag": "keydrop-" + name}
sj = r"E:\StutterFixWork\night-results\keydrop-%s-steps.json" % name
open(sj, "w", encoding="utf-8").write(json.dumps(steps, ensure_ascii=False))
with GameLock("keydrop-" + name):
    inj = subprocess.Popen([sys.executable, r"E:\StutterFixWork\keyinject.py", name, reps], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8")
    r = subprocess.run([sys.executable, r"E:\StutterFixWork\run_json.py", sj, "StutterFix", "keydrop-" + name, "--keys", "inputprobe,키씹,[입력"], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=900)
    try: inj.wait(timeout=30)
    except Exception: inj.kill()
    print(r.stdout[-2500:]); print("injector:", (inj.stdout.read() if inj.stdout else "")[-600:])
