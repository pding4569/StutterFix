using HarmonyLib;
using StutterFix;
using UnityEngine;

int checks = 0, cases = 0;
void Check(bool ok, string message)
{
    checks++;
    if (!ok) throw new Exception("FAIL " + message);
}
scrConductor Setup(int seed, bool on)
{
    BeatFix.Shutdown(); Harmony.Foreign = false; AccessTools.MissingBridge = false;
    BeatFix.Install(new Harmony()); BeatFix.Enabled = true; BeatFix.SetSkipNoChange(on);
    scrVfx.instance = new scrVfx { style = seed % 6, overrideStyle = seed / 6 % 3,
        overrideTileSprites = (seed & 32) != 0, overrideGlowSprites = (seed & 64) != 0 };
    ADOBase.controller = new(); ADOBase.lm = new();
    var c = new scrConductor();
    for (int i = 0; i < 12; i++)
    {
        var f = new scrFloor { id = i, vfx = scrVfx.instance,
            hasLit = ((seed >> (i % 10)) & 1) != 0, dontChangeMySprite = ((seed >> ((i + 3) % 10)) & 1) != 0,
            bottomGlow = i % 3 == 0 ? null : new SpriteRenderer() };
        f.topGlow.color = new Color(i % 4); f.floorRenderer.color = new Color(i % 3);
        if (f.bottomGlow) f.bottomGlow.color = new Color(i % 5);
        ADOBase.lm.listFloors.Add(f); c.onBeats.Add(f);
        if (i % 4 == 0) c.onBeats.Add(f); // 첫 반복에 같은 타일이 중복 등록되는 실제 구조
    }
    scrFloor.Rng = (uint)seed + 123; scrFloor.Beat = 0; scrFloor.WorkOrder.Clear();
    return c;
}
string State(scrConductor c)
{
    var s = new System.Text.StringBuilder();
    foreach (var f in ADOBase.lm.listFloors)
    {
        void ColorState(Color value) => s.Append(value.r).Append(',').Append(value.g).Append(',').Append(value.b).Append(',').Append(value.a).Append(';');
        s.Append(f.hasLit).Append(f.dontChangeMySprite).Append(':').Append(f.randomNumberForLitAndGlow).Append(';');
        ColorState(f.topGlow.color); ColorState(f.floorRenderer.color); if (f.bottomGlow) ColorState(f.bottomGlow.color);
        s.Append(f.topGlow.sprite?.id).Append('/').Append(f.bottomGlow?.sprite?.id).Append('/').Append(f.floorRenderer.sprite?.id).Append('|');
    }
    return s.Append("rng=").Append(scrFloor.Rng).Append(" order=").AppendJoin(',', scrFloor.WorkOrder).Append(" frame=").Append(c.onBeatFrame).ToString();
}
void Beat(scrConductor c, bool vanilla = false)
{
    scrFloor.WorkOrder.Clear(); scrFloor.Beat++; Time.frameCount = scrFloor.Beat;
    if (vanilla || BeatFix.PropPrefix(c))
    {
        foreach (var f in c.onBeats) f.OnBeat();
        if (ADOBase.controller.gameworld) foreach (var f in ADOBase.lm.listFloors) f.OnBeat();
        c.onBeatFrame = Time.frameCount;
    }
}
void Event(scrConductor c, int seed, int step, bool hooks)
{
    var f = ADOBase.lm.listFloors[(seed + step * 7) % 12];
    switch ((step + seed) % 8)
    {
        case 0: f.LightUp(); if (hooks) BeatFix.LitChanged(f); break;
        case 1: f.Reset(); if (hooks) BeatFix.LitChanged(f); break;
        case 2: f.TweenOpacity(); if (hooks) BeatFix.OpacityChanged(f); break;
        case 3:
            var bridge = new ffxLightBridge { brigeFloors = new() { f, ADOBase.lm.listFloors[(f.id + 1) % 12] } };
            bridge.StartEffect(null); if (hooks) BeatFix.BridgeChanged(bridge); break;
        case 4: scrVfx.instance.overrideTileSprites = !scrVfx.instance.overrideTileSprites; break;
        case 5: scrVfx.instance.overrideStyle = (scrVfx.instance.overrideStyle + 1) % 3; scrVfx.instance.style = (scrVfx.instance.style + 1) % 6; break;
        case 6: f.topGlow.color = new Color(9); if (f.bottomGlow) f.bottomGlow.color = new Color(8); f.floorRenderer.color = new Color(0.4f); break;
        case 7: c.onBeats.Add(f); break;
    }
}

// 원래의 전부 호출하는 게임 반복을 독립 대조군으로 삼아, 프로덕션 BeatFix 자체를 실행한다.
for (int seed = 0; seed < 4096; seed++)
{
    var expected = new List<string>();
    var c = Setup(seed, false);
    for (int step = 0; step < 16; step++) { Event(c, seed, step, false); Beat(c, true); expected.Add(State(c)); }
    foreach (bool on in new[] { false, true })
    {
        c = Setup(seed, on);
        for (int step = 0; step < 16; step++)
        {
            Event(c, seed, step, true); Beat(c);
            Check(State(c) == expected[step], $"seed={seed} on={on} step={step}: state/RNG/order");
        }
    }
    cases++;
}

// 재검사는 제외된 타일도 읽어 다시 활성화해야 한다. 256번째 박자 이전에 수리한다.
{
    var c = Setup(0, true); var f = ADOBase.lm.listFloors[0];
    f.hasLit = true; f.dontChangeMySprite = true; BeatFix.LitChanged(f);
    for (int i = 0; i < 255; i++) Beat(c);
    f.dontChangeMySprite = false; int calls = f.calls;
    SpriteRenderer.Reads = 0; Beat(c);
    Check(f.calls - calls == 3, "rescan restores duplicate listeners and floor call on same beat");
    Check(BeatFix.NoChangeMismatch == 1 && BeatFix.NoChangeRechecks == 1, "rescan counts exactly one mismatch");
    Check(BeatFix.NoChangeStatus.Contains("목록 불일치=1"), "counters exposed");
    f.dontChangeMySprite = true; BeatFix.OpacityChanged(f);
    for (int i = 0; i < 255; i++) Beat(c);
    SpriteRenderer.Reads = 0; Beat(c);
    Check(SpriteRenderer.Reads == 0, "normal/recheck no renderer reads when all floors are no-op");
}
// 알려진 이벤트의 누락은 정상 시 0이어야 한다. 플래그 끔/켬은 바로 목록을 다시 만든다.
{
    var c = Setup(1023, true);
    for (int i = 0; i < 1024; i++) { Event(c, 1023, i, true); Beat(c); }
    Check(BeatFix.NoChangeMismatch == 0 && BeatFix.NoChangeRechecks == 4, "hook sequences never require repair");
    BeatFix.SetSkipNoChange(false); Check(!BeatFix.SkipNoChangeActive, "off immediate");
    Beat(c); BeatFix.SetSkipNoChange(true); Beat(c); Check(BeatFix.SkipNoChangeActive, "on after off");
    BeatFix.ForgetLevel(); Beat(c); Check(BeatFix.NoChangeMismatch == 0, "map rebuild preserves membership");
}
// 검증 표본은 정확히 한 장이고, 상태 변화가 보이면 전체 새 정책을 중단한다.
{
    var c = Setup(1023, true); var f = ADOBase.lm.listFloors[1];
    var baseline = BeatNoChangeSnapshot.Read(f);
    f.topGlow.color = new Color(0.12f); Check(!baseline.Same(BeatNoChangeSnapshot.Read(f)), "snapshot top color");
    baseline = BeatNoChangeSnapshot.Read(f);
    f.bottomGlow.color = new Color(0.13f); Check(!baseline.Same(BeatNoChangeSnapshot.Read(f)), "snapshot bottom color");
    baseline = BeatNoChangeSnapshot.Read(f);
    f.floorRenderer.color = new Color(0.14f); Check(!baseline.Same(BeatNoChangeSnapshot.Read(f)), "snapshot floor color");
    foreach (var renderer in new[] { f.topGlow, f.bottomGlow, f.floorRenderer })
    {
        baseline = BeatNoChangeSnapshot.Read(f); renderer.sprite = scrFloor.Sprites[3];
        Check(!baseline.Same(BeatNoChangeSnapshot.Read(f)), "snapshot sprite");
    }
    baseline = BeatNoChangeSnapshot.Read(f); f.randomNumberForLitAndGlow++;
    Check(!baseline.Same(BeatNoChangeSnapshot.Read(f)), "snapshot random number");
}
{
    var c = Setup(1023, true); scrVfx.instance.overrideTileSprites = false; BeatFix.Verify = true;
    for (int i = 0; i < 64; i++) Beat(c);
    Check(BeatFix.NoChangeVerified == 1 && BeatFix.NoChangeVerifyMismatch == 0, "no-op verification sample");
    foreach (var f in ADOBase.lm.listFloors) f.violateNoOp = true;
    for (int i = 0; i < 64; i++) Beat(c);
    Check(BeatFix.NoChangeVerifyMismatch == 1 && !BeatFix.SkipNoChangeActive, "verify mismatch disables optimization");
    BeatFix.SetSkipNoChange(false); BeatFix.SetSkipNoChange(true);
    Check(!BeatFix.SkipNoChangeActive, "verify failure stays latched until unload");
    BeatFix.Shutdown(); Check(!BeatFix.SkipNoChangeActive && BeatFix.NoChangeVerifyMismatch == 0, "unload resets state");
}
{
    var c = Setup(1023, true); Harmony.Foreign = true;
    Check(BeatFix.PropPrefix(c), "foreign patches use original propagation"); Harmony.Foreign = false;
    BeatFix.Shutdown(); AccessTools.MissingBridge = true; BeatFix.Install(new Harmony()); BeatFix.SetSkipNoChange(true);
    Check(!BeatFix.SkipNoChangeActive, "missing required hook refuses optimization"); AccessTools.MissingBridge = false;
    Check(Harmony.Installed.Contains("scrFloor.TweenOpacity") && Harmony.Installed.Contains("ffxLightBridge.StartEffect"), "writer postfixes installed");
}
// 일반 타일이라는 전제가 깨지면 FastFloors 대신 게임의 타일 반복으로 돌아간다.
{
    var c = Setup(0, true);
    var f = new DerivedFloor { id = 99, vfx = scrVfx.instance, hasLit = true, dontChangeMySprite = true };
    ADOBase.lm.listFloors[5] = f; c.onBeats[6] = f; BeatFix.MarkDirty(); Beat(c);
    Check(f.calls == 2, "derived floor is never skipped");
    c = Setup(1023, true); var first = ADOBase.lm.listFloors[0];
    Beat(c); int calls = first.calls; BeatFix.DiagSkipFloors = true; Beat(c);
    Check(first.calls == calls, "beatskip diagnostic still bypasses floor loop");
    c = Setup(1023, false); scrVfx.instance.overrideTileSprites = false; first = ADOBase.lm.listFloors[0];
    Beat(c); Check(first.calls == 1 && !BeatFix.SkipNoChangeActive, "off retains original lit floor no-op calls");
    BeatFix.SetSkipNoChange(true); calls = first.calls; Beat(c); Check(first.calls == calls, "on removes no-op calls");
    BeatFix.SetSkipNoChange(false); Beat(c); Check(first.calls == calls + 1, "off restores no-op calls immediately");
}
Console.WriteLine($"PASS cases={cases}, assertions={checks}; production BeatFix, vanilla equality, RNG/order, rescan, verification, unload, fallback");

sealed class DerivedFloor : scrFloor { }
