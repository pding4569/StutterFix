# 맵 열기 A/B: 두 DLL 을 번갈아 깔고(판마다 게임을 새로 켬) 같은 맵을 열어 시간을 비교한다.
#   py -3 open_ab.py <A.dll> <B.dll> <반복> <맵 경로> [<맵 경로> ...]
# 순서는 ABBA. 결과: 맵 열림 초, 이미지 단계 초, 메인 스레드 넣기/기다림.
import re, shutil, sys, glob, os
import server as S

MODS = r'D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\StutterFix.dll'
RUNS = os.path.join(os.environ['LOCALAPPDATA'], 'StutterFix', 'runs')


def one(dll, path, tag):
    shutil.copyfile(dll, MODS)
    S.sf_run(['timeout 900', 'open ' + path, 'wait 2'], None, 18, tag)
    log = max(glob.glob(os.path.join(RUNS, '*.log')), key=os.path.getmtime)
    t = open(log, encoding='utf-8', errors='replace').read()
    o = re.search(r'맵 열림 \(([0-9.]+)초', t)
    im = re.search(r'\[이미지\] 미리 푼 것 [^\n]*?넣기 ([0-9]+)ms[^\n]*?기다림 ([0-9]+)ms[^\n]*?전체 ([0-9.]+)초', t)
    return (float(o.group(1)) if o else None,
            float(im.group(3)) if im else None,
            int(im.group(1)) / 1000 if im else None,
            int(im.group(2)) / 1000 if im else None)


def main():
    a, b, reps, maps = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4:]
    for path in maps:
        res = {'A': [], 'B': []}
        order = ['A', 'B', 'B', 'A'] * (reps // 2) + (['A', 'B'] if reps % 2 else [])
        for k in order:
            r = one(a if k == 'A' else b, path, 'openab-' + k)
            res[k].append(r)
            print(k, os.path.basename(os.path.dirname(path)), r, flush=True)
        for k in 'AB':
            v = [x[0] for x in res[k] if x[0]]
            print('==', k, os.path.basename(os.path.dirname(path)), '열림', v, '이미지', [x[1] for x in res[k]], '넣기', [x[2] for x in res[k]], '기다림', [x[3] for x in res[k]], flush=True)


if __name__ == '__main__':
    main()
