# framegensim

프레임 생성 방식 비교 시뮬레이션. 게임 없이 얼불춤 비슷한 2D 장면으로 none / reproj / reproj+pl / interp / render 를 비교한다.
결과와 해석은 `docs/framegen-research.md` 10장.

```
pip install numpy pillow
python3 sim.py --base 30 60 100 --hz 164 --seconds 4 --gif out.gif
```
