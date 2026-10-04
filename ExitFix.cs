using System;
using System.Collections.Generic;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 편집으로 나가기(scnEditor.SwitchToEditMode) 멈춤 줄이기: 판 중에 안 바뀐 장식은 다시 설정(Setup)을 가볍게.
    //
    // 2026-09-27 Arche(타일 4139, 장식 28835, 효과 약 11만 개), 개발자용 측정: 나가기 2.6초 =
    //   장식 다시 설정(scrDecorationManager.ResetDecorations) 1.5초: 장식마다 Setup(하나 50us: 이벤트 값 읽기 + 엔진 설정 수십 번)
    //   타일 다시 만들기(MakeLevel) 0.76초: 타일마다 붙어 있던 효과 컴포넌트를 하나씩 DestroyImmediate (11만 5천 번).
    //     뒤에서부터 지우기, 그동안 타일 충돌체 꺼 두기 둘 다 0.76초 그대로 - 유니티의 컴포넌트 지우기 값 자체라 손대지 않는다.
    //
    // Setup 은 장식 이벤트 값으로 장식의 모든 상태를 처음부터 다시 정한다. 재생 시작(마지막 장식 다시 설정) 뒤로 한 번도 안 바뀐 장식이면,
    // 그 결과는 지금 상태와 같다. 그래서 나가기에서는 장식마다:
    //   바뀌었는지 판단 = (1) 재생 시작 때 찍어 둔 값(Setup 이 정하고, 곡 중 효과가 바꾸는 필드 전부)과 지금 값이 같고
    //                     (2) 필드에 남지 않는 설정(깊이, 마스크 깊이, 히트박스 갱신, Setup)이 그 뒤로 불린 적 없고
    //                     (3) 장식 이벤트 데이터 전체(LoadFix 지문)와 장식 목록·이미지·붙은 타일이 그대로
    //                     (4) SetBlendMode(None) 가 두는 재질 상태(블렌드 효과 꺼짐, 셰이더, 메시 재질) 그대로
    //   안 바뀐 장식 = Setup 중 결과가 "지금 값 그대로" 인 설정은 건너뛰고, 나머지는 게임 함수를 그대로 부른다:
    //     이미지 확인(GetOrAddSprite), 놓는 기준 위치(SetPlacementType), 태그 목록 등록(과 태그 없는 장식의 "NO TAG" 빼기),
    //     점 샘플링 키워드·텍스처 반복 끔(SetSprite 끝), 마스크 캐시·타일링 갱신 알림,
    //     위치(SetPosition: 카메라·시차·회전·크기까지 다시 계산), 색(ApplyColor), 보임(SetVisible), 필터 목록, 에디터 클릭 상자.
    //   바뀐 장식, 그 밖의 종류(글자, 오브젝트, 파티클), 히트박스·블렌드·마스크·타일에 붙음·행성 따라감·"components" 가 있는 장식은 원래 Setup.
    //   장식 순서대로 섞어서 부르므로 태그 목록 순서, 마스크 캐시 알림 순서도 원래와 같다.
    // 개발자용 검증(exit-verify.txt 가 있으면 나가기마다): 가볍게 한 결과를 넓게 찍고(StateDump), 같은 프레임에 원래 방식으로 전부
    // 다시 설정해 다시 찍어 이름별로 비교한다(끝 상태는 원래 방식 결과).
    // 2026-09-27 자동 시험(재생 -> 나가기, 재생 -> 다시 하기 -> 나가기): Arche, Windflower, DDONGSSADA3302, HELLO (BPM) 2026, QuomodocunquizE,
    //   Battle Against A True Hero, 7777, Plum - Timeline 에서 가볍게 한 장식 전부 원래 방식과 같음, 매니저 목록도 같음.
    //   검증이 잡은 것: 태그 없는 장식을 Setup 이 "NO TAG" 목록에 넣었다가 곧바로 빼는 동작(따라 하게 고침).
    //   검증기 시험("sabotage"): 500개마다 하나씩 정렬·위치·색·태그 목록을 일부러 틀리게 하면 4종류 모두 잡음.
    internal static class ExitFix
    {
        internal static bool Enabled = true;
        internal static bool Verify;   // (개발자용) exit-verify.txt
        internal static long Exits, LightTotal, FullTotal, Fallbacks, VerifyRuns, VerifyDiffDecos, VerifyDiffGlobal;
        internal static string LastLine = "";
        private static bool foreignChecked, foreign;

        private static readonly AccessTools.FieldRef<scrDecorationManager, List<scrDecoration>> allRef = AccessTools.FieldRefAccess<scrDecorationManager, List<scrDecoration>>("allDecorations");
        private static readonly AccessTools.FieldRef<scrDecoration, bool> rendEnRef = AccessTools.FieldRefAccess<scrDecoration, bool>("rendererEnabled");
        private static readonly AccessTools.FieldRef<scrVisualDecoration, DecorationBlendMode> blendRef = AccessTools.FieldRefAccess<scrVisualDecoration, DecorationBlendMode>("blendMode");
        private static readonly AccessTools.FieldRef<scrVisualDecoration, MaskingType> maskRef = AccessTools.FieldRefAccess<scrVisualDecoration, MaskingType>("maskingType");
        private static readonly AccessTools.FieldRef<scrVisualDecoration, TextureManager.CustomSprite> spriteRef = AccessTools.FieldRefAccess<scrVisualDecoration, TextureManager.CustomSprite>("_sprite");
        private static readonly AccessTools.FieldRef<scrVisualDecoration, Material> meshMatRef = AccessTools.FieldRefAccess<scrVisualDecoration, Material>("meshRendererMat");
        private static readonly AccessTools.FieldRef<scrDecorationManager, HashSet<scrVisualDecoration>> alphaSetRef = AccessTools.FieldRefAccess<scrDecorationManager, HashSet<scrVisualDecoration>>("decorationsToUpdateAlphaMaskCache");
        private static readonly AccessTools.FieldRef<scrDecorationManager, HashSet<string>> tileKeysRef = AccessTools.FieldRefAccess<scrDecorationManager, HashSet<string>>("textureKeysToUpdate");
        private static Action<scrDecoration> applyColor;

        internal static void Install(Harmony h)
        {
            try
            {
                var sw = AccessTools.Method(typeof(scnEditor), "SwitchToEditMode");
                var rd = AccessTools.Method(typeof(scrDecorationManager), "ResetDecorations");
                var setup = AccessTools.Method(typeof(scrDecoration), "Setup");
                var ac = AccessTools.Method(typeof(scrDecoration), "ApplyColor");
                if (sw == null || rd == null || setup == null || ac == null) { Main.Entry.Logger.Log("[나가기] 게임 코드 모양이 달라 끔"); return; }
                applyColor = AccessTools.MethodDelegate<Action<scrDecoration>>(ac);   // 가상 호출 (scrVisualDecoration.ApplyColor, 여기 붙은 패치 포함)
                h.Patch(sw, prefix: new HarmonyMethod(typeof(ExitFix), nameof(SwitchPrefix)), finalizer: new HarmonyMethod(typeof(ExitFix), nameof(SwitchFinalizer)));
                var play = AccessTools.Method(typeof(scnEditor), "Play", Type.EmptyTypes);
                if (play != null) h.Patch(play, prefix: new HarmonyMethod(typeof(ExitFix), nameof(PlayPrefix)), finalizer: new HarmonyMethod(typeof(ExitFix), nameof(PlayFinalizer)));
                // 다른 앞 패치(LoadFix·TransitionFix 의 건너뛰기)가 정한 뒤에 본다
                h.Patch(rd, prefix: new HarmonyMethod(typeof(ExitFix), nameof(ResetPrefix)) { priority = Priority.Last }, postfix: new HarmonyMethod(typeof(ExitFix), nameof(ResetPostfix)));
                // 필드에 흔적이 안 남는 설정: 불리면 그 장식은 "바뀜"
                var touch = new HarmonyMethod(typeof(ExitFix), nameof(Touch));
                h.Patch(setup, prefix: touch);
                foreach (var m in typeof(scrVisualDecoration).GetMethods(AccessTools.all))
                    if (m.DeclaringType == typeof(scrVisualDecoration) && (m.Name == "SetDepth" || m.Name == "SetMaskingDepth" || m.Name == "UpdateHitbox"))
                        h.Patch(m, prefix: touch);
                if (Edition.Dev)
                {
                    Verify = System.IO.File.Exists(System.IO.Path.Combine(Main.Entry.Path, "exit-verify.txt"));
                    Sabotage = Verify && System.IO.File.ReadAllText(System.IO.Path.Combine(Main.Entry.Path, "exit-verify.txt")).Contains("sabotage");
                    if (Verify) Main.Entry.Logger.Log("[나가기] (개발자용) 나가기마다 원래 방식과 비교함");
                }
                foreach (var m in typeof(scnGame).GetMethods(AccessTools.all))
                    if (m.Name == "LoadLevel" && m.DeclaringType == typeof(scnGame) && !m.IsAbstract) h.Patch(m, prefix: new HarmonyMethod(typeof(ExitFix), nameof(LoadLevelPrefix)));
                Main.Entry.Logger.Log("[나가기] 설치");
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[나가기] 설치 실패: " + ex.Message); }
        }

        internal static bool Exiting, PlayStarting;
        public static void SwitchPrefix() { Exiting = true; }
        public static Exception SwitchFinalizer(Exception __exception) { Exiting = false; return __exception; }
        // 에디터 재생 시작(scnEditor.Play): 재생 준비 끝의 다시 설정(FinishCustomLevelLoading)도 같은 방식. 편집 화면에서 손대지 않은 장식은
        // 지난 다시 설정(나가기) 뒤 그대로다. 편집으로 바뀐 장식은 Setup 을 거치거나(표시) 이벤트 값이 바뀌어(지문) 원래대로 한다.
        public static void PlayPrefix() { PlayStarting = true; }
        public static Exception PlayFinalizer(Exception __exception) { PlayStarting = false; return __exception; }
        // 에디터에서 죽고 다시 하기(TransitionFix.InRestart): 판이 끝난 뒤 장식을 처음 상태로 되돌리는 것은 나가기와 같다.
        // Arche 다시 하기 3.0초 중 장식 다시 설정이 1.3~1.45초씩 두 번(ResetScene 끝, 재생 준비 끝 FinishCustomLevelLoading).
        // 가볍게 하는 것은 ResetScene 안의 다시 설정(나가기와 같은 자리)과, 같은 다시 하기에서 그 뒤에 찍은 값이 있을 때의 재생 준비 끝뿐이다.
        // ResetScene 쪽을 건너뛰고(TransitionFix) 재생 준비 끝에서 재생 시작 때 찍은 값으로 가볍게 하면, 그 사이에 바뀐 장식 변환(회전·위치·크기)을
        // 놓쳤다(2026-10-04 검증: Windflower 10개, HELLO 2026 180~190개). 그래서 가볍게 할 수 있으면 TransitionFix 는 건너뛰지 않는다(LightReady).
        private static bool baseAtSceneReset;   // 지금 값이 이번 다시 하기의 ResetScene 안에서 찍은 것인가
        internal static bool LightReady { get { return Enabled && haveBase && ADOBase.isLevelEditor; } }
        // 게임 화면에서 죽고 다시 하기: 장식 다시 설정은 ResetScene 안의 한 번뿐이다(Arche 1.2초). 첫 다시 하기는 원래대로 하고 끝에서 값을 찍어,
        // 두 번째부터 가볍게 한다(나가기와 같은 자리, 같은 판단). 재생 시작에는 다시 설정이 없어서 맵을 연 직후 값은 쓰지 않는다.
        internal static bool GameRestartReset { get { return TransitionFix.InRestartGame && SceneReset.Resetting && !ADOBase.isLevelEditor; } }
        private static string Label { get { return Exiting ? "[나가기]" : TransitionFix.InRestart || TransitionFix.InRestartGame ? "[다시 하기]" : "[재생 시작]"; } }

        // ── 재생 시작 때 찍어 두는 값 ──
        private struct Base
        {
            public scrVisualDecoration D;
            public bool Ok;   // 가볍게 할 수 있는 종류
            public Vector2 PivotPos, PivotOff, ParOff, Scale, StartPos, TexScale;
            public float Rot, StartRot, ScaleMul, Opa, RepX, RepY, MulX, MulY;
            public Color Col;
            public bool RendEn, ForceHide, LockRot, LockScale, Smooth;
            public DecPlacementType Place;
            public TextureManager.CustomSprite Sprite;
            public scrFloor Floor;
            public int FloorNum;
            public HashSet<string> Tags;
            public int TagsCount;
            public string MaskTarget, DecoTag;
            public string[] RemoveTags;   // Setup 의 "예전 태그 - 새 태그" (지금 태그 집합과 이벤트 태그 문자열로 정해진다)
        }
        private static Base[] bases = new Base[0];
        private static List<scrDecoration> baseList;
        private static int filled;
        // 새 맵 파일을 열 때: 지난 맵의 값 기록을 버린다 (다음 다시 설정까지는 원래 방식으로 돈다)
        internal static void Forget()
        {
            if (filled > 0) Array.Clear(bases, 0, filled);
            filled = 0; baseList = null; baseCount = 0; haveBase = false;
            touched.Clear();
        }
        public static void LoadLevelPrefix() { try { Forget(); } catch { } try { FfxReuse.Prune(); } catch { } try { EffectBudget.ForgetDecos(); } catch { } try { Dormancy.ForgetLevel(); } catch { } try { LoadFix.ForgetLevel(); } catch { } try { BeatFix.ForgetLevel(); } catch { } try { MeshWarm.ForgetLevel(); } catch { } try { FastMove.ForgetLevel(); } catch { } }
        private static int baseCount;
        private static bool haveBase;
        private sealed class RefEq : IEqualityComparer<scrDecoration>
        {
            public bool Equals(scrDecoration a, scrDecoration b) { return ReferenceEquals(a, b); }
            public int GetHashCode(scrDecoration o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly HashSet<scrDecoration> touched = new HashSet<scrDecoration>(new RefEq());

        public static void Touch(scrDecoration __instance) { if (haveBase) touched.Add(__instance); }

        private static bool lightRan;

        public static bool ResetPrefix(scrDecorationManager __instance, bool __runOriginal)
        {
            lightRan = false;
            if (!__runOriginal) return false;
            bool restartOk = TransitionFix.InRestart && (SceneReset.Resetting || baseAtSceneReset);
            if (((Exiting || PlayStarting || restartOk) && ADOBase.isLevelEditor || GameRestartReset) && Enabled && haveBase)
            {
                try { if (Light(__instance)) { lightRan = true; haveBase = false; return false; } }
                catch (Exception ex) { Fallbacks++; Main.Entry.Logger.Log("[나가기] 가볍게 다시 설정 실패, 원래대로: " + ex.Message); }
            }
            haveBase = false;   // 원래 다시 설정 중에는 표시하지 않는다(끝나면 새로 찍는다)
            return true;
        }

        public static void ResetPostfix(scrDecorationManager __instance, bool __runOriginal)
        {
            if (!__runOriginal && !lightRan) return;   // 원래가 돌았거나 우리가 가볍게 했을 때만 (가볍게 하면 원래를 건너뛰어 __runOriginal 이 false)
            lightRan = false;   // 가볍게 한 뒤에도 찍는다(결과가 원래 방식과 같음 - 검증)
            if (!Enabled || !(ADOBase.isLevelEditor || GameRestartReset)) return;
            baseAtSceneReset = TransitionFix.InRestart && SceneReset.Resetting;
            try { Capture(__instance); } catch (Exception ex) { haveBase = false; Main.Entry.Logger.Log("[나가기] 값 찍기 실패: " + ex.Message); }
        }

        private static void Capture(scrDecorationManager mgr)
        {
            var all = allRef(mgr);
            if (all == null || !ADOBase.customLevel) { haveBase = false; return; }
            if (bases.Length < all.Count) bases = new Base[all.Count + 256];
            // 지난번이 더 많았으면 그 뒤 칸을 비운다 (지난 맵의 지워진 장식을 계속 붙잡았다: Arche 뒤 다른 맵에서 장식 2만 8천 개)
            if (filled > all.Count) Array.Clear(bases, all.Count, filled - all.Count);
            filled = all.Count;
            for (int i = 0; i < all.Count; i++)
            {
                bases[i] = default(Base);
                var v = all[i] as scrVisualDecoration;
                if ((object)v == null || v == null) continue;
                ref Base b = ref bases[i];
                b.D = v;
                var ev = v.sourceLevelEvent;
                b.Ok = ev != null && ev.eventType == LevelEventType.AddDecoration && v.decType == DecorationType.Image && v.hitbox == HitboxType.None
                    && blendRef(v) == DecorationBlendMode.None && maskRef(v) == MaskingType.None && !v.stickToFloor && !v.syncFloorDepth
                    && (object)v.followPlanet == null && spriteRef(v) != null && v.tags != null && (object)v.parallax != null && !HasComponents(ev);
                if (!b.Ok) { if (Edition.Dev) Why(v, ev); continue; }
                try { Read(v, ref b); } catch { b.Ok = false; }   // 읽다 실패한 장식만 원래대로
            }
            baseList = all; baseCount = all.Count;
            touched.Clear();
            haveBase = true;
            if (Edition.Dev && whyCount.Count > 0) { Main.Entry.Logger.Log("[나가기] (개발자용) 값 찍기: 가볍게 못 하는 종류 " + StateDump.Top(whyCount, 12)); whyCount.Clear(); }
            if (Edition.Dev && DevRemoveMismatch > 0) { Main.Entry.Logger.Log("[나가기] (개발자용) 빼는 태그 계산이 원래 계산과 다른 장식 " + DevRemoveMismatch + "개 (원래 계산으로 씀)"); DevRemoveMismatch = 0; }
        }
        private static readonly Dictionary<string, int> whyCount = new Dictionary<string, int>();
        private static void Why(scrVisualDecoration v, LevelEvent ev)
        {
            string w = ev == null ? "이벤트 없음" : ev.eventType != LevelEventType.AddDecoration ? "이벤트 " + ev.eventType : v.decType != DecorationType.Image ? "종류 " + v.decType
                : v.hitbox != HitboxType.None ? "히트박스" : blendRef(v) != DecorationBlendMode.None ? "블렌드" : maskRef(v) != MaskingType.None ? "마스크"
                : v.stickToFloor ? "타일에 붙음" : v.syncFloorDepth ? "타일 깊이" : (object)v.followPlanet != null ? "행성 따라감" : spriteRef(v) == null ? "그림 없음"
                : v.tags == null ? "태그 없음" : (object)v.parallax == null ? "시차 없음" : HasComponents(ev) ? "components" : "?";
            int n; whyCount.TryGetValue(w, out n); whyCount[w] = n + 1;
        }

        private static bool HasComponents(LevelEvent ev)
        {
            string s = "";   // Setup 과 같은 방법으로 읽는다
            ev.TryGetAndSet("components", ref s);
            return !string.IsNullOrEmpty(s);
        }

        private static void Read(scrVisualDecoration v, ref Base b)
        {
            b.PivotPos = v.pivotPosVec; b.PivotOff = v.pivotOffsetVec; b.ParOff = v.parallaxOffset; b.Scale = v.scaleVec; b.StartPos = v.startPos; b.TexScale = v.textureScaleMultiplier;
            b.Rot = v.rotAngle; b.StartRot = v.startRot; b.ScaleMul = v.scaleMultiplier; b.Opa = v.opacity; b.RepX = v.repeatX; b.RepY = v.repeatY;
            b.MulX = v.parallax.multiplier_x; b.MulY = v.parallax.multiplier_y;
            b.Col = v.color; b.RendEn = rendEnRef(v); b.ForceHide = v.forceHide; b.LockRot = v.lockRotation; b.LockScale = v.lockScale; b.Smooth = v.smoothing;
            b.Place = v.placementType; b.Sprite = spriteRef(v); b.Floor = v.parentFloor; b.FloorNum = v.parentFloorNum;
            b.Tags = v.tags; b.TagsCount = v.tags.Count; b.MaskTarget = v.spriteAlphaMask != null ? v.spriteAlphaMask.targetTag : null; b.DecoTag = v.decorationTag;
            // Setup 의 계산은 tags.Except(ev["tag"].ToString().Split(' ', 빈 것 빼고)). tags 는 방금 Setup 이 그 나눈 결과로 만든 집합(비었으면 {"NO TAG"})이고
            // 게임 코드는 tags 를 통째로 바꿀 뿐 안을 고치지 않으므로(IL: stfld 만), 결과는 "태그 없는 장식이면 NO TAG, 아니면 없음" 이다.
            // (공백으로 나누므로 "NO TAG" 하나짜리 태그는 이벤트에서 나올 수 없다.) 나갈 때도 tags 는 같은 객체·같은 개수여야 한다.
            b.RemoveTags = v.tags.Count == 1 && v.tags.Contains("NO TAG") ? NoTagArr : EmptyArr;
            if (Edition.Dev)
            {
                var full = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Except(v.tags, v.sourceLevelEvent["tag"].ToString().Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries)));
                if (full.Length != b.RemoveTags.Length || (full.Length == 1 && full[0] != b.RemoveTags[0])) { DevRemoveMismatch++; b.RemoveTags = full; }
            }
        }
        private static readonly string[] NoTagArr = { "NO TAG" }, EmptyArr = new string[0];
        internal static long DevRemoveMismatch;

        // 찍어 둔 값과 지금 값이 모두 같은가 (float 는 비트까지: 같은 계산이면 같은 값)
        private static bool Same(scrVisualDecoration v, ref Base b)
        {
            return Eq(v.pivotPosVec, b.PivotPos) && Eq(v.pivotOffsetVec, b.PivotOff) && Eq(v.parallaxOffset, b.ParOff) && Eq(v.scaleVec, b.Scale)
                && Eq(v.startPos, b.StartPos) && Eq(v.textureScaleMultiplier, b.TexScale)
                && Eq(v.rotAngle, b.Rot) && Eq(v.startRot, b.StartRot) && Eq(v.scaleMultiplier, b.ScaleMul) && Eq(v.opacity, b.Opa) && Eq(v.repeatX, b.RepX) && Eq(v.repeatY, b.RepY)
                && (object)v.parallax != null && Eq(v.parallax.multiplier_x, b.MulX) && Eq(v.parallax.multiplier_y, b.MulY)
                && Eq(v.color.r, b.Col.r) && Eq(v.color.g, b.Col.g) && Eq(v.color.b, b.Col.b) && Eq(v.color.a, b.Col.a)
                && rendEnRef(v) == b.RendEn && v.forceHide == b.ForceHide && v.lockRotation == b.LockRot && v.lockScale == b.LockScale && v.smoothing == b.Smooth
                && v.placementType == b.Place && ReferenceEquals(spriteRef(v), b.Sprite) && ReferenceEquals(v.parentFloor, b.Floor) && v.parentFloorNum == b.FloorNum
                && ReferenceEquals(v.tags, b.Tags) && v.tags.Count == b.TagsCount && v.spriteAlphaMask != null && string.Equals(v.spriteAlphaMask.targetTag, b.MaskTarget, StringComparison.Ordinal)
                && ReferenceEquals(v.decorationTag, b.DecoTag)
                && v.hitbox == HitboxType.None && blendRef(v) == DecorationBlendMode.None && maskRef(v) == MaskingType.None && !v.stickToFloor && !v.syncFloorDepth
                && (object)v.followPlanet == null && v.decType == DecorationType.Image;
        }
        private static bool Eq(float a, float b) { return BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b); }
        private static bool Eq(Vector2 a, Vector2 b) { return Eq(a.x, b.x) && Eq(a.y, b.y); }

        // 다른 모드가 Setup 이나 건너뛰는 설정 함수를 고쳤으면 가볍게 하지 않는다(그 모드가 할 일이 빠지므로)
        private static bool Foreign()
        {
            if (foreignChecked) return foreign;
            foreignChecked = true;
            var names = new[] { "Setup", "SetBlendMode", "SetSprite", "SetTile", "SetTextureScaleMultiplier", "SetMaskingType", "SetMaskingTarget", "SetMaskingDepth",
                                "UpdateHitbox", "SetScale", "SetDepth", "SetParallax", "SetColor", "SetOpacity", "SetRotation" };
            var who = new List<string>();
            foreach (var t in new[] { typeof(scrDecoration), typeof(scrVisualDecoration) })
                foreach (var m in t.GetMethods(AccessTools.all))
                {
                    if (m.DeclaringType != t || Array.IndexOf(names, m.Name) < 0) continue;
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null) continue;
                    foreach (var o in info.Owners) if (!o.StartsWith("StutterFix", StringComparison.OrdinalIgnoreCase) && !who.Contains(o + ":" + m.Name)) who.Add(o + ":" + m.Name);
                }
            var rd = Harmony.GetPatchInfo(AccessTools.Method(typeof(scrDecorationManager), "ResetDecorations"));
            if (rd != null) foreach (var p in rd.Transpilers) if (!p.owner.StartsWith("StutterFix", StringComparison.OrdinalIgnoreCase)) who.Add(p.owner + ":ResetDecorations");
            foreign = who.Count > 0;
            if (foreign) Main.Entry.Logger.Log("[나가기] 다른 모드가 장식 설정을 고쳐서 가볍게 다시 설정은 끔: " + string.Join(", ", who.ToArray()));
            return foreign;
        }

        private static readonly Dictionary<Type, bool> cfpType = new Dictionary<Type, bool>();
        private static readonly List<MonoBehaviour> cfpTmp = new List<MonoBehaviour>();

        // scrDecoration.Setup 끝의 cfpCache = GetComponents<MonoBehaviour>().Where(이름이 "CameraFilterPack_" 로 시작).ToArray() 와 같다
        // (형식 이름 비교 결과만 형식마다 기억한다. 비교 방식은 원래와 같은 string.StartsWith(string))
        private static MonoBehaviour[] CfpCache(scrDecoration d)
        {
            cfpTmp.Clear();
            foreach (var c in d.GetComponents<MonoBehaviour>())
            {
                var t = c.GetType();
                bool yes;
                if (!cfpType.TryGetValue(t, out yes)) { yes = t.Name.StartsWith("CameraFilterPack_"); cfpType[t] = yes; }
                if (yes) cfpTmp.Add(c);
            }
            return cfpTmp.ToArray();
        }

        // 나가기의 ResetDecorations 대신. 원래: 태그·히트박스 목록 비우고, 장식 목록 순서대로 Setup(ev) + hitOnce = false.
        // false 면 아무것도 안 했으니 원래대로 하면 된다.
        private static bool Light(scrDecorationManager mgr)
        {
            var all = allRef(mgr);
            var cl = ADOBase.customLevel;
            string why = null;
            if (all == null || !cl) why = "맵 없음";
            else if (!ReferenceEquals(all, baseList) || all.Count != baseCount) why = "장식 목록이 바뀜";
            else if (Foreign()) why = "다른 모드";
            else if (!LoadFix.SameDecorationData()) why = "장식 데이터가 바뀜";
            if (why != null) { Main.Entry.Logger.Log(Label + " 장식 다시 설정 원래대로 (" + why + ")"); return false; }

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            Exits++;
            var floors = ADOBase.lm.listFloors;
            string dir = System.IO.Path.GetDirectoryName(ADOBase.levelPath);
            var sprites = mgr.imageHolder.customSprites;   // Setup 이 그림을 찾는 곳
            bool editor = ADOBase.isLevelEditor;
            var tagged = mgr.taggedDecorations;
            List<int> lightIdx = Verify ? new List<int>() : null;
            HashSet<scrVisualDecoration> alphaBefore = null; HashSet<string> tileBefore = null; bool refreshBefore = SpriteAlphaMaskUtils.doRefreshMaskCache;
            if (Verify) { alphaBefore = new HashSet<scrVisualDecoration>(alphaSetRef(mgr)); tileBefore = new HashSet<string>(tileKeysRef(mgr)); }

            tagged.Clear();
            mgr.hitboxEventTags.Clear();
            mgr.hitboxEventTagDecorations.Clear();
            int light = 0, full = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                bool done = false;
                if (bases[i].Ok && ReferenceEquals(bases[i].D, d) && d != null && !touched.Contains(d))
                    done = LightOne(mgr, bases[i].D, ref bases[i], floors, dir, sprites, tagged, editor);
                if (done) { light++; if (lightIdx != null) lightIdx.Add(i); if (Sabotage && light % 500 == 0) Sabotage1(mgr, (scrVisualDecoration)d, light / 500); }
                else { d.Setup(d.sourceLevelEvent, out _); full++; }
                d.hitOnce = false;
            }
            try { Dormancy.MarkHitboxDirty(); } catch { }
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            LightTotal += light; FullTotal += full;
            LastLine = string.Format("장식 {0}개 중 판 중에 안 바뀐 {1}개는 가볍게, {2}개는 원래대로 다시 설정 {3:F0}ms", all.Count, light, full, ms);
            Main.Entry.Logger.Log(Label + " " + LastLine);
            if (Verify) RunVerify(mgr, all, lightIdx, alphaBefore, tileBefore, refreshBefore);
            return true;
        }

        private static bool LightOne(scrDecorationManager mgr, scrVisualDecoration v, ref Base b, List<scrFloor> floors, string dir,
            Dictionary<string, TextureManager.CustomSprite> sprites, Dictionary<string, List<scrDecoration>> tagged, bool editor)
        {
            var ev = v.sourceLevelEvent;
            // 판단 (아무것도 바꾸기 전)
            if (!Same(v, ref b)) return false;
            int fn = Math.Clamp(ev.floor, 0, floors.Count - 1);
            if (fn != v.parentFloorNum || fn < 0 || fn >= floors.Count || !ReferenceEquals(floors[fn], v.parentFloor)) return false;
            string img = ev["decorationImage"] as string;
            if (string.IsNullOrEmpty(img) || img.StartsWith("prefab:", StringComparison.CurrentCultureIgnoreCase)) return false;
            // SetBlendMode(None) 가 두는 상태 그대로인가(블렌드 효과 꺼짐, 그림 재질 셰이더, 메시 재질). 누가 바꿨으면 원래 Setup
            var sr = v.spriteRenderer;
            var sm = sr.sharedMaterial;
            if (v.blendModeEffect.enabled || v.meshBlendModeEffect.enabled || sm == null || sm.shader != scrDecorationManager.tileShader
                || meshMatRef(v) == null || v.meshRenderer.sharedMaterial != meshMatRef(v)) return false;
            // Setup 의 첫 일: 이미지 확인(파일이 바뀌었으면 다시 부름). 그 결과가 지금 그림과 다르면 원래 Setup (다시 불러도 같은 결과)
            ADOBase.customLevel.imgHolder.GetOrAddSprite(img, System.IO.Path.Combine(dir, img), out _);
            TextureManager.CustomSprite cs;
            if (!sprites.TryGetValue(img, out cs) || cs == null || !ReferenceEquals(cs, b.Sprite)) return false;

            // 여기부터 Setup 과 같은 순서
            v.SetPlacementType(v.placementType);   // 붙은 타일 위치로 startPos 다시 계산 (타일이 옮겨졌을 수 있다)
            foreach (string tag in v.tags)
            {
                List<scrDecoration> l;
                if (!tagged.TryGetValue(tag, out l)) { l = new List<scrDecoration>(); tagged[tag] = l; }
                l.Add(v);
            }
            // Setup 은 "예전 태그 중 새 태그에 없는 것" 목록에서 이 장식을 뺀다(RemoveAll(Equals)). 태그 없는 장식은 예전 태그가 "NO TAG" 이고
            // 새 태그가 비어 있어서, 방금 넣은 "NO TAG" 목록에서 바로 다시 빠진다(게임 동작 그대로 따라 함)
            var rm = b.RemoveTags;
            for (int k = 0; k < rm.Length; k++) tagged[rm[k]].RemoveAll(v.Equals);
            // SetSprite 끝: 그림이 있으면 점 샘플링 키워드, (1.5 기능이면) 텍스처 반복 끔 - 여러 장식이 같이 쓰는 텍스처라 원래처럼 다시 한다
            var spr = sr.sprite;
            if ((bool)spr)
            {
                sr.material.SetPointSamplingVariant(!v.smoothing);
                if (!ADOBase.controller.disableV15Features) spr.texture.wrapMode = TextureWrapMode.Clamp;
            }
            // SetTile: 그림이 있으면 타일링 갱신 알림 / SetMaskingTarget: 같은 태그를 마스크 대상으로 하는 장식들의 마스크 캐시 갱신 알림
            if (spr != null) mgr.UpdateDecorationTiling(img);
            List<scrDecoration> same;
            if (b.MaskTarget != null && tagged.TryGetValue(b.MaskTarget, out same))
                for (int k = 0; k < same.Count; k++) { var sv = same[k] as scrVisualDecoration; if ((object)sv != null) mgr.ForceUpdateAlphaMaskCache(sv); }
            v.SetPosition(v.startPos, v.pivotOffsetVec);   // 위치·시차·회전·크기 (카메라 기준도 지금 카메라로)
            applyColor(v);
            v.SetVisible(ev.visible && !v.forceHide);
            SpriteAlphaMaskUtils.doRefreshMaskCache = true;
            v.cfpCache = CfpCache(v);
            if (editor) v.SetCollider(true);
            return true;
        }

        // (개발자용, 검증기 시험) 가볍게 한 장식 500개마다 하나씩 일부러 틀리게: 검증이 잡아내는지 본다
        internal static bool Sabotage;
        private static void Sabotage1(scrDecorationManager mgr, scrVisualDecoration v, int k)
        {
            switch (k % 4)
            {
                case 0: v.spriteRenderer.sortingOrder += 1; break;
                case 1: foreach (var l in mgr.taggedDecorations.Values) l.Remove(v); break;
                case 2: v.childTransform.localPosition += new Vector3(0.01f, 0f, 0f); break;
                default: v.spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); break;
            }
        }

        // ── (개발자용) 검증 ──
        private static void RunVerify(scrDecorationManager mgr, List<scrDecoration> all, List<int> lightIdx,
            HashSet<scrVisualDecoration> alphaBefore, HashSet<string> tileBefore, bool refreshBefore)
        {
            try
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var a = new List<StateDump.Item>[lightIdx.Count];
                for (int k = 0; k < lightIdx.Count; k++) a[k] = StateDump.Deco(all[lightIdx[k]]);
                var ga = StateDump.Manager(mgr);
                // 원래 방식으로 같은 출발점에서: 매니저의 알림 목록을 가볍게 하기 전으로 돌리고 원래 ResetDecorations 본문 그대로
                var aset = alphaSetRef(mgr); aset.Clear(); aset.UnionWith(alphaBefore);
                var tset = tileKeysRef(mgr); tset.Clear(); tset.UnionWith(tileBefore);
                SpriteAlphaMaskUtils.doRefreshMaskCache = refreshBefore;
                mgr.taggedDecorations.Clear(); mgr.hitboxEventTags.Clear(); mgr.hitboxEventTagDecorations.Clear();
                foreach (var d in all) { d.Setup(d.sourceLevelEvent, out _); d.hitOnce = false; }
                var byKey = new Dictionary<string, int>(); var first = new List<string>(); int diffDecos = 0;
                var ex = new System.Text.StringBuilder();
                for (int k = 0; k < lightIdx.Count; k++)
                {
                    var keys = new List<string>();
                    int n = StateDump.Compare(a[k], StateDump.Deco(all[lightIdx[k]]), byKey, keys);
                    if (n == 0) continue;
                    diffDecos++;
                    if (diffDecos <= 4)
                    {
                        string tag = ""; try { tag = Convert.ToString(all[lightIdx[k]].sourceLevelEvent["tag"]); } catch { }
                        ex.AppendFormat(" [#{0} 태그 '{1}': {2}]", lightIdx[k], tag, string.Join(", ", keys.ToArray()));
                    }
                }
                var gKeys = new Dictionary<string, int>();
                int gd = StateDump.Compare(ga, StateDump.Manager(mgr), gKeys, null);
                VerifyRuns++; VerifyDiffDecos += diffDecos; VerifyDiffGlobal += gd;
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Main.Entry.Logger.Log(string.Format((Exiting ? "[나가기 검증]" : "[재생 시작 검증]") + " 가볍게 한 장식 {0}개 중 원래 방식과 다른 것 {1}개{2}{3} | 매니저 {4} | 검증 {5:F0}ms",
                    lightIdx.Count, diffDecos, diffDecos > 0 ? " (" + StateDump.Top(byKey, 8) + ")" : "", ex,
                    gd == 0 ? "같음" : "다름 (" + StateDump.Top(gKeys, 6) + ")", ms));
            }
            catch (Exception e) { Main.Entry.Logger.Log("[나가기 검증] 실패: " + e); }
        }
    }
}
