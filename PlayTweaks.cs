using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 연출 끄기 (설정 창 "연출 끄기" 페이지, 전부 기본 꺼짐: 켜면 화면이 달라진다)
    //  - 맵 효과 종류별 끄기: 그 효과의 StartEffect 를 건너뛴다(시작하지 않음). 이미 켜진 효과는 다음 다시 하기부터.
    //  - 판정 글자 숨기기(전부 / 완벽만): scrHitTextManager.ShowHitText 를 건너뛴다.
    //  - 플레이 중 마우스 휠로 화면 크기 바꾸기 막기: 에디터 재생 중 휠로 부르는 scnEditor.ZoomCamera 를 건너뛴다.
    // Quartz 의 효과 제거·판정 숨기기와 함께 켜도 둘 다 건너뛸 뿐이라 부딪히지 않는다.
    internal static class PlayTweaks
    {
        internal sealed class Kind
        {
            public string Ko, En, Event, Class;   // Class: 효과 클래스 (여럿이면 쉼표로)
            public string NoteKo, NoteEn;          // 설명 (없으면 "이 효과를 시작하지 않습니다")
            public bool Deco;                      // 장식을 움직이는 효과: 히트박스 장식을 건드리면 그대로 둔다
            public bool Off;
            public bool OffNow { get { return Off || NoFx; } }
        }

        // 노이펙 모드: 아래 효과를 전부 끄고 장식도 숨긴다(게임 화면은 아예 만들지 않음). 히트박스 장식은 플레이에 필요해 그대로.
        internal static bool NoFx;
        internal static Kind DecoKind;
        internal static bool DecoOff { get { return DecoKind != null && DecoKind.OffNow; } }
        internal static long DecoNotMade, DecoHidden;

        internal static readonly Kind[] Kinds =
        {
            new Kind { Ko = "카메라 이동", En = "Camera moves", Event = "MoveCamera", Class = "ffxCameraPlus",
                NoteKo = "맵 시작 때의 카메라 설정은 그대로 두고, 그 뒤 카메라 움직임(이동·회전·확대)만 하지 않습니다.", NoteEn = "Keeps the level's starting camera and skips later camera moves (position, rotation, zoom)." },
            new Kind { Ko = "타일 색 바꾸기", En = "Tile recolor", Event = "RecolorTrack", Class = "ffxRecolorFloorPlus" },
            new Kind { Ko = "타일 이동", En = "Tile moves", Event = "MoveTrack", Class = "ffxMoveFloorPlus",
                NoteKo = "타일이 움직이거나 나타나고 사라지는 효과를 하지 않습니다. 타일을 처음에 숨겨 두었다가 이 효과로 보여 주는 맵은 타일이 안 보일 수 있습니다.", NoteEn = "Skips tile moves, fades and appearances. Levels that hide tiles at first and reveal them with this effect may show no tiles." },
            new Kind { Ko = "배경 바꾸기", En = "Background changes", Event = "CustomBackground", Class = "ffxCustomBackgroundPlus" },
            new Kind { Ko = "행성 크기", En = "Planet scale", Event = "ScalePlanets", Class = "ffxScalePlanetsPlus" },
            new Kind { Ko = "장식", En = "Decorations", Event = "Decorations", Class = "ffxMoveDecorationsPlus,ffxSetTextPlus,ffxSetObjectPlus,ffxEmitParticlePlus,ffxSetParticlePlus", Deco = true,
                NoteKo = "장식(이미지·글자·파티클·오브젝트)을 숨기고 장식 이동·글자 바꾸기·파티클 효과를 하지 않습니다. 히트박스 장식(닿으면 죽거나 이벤트가 일어나는 것)은 플레이에 필요해서 그대로 둡니다. 게임 화면으로 열면 장식을 아예 만들지 않아 맵이 빨리 열리고, 에디터에서는 재생하는 동안만 숨깁니다. 다음에 맵을 열거나 재생할 때부터 적용됩니다.",
                NoteEn = "Hides decorations (images, text, particles, objects) and skips decoration moves, text changes and particle effects. Hitbox decorations (that kill or trigger events) stay, since play needs them. Opened in the game screen, decorations are not created at all so the level opens faster; in the editor they are hidden only while playing. Applies from the next open or play." },
            new Kind { Ko = "필터", En = "Filters", Event = "SetFilter", Class = "ffxSetFilterPlus" },
            new Kind { Ko = "고급 필터", En = "Advanced filters", Event = "SetFilterAdvanced", Class = "ffxSetFilterAdvancedPlus" },
            new Kind { Ko = "블룸", En = "Bloom", Event = "Bloom", Class = "ffxBloomPlus" },
            new Kind { Ko = "플래시", En = "Flash", Event = "Flash", Class = "ffxFlashPlus" },
            new Kind { Ko = "거울의 방", En = "Hall of mirrors", Event = "HallOfMirrors", Class = "ffxHallOfMirrorsPlus" },
            new Kind { Ko = "화면 흔들기", En = "Screen shake", Event = "ShakeScreen", Class = "ffxShakeScreenPlus" },
            new Kind { Ko = "화면 타일", En = "Screen tile", Event = "ScreenTile", Class = "ffxScreenTilePlus" },
            new Kind { Ko = "화면 스크롤", En = "Screen scroll", Event = "ScreenScroll", Class = "ffxScreenScrollPlus" },
            new Kind { Ko = "프레임레이트 연출", En = "Frame rate effects", Event = "SetFrameRate", Class = "ffxSetFrameRatePlus" },
        };

        internal static bool HideJudgeAll, HideJudgePerfect, NoPlayZoom;
        // 필터 하나씩 끄기: "SetFilter:<Filter 이름>" / "SetFilterAdvanced:<필터 클래스 이름>"
        internal static readonly HashSet<string> FiltersOff = new HashSet<string>();
        internal static long FilterOffs;
        internal static long Skipped, HiddenJudge, BlockedZoom;
        private static readonly Dictionary<MethodBase, Kind> byMethod = new Dictionary<MethodBase, Kind>();

        internal static void Install(Harmony h)
        {
            int n = 0;
            foreach (var k in Kinds)
            {
                if (k.Deco) DecoKind = k;
                foreach (var cls in k.Class.Split(','))
                {
                    try
                    {
                        var t = AccessTools.TypeByName(cls);
                        var m = t == null ? null : t.GetMethod("StartEffect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(scrPlanet) }, null);   // StartEffect(scrPlanet) (부모의 StartEffect() 도 이걸 부른다)
                        if (m == null) { Main.Entry.Logger.Log("[연출 끄기] " + k.Ko + ": 효과 함수 없음 (" + cls + ")"); continue; }
                        h.Patch(m, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(EffectPrefix)) { priority = Priority.First });
                        byMethod[m] = k; n++;
                        if (k.Deco) { var f = AccessTools.Field(t, "targetTags"); if (f != null) tagField[m] = f; }
                        // 필터는 효과를 통째로 건너뛰지 않고(다른 필터 끄기 등은 그대로), 돈 직후에 꺼 둔 필터만 끈다
                        if (k.Event == "SetFilter") h.Patch(m, postfix: new HarmonyMethod(typeof(PlayTweaks), nameof(FilterPostfix)) { priority = Priority.Last });
                        if (k.Event == "SetFilterAdvanced") h.Patch(m, postfix: new HarmonyMethod(typeof(PlayTweaks), nameof(AdvFilterPostfix)) { priority = Priority.Last });
                    }
                    catch (Exception ex) { Main.Entry.Logger.Log("[연출 끄기] " + k.Ko + " 설치 실패: " + ex.Message); }
                }
            }
            try
            {
                // 장식: 게임 화면은 만들지 않고, 에디터는 재생 동안만 숨긴다 (에디터는 장식 목록 번호로 장식을 찾아서 빼면 안 된다)
                var create = AccessTools.Method(typeof(scrDecorationManager), "CreateDecoration");
                if (create != null) h.Patch(create, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(CreatePrefix)) { priority = Priority.First }, finalizer: new HarmonyMethod(typeof(PlayTweaks), nameof(CreateFinalizer)));
                var sprite = AccessTools.Method(typeof(TextureManager), "GetOrAddSprite");
                if (sprite != null) h.Patch(sprite, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(SpritePrefix)) { priority = Priority.First });
                var play = AccessTools.Method(typeof(scnEditor), "Play", Type.EmptyTypes);
                if (play != null) h.Patch(play, postfix: new HarmonyMethod(typeof(PlayTweaks), nameof(PlayPostfix)));
                var upd = AccessTools.Method(typeof(scnGame), "UpdateDecorationObjects");
                if (upd != null) h.Patch(upd, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(ResetDecoCount)), finalizer: new HarmonyMethod(typeof(PlayTweaks), nameof(DecoLoadDone)));
                var sw = AccessTools.Method(typeof(scnEditor), "SwitchToEditMode");
                if (sw != null) h.Patch(sw, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(UnhideDecorations)) { priority = Priority.First });
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[연출 끄기] 장식 설치 실패: " + ex.Message); }
            try
            {
                var show = AccessTools.Method(AccessTools.TypeByName("scrHitTextManager"), "ShowHitText");
                if (show != null) h.Patch(show, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(HitTextPrefix)) { priority = Priority.First });
                var zoom = AccessTools.Method(typeof(scnEditor), "ZoomCamera");
                if (zoom != null) h.Patch(zoom, prefix: new HarmonyMethod(typeof(PlayTweaks), nameof(ZoomPrefix)) { priority = Priority.First });
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[연출 끄기] 판정 글자·확대 설치 실패: " + ex.Message); }
            Main.Entry.Logger.Log("[연출 끄기] 설치 (효과 " + n + "종)");
        }

        private static readonly Dictionary<MethodBase, FieldInfo> tagField = new Dictionary<MethodBase, FieldInfo>();

        // 맵 이벤트로 만든 효과만 건너뛴다 (맵 시작 카메라처럼 게임이 이벤트 없이 만든 것은 그대로)
        public static bool EffectPrefix(ffxPlusBase __instance, MethodBase __originalMethod)
        {
            Kind k;
            if (!byMethod.TryGetValue(__originalMethod, out k) || !k.OffNow) return true;
            if (__instance == null || __instance.sourceLevelEvent == null) return true;
            if (k.Deco && TouchesHitbox(__instance, __originalMethod)) return true;
            Skipped++;
            return false;
        }

        // 이 장식 효과가 히트박스 장식을 건드리는지 (그러면 플레이가 달라지므로 그대로 돌린다)
        private static bool TouchesHitbox(ffxPlusBase fx, MethodBase m)
        {
            try
            {
                FieldInfo f;
                if (!tagField.TryGetValue(m, out f)) return false;
                var tags = f.GetValue(fx) as List<string>;
                var mgr = scrDecorationManager.instance;
                if (tags == null || mgr == null) return false;
                foreach (var d in mgr.GetTaggedDecorations(tags)) if (d != null && d.useHitbox) return true;
            }
            catch { return true; }
            return false;
        }

        private static bool HasHitbox(LevelEvent ev)
        {
            try { HitboxType hb; return ev != null && ev.TryGet<HitboxType>("hitbox", out hb) && hb != HitboxType.None; }
            catch { return true; }
        }

        // 맵을 열 때 이 장식을 만들지 않을지 (게임 화면에서만. 이미지 미리 풀기도 이걸 보고 건너뛴다)
        internal static bool SkipDecoAtLoad(LevelEvent ev)
        {
            return DecoOff && !ADOBase.isLevelEditor && !HasHitbox(ev);
        }

        // 게임 화면에서 장식을 안 만들 때는 장식 이동 효과가 바꿔 넣을 이미지도 미리 불러오지 않는다
        // (UpdateDecorationObjects 끝의 MoveDecorations 이미지 불러오기. 장식 만들기 안에서 부른 것은 그대로)
        internal static bool SkipMoveImagesAtLoad { get { return DecoOff && !ADOBase.isLevelEditor; } }
        private static bool decoLoad;
        private static int createDepth;
        internal static long ImagesNotLoaded;
        public static void ResetDecoCount(bool reloadDecorations)
        {
            if (reloadDecorations) { DecoNotMade = 0; ImagesNotLoaded = 0; }
            decoLoad = reloadDecorations && SkipMoveImagesAtLoad; createDepth = 0;
        }
        public static Exception DecoLoadDone(Exception __exception, bool reloadDecorations)
        {
            decoLoad = false;
            if (reloadDecorations && DecoOff)
                Main.Entry.Logger.Log("[연출 끄기] " + (NoFx ? "노이펙: " : "") + (ADOBase.isLevelEditor ? "에디터라 장식은 재생 때 숨김"
                    : "장식 " + DecoNotMade + "개 안 만듦 (히트박스 장식만 남김), 장식 이동용 이미지 " + ImagesNotLoaded + "개 안 불러옴"));
            return __exception;
        }
        public static bool SpritePrefix(ref TextureManager.CustomSprite __result, ref LoadResult status)
        {
            if (!decoLoad || createDepth > 0) return true;
            __result = null; status = LoadResult.Successful;
            ImagesNotLoaded++;
            return false;
        }
        public static Exception CreateFinalizer(Exception __exception, bool __runOriginal)
        {
            if (__runOriginal && createDepth > 0) createDepth--;
            return __exception;
        }

        public static bool CreatePrefix(LevelEvent levelEvent, ref bool spritesLoaded)
        {
            if (!SkipDecoAtLoad(levelEvent)) { if (decoLoad) createDepth++; return true; }
            spritesLoaded = false;
            DecoNotMade++;
            return false;
        }

        // 에디터: 재생을 시작하면 히트박스 없는 장식을 끄고, 편집으로 돌아갈 때 다시 켠다
        private static readonly List<GameObject> hidden = new List<GameObject>();
        private static readonly AccessTools.FieldRef<scrDecorationManager, List<scrDecoration>> allDecos =
            AccessTools.FieldRefAccess<scrDecorationManager, List<scrDecoration>>("allDecorations");
        public static void PlayPostfix()
        {
            if (!DecoOff) return;
            try
            {
                var mgr = scrDecorationManager.instance;
                var all = mgr != null ? allDecos(mgr) : null;
                if (all == null) return;
                foreach (var d in all)
                {
                    if (d == null || d.useHitbox) continue;
                    var go = d.gameObject;
                    if (!go.activeSelf) continue;
                    go.SetActive(false);
                    hidden.Add(go);
                }
                DecoHidden = hidden.Count;
            }
            catch (Exception ex) { Main.Entry.Logger.Log("[연출 끄기] 장식 숨기기 실패: " + ex.Message); }
        }
        public static void UnhideDecorations()
        {
            if (hidden.Count == 0) return;
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear();
        }

        // 일반 필터: 그 필터의 애니메이션(매 프레임 필터를 다시 켠다)을 멈추고 컴포넌트를 끈다
        private static System.Reflection.PropertyInfo compsProp, tweensProp;
        public static void FilterPostfix(ffxSetFilterPlus __instance)
        {
            if (FiltersOff.Count == 0 || __instance == null || !FiltersOff.Contains("SetFilter:" + __instance.filter)) return;
            try
            {
                if (compsProp == null) { compsProp = AccessTools.Property(typeof(ffxSetFilterPlus), "filterToComp"); tweensProp = AccessTools.Property(typeof(ffxSetFilterPlus), "filterTween"); }
                var tweens = tweensProp.GetValue(__instance, null) as Dictionary<Filter, DG.Tweening.Tween>;
                DG.Tweening.Tween tw;
                if (tweens != null && tweens.TryGetValue(__instance.filter, out tw)) { DG.Tweening.TweenExtensions.Kill(tw, false); tweens.Remove(__instance.filter); }
                var comps = compsProp.GetValue(__instance, null) as Dictionary<Filter, MonoBehaviour>;
                MonoBehaviour c;
                if (comps != null && comps.TryGetValue(__instance.filter, out c) && c != null) c.enabled = false;
                FilterOffs++;
            }
            catch { }
        }

        // 고급 필터: 효과 끝에서 켠 그 필터 컴포넌트를 끈다 (뒤따르는 애니메이션은 필드 값만 바꾼다)
        private static readonly AccessTools.FieldRef<ffxSetFilterAdvancedPlus, Dictionary<GameObject, MonoBehaviour>> advComps =
            AccessTools.FieldRefAccess<ffxSetFilterAdvancedPlus, Dictionary<GameObject, MonoBehaviour>>("filterMonoBehaviours");
        public static void AdvFilterPostfix(ffxSetFilterAdvancedPlus __instance)
        {
            if (FiltersOff.Count == 0 || __instance == null || __instance.filterName == null || !FiltersOff.Contains("SetFilterAdvanced:" + __instance.filterName)) return;
            try
            {
                var comps = advComps(__instance);
                if (comps != null) foreach (var c in comps.Values) if (c != null) c.enabled = false;
                FilterOffs++;
            }
            catch { }
        }

        // 지금 맵의 필터 종류별 개수 (키, 보여 줄 이름, 개수)
        private static object filtersFor;
        private static readonly List<KeyValuePair<string, int>> filterList = new List<KeyValuePair<string, int>>();
        internal static List<KeyValuePair<string, int>> FiltersInLevel()
        {
            var ld = scnGame.instance != null ? scnGame.instance.levelData : null;
            if (!ReferenceEquals(ld, filtersFor))
            {
                filtersFor = ld; filterList.Clear();
                var d = new Dictionary<string, int>();
                if (ld != null && ld.levelEvents != null)
                    foreach (var ev in ld.levelEvents)
                    {
                        if (ev == null) continue;
                        string t = ev.eventType.ToString();
                        if (t != "SetFilter" && t != "SetFilterAdvanced") continue;
                        object f = null; try { f = ev["filter"]; } catch { }
                        if (f == null) continue;
                        string key = t + ":" + f;
                        int c; d.TryGetValue(key, out c); d[key] = c + 1;
                    }
                filterList.AddRange(d);
                filterList.Sort((a, b) => b.Value.CompareTo(a.Value));
            }
            return filterList;
        }
        // 필터 묶음 (보기 편하게): 일반 필터는 종류별로, 고급 필터는 이름의 분류 부분(CameraFilterPack_<분류>_...)으로
        private static readonly Dictionary<string, string[]> normalGroup = new Dictionary<string, string[]>();
        private static void G(string ko, string en, params string[] names) { foreach (var n in names) normalGroup[n] = new[] { ko, en }; }
        static PlayTweaks()
        {
            G("색·톤", "Color & tone", "Grayscale", "Sepia", "Invert", "Posterize", "Contrast", "Neon", "Funk", "Sharpen");
            G("흐림", "Blur", "Blur", "BlurFocus", "GaussianBlur", "MotionBlur");
            G("왜곡", "Distortion", "Fisheye", "Waves", "Aberration", "Tunnel", "Weird3D", "Pixelate", "WaterDrop", "LightWater");
            G("TV·노이즈", "TV & noise", "VHS", "EightiesTV", "FiftiesTV", "Arcade", "LED", "Glitch", "Static", "Grain", "Compression", "Handheld", "NightVision");
            G("그림", "Drawing", "Drawing", "OilPaint", "SuperDot", "HexagonBlack", "EdgeBlackLine");
            G("날씨·입자", "Weather & particles", "Rain", "Blizzard", "PixelSnow", "Petals", "PetalsInstant");
        }
        private static readonly Dictionary<string, string> advKo = new Dictionary<string, string>
        {
            { "FX", "특수" }, { "TV", "TV" }, { "Blur", "흐림" }, { "Color", "색" }, { "Colors", "색" }, { "Distortion", "왜곡" }, { "Glow", "빛" },
            { "Light", "빛" }, { "Drawing", "그림" }, { "Pixel", "픽셀" }, { "Pixelisation", "픽셀" }, { "Vision", "시야" }, { "Atmosphere", "날씨" },
            { "Gradients", "그라디언트" }, { "Film", "필름" }, { "Edge", "외곽선" }, { "Noise", "노이즈" }, { "Real", "실사" }, { "Retro", "레트로" },
            { "Sharpen", "선명" }, { "Special", "특수" }, { "Alien", "외계" }, { "Classic", "고전" }, { "Lut", "LUT" }, { "Blend2Camera", "합성" },
            { "Broken", "깨짐" }, { "Gradient", "그라디언트" }, { "AAA", "고급 효과" }, { "Glitch", "글리치" }, { "NewGlitch", "글리치" }, { "Oculus", "VR" },
            { "3D", "3D" }, { "Cartoon", "만화" }, { "Mask", "마스크" }, { "Night", "밤" }, { "Weather", "날씨" },
        };
        // (묶음 키, 보여 줄 이름)
        internal static KeyValuePair<string, string> FilterGroup(string key)
        {
            int i = key.IndexOf(':');
            string name = i >= 0 ? key.Substring(i + 1) : key;
            if (key.StartsWith("SetFilterAdvanced:"))
            {
                string rest = name.StartsWith("CameraFilterPack_") ? name.Substring(17) : name;
                int u = rest.IndexOf('_');
                string cat = u > 0 ? rest.Substring(0, u) : rest;
                string ko; if (!advKo.TryGetValue(cat, out ko)) ko = cat;
                return new KeyValuePair<string, string>("A:" + cat, SettingsWindow.T("고급 · " + ko, "Advanced · " + cat));
            }
            string[] g;
            if (normalGroup.TryGetValue(name, out g)) return new KeyValuePair<string, string>("N:" + g[1], SettingsWindow.T(g[0], g[1]));
            return new KeyValuePair<string, string>("N:other", SettingsWindow.T("기타", "Other"));
        }

        internal static string FilterLabel(string key)
        {
            int i = key.IndexOf(':');
            string name = i >= 0 ? key.Substring(i + 1) : key;
            if (name.StartsWith("CameraFilterPack_")) name = name.Substring(17);
            if (key.StartsWith("SetFilterAdvanced:")) { int u = name.IndexOf('_'); if (u > 0) name = name.Substring(u + 1); }   // 묶음 이름(분류)은 빼고
            return name.Replace('_', ' ');
        }

        public static bool HitTextPrefix(HitMargin hitMargin)
        {
            if (HideJudgeAll || (HideJudgePerfect && hitMargin == HitMargin.Perfect)) { HiddenJudge++; return false; }
            return true;
        }

        // 휠로 부른 것만 (에디터 재생 중, Ctrl 없이 휠을 굴린 프레임)
        public static bool ZoomPrefix(scnEditor __instance)
        {
            if (!NoPlayZoom || __instance == null || !__instance.playMode) return true;
            if (Mathf.Abs(Input.mouseScrollDelta.y) <= 0.05f) return true;
            BlockedZoom++;
            return false;
        }

        // 지금 맵에 효과가 종류별로 몇 개 있는지 (설정 창이 맵이 바뀔 때만 다시 센다)
        private static object countedFor;
        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        internal static int CountIn(string eventName)
        {
            var ld = scnGame.instance != null ? scnGame.instance.levelData : null;
            if (!ReferenceEquals(ld, countedFor))
            {
                countedFor = ld; counts.Clear();
                if (ld != null && ld.levelEvents != null)
                    foreach (var ev in ld.levelEvents)
                    {
                        if (ev == null) continue;
                        string name = ev.eventType.ToString();
                        int c; counts.TryGetValue(name, out c); counts[name] = c + 1;
                    }
            }
            if (ld != null && eventName == "Decorations") return ld.decorations != null ? ld.decorations.Count : 0;
            int r; return ld == null ? -1 : counts.TryGetValue(eventName, out r) ? r : 0;
        }
        internal static bool HaveLevel { get { return scnGame.instance != null && scnGame.instance.levelData != null; } }
    }
}
