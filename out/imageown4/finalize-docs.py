from pathlib import Path

root = Path(__file__).resolve().parents[2]
summary = '''IMAGEOWN=FAIL fps_before=NA fps_after=NA gain_pct=NA pixel_max=NA independent_pixel_max=1 fallback=NA spikes_30ms=NA reason=TEXTURE_ADDRESS_MISSING
이번 게임·설치·커밋·푸시·자동 장치·Editor·설정/제어변경0회; 새게임실측·새계획생성 없음.
외부run3 직접35줄 원시순서대조;3440×1440@164.999·준비82구간/14595포함·3배열117복사/318.75MiB·직접32/32·준비폴백0.
live on의 image_texture_not_registered_in_collapse로 활성화실패;GPU28523분류 뒤 Rec11821/11821복원·종료native_contexts0.
현재항목만검사해 미래이미지누락을 놓치는 준비계약수정:전체등록합집합 대 실제주소검사·누락직접/배열분류·CPU기록분리 전 중단.
허용helper실패로그5줄로 등록됐지만주소없음/새자산ID 구분;기존주소·범위·mip/wrap·정렬·폴백가드 유지·매프레임스캔 추가없음.
기존로그에 누락ID/총요청수없어 실제슬롯초과 세부귀속미확정;이번수정은 실제수용량증가·게임통과·통합불가능 증명아님.
격리3빌드 오류0/0/0·경고48/36/40·LoadCheck279/229/281곳 틀림0·일반Player 연구제외·실제메서드추출20/생성기8검사통과.
보호14개SHA유지·renderer/policy/export/셰이더/번들/생성기/기존JSON/tag/설정유지;주소배정·자산수명은 소유자인계.
통합픽셀/FPS/+20%/끊김/전곡/설치복원NA;원인수정 전 같은333단계 자동재시도 중단필요·장치직접정지안함·상세결과/장부/out/imageown4 참조。
'''.replace('참조。','참조.')
assert len(summary.splitlines()) == 10
(root / 'docs/handoff/codex-imageown-summary.txt').write_text(summary, encoding='utf-8')
for name in ('imageown-autoloop-ledger.md', 'codex-master-20261022.md'):
    path = root / 'docs/handoff' / name
    content = path.read_text(encoding='utf-8-sig')
    content = content.replace('外部 run3', '외부 run3').replace('변경0회。', '변경0회.')
    path.write_text(content, encoding='utf-8')
print('result summary: 10 lines; Korean typo corrections applied')
