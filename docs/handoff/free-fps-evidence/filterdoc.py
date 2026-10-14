from pathlib import Path
import re,json
p=Path('docs/handoff/free-fps-findings.md');s=p.read_text();s=s.replace('최종 표본 비율 대기','완주 전 92표본: 3개 동시 4(4.3%), 연속 3개 0');s=s.replace('빈도 및 원래 pass별 양자화 보존을 확인할 설계만, 구현 금지 준수','30% 빈도 기준 미달, 연속 조합 없음. 일반적인 동일성 설계 조건만 기록, 구현 금지 준수');s+='''

## 필터 최종 표본

HELLO는 [임무완주] OnLandOnPortal로 완주를 확인했다. 포털 이전 2초 간격 92표본(마지막 184.5초)만 사용하며 포털 이후 계속 증가한 songposition 표본은 제외한다. Color_Noise + Color_BrightContrastSaturation + Colors_Brightness가 함께 켜진 표본은 58/60/62/64초 4개(4.35%). 그러나 TV_WideScreenVertical/TV_Chromatical2 등의 왜곡 패스가 사이에 있어 **연속된 단순 색 필터 3개**는 0/92다. 초기 중간 집계의 0회는 Colors_Brightness 이름 분류 누락과 포털 이후 표본 포함이 있었으므로 폐기했다. 원시 표본은 filter-final.json에 저장했다. 이 값은 모든 프레임을 센 정확한 프레임 비율이 아니라 2초 스냅샷 비율이다.
''';p.write_text(s,encoding='utf-8')
