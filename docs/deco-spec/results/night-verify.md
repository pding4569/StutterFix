# 밤샘 자동 검증 (게임만 실행)

빌드: `df390b6 cleanup: isolate inactive research backends and freeze fixed policies`. 맵마다 조건별 1판, 곡 전체.

| 맵 | 기준 wt-cand 끔 | 이번 빌드 끔 | 이번 빌드 켬 | 켬 상태 |
|---|---|---|---|---|
| Arche | 432 FPS, 끊김 0, 최악 10초 103 | 434 FPS, 끊김 0, 최악 10초 104 | 480 FPS, 끊김 0, 최악 10초 323 | 켜짐: 소유 28523개 |
| Windflower | 314 FPS, 끊김 0, 최악 10초 202 | 316 FPS, 끊김 0, 최악 10초 203 | 219 FPS, 끊김 1, 최악 10초 132 | 켜짐: 소유 2818개 |
| Exit | 390 FPS, 끊김 0, 최악 10초 296 | 386 FPS, 끊김 0, 최악 10초 301 | 369 FPS, 끊김 0, 최악 10초 294 | 켜짐: 소유 2999개 |
| HELLO | 277 FPS, 끊김 5, 최악 10초 222 | 269 FPS, 끊김 0, 최악 10초 213 | 260 FPS, 끊김 2, 최악 10초 203 | 켜짐: 소유 778개 |
| DDONGSSADA | 349 FPS, 끊김 3, 최악 10초 208 | 359 FPS, 끊김 0, 최악 10초 205 | 301 FPS, 끊김 0, 최악 10초 191 | 켜짐: 소유 548개 |
| 7777 | 374 FPS, 끊김 0, 최악 10초 273 | 383 FPS, 끊김 0, 최악 10초 275 | 357 FPS, 끊김 0, 최악 10초 265 | 켜짐: 소유 144개 |

## Arche 262초 픽셀

wt-cand: /3686400 ratio=5.4253472222222222E-07 PSNR=75.00171201894085 top32(bottom-left ; [StutterFix] [framecmp] saved=D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\framecmp\AR262_C.png ; [StutterFix] [framecmp] saved=D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\framecmp\AR262_A.png ; [StutterFix] [framecmp] resume restored continuity_frame=135373 tracking=True ; [StutterFix] [framecmp] framecmp RESULT noise_out=0 aa2_out=0 ab_out=2 ctl_order=NA ctl_hide=NA ; [StutterFix] [framecmp] framecmp VERDICT noise=PASS aa2=PASS order_ctl=FAIL hide_ctl=FAIL

wt-partial: /3686400 ratio=2.7126736111111111E-07 PSNR=75.057957358430244 top32(bottom-lef ; [StutterFix] [framecmp] saved=D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\framecmp\AR262_C.png ; [StutterFix] [framecmp] saved=D:\SteamLibrary\steamapps\common\A Dance of Fire and Ice\Mods\StutterFix\framecmp\AR262_A.png ; [StutterFix] [framecmp] resume restored continuity_frame=134609 tracking=True ; [StutterFix] [framecmp] framecmp RESULT noise_out=0 aa2_out=0 ab_out=1 ctl_order=NA ctl_hide=NA ; [StutterFix] [framecmp] framecmp VERDICT noise=PASS aa2=PASS order_ctl=FAIL hide_ctl=FAIL
