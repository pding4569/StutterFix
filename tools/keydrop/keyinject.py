"""키씹 시험용 키 주입기. Player.log 에 '[inputprobe] begin' 이 뜨면 게임 창을 앞으로 가져와 SendInput 으로 J/K 를 넣고 결과 JSON 을 쓴다.
사용: python keyinject.py <이름> [반복=2]
주의: 타이머 해상도를 건드리지 않으려고 sleep 대신 perf_counter 바쁜 대기만 쓴다(주입기가 timeBeginPeriod 로 시스템 타이머를 바꾸면 A/B 가 오염됨)."""
import ctypes, ctypes.wintypes as W, os, sys, time, json, random
from pathlib import Path

LOG = Path(os.path.expandvars(r"%USERPROFILE%\AppData\LocalLow\7th Beat Games\A Dance of Fire and Ice\Player.log"))
OUT = Path(r"E:\StutterFixWork\night-results")
name = sys.argv[1]; reps = int(sys.argv[2]) if len(sys.argv) > 2 else 2
user32 = ctypes.windll.user32
KEYEVENTF_SCANCODE, KEYEVENTF_KEYUP = 0x0008, 0x0002
SC = {"J": 0x24, "K": 0x25}

class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", W.WORD), ("wScan", W.WORD), ("dwFlags", W.DWORD), ("time", W.DWORD), ("dwExtraInfo", ctypes.c_size_t)]
class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", W.LONG), ("dy", W.LONG), ("mouseData", W.DWORD), ("dwFlags", W.DWORD), ("time", W.DWORD), ("dwExtraInfo", ctypes.c_size_t)]
class _U(ctypes.Union):
    _fields_ = [("ki", KEYBDINPUT), ("mi", MOUSEINPUT)]
class INPUT(ctypes.Structure):
    _fields_ = [("type", W.DWORD), ("u", _U)]

def send(key, up):
    i = INPUT(type=1); i.u.ki = KEYBDINPUT(0, SC[key], KEYEVENTF_SCANCODE | (KEYEVENTF_KEYUP if up else 0), 0, 0)
    user32.SendInput(1, ctypes.byref(i), ctypes.sizeof(INPUT))

def spin(ms):
    end = time.perf_counter() + ms / 1000.0
    while time.perf_counter() < end: pass

def log_has_begin(t0):
    try:
        if LOG.stat().st_mtime < t0: return False
        return "[inputprobe] begin" in LOG.read_text(encoding="utf-8", errors="replace")
    except Exception: return False

t0 = time.time()
print("대기: inputprobe begin", flush=True)
while not log_has_begin(t0 - 1):
    time.sleep(0.5)
    if time.time() - t0 > 900: print("시간 초과"); sys.exit(2)
hw = user32.FindWindowW(None, "A Dance of Fire and Ice")
for _ in range(10):
    if hw: user32.SetForegroundWindow(hw)
    time.sleep(0.5)
    if hw and user32.GetForegroundWindow() == hw: break
else:
    print("게임 창을 앞으로 못 가져옴 — 다른 앱에 키가 들어갈 수 있어 중단"); sys.exit(3)
time.sleep(1.0)
timer_start = None
try:
    _mn = ctypes.c_ulong(); _mx = ctypes.c_ulong(); _c = ctypes.c_ulong(); ctypes.windll.ntdll.NtQueryTimerResolution(ctypes.byref(_mn), ctypes.byref(_mx), ctypes.byref(_c)); timer_start = _c.value / 10000.0
except Exception: pass
rng = random.Random(1234)
down = {"single": 0, "burst": 0, "rapid": 0}; start = time.perf_counter(); real_gaps = []
for rep in range(reps):
    keys = ["J", "K"]
    for n in range(400):                       # 단독 입력: 누름 12ms, 간격 8~30ms
        k = keys[n & 1]; send(k, False); down["single"] += 1; spin(12); send(k, True); spin(rng.uniform(8, 30))
    for n in range(200):                       # 동시 두 키(코드): J 뒤 1~3ms 에 K
        send("J", False); spin(rng.uniform(1, 3)); send("K", False); down["burst"] += 2
        spin(12); send("J", True); send("K", True); spin(40)
    for n in range(200):                       # 같은 키 빠른 연타: 4~8ms
        send("J", False); down["rapid"] += 1; spin(rng.uniform(2, 4)); send("J", True); spin(rng.uniform(2, 4))
def timer_ms():
    mn = ctypes.c_ulong(); mx = ctypes.c_ulong(); cur = ctypes.c_ulong()
    ctypes.windll.ntdll.NtQueryTimerResolution(ctypes.byref(mn), ctypes.byref(mx), ctypes.byref(cur)); return cur.value / 10000.0
res = {"timer_ms_at_start": timer_start, "timer_ms_at_end": timer_ms(), "name": name, "injected": down, "total_down": sum(down.values()), "seconds": round(time.perf_counter() - start, 2), "reps": reps}
OUT.mkdir(exist_ok=True); (OUT / f"keyinject-{name}.json").write_text(json.dumps(res), encoding="utf-8")
print(json.dumps(res), flush=True)
