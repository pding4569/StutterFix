using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using ADOFAI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace StutterFix
{
    // 직통 렌더러(DR2)의 장식 쪽.
    //
    // 이미지 장식을 Unity 의 SpriteRenderer 로 그리지 않고, 장식의 논리 값(위치·회전·크기·색·시차 16개)을 GPU 버퍼에 올려
    // 네이티브(sfnative dr2)가 한 번에 순서대로 그린다. 화면 위치(시차·화면 고정·회전·크기)는 정점 셰이더가 계산한다.
    //   - 맡은 장식의 Transform·SpriteRenderer 는 매 프레임 건드리지 않는다(TightLate/LogicUpdate 자리에서 값만 읽는다).
    //   - 게임 로직(효과·트윈)은 그대로 논리 값(C# 필드)을 바꾼다. 여기서는 보이는 장식만 프레임마다 필드를 읽어 바뀐 것만 올린다.
    //   - 그리기 순서: 배경 장식(정렬 층 Bg)은 메인 카메라 반투명 그리기 앞(BeforeForwardAlpha), 앞 장식(Default)은 뒤(AfterForwardAlpha).
    //     같은 층에 남는 Unity 렌더러(파티클 등)와 순서가 얽히면 그 렌더러를 명령 버퍼로 차례에 끼워 그리고,
    //     끼울 수 없는 종류(글자 캔버스·화면 복사 블렌드 등)면 그 경계 너머의 장식은 맡지 않는다(원래 렌더러 그대로).
    //   - 스프라이트 마스크: 마스크 장식은 스텐실을 +1/-1 하는 두 번의 그리기(범위 시작·끝)로, 마스크 대상은 스텐실 시험으로 그린다(Unity 와 같은 규칙).
    //   - 더하기 블렌드(모드의 FastBlend 가 바꿔 끼운 Particles/Additive 재질)도 맡는다.
    // 기본 꺼짐. AUTOTEST `dr2 on` 뒤 다음 재생 준비(PrepVfx)부터 켜진다.
    // GO 풀의 켜짐/꺼짐만 통지한다. 비활성 수천 개에 Update/Poll을 돌리지 않는다.
    internal sealed class DirectDecoLifetime : MonoBehaviour
    {
        internal Action<bool> Changed;
        void OnEnable() { Changed?.Invoke(true); }
        void OnDisable() { Changed?.Invoke(false); }
        void OnDestroy() { Changed?.Invoke(false); Changed = null; }
    }

    internal static unsafe class DirectDeco
    {
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] static extern int sf_dr2_create(IntPtr anyResource, uint maxItems);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] static extern void sf_dr2_destroy(int token);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] static extern int sf_dr2_texture(int token, int slot, IntPtr texture, int filter, int wrapU, int wrapV, int aniso);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] static extern int sf_dr2_geometry(int token, int id, float* st, uint vcount, uint* indices, uint icount);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] static extern int sf_dr2_statics(int token, uint first, uint count, float* data);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] static extern int sf_dr2_commit(int token, FrameIn* frame);
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr sf_dr2_event();
        [DllImport("sfnative", CallingConvention = CallingConvention.Cdecl)] static extern double sf_dr2_stat(int token, int index);

        [StructLayout(LayoutKind.Sequential)] struct Run { public uint start, count; public int texture, geometry; public uint flags; }
        [StructLayout(LayoutKind.Sequential)]
        struct FrameIn
        {
            public fixed float vp[16]; public fixed float cam[4]; public fixed float ortho[4];
            public uint dirtyCount, listChanged; public uint* dirtySlots; public float* channels;
            public uint* list; public Run* runs; public uint* segs; public uint listCount, runCount, segCount, clearStencil;
        }
        const uint FPoint = 1, FAdd = 2, FEnter = 4, FLeave = 8, FInside = 16, FOutside = 32;
        const int BlendShift = 8;

        internal static bool Enabled;          // 설정/명령으로 켬
        internal static bool Active;           // 지금 장식을 맡고 있음
        internal static bool Sequence = true;  // 남는 렌더러를 명령 버퍼로 차례에 끼우기
        internal static bool SeqParticles = true; // 파티클 렌더러도 끼우기
        internal static bool Tight = true;     // 텍스처의 빈 칸을 뺀 도형으로 그리기
        internal static bool Masks = true;     // 마스크·마스크 대상 장식도 맡기
        internal static int Only;              // 진단: 1 = 배경 층만, 2 = 앞 층만 맡는다
        internal static bool Force;            // 진단: 순서 경계를 무시하고 가능한 장식을 모두 맡는다(순서가 틀릴 수 있음)
        internal static bool SeqTest;          // 진단: 장식은 맡지 않고 남는 렌더러 끼워 그리기만 한다
        static Harmony hooks; static bool hooked;
        const string Owner = "StutterFix.DirectDeco";
        const int ChannelFloats = 16, StaticFloats = 32, MaxSegs = 4000, HiddenLayer = 31;

        sealed class Item
        {
            public scrVisualDecoration D; public SpriteRenderer R; public SpriteMask M; public scrParallax P; public Transform Child;
            public int Slot, Tex = -1, Geom, Placement, Layer, Order; public uint Flags; public SceneRenderOrder.Key Key;
            public bool Ok, Drawable, StructDirty, IsMask, Released, BackForceR, BackForceM;
            public int BackLayer, BackOrder, FrontLayer, FrontOrder;
            public readonly Entry First = new Entry(), Second = new Entry();
        }
        sealed class Leftover
        {
            public Renderer R; public GameObject Go; public scrDecoration Deco; public SceneRenderOrder.Key Key; public bool Seq, Bg, Suppressed, On, Masked; public int GoLayer, PollIndex = -1, EntryIndex = -1; public string Why; public Material[] Materials; public DirectDecoLifetime Lifetime;
            public readonly Entry DrawEntry = new Entry();
            public List<Material> MaterialScratch;
        }
        // 그리기 차례의 한 칸: 장식 그리기(Kind 0), 마스크 들어감(1)/나옴(2), 끼운 렌더러(Left != null)
        sealed class Entry
        {
            public Item It; public Leftover Left; public int Kind, Layer, Order, Sub, Seg; public bool Bg; public SceneRenderOrder.Key Key;
        }
        sealed class RefEq : IEqualityComparer<scrDecoration>
        {
            public bool Equals(scrDecoration a, scrDecoration b) => ReferenceEquals(a, b);
            public int GetHashCode(scrDecoration d) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(d);
        }
        static readonly Dictionary<scrDecoration, Item> itemOf = new Dictionary<scrDecoration, Item>(new RefEq());
        static readonly Dictionary<scrDecoration, List<Leftover>> leftoverOf = new Dictionary<scrDecoration, List<Leftover>>(new RefEq());
        static Item[] items = new Item[0];
        static readonly List<Entry> entries = new List<Entry>();
        static readonly List<Entry> nativeEntries = new List<Entry>();
        static readonly DirectRenderVisibilityIndex commandEntries = new DirectRenderVisibilityIndex();
        static readonly List<Leftover> sequenced = new List<Leftover>(); static readonly List<Leftover> polled = new List<Leftover>();
        static readonly Dictionary<GameObject, int> movedLayers = new Dictionary<GameObject, int>();
        static int totalSegs, bgValue, fgValue;
        static int token; static Camera main; static scrDecorationManager manager; static Transform managerTransform; static object level;
        static CommandBuffer cbBefore, cbAfter; static bool cbDirty, orderDirty, anyMask;
        static float[] channels, statics; static GCHandle hChannels, hStatics; static float* pChannels, pStatics;
        static uint[] dirty, list, segs; static GCHandle hDirty, hList, hSegs, hRuns; static Run[] runs; static int dirtyCount; static int[] dirtyStamp; static int stamp = 1;
        static bool listDirty, listSend; static int listCount, runCount, segCount;
        // 비활성 캔버스/남는 장식도 후크로 지켜본다. 안정 프레임에는 재검색/할당하지 않는다.
        static readonly HashSet<scrDecoration> boundaryWatch = new HashSet<scrDecoration>(new RefEq());
        static readonly Dictionary<scrDecoration, bool> boundaryVisible = new Dictionary<scrDecoration, bool>(new RefEq());
        static readonly Dictionary<scrDecoration, List<Canvas>> boundaryCanvases = new Dictionary<scrDecoration, List<Canvas>>(new RefEq());
        static bool boundaryDirty, verifying;
        static readonly List<Leftover> leftovers = new List<Leftover>();
        static readonly List<Canvas> canvasBounds = new List<Canvas>();
        static readonly Dictionary<string, int> unseqReasons = new Dictionary<string, int>();
        static int generation, activationOwned, staticFailures, failureLogs;
        static long boundaryChanges, pollTicks, commitMaxTicks, prepTicks;
        static bool timing;
        static readonly Dictionary<Texture2D, int> texSlot = new Dictionary<Texture2D, int>();
        static readonly Dictionary<Texture2D, int> texGeom = new Dictionary<Texture2D, int>();
        static int nextGeom = 1;
        static readonly List<Item> structQueue = new List<Item>();
        static Vector4 camLogic, orthoLogic; static int camFrame = -1;
        // 경계(끼울 수 없는 남는 것)
        static bool bgLimit, fgLimit; static SceneRenderOrder.Key bgMin, fgMax; static int bgCanvasLayer, bgCanvasOrder, fgCanvasLayer, fgCanvasOrder;
        // 통계
        static long polls, pollDirty, commits, listBuilds, structRefresh, orderBuilds, cbBuilds, released; static int ownedBg, ownedFg, ownedMask, ownedMasked, ownedAdd, rejected; static readonly Dictionary<string, int> rejectWhy = new Dictionary<string, int>();
        static long commitTicks; static int tightTextures, tightRects; static double tightCover;

        static readonly AccessTools.FieldRef<scrDecoration, bool> rendererOn = AccessTools.FieldRefAccess<scrDecoration, bool>("rendererEnabled");
        static readonly AccessTools.FieldRef<scrDecoration, Color> rendererColor = AccessTools.FieldRefAccess<scrDecoration, Color>("rendererColor");
        static readonly AccessTools.FieldRef<scrVisualDecoration, bool> meshOn = AccessTools.FieldRefAccess<scrVisualDecoration, bool>("meshRendererEnabled");
        static readonly AccessTools.FieldRef<scrParallax, bool> parInited = AccessTools.FieldRefAccess<scrParallax, bool>("inited");
        static readonly AccessTools.FieldRef<scrParallax, bool> parIsDeco = AccessTools.FieldRefAccess<scrParallax, bool>("isDecoration");
        static readonly AccessTools.FieldRef<scrParallax, Transform> parCached = AccessTools.FieldRefAccess<scrParallax, Transform>("cachedTransform");
        static readonly AccessTools.FieldRef<scrParallax, Vector2> parStart = AccessTools.FieldRefAccess<scrParallax, Vector2>("startPosition");
        static readonly AccessTools.FieldRef<scrParallax, Vector2> parEnd = AccessTools.FieldRefAccess<scrParallax, Vector2>("endPosition");
        static readonly int pTintColor = Shader.PropertyToID("_TintColor");
        static readonly GpuDecoScene.BlockCensus propertyBlocks = new GpuDecoScene.BlockCensus();
        const string TileShader = "Sprites/Tile", AddShader = "Legacy Shaders/Particles/Additive";

        static void Log(string s) => Main.Entry.Logger.Log("[직통 장식] " + s);

        // ---------------------------------------------------------------- 후크
        internal static void Install()
        {
            if (hooked) return;
            try
            {
                hooks = new Harmony(Owner);
                var prep = AccessTools.Method(typeof(scnGame), "PrepVfx", new[] { typeof(List<scrFloor>), typeof(int), typeof(List<LevelEvent>), typeof(bool) });
                if (prep == null) throw new MissingMethodException("PrepVfx");
                hooks.Patch(prep, postfix: new HarmonyMethod(typeof(DirectDeco), nameof(Prepared)) { priority = Priority.Last });
                foreach (var pair in new[] {
                    (typeof(scrController), "Restart"), (typeof(scrController), "ResetCustomLevel"), (typeof(scrController), "QuitToMainMenu"),
                    (typeof(scnEditor), "SwitchToEditMode"), (typeof(scnEditor), "QuitToMenu"), (typeof(scnEditor), "Play"), (typeof(scnGame), "ResetScene") })
                {
                    var m = AccessTools.Method(pair.Item1, pair.Item2);
                    if (m != null) hooks.Patch(m, prefix: new HarmonyMethod(typeof(DirectDeco), nameof(Ending)) { priority = Priority.First });
                }
                hooks.Patch(AccessTools.Method(typeof(scrDecoration), "LogicUpdate"), prefix: new HarmonyMethod(typeof(DirectDeco), nameof(LogicPrefix)) { priority = Priority.First });
                hooks.Patch(AccessTools.Method(typeof(scrDecorationManager), "LateUpdate"), prefix: new HarmonyMethod(typeof(DirectDeco), nameof(ManagerPrefix)) { priority = Priority.First });
                hooks.Patch(AccessTools.Method(typeof(scrVisualDecoration), "ApplyColor"), postfix: new HarmonyMethod(typeof(DirectDeco), nameof(ColorPostfix)) { priority = Priority.First });
                foreach (var t in new[] { typeof(scrVisualDecoration), typeof(scrObjectDecoration), typeof(scrParticleDecoration), typeof(scrPrefabDecoration), typeof(scrTextDecoration) })
                {
                    var m = AccessTools.DeclaredMethod(t, "SetVisible", new[] { typeof(bool) });
                    if (m != null) hooks.Patch(m, postfix: new HarmonyMethod(typeof(DirectDeco), nameof(VisiblePostfix)));
                }
                foreach (var m in typeof(scrVisualDecoration).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                    if (m.Name == "SetSprite" || m.Name == "SetDepth" || m.Name == "SetTile" || m.Name == "SetMaskingType" || m.Name == "SetBlendMode" || m.Name == "SetTextureScaleMultiplier" || m.Name == "SetMaskingDepth")
                        hooks.Patch(m, postfix: new HarmonyMethod(typeof(DirectDeco), nameof(StructPostfix)));
                foreach (var name in new[] { "SetParallax", "SetPlacementType" })
                {
                    var m = AccessTools.Method(typeof(scrDecoration), name);
                    if (m != null) hooks.Patch(m, postfix: new HarmonyMethod(typeof(DirectDeco), nameof(StructPostfix)));
                }
                UnityEngine.SceneManagement.SceneManager.activeSceneChanged += SceneChanged;
                Camera.onPreCull += PreCull;
                hooked = true;
            }
            catch (Exception e) { Log("후크 설치 실패: " + e.Message); Uninstall(); }
        }
        internal static void Uninstall()
        {
            try { Deactivate("모드 내림"); } catch { }
            if (hooked) { UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= SceneChanged; Camera.onPreCull -= PreCull; }
            try { hooks?.UnpatchAll(Owner); } catch { }
            hooks = null; hooked = false;
        }
        static void SceneChanged(UnityEngine.SceneManagement.Scene a, UnityEngine.SceneManagement.Scene b) => Deactivate("화면 전환");
        static void Ending(System.Reflection.MethodBase __originalMethod) { if (Active) Deactivate(__originalMethod.Name); }
        static void Prepared()
        {
            if (!Enabled) return;
            if (Active && ReferenceEquals(level, ADOBase.customLevel?.levelData)) return;
            try { Deactivate("재생 준비"); Activate(); }
            catch (Exception e) { Log("켜기 실패: " + e); try { Deactivate("켜기 실패"); } catch { } }
        }
        static bool LogicPrefix(scrDecoration __instance) => !(Active && SkipLate(__instance));
        static void ManagerPrefix() { if (Active) CaptureCamera(); }
        static void VisiblePostfix(scrDecoration __instance)
        {
            if (!Active) return;
            NoteBoundary(__instance);
            if (itemOf.TryGetValue(__instance, out var it)) Poll(it);
            else if (leftoverOf.TryGetValue(__instance, out var ls)) CheckLeftovers(ls);
        }
        static void ColorPostfix(scrVisualDecoration __instance)
        {
            if (!Active) return;
            if (!verifying) NoteBoundary(__instance);
            if (!OnApplyColor(__instance) && leftoverOf.TryGetValue(__instance, out var ls)) CheckLeftovers(ls);
        }
        static void StructPostfix(scrDecoration __instance)
        {
            if (!Active) return;
            if (itemOf.TryGetValue(__instance, out var it)) QueueStruct(it);
            else if (leftoverOf.TryGetValue(__instance, out var ls)) LeftoverStruct(ls);
            else if (boundaryWatch.Contains(__instance)) boundaryDirty = true;
        }
        static void QueueStruct(Item it) { if (!it.StructDirty) { it.StructDirty = true; structQueue.Add(it); } }
        // FastBlend 가 재질을 바꿔 끼웠다(화면 복사 블렌드 <-> 더하기).
        internal static void MaterialChanged(SpriteRenderer r)
        {
            if (!Active || !r) return;
            var d = r.GetComponentInParent<scrDecoration>(true); if (!d) return;
            if (itemOf.TryGetValue(d, out var it)) QueueStruct(it); else boundaryDirty = true;
        }

        // TightLate.Process / LogicUpdate 자리에서 불린다. 맡은 장식이면 값만 읽고 true(원래 처리 건너뜀).
        internal static bool SkipLate(scrDecoration d)
        {
            if (!itemOf.TryGetValue(d, out var it)) return false;
            Poll(it); return true;
        }
        internal static bool Owns(scrDecoration d) => Active && itemOf.ContainsKey(d);
        // ApplyColor 뒤: 맡은 장식이면 true.
        internal static bool OnApplyColor(scrVisualDecoration d)
        {
            if (!itemOf.TryGetValue(d, out var it)) return false;
            if (!it.Drawable && LazySprite.Count > 0 && rendererOn(d) && rendererColor(d).a != 0f) { LazySprite.Flush(d); QueueStruct(it); }
            Poll(it);
            return true;
        }

        static void CaptureCamera()
        {
            if (camFrame == Time.frameCount || !main) return;
            camFrame = Time.frameCount;
            var t = ADOBase.controller.camy.transform; Vector3 p = t.position;
            float lockMul = 1f;
            try { lockMul = main.orthographicSize * 0.2f / (ADOBase.customLevel.levelData.camZoom / 100f); } catch { }
            camLogic = new Vector4(p.x, p.y, t.rotation.eulerAngles.z, lockMul);
            // ScreenToWorldPoint는 pixelRect 비율을 강제하지 않고 카메라의 실제 aspect를 쓴다.
            orthoLogic = new Vector4(main.orthographicSize, main.aspect, Screen.width > 0 ? (float)Screen.height / Screen.width : 1f, 0f);
        }

        // ---------------------------------------------------------------- 값 읽기
        static void MarkDirty(int slot)
        {
            if (dirtyStamp[slot] == stamp) return;
            dirtyStamp[slot] = stamp; dirty[dirtyCount++] = (uint)slot;
        }
        static void Poll(Item it)
        {
            long t0 = timing ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            polls++;
            var d = it.D; float* c = pChannels + it.Slot * ChannelFloats;
            Vector2 pos = d.pivotPosVec, piv = d.pivotOffsetVec, sc = d.scaleVec, po = d.parallaxOffset; Color col = d.color; var p = it.P;
            float rot = d.rotAngle, op = d.opacity, px = p.multiplier_x, py = p.multiplier_y;
            if (c[0] != pos.x || c[1] != pos.y || c[2] != piv.x || c[3] != piv.y || c[4] != rot || c[5] != sc.x || c[6] != sc.y
                || c[7] != col.r || c[8] != col.g || c[9] != col.b || c[10] != col.a || c[11] != op || c[12] != px || c[13] != py || c[14] != po.x || c[15] != po.y)
            {
                c[0] = pos.x; c[1] = pos.y; c[2] = piv.x; c[3] = piv.y; c[4] = rot; c[5] = sc.x; c[6] = sc.y;
                c[7] = col.r; c[8] = col.g; c[9] = col.b; c[10] = col.a; c[11] = op; c[12] = px; c[13] = py; c[14] = po.x; c[15] = po.y;
                MarkDirty(it.Slot); pollDirty++;
            }
            if ((int)d.placementType != it.Placement) QueueStruct(it);
            bool drawable = it.Ok && rendererOn(d) && d.gameObject.activeInHierarchy && (it.IsMask || rendererColor(d).a != 0f);
            if (drawable != it.Drawable) { it.Drawable = drawable; listDirty = true; }
            if (timing) pollTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
        }

        // ---------------------------------------------------------------- 분류와 정적 값
        // 맡을 수 있는 이미지 장식인지. null = 맡을 수 있음.
        static string Classify(scrDecoration d)
        {
            if (d == null || d.GetType() != typeof(scrVisualDecoration)) return "이미지 장식 아님";
            var v = (scrVisualDecoration)d; var r = v.spriteRenderer;
            if (!r) return "렌더러 없음";
            if (d.hitbox != 0) return "히트박스";
            bool mask = v.isMask();
            if (mask) { if (!Masks) return "마스크"; if (!v.spriteMask) return "마스크 없음"; }
            if (r.maskInteraction != SpriteMaskInteraction.None && !Masks) return "마스크 대상";
            if (meshOn(v)) return "메시 렌더러";
            if (d.stickToFloor || d.syncFloorDepth) return "타일 따라감";
            if ((object)d.followPlanet != null) return "행성 따라감";
            var p = d.parallax; var pivot = d.pivotTrans;
            if (!p || !pivot || !parInited(p) || !parIsDeco(p) || !ReferenceEquals(p.decoration, d) || !ReferenceEquals(parCached(p), pivot) || p.dontAlterX || p.dontAlterY) return "시차 구성";
            if (!ReferenceEquals(p.cameraTransform, ADOBase.controller.camy.transform)) return "시차 카메라";
            if (!ReferenceEquals(pivot.parent, managerTransform) && (object)pivot.parent != null) return "부모 다름";
            if (!v.childTransform) return "자식 없음";
            if (r.drawMode != SpriteDrawMode.Simple) return "그리기 모드";
            var m = r.sharedMaterial;
            if (!m || !m.shader) return "재질 없음";
            string sh = m.shader.name;
            if (sh != TileShader && sh != AddShader && !(sh == GpuDecoBlend.GrabShader && !mask && GrabMode(v) > 0)) return "셰이더 " + sh;
            if (sh == AddShader && mask) return "더하기 마스크";
            if (sh == GpuDecoBlend.GrabShader && ((main.targetTexture && main.targetTexture.antiAliasing > 1) || (main.allowMSAA && QualitySettings.antiAliasing > 1))) return "Grab MSAA";
            if (v.childTransform.localRotation.x != 0f || v.childTransform.localRotation.y != 0f) return "3D 자식 회전";
            // Unity가 스프라이트용으로 넣은 기본 블록은 허용하고, 실제 사용자 덮어쓰기만 남긴다.
            if (r.HasPropertyBlock()) { string block = propertyBlocks.Read(r, new[] { m }); if (block != null) return block; }
            if (r.sprite && r.sprite.associatedAlphaSplitTexture) return "분리 알파";
            if (m.IsKeywordEnabled("PIXELSNAP_ON")) return "픽셀 맞춤";
            string layer = r.sortingLayerName;
            if (layer != "Bg" && layer != "Default") return "정렬 층 " + layer;
            int sourceLayer = movedLayers.TryGetValue(r.gameObject, out int savedLayer) ? savedLayer : r.gameObject.layer;
            if ((main.cullingMask & (1 << sourceLayer)) == 0) return "카메라 층";
            return null;
        }
        static int GrabMode(scrVisualDecoration d)
        {
            int mode = GpuDecoBlend.Id(d); var fx = d.blendModeEffect;
            return mode > 0 && fx && fx.MaskMode == BlendModes.MaskMode.Disabled
                && fx.RenderMode == BlendModes.RenderMode.SelfWithScreen ? mode : -1;
        }
        static void WatchBoundary(scrDecoration d)
        {
            if (!d || !boundaryWatch.Add(d)) return;
            boundaryVisible[d] = BoundaryVisible(d);
        }
        static bool BoundaryVisible(scrDecoration d)
        {
            if (boundaryCanvases.TryGetValue(d, out var canvases))
            {
                for (int i = 0; i < canvases.Count; i++) if (canvases[i] && canvases[i].isActiveAndEnabled) return true;
                return false;
            }
            return d && d.gameObject.activeInHierarchy && d.GetVisible();
        }
        static void NoteBoundary(scrDecoration d)
        {
            // 비활성 상태까지 준비 때 경계에 넣는다. 켜짐 변경은 재검색할 필요가 없다.
        }
        // 사각형 꼭짓점인지도 검사한다. Tight Sprite의 삼각형을 임의 사각형으로 넓히지 않는다.
        internal static bool TryUv(Vector2[] vertices, Vector2[] uv, Bounds b, out Vector2 origin, out Vector2 x, out Vector2 y)
        {
            origin = x = y = Vector2.zero;
            if (vertices.Length != 4 || uv.Length != 4 || b.size.x <= 0 || b.size.y <= 0) return false;
            int seen = 0; Vector2 right = Vector2.zero, up = Vector2.zero, far = Vector2.zero;
            for (int k = 0; k < 4; k++)
            {
                Vector2 st = new Vector2((vertices[k].x - b.min.x) / b.size.x, (vertices[k].y - b.min.y) / b.size.y);
                int ix = st.x > .5f ? 1 : 0, iy = st.y > .5f ? 1 : 0, bit = 1 << (ix + iy * 2);
                if (Mathf.Abs(st.x - ix) > 1e-5f || Mathf.Abs(st.y - iy) > 1e-5f || (seen & bit) != 0) return false;
                seen |= bit;
                if (ix == 0 && iy == 0) origin = uv[k]; else if (iy == 0) right = uv[k]; else if (ix == 0) up = uv[k]; else far = uv[k];
            }
            x = right - origin; y = up - origin;
            return (far - (origin + x + y)).sqrMagnitude < 1e-10f;
        }
        static bool itemOfCand(List<Item> cand, scrVisualDecoration d) { for (int i = 0; i < cand.Count; i++) if (ReferenceEquals(cand[i].D, d)) return true; return false; }
        static bool Identity(Transform t) => t.position == Vector3.zero && t.rotation == Quaternion.identity && t.lossyScale == Vector3.one;
        // 이 장식 자리(정렬 키)가 끼울 수 없는 남는 것들과 순서가 얽히지 않는가
        static bool WithinLimits(bool bg, SceneRenderOrder.Key key)
        {
            if (Force) return true;
            if (bg) return (!bgLimit || key.CompareTo(bgMin) < 0) && (key.Layer < bgCanvasLayer || (key.Layer == bgCanvasLayer && key.Order < bgCanvasOrder));
            return (!fgLimit || key.CompareTo(fgMax) > 0) && (key.Layer > fgCanvasLayer || (key.Layer == fgCanvasLayer && key.Order > fgCanvasOrder));
        }
        static void ReturnedMaskBoundary(Item it)
        {
            if (it.R.maskInteraction == SpriteMaskInteraction.None || it.IsMask) return;
            WatchBoundary(it.D);
            // Unity로 돌린 마스크 대상은 끼워 그릴 수 없는 렌더러다. 후보에서만 빼면 순서 경계가 사라진다.
            var key = it.Key;
            if (it.Layer <= bgValue) { if (!bgLimit || key.CompareTo(bgMin) < 0) { bgMin = key; bgLimit = true; } }
            else { if (!fgLimit || key.CompareTo(fgMax) > 0) { fgMax = key; fgLimit = true; } }
        }

        // 장식 하나의 정적 값(도형 대응·상수·텍스처·정렬 자리)을 다시 뽑는다. 맡을 수 없게 됐으면 놓는다.
        static void Refresh(Item it)
        {
            structRefresh++;
            it.StructDirty = false;
            if (it.Released) return;
            var d = it.D; var r = it.R; float* s = pStatics + it.Slot * StaticFloats;
            bool okBefore = it.Ok; int texBefore = it.Tex, geomBefore = it.Geom; uint flagsBefore = it.Flags; bool maskBefore = it.IsMask;
            string why = Classify(d);
            if (why != null) { ReleaseItem(it, why); return; }
            it.Ok = false; it.Placement = (int)d.placementType; it.IsMask = d.isMask(); it.M = d.spriteMask;
            // 정렬 자리
            var key = GpuDecoScene.SortKey(r, r.sharedMaterial, main);
            if (key.Layer != it.Layer || key.Order != it.Order || key.Material != it.Key.Material || key.Shader != it.Key.Shader)
            {
                bool bg = key.Layer <= bgValue;
                if (!it.IsMask && !WithinLimits(bg, key)) { ReleaseItem(it, "순서 바뀜"); return; }
                it.Key = key; it.Layer = key.Layer; it.Order = key.Order; orderDirty = true;
            }
            if (it.IsMask)
            {
                var m0 = it.M; int bl, bo, fl, fo;
                if (m0.isCustomRangeActive) { bl = SortingLayer.GetLayerValueFromID(m0.backSortingLayerID); bo = m0.backSortingOrder; fl = SortingLayer.GetLayerValueFromID(m0.frontSortingLayerID); fo = m0.frontSortingOrder; }
                else { bl = bo = int.MinValue; fl = fo = int.MaxValue; }
                if (bl != it.BackLayer || bo != it.BackOrder || fl != it.FrontLayer || fo != it.FrontOrder) { it.BackLayer = bl; it.BackOrder = bo; it.FrontLayer = fl; it.FrontOrder = fo; orderDirty = true; }
            }
            if (it.IsMask != maskBefore) orderDirty = true;
            Transform rt = it.IsMask ? it.M.transform : r.transform;
            var sp = it.IsMask ? it.M.sprite : r.sprite;
            string failure = !sp ? "sprite=null" : sp.packed ? "sprite.packed=true" : "texture=null";
            var tex = sp ? sp.texture : null;
            var mat = r.sharedMaterial;
            // LazySprite가 아직 만들지 않은 투명 장식은 다음 SetSprite까지 빈 슬롯으로 둔다.
            if (!sp && (!rendererOn(d) || rendererColor(d).a == 0f && !it.IsMask)) { if (okBefore) listDirty = true; return; }
            if (sp && tex && !sp.packed)
            {
                // 스프라이트 지역 좌표 -> 피벗 지역 좌표(자식의 위치는 채널에서 온다)
                Matrix4x4 M = Matrix4x4.identity; Transform t = rt; var child = it.Child; bool chain = true;
                while (!ReferenceEquals(t, child)) { M = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * M; t = t.parent; if ((object)t == null) { chain = false; break; } }
                failure = "transform.chain=false";
                if (chain)
                {
                    M = Matrix4x4.TRS(Vector3.zero, child.localRotation, child.localScale) * M;
                    Bounds b = sp.bounds; Vector3 mn = b.min, size = b.size;
                    float fx = !it.IsMask && r.flipX ? -1f : 1f, fy = !it.IsMask && r.flipY ? -1f : 1f;
                    s[0] = M.m00 * fx * size.x; s[1] = M.m01 * fy * size.y; s[2] = M.m00 * fx * mn.x + M.m01 * fy * mn.y + M.m03; s[3] = 0;
                    s[4] = M.m10 * fx * size.x; s[5] = M.m11 * fy * size.y; s[6] = M.m10 * fx * mn.x + M.m11 * fy * mn.y + M.m13; s[7] = 0;
                    Vector2 ts = d.scaleMultiplier * d.textureScaleMultiplier, crop = d.croppedPivotOffset, tm = d.textureScaleMultiplier;
                    s[8] = ts.x; s[9] = ts.y; s[10] = crop.x; s[11] = crop.y;
                    s[12] = d.startRot; s[13] = d.pivotTrans.position.z + M.m23; s[14] = tm.x; s[15] = tm.y;
                    bool add = !it.IsMask && mat.shader.name == AddShader;
                    float rx = 1f, ry = 1f;
                    if (!it.IsMask && !add)
                    {
                        if (mat.HasProperty(scrDecorationManager.ShaderProperty_RepeatX)) rx = mat.GetFloat(scrDecorationManager.ShaderProperty_RepeatX);
                        if (mat.HasProperty(scrDecorationManager.ShaderProperty_RepeatY)) ry = mat.GetFloat(scrDecorationManager.ShaderProperty_RepeatY);
                    }
                    uint flags = (d.lockScale ? 1u : 0u) | (d.lockRotation ? 2u : 0u);
                    if (d.placementType == DecPlacementType.Camera || d.placementType == DecPlacementType.CameraAspect) flags |= 128u;
                    if (d.placementType == DecPlacementType.CameraAspect) flags |= 256u;
                    int bm = !it.IsMask && mat.shader.name == GpuDecoBlend.GrabShader ? GrabMode(d) : 0;
                    s[16] = rx; s[17] = ry; s[18] = flags; s[19] = bm;
                    // min/max UV는 방향을 잃는다. 실제 정점-UV 대응으로 affine 식을 구한다.
                    var uv = sp.uv; var vertices = sp.vertices;
                    bool uvOk = TryUv(vertices, uv, b, out Vector2 uv0, out Vector2 uvX, out Vector2 uvY);
                    s[20] = uv0.x; s[21] = uv0.y; s[22] = uvX.x; s[23] = uvY.y; s[3] = uvY.x; s[7] = uvX.y;
                    Color tint = Color.white;
                    if (add) { if (mat.HasProperty(pTintColor)) { tint = mat.GetColor(pTintColor) * 2f; } }
                    else if (!it.IsMask && mat.HasProperty("_Color")) tint = mat.GetColor("_Color");
                    s[24] = tint.r; s[25] = tint.g; s[26] = tint.b; s[27] = tint.a;
                    s[28] = 1f; s[29] = it.IsMask ? it.M.alphaCutoff : 0f; s[30] = tex.width; s[31] = tex.height;
                    bool finite = true; for (int i = 0; i < StaticFloats; i++) if (float.IsNaN(s[i]) || float.IsInfinity(s[i])) finite = false;
                    failure = !uvOk ? "sprite.uv=non-affine/non-quad vertices=" + vertices.Length :
                        "scale=" + ts + " textureScale=" + tm + " repeat=" + rx + "," + ry;
                    if (!finite) for (int i = 0; i < StaticFloats; i++) if (float.IsNaN(s[i]) || float.IsInfinity(s[i])) { failure = "static[" + i + "]=" + s[i]; break; }
                    if (finite && uvOk && ts.x != 0 && ts.y != 0 && tm.x != 0 && tm.y != 0 && rx > 0 && ry > 0)
                    {
                        it.Tex = TextureSlot(tex);
                        uint f = 0;
                        if (bm > 0) f |= (uint)bm << BlendShift;
                        if (!it.IsMask)
                        {
                            if (add) f |= FAdd; else if (mat.IsKeywordEnabled("USE_POINT_SAMPLING")) f |= FPoint;
                            if (r.maskInteraction == SpriteMaskInteraction.VisibleInsideMask) f |= FInside; else if (r.maskInteraction == SpriteMaskInteraction.VisibleOutsideMask) f |= FOutside;
                        }
                        it.Flags = f;
                        bool whole = uv0 == Vector2.zero && uvX == Vector2.right && uvY == Vector2.up && rx == 1f && ry == 1f;
                        it.Geom = Tight && whole ? GeometryFor(tex) : 0;
                        it.Ok = it.Tex >= 0;
                        if (!it.Ok) failure = "texture.registration=-1 name=" + tex.name;
                    }
                }
            }
            if (sf_dr2_statics(token, (uint)it.Slot, 1, s) == 0) { it.Ok = false; failure = "native.statics=0"; }
            // 유효하지 않은 도형을 숨긴 채 남겨 두지 않는다. 마스크/순서도 다음 렌더 전에 다시 만든다.
            if (!it.Ok)
            {
                staticFailures++;
                if (failureLogs++ < 10) Log("정적 실패 top" + failureLogs + " name=" + d.name + " slot=" + it.Slot + " field/value=" + failure);
                ReleaseItem(it, "정적 값/도형/텍스처"); return;
            }
            if (it.Ok != okBefore || it.Tex != texBefore || it.Geom != geomBefore || it.Flags != flagsBefore) listDirty = true;
        }
        // 더는 맡을 수 없는 장식: 원래 렌더러로 돌려보낸다(그 장식만 순서가 어긋날 수 있다).
        static void ReleaseItem(Item it, string why)
        {
            if (it.Released) return;
            it.Released = true; it.Ok = false; it.Drawable = false; listDirty = true; orderDirty = true; released++;
            rejected++; Count(why);
            itemOf.Remove(it.D);
            try
            {
                if (it.R) it.R.forceRenderingOff = it.BackForceR || InvisibleSkip.Enabled && InvisibleSkip.IsHidden(it.D);
                if (it.M) it.M.forceRenderingOff = it.BackForceM;
                it.D.SetOpacity(it.D.opacity); if (it.D.GetVisible()) it.D.UpdatePosition();
            }
            catch { }
            // 같은 슬롯의 원래 렌더러를 끼워 그린다. 불가능할 때만 이 키를 경계로 추가한다.
            var lf = MakeLeftover(it.R);
            if (it.IsMask) { lf.Seq = false; lf.Why = "returned-mask"; }
            leftovers.Add(lf);
            if (lf.Seq && !NeedsMask(it) && WithinLimits(lf.Bg, lf.Key)) SequenceLeftover(lf);
            else { lf.Seq = false; AddLimit(lf); boundaryDirty = true; }
        }
        static int TextureSlot(Texture2D tex)
        {
            if (texSlot.TryGetValue(tex, out int slot)) return slot;
            slot = texSlot.Count;
            int aniso = QualitySettings.anisotropicFiltering == AnisotropicFiltering.Disable ? 1 : tex.anisoLevel;
            IntPtr ptr = tex.GetNativeTexturePtr();
            int filter = (int)tex.filterMode;
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && tex.isDataSRGB) filter |= 256;
            if (ptr == IntPtr.Zero || sf_dr2_texture(token, slot, ptr, filter, (int)tex.wrapModeU, (int)tex.wrapModeV, aniso) == 0) { texSlot[tex] = -1; return -1; }
            texSlot[tex] = slot; return slot;
        }
        // 텍스처의 빈 칸(알파 0)을 뺀 도형. 형태 칸 정보가 없으면 0(사각형 전체).
        static int GeometryFor(Texture2D tex)
        {
            if (texGeom.TryGetValue(tex, out int g)) return g;
            g = 0;
            try { g = BuildTight(tex); } catch { g = 0; }
            texGeom[tex] = g; return g;
        }
        static int BuildTight(Texture2D tex)
        {
            if (!ShapeCellsCpu.TryGetFine(tex.GetInstanceID(), out var bits) || bits == null) return 0;
            int G = (int)Math.Round(Math.Sqrt(bits.Length * 64.0)); if (G < 2 || G * G > bits.Length * 64) return 0;
            // 채워진 칸을 가로 줄 단위로 합치고, 같은 가로 범위가 이어지는 줄은 세로로 합친다.
            var rects = new List<int>(); int filled = 0;
            var prev = new List<int>(); var cur = new List<int>(); var prevY = new List<int>();
            for (int y = 0; y <= G; y++)
            {
                cur.Clear();
                if (y < G) { int x = 0; while (x < G) { if (!Cell(bits, G, x, y)) { x++; continue; } int x0 = x; while (x < G && Cell(bits, G, x, y)) { x++; filled++; } cur.Add(x0); cur.Add(x); } }
                var nextPrev = new List<int>(); var nextY = new List<int>();
                for (int i = 0; i < prev.Count; i += 2)
                {
                    bool cont = false; for (int j = 0; j < cur.Count; j += 2) if (cur[j] == prev[i] && cur[j + 1] == prev[i + 1]) { cont = true; break; }
                    if (cont) { nextPrev.Add(prev[i]); nextPrev.Add(prev[i + 1]); nextY.Add(prevY[i / 2]); }
                    else { rects.Add(prev[i]); rects.Add(prevY[i / 2]); rects.Add(prev[i + 1]); rects.Add(y); }
                }
                for (int j = 0; j < cur.Count; j += 2)
                {
                    bool had = false; for (int i = 0; i < nextPrev.Count; i += 2) if (nextPrev[i] == cur[j] && nextPrev[i + 1] == cur[j + 1]) { had = true; break; }
                    if (!had) { nextPrev.Add(cur[j]); nextPrev.Add(cur[j + 1]); nextY.Add(y); }
                }
                prev = nextPrev; prevY = nextY;
            }
            int n = rects.Count / 4;
            if (n == 0 || filled * 10 > G * G * 8 || n > 16384) return 0;   // 80% 넘게 차 있으면 사각형 전체가 낫다
            var st = new float[n * 8]; var idx = new uint[n * 6];
            for (int i = 0; i < n; i++)
            {
                float x0 = rects[i * 4] / (float)G, y0 = rects[i * 4 + 1] / (float)G, x1 = rects[i * 4 + 2] / (float)G, y1 = rects[i * 4 + 3] / (float)G;
                st[i * 8] = x0; st[i * 8 + 1] = y0; st[i * 8 + 2] = x1; st[i * 8 + 3] = y0; st[i * 8 + 4] = x0; st[i * 8 + 5] = y1; st[i * 8 + 6] = x1; st[i * 8 + 7] = y1;
                uint b = (uint)(i * 4); idx[i * 6] = b; idx[i * 6 + 1] = b + 2; idx[i * 6 + 2] = b + 1; idx[i * 6 + 3] = b + 1; idx[i * 6 + 4] = b + 2; idx[i * 6 + 5] = b + 3;
            }
            int id = nextGeom++;
            fixed (float* ps = st) fixed (uint* pi = idx) if (sf_dr2_geometry(token, id, ps, (uint)(n * 4), pi, (uint)(n * 6)) == 0) return 0;
            tightTextures++; tightRects += n; tightCover += filled / (double)(G * G);
            return id;
        }
        static bool Cell(ulong[] bits, int G, int x, int y) { int i = y * G + x; return ((bits[i >> 6] >> (i & 63)) & 1UL) != 0; }

        // ---------------------------------------------------------------- 남는 렌더러
        static Leftover MakeLeftover(Renderer r)
        {
            var m = r.sharedMaterial; var sr = r as SpriteRenderer; var ps = r as ParticleSystemRenderer;
            bool masked = sr && sr.maskInteraction != SpriteMaskInteraction.None || ps && ps.maskInteraction != SpriteMaskInteraction.None;
            var lf = new Leftover { R = r, Go = r.gameObject, GoLayer = r.gameObject.layer, Deco = r.GetComponentInParent<scrDecoration>(true), Masked = masked,
                Key = GpuDecoScene.SortKey(r, m, main), Materials = r.sharedMaterials };
            lf.Bg = lf.Key.Layer <= bgValue;
            string reason = !Sequence ? "sequence-off" : masked ? "mask" : !m || !m.shader ? "material-missing" : null;
            if (reason == null && !(r is SpriteRenderer || r is MeshRenderer || r is LineRenderer || r is TrailRenderer || ps && SeqParticles)) reason = "renderer-type/particles-off";
            for (int i = 0; reason == null && i < lf.Materials.Length; i++)
            {
                var mat = lf.Materials[i];
                if (!mat || !mat.shader || mat.passCount != 1) reason = "material[" + i + "].passes=" + (mat ? mat.passCount : -1);
            }
            if (lf.Materials.Length == 0) reason = "materials-empty";
            lf.Seq = reason == null;
            lf.MaterialScratch = new List<Material>(Math.Max(4, lf.Materials.Length));
            lf.Why = r.GetType().Name + ":" + (m && m.shader ? m.shader.name : "-") + ":" + (reason ?? "single-pass");
            return lf;
        }
        static void SetAwake(Leftover lf, bool awake)
        {
            if (!lf.Suppressed) return;
            cbDirty = true;
            SetLeftoverVisible(lf);
            if (awake && lf.PollIndex < 0) { lf.PollIndex = polled.Count; polled.Add(lf); }
            else if (!awake && lf.PollIndex >= 0)
            {
                int last = polled.Count - 1, index = lf.PollIndex;
                var tail = polled[last]; polled[index] = tail; tail.PollIndex = index; polled.RemoveAt(last); lf.PollIndex = -1;
            }
        }
        static void SetLeftoverVisible(Leftover lf)
        {
            bool on = LeftoverVisible(lf);
            if (lf.On != on) { lf.On = on; cbDirty = true; }
            commandEntries.Set(lf.EntryIndex, on);
        }
        static void SequenceLeftover(Leftover lf)
        {
            sequenced.Add(lf);
            if (lf.Deco) { if (!leftoverOf.TryGetValue(lf.Deco, out var ls)) leftoverOf[lf.Deco] = ls = new List<Leftover>(); if (!ls.Contains(lf)) ls.Add(lf); }
            Suppress(lf);
            lf.Lifetime = lf.Go.AddComponent<DirectDecoLifetime>();
            lf.Lifetime.Changed = awake => SetAwake(lf, awake);
            SetAwake(lf, lf.Go.activeInHierarchy);
        }
        static void RecountOwned()
        {
            ownedBg = ownedFg = ownedMask = ownedMasked = ownedAdd = 0;
            foreach (var it in items) if (!it.Released)
            {
                if (it.IsMask) ownedMask++;
                else { if (it.Layer <= bgValue) ownedBg++; else ownedFg++; if ((it.Flags & (FInside | FOutside)) != 0) ownedMasked++; if ((it.Flags & FAdd) != 0) ownedAdd++; }
            }
        }
        static bool NeedsMask(Item it) => it.IsMask || it.R && it.R.maskInteraction != SpriteMaskInteraction.None;
        static void AddLimit(Leftover lf)
        {
            if (lf.Bg) { if (!bgLimit || lf.Key.CompareTo(bgMin) < 0) { bgMin = lf.Key; bgLimit = true; } }
            else { if (!fgLimit || lf.Key.CompareTo(fgMax) > 0) { fgMax = lf.Key; fgLimit = true; } }
        }
        static void ApplyBoundaries()
        {
            boundaryDirty = false; boundaryChanges++;
            bool masksReturned = false;
            foreach (var it in items) if (it.Released && NeedsMask(it)) masksReturned = true;
            if (masksReturned) foreach (var it in items) if (!it.Released && NeedsMask(it)) ReleaseItem(it, "마스크 묶음 반환");
            // 재검색/텍스처/도형/네이티브 재생성 없이 기존 키만 본다. 경계는 곡 중 완화하지 않는다.
            for (int i = 0; i < leftovers.Count; i++)
            {
                var lf = leftovers[i]; if (lf.Seq || !lf.R) continue;
                lf.Key = GpuDecoScene.SortKey(lf.R, lf.R.sharedMaterial, main); lf.Bg = lf.Key.Layer <= bgValue;
                if (!unseqReasons.ContainsKey(lf.Why)) unseqReasons[lf.Why] = 1;
                if (lf.Bg) { if (!bgLimit || lf.Key.CompareTo(bgMin) < 0) { bgMin = lf.Key; bgLimit = true; } }
                else { if (!fgLimit || lf.Key.CompareTo(fgMax) > 0) { fgMax = lf.Key; fgLimit = true; } }
            }
            foreach (var c in canvasBounds) if (c)
            {
                int lv = SortingLayer.GetLayerValueFromID(c.sortingLayerID), order = c.sortingOrder;
                if (lv <= bgValue) { if (lv < bgCanvasLayer || lv == bgCanvasLayer && order < bgCanvasOrder) { bgCanvasLayer = lv; bgCanvasOrder = order; } }
                else { if (lv > fgCanvasLayer || lv == fgCanvasLayer && order > fgCanvasOrder) { fgCanvasLayer = lv; fgCanvasOrder = order; } }
            }
            foreach (var it in items) if (!it.Released && NeedsMask(it) && !WithinLimits(it.Layer <= bgValue, it.Key)) masksReturned = true;
            if (masksReturned) foreach (var it in items) if (!it.Released && NeedsMask(it)) ReleaseItem(it, "마스크 대상 경계 반환");
            for (int i = sequenced.Count - 1; i >= 0; i--)
            {
                if (i >= sequenced.Count) continue;
                var lf = sequenced[i];
                if (!WithinLimits(lf.Bg, lf.Key)) RetireSequence(lf.Go);
            }
            // 반환된 마스크/대상이 있으면 혼합 소유를 만들지 않는다.
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i]; if (it.Released) continue;
                if (!WithinLimits(it.Layer <= bgValue, it.Key)) ReleaseItem(it, "부분 실패 순서/마스크 경계");
            }
            unseqReasons.Clear();
            foreach (var lf in leftovers) if (!lf.Seq) { unseqReasons.TryGetValue(lf.Why, out int n); unseqReasons[lf.Why] = n + 1; }
            boundaryDirty = false; RecountOwned();
        }
        static bool LeftoverVisible(Leftover lf) => lf.R && lf.R.enabled && !lf.R.forceRenderingOff && lf.Go.activeInHierarchy;
        static void CheckLeftovers(List<Leftover> ls)
        {
            for (int i = 0; i < ls.Count; i++) { var lf = ls[i]; if (lf.Suppressed && LeftoverVisible(lf) != lf.On) { cbDirty = true; return; } }
        }
        // 남는 장식의 구조가 바뀌었다(깊이·재질 등): 정렬 자리를 다시 읽고, 게임이 되돌린 층을 다시 숨긴다.
        static void LeftoverStruct(List<Leftover> ls)
        {
            for (int i = 0; i < ls.Count; i++)
            {
                var lf = ls[i]; if (!lf.R) continue;
                if (!lf.Suppressed) { boundaryDirty = true; continue; }
                if (lf.Go.layer != HiddenLayer) { movedLayers[lf.Go] = lf.Go.layer; lf.Go.layer = HiddenLayer; }
                RefreshLeftover(lf);
            }
            cbDirty = true;
        }
        static void RefreshLeftover(Leftover lf)
        {
            var now = MakeLeftover(lf.R);
            lf.Key = now.Key; lf.Bg = now.Bg; lf.Materials = now.Materials; lf.Why = now.Why;
            orderDirty = cbDirty = true;
            if (now.Seq && WithinLimits(lf.Bg, lf.Key)) return;
            RetireSequence(lf.Go);
            boundaryDirty = true;
        }
        static void RetireSequence(GameObject go)
        {
            // 한 GO에 여러 Renderer가 있으면 함께 원래 층으로 돌린다.
            for (int i = sequenced.Count - 1; i >= 0; i--)
            {
                var other = sequenced[i]; if (!ReferenceEquals(other.Go, go)) continue;
                SetAwake(other, false); other.On = false; commandEntries.Set(other.EntryIndex, false);
                other.Suppressed = other.Seq = false;
                if (other.Lifetime) { other.Lifetime.Changed = null; UnityEngine.Object.Destroy(other.Lifetime); }
                sequenced.RemoveAt(i);
            }
            if (movedLayers.TryGetValue(go, out int original)) { go.layer = original; movedLayers.Remove(go); }
            orderDirty = cbDirty = true;
        }
        static void Suppress(Leftover lf)
        {
            lf.Suppressed = true;
            if (!movedLayers.ContainsKey(lf.Go)) movedLayers[lf.Go] = lf.Go.layer;
            lf.Go.layer = HiddenLayer;
        }

        // ---------------------------------------------------------------- 켜기
        static void Count(string why) { rejectWhy.TryGetValue(why, out int n); rejectWhy[why] = n + 1; }
        static void Activate()
        {
            if (Active) return;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11) { Log("건너뜀: D3D11 아님"); return; }
            if (Main.Config.GpuDeco == 1 && GpuDecoPlayer.Available) { Log("건너뜀: GPU 장식 처리(GpuDeco)가 켜져 있음"); return; }
            if (ADOBase.customLevel == null || ADOBase.controller == null || !ADOBase.controller.disableV15Features) { Log("건너뜀: 커스텀 맵이 아니거나 v15 기능"); return; }
            manager = scrDecorationManager.instance; if (!manager) { Log("건너뜀: 장식 관리자 없음"); return; }
            main = GpuDecoScene.FindMain();
            if (!main.orthographic || main.rect != new Rect(0, 0, 1, 1)) { Log("건너뜀: 카메라 설정"); return; }
            if ((main.cullingMask & (1 << HiddenLayer)) != 0) { Log("건너뜀: 숨김 층을 카메라가 그림"); return; }
            managerTransform = manager.transform;
            if (!Identity(managerTransform)) { Log("건너뜀: 장식 부모 변환"); return; }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            generation++; staticFailures = failureLogs = 0; boundaryChanges = pollTicks = commitMaxTicks = 0;
            leftovers.Clear(); canvasBounds.Clear(); unseqReasons.Clear();
            rejectWhy.Clear(); rejected = 0; itemOf.Clear(); leftoverOf.Clear(); sequenced.Clear(); polled.Clear(); entries.Clear(); texSlot.Clear(); texGeom.Clear(); nextGeom = 1; structQueue.Clear(); movedLayers.Clear();
            boundaryWatch.Clear(); boundaryVisible.Clear(); boundaryCanvases.Clear(); boundaryDirty = false;
            tightTextures = tightRects = 0; tightCover = 0; polls = pollDirty = commits = listBuilds = structRefresh = orderBuilds = cbBuilds = released = 0; commitTicks = 0;
            bgValue = SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("Bg")); fgValue = SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("Default"));

            // 1. 후보 장식
            var all = manager.allDecorations; var cand = new List<Item>(all.Count);
            var ownedRenderers = new HashSet<Renderer>();
            if (!SeqTest) for (int i = 0; i < all.Count; i++)
            {
                var d = all[i]; string why = Classify(d);
                if (why != null) { rejected++; Count(why); continue; }
                var v = (scrVisualDecoration)d; var r = v.spriteRenderer;
                var it = new Item { D = v, R = r, M = v.spriteMask, P = d.parallax, Child = d.childTransform, IsMask = v.isMask(), BackForceR = r.forceRenderingOff, BackForceM = v.spriteMask && v.spriteMask.forceRenderingOff };
                it.Key = GpuDecoScene.SortKey(r, r.sharedMaterial, main); it.Layer = it.Key.Layer; it.Order = it.Key.Order;
                it.BackLayer = it.BackOrder = int.MinValue; it.FrontLayer = it.FrontOrder = int.MaxValue;
                cand.Add(it); ownedRenderers.Add(r);
            }
            // 2. 남는 렌더러(메인 카메라가 그리는 다른 모든 것)와 경계
            var left = leftovers; int maskedLeft = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (!r || !r.gameObject.scene.IsValid() || ownedRenderers.Contains(r)) continue;
                if ((main.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                if (r is SpriteMask) continue;   // 마스크 자체는 색을 그리지 않는다
                // 장식마다 붙은 셰이더 필터용 메시 렌더러: disableV15Features 맵에서는 UpdateShader(true) 가 항상 끈다(켜질 일이 없다)
                var pv = r.GetComponentInParent<scrVisualDecoration>(true);
                if (pv && ReferenceEquals(r, pv.meshRenderer) && (!r.gameObject.activeInHierarchy || !meshOn(pv))) continue;
                int lv = SortingLayer.GetLayerValueFromID(r.sortingLayerID);
                var sr = r as SpriteRenderer; var psr = r as ParticleSystemRenderer;
                bool masked = (sr && sr.maskInteraction != SpriteMaskInteraction.None) || (psr && psr.maskInteraction != SpriteMaskInteraction.None);
                if (lv > bgValue && lv < fgValue) continue;   // 타일·행성 층: 두 장식 묶음 사이라 순서가 얽히지 않는다
                Leftover lf;
                try { lf = MakeLeftover(r); } catch { continue; }
                WatchBoundary(lf.Deco);
                if (masked) maskedLeft++;
                left.Add(lf);
                if (lf.Deco) { if (!leftoverOf.TryGetValue(lf.Deco, out var ls)) leftoverOf[lf.Deco] = ls = new List<Leftover>(); ls.Add(lf); }
            }
            // 마스크 대상인 남는 것이 있으면 Unity 마스크가 그것들에 필요하다. 우리 마스크와 겹쳐도 되지만(각자 균형), 안전하게 알려 둔다.
            // 월드 캔버스(글자 장식 등)는 명령 버퍼로 그릴 수 없다.
            bgLimit = fgLimit = false; bgCanvasLayer = bgCanvasOrder = int.MaxValue; fgCanvasLayer = fgCanvasOrder = int.MinValue; int canvases = 0;
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!c || !c.gameObject.scene.IsValid() || c.renderMode != RenderMode.WorldSpace || !c.isRootCanvas) continue;
                var cd = c.GetComponentInParent<scrDecoration>(true);
                if (cd)
                {
                    if (!boundaryCanvases.TryGetValue(cd, out var cs)) boundaryCanvases[cd] = cs = new List<Canvas>();
                    cs.Add(c); WatchBoundary(cd); boundaryVisible[cd] = BoundaryVisible(cd);
                }
                // 외부 활성화 후크가 없는 Canvas도 미래의 켜짐까지 보수적으로 예약한다.
                if ((main.cullingMask & (1 << c.gameObject.layer)) == 0) continue;
                int lv = SortingLayer.GetLayerValueFromID(c.sortingLayerID);
                if (lv > bgValue && lv < fgValue) continue;
                canvasBounds.Add(c);
                canvases++;
                if (lv <= bgValue) { if (lv < bgCanvasLayer || (lv == bgCanvasLayer && c.sortingOrder < bgCanvasOrder)) { bgCanvasLayer = lv; bgCanvasOrder = c.sortingOrder; } }
                else { if (lv > fgCanvasLayer || (lv == fgCanvasLayer && c.sortingOrder > fgCanvasOrder)) { fgCanvasLayer = lv; fgCanvasOrder = c.sortingOrder; } }
            }
            var unseqWhy = unseqReasons;
            foreach (var lf in left)
            {
                if (lf.Seq) continue;
                // 장식의 재활성화는 SetVisible/ApplyColor 후크가 다음 렌더 전에 재컴파일한다.
                // 외부 오브젝트는 후크가 없으므로 경계로 남긴다.
                unseqWhy.TryGetValue(lf.Why, out int n0); unseqWhy[lf.Why] = n0 + 1;
                if (lf.Bg) { if (!bgLimit || lf.Key.CompareTo(bgMin) < 0) { bgMin = lf.Key; bgLimit = true; } }
                else { if (!fgLimit || lf.Key.CompareTo(fgMax) > 0) { fgMax = lf.Key; fgLimit = true; } }
            }
            // 마스크와 마스크 대상은 같은 스텐실을 쓴다. 우리가 그리는 것과 Unity 가 그리는 것이 섞이면 서로의 스텐실을 볼 수 없으므로,
            // 마스크·마스크 대상이 전부 맡을 수 있을 때만 맡고, 하나라도 남으면 모두 Unity 에 남긴다.
            int unownedMasks = 0;
            foreach (var m0 in Resources.FindObjectsOfTypeAll<SpriteMask>()) if (m0 && m0.gameObject.scene.IsValid() && (main.cullingMask & (1 << m0.gameObject.layer)) != 0) { var vd = m0.GetComponentInParent<scrVisualDecoration>(true); WatchBoundary(vd); if (!vd || !itemOfCand(cand, vd)) unownedMasks++; }
            bool masksOk = Masks && maskedLeft == 0 && unownedMasks == 0;
            if (!masksOk)
            {
                foreach (var it in cand) ReturnedMaskBoundary(it);
                int dropped = cand.RemoveAll(it => it.IsMask || it.R.maskInteraction != SpriteMaskInteraction.None);
                if (dropped > 0) { rejected += dropped; rejectWhy["마스크(남는 마스크/대상 " + (maskedLeft + unownedMasks) + ")"] = dropped; }
            }
            var keep = new List<Item>(cand.Count);
            foreach (var it in cand)
            {
                bool bg = it.Layer <= bgValue;
                if (Only == 1 && !bg || Only == 2 && bg) { rejected++; Count("진단: 한쪽만"); continue; }
                if (it.IsMask || WithinLimits(bg, it.Key)) keep.Add(it); else { rejected++; Count(bg ? "순서: 배경 쪽 남는 것 위" : "순서: 앞쪽 남는 것 아래"); }
            }
            // 순서 경계에서 대상이 탈락한 뒤에도 마스크만 소유하는 경우를 막는다.
            if (cand.Exists(it => !keep.Contains(it) && it.R.maskInteraction != SpriteMaskInteraction.None))
            {
                foreach (var it in cand) ReturnedMaskBoundary(it);
                int dropped = keep.RemoveAll(it => it.IsMask || it.R.maskInteraction != SpriteMaskInteraction.None);
                rejected += dropped; if (dropped > 0) Count("마스크 대상 순서 경계");
                dropped = keep.RemoveAll(it => !WithinLimits(it.Layer <= bgValue, it.Key));
                rejected += dropped; if (dropped > 0) Count("원래 마스크 대상 경계");
            }
            if (keep.Count == 0 && !SeqTest) { Log("건너뜀: 맡을 장식 없음 (" + WhyText(rejectWhy) + " | 끼울 수 없는 것: " + WhyText(unseqWhy) + ", 캔버스 " + canvases + ")"); return; }

            // 3. 네이티브와 버퍼
            int n = keep.Count;
            left.Capacity = Math.Max(left.Capacity, left.Count + n);
            commandEntries.Reset(left.Count + n * 2);
            polled.Capacity = Math.Max(polled.Capacity, left.Count + n);
            sequenced.Capacity = Math.Max(sequenced.Capacity, left.Count + n);
            structQueue.Capacity = Math.Max(structQueue.Capacity, n);
            entries.Capacity = Math.Max(entries.Capacity, left.Count + n * 2);
            nativeEntries.Capacity = Math.Max(nativeEntries.Capacity, n * 2);
            token = sf_dr2_create(GetAnyResource(), (uint)Math.Max(1, n));
            if (token == 0) { Log("건너뜀: 네이티브 준비 실패"); return; }
            int cap = Math.Max(1, n);
            channels = new float[cap * ChannelFloats]; statics = new float[cap * StaticFloats]; dirty = new uint[cap]; dirtyStamp = new int[cap]; list = new uint[cap * 2 + 2]; runs = new Run[cap * 2 + 2]; segs = new uint[MaxSegs * 2];
            hChannels = GCHandle.Alloc(channels, GCHandleType.Pinned); pChannels = (float*)hChannels.AddrOfPinnedObject();
            hStatics = GCHandle.Alloc(statics, GCHandleType.Pinned); pStatics = (float*)hStatics.AddrOfPinnedObject();
            hDirty = GCHandle.Alloc(dirty, GCHandleType.Pinned); hList = GCHandle.Alloc(list, GCHandleType.Pinned); hSegs = GCHandle.Alloc(segs, GCHandleType.Pinned); hRuns = GCHandle.Alloc(runs, GCHandleType.Pinned);
            items = keep.ToArray(); dirtyCount = 0; stamp = 1;
            Active = true; level = ADOBase.customLevel.levelData; camFrame = -1; CaptureCamera();
            // 4. 값
            ownedBg = ownedFg = ownedMask = ownedMasked = ownedAdd = 0;
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i]; it.Slot = i; itemOf[it.D] = it;
                if (LazySprite.Count > 0 && rendererOn(it.D) && rendererColor(it.D).a != 0f) LazySprite.Flush(it.D);
                it.Layer = int.MinValue;   // Refresh 가 정렬 자리와 마스크 범위를 채운다(경계는 위에서 이미 봤다)
                bool force = Force; Force = true; Refresh(it); Force = force;
                float* c = pChannels + i * ChannelFloats; for (int k = 0; k < ChannelFloats; k++) c[k] = float.NaN;   // 첫 읽기에서 반드시 올라가게
                if (it.Released) continue;
                Poll(it);
                if (it.IsMask) ownedMask++; else { if (it.Layer <= bgValue) ownedBg++; else ownedFg++; if ((it.Flags & (FInside | FOutside)) != 0) ownedMasked++; if ((it.Flags & FAdd) != 0) ownedAdd++; }
            }
            fixed (float* ps = statics) if (sf_dr2_statics(token, 0, (uint)cap, ps) == 0) throw new InvalidOperationException("native statics preparation rejected");
            if (boundaryDirty) ApplyBoundaries();
            RecountOwned(); activationOwned = itemOf.Count;
            // 5. 차례에 끼울 남는 렌더러: 배경 쪽은 맡은 배경 장식 중 가장 위보다 아래인 것, 앞쪽은 맡은 앞 장식 중 가장 아래보다 위인 것
            bool anyBg = false, anyFg = false; SceneRenderOrder.Key bgTop = default(SceneRenderOrder.Key), fgBottom = default(SceneRenderOrder.Key);
            foreach (var it in items)
            {
                if (it.Released || it.IsMask) continue;
                if (it.Layer <= bgValue) { if (!anyBg || it.Key.CompareTo(bgTop) > 0) { bgTop = it.Key; anyBg = true; } }
                else { if (!anyFg || it.Key.CompareTo(fgBottom) < 0) { fgBottom = it.Key; anyFg = true; } }
            }
            foreach (var lf in left)
            {
                if (!lf.Seq || lf.Suppressed) continue;
                // 이후 SetDepth도 포함해 예약한 안전 구간의 남는 것을 미리 맡는다.
                bool need = SeqTest || (lf.Bg ? anyBg : anyFg) && WithinLimits(lf.Bg, lf.Key);
                if (!need) continue;
                SequenceLeftover(lf);
            }
            // 6. 원래 렌더러 끄기, 차례, 명령 버퍼
            foreach (var it in items) if (!it.Released) { it.R.forceRenderingOff = true; if (it.IsMask && it.M) it.M.forceRenderingOff = true; }
            foreach (var lf in sequenced) Suppress(lf);
            cbBefore = new CommandBuffer { name = "StutterFix.DirectDeco.Bg" }; cbAfter = new CommandBuffer { name = "StutterFix.DirectDeco.Front" };
            main.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cbBefore); main.AddCommandBuffer(CameraEvent.AfterForwardAlpha, cbAfter);
            RebuildOrder();
            if (!Active) return;
            BuildList(); prepTicks = sw.ElapsedTicks; polls = pollDirty = pollTicks = 0;
            Log("켜짐: 맡은 장식 " + itemOf.Count + "개(배경 " + ownedBg + ", 앞 " + ownedFg + ", 마스크 " + ownedMask + ", 마스크 대상 " + ownedMasked + ", 더하기 " + ownedAdd + "), 맡지 않음 " + rejected + " (" + WhyText(rejectWhy)
                + "), 끼워 그리는 렌더러 " + sequenced.Count + "개(장식 아님 " + polled.Count + "), 끼울 수 없는 것: " + WhyText(unseqWhy) + ", 캔버스 " + canvases + ", 마스크 대상 남는 것 " + maskedLeft
                + ", 구간 " + totalSegs + ", 텍스처 " + texSlot.Count + "장, 빈 칸 뺀 도형 " + tightTextures + "장(사각형 " + tightRects + "개, 평균 덮음 " + (tightTextures > 0 ? (tightCover / tightTextures * 100).ToString("F0") : "-") + "%), 준비 " + sw.ElapsedMilliseconds + "ms");
        }
        static string WhyText(Dictionary<string, int> why)
        {
            var sb = new StringBuilder(); var keys = new List<string>(why.Keys); keys.Sort((a, b) => why[b].CompareTo(why[a]));
            for (int i = 0; i < keys.Count && i < 10; i++) { if (i > 0) sb.Append(", "); sb.Append(keys[i]).Append(' ').Append(why[keys[i]]); }
            return sb.Length == 0 ? "없음" : sb.ToString();
        }
        static IntPtr GetAnyResource()
        {
            var rt = main.targetTexture; if (rt) { if (!rt.IsCreated()) rt.Create(); var p = rt.GetNativeTexturePtr(); if (p != IntPtr.Zero) return p; }
            return Texture2D.whiteTexture.GetNativeTexturePtr();
        }

        // ---------------------------------------------------------------- 차례(구간)
        static int Compare(Entry a, Entry b)
        {
            if (a.Bg != b.Bg) return a.Bg ? -1 : 1;
            int c = a.Layer.CompareTo(b.Layer); if (c != 0) return c;
            c = a.Order.CompareTo(b.Order); if (c != 0) return c;
            c = a.Sub.CompareTo(b.Sub); if (c != 0) return c;
            if (a.Sub != 0) return a.It.Slot.CompareTo(b.It.Slot);
            return a.Key.CompareTo(b.Key);
        }
        // 맡은 장식·마스크 들어감/나옴·끼운 렌더러를 한 줄로 세우고, 장식이 이어지는 구간마다 번호를 준다.
        static void RebuildOrder()
        {
            orderBuilds++; orderDirty = false; entries.Clear(); nativeEntries.Clear(); anyMask = false;
            foreach (var it in items)
            {
                if (it.Released) continue;
                if (it.IsMask)
                {
                    // 범위가 비어 있으면(앞 <= 뒤) 아무것도 가리지 않는다
                    if (it.FrontLayer < it.BackLayer || (it.FrontLayer == it.BackLayer && it.FrontOrder <= it.BackOrder)) continue;
                    anyMask = true;
                    var enter = it.First; enter.It = it; enter.Kind = 1; enter.Layer = it.BackLayer; enter.Order = it.BackOrder; enter.Sub = -2; enter.Bg = it.BackLayer <= bgValue; entries.Add(enter);
                    var leave = it.Second; leave.It = it; leave.Kind = 2; leave.Layer = it.FrontLayer; leave.Order = it.FrontOrder; leave.Sub = -1; leave.Bg = it.FrontLayer <= bgValue; entries.Add(leave);
                }
                else { var image = it.First; image.It = it; image.Kind = image.Sub = 0; image.Layer = it.Layer; image.Order = it.Order; image.Key = it.Key; image.Bg = it.Layer <= bgValue; entries.Add(image); }
            }
            foreach (var lf in sequenced) if (lf.R) { var e = lf.DrawEntry; e.Left = lf; e.Layer = lf.Key.Layer; e.Order = lf.Key.Order; e.Key = lf.Key; e.Bg = lf.Bg; entries.Add(e); }
            entries.Sort(Compare);
            commandEntries.Reset(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Left == null) { nativeEntries.Add(e); commandEntries.Set(i, true); }
                else { e.Left.EntryIndex = i; SetLeftoverVisible(e.Left); }
            }
            // 구간 번호: 0 은 항상 배경 쪽 첫 이벤트. 끼운 렌더러나 배경/앞 경계를 만나면 새 구간.
            int seg = 0; bool open = true; bool bg = true;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (bg && !e.Bg) { bg = false; seg++; open = true; }
                if (e.Left != null) { open = false; continue; }
                if (!open) { seg++; open = true; }
                e.Seg = seg;
            }
            if (bg) seg++;   // 앞쪽 구간이 하나도 없어도 번호는 둔다
            totalSegs = seg + 1;
            if (totalSegs > MaxSegs) { Log("구간이 너무 많음(" + totalSegs + ")"); Deactivate("구간 초과"); return; }
            listDirty = true; RebuildCommands();
        }
        static void RebuildCommands()
        {
            cbBuilds++; cbDirty = false; cbBefore.Clear(); cbAfter.Clear();
            IntPtr ev = sf_dr2_event(); var cb = cbBefore; int lastSeg = -1; bool bg = true;
            cb.IssuePluginEvent(ev, (token << 12) | 0); lastSeg = 0;
            for (int i = commandEntries.Next(-1); i >= 0; i = commandEntries.Next(i))
            {
                var e = entries[i];
                if (bg && !e.Bg) { bg = false; cb = cbAfter; }
                if (e.Left == null) { if (e.Seg != lastSeg) { cb.IssuePluginEvent(ev, (token << 12) | e.Seg); lastSeg = e.Seg; } continue; }
                var lf = e.Left;
                if (!lf.On) continue;
                var mats = lf.Materials;
                for (int k = 0; k < mats.Length; k++) if (mats[k]) cb.DrawRenderer(lf.R, mats[k], k, 0);
            }
        }

        // ---------------------------------------------------------------- 프레임
        static void PreCull(Camera c)
        {
            if (!Active || !ReferenceEquals(c, main)) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try { Commit(); }
            catch (Exception e) { Log("프레임 오류: " + e); Deactivate("프레임 오류"); }
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            commitTicks += elapsed; if (elapsed > commitMaxTicks) commitMaxTicks = elapsed;
        }
        static void Commit()
        {
            if (!main || !manager || !ReferenceEquals(level, ADOBase.customLevel != null ? ADOBase.customLevel.levelData : null)) { Deactivate("맵 바뀜"); return; }
            if (boundaryDirty) ApplyBoundaries();
            if (camFrame != Time.frameCount) CaptureCamera();   // 관리자 LateUpdate 가 안 돈 프레임
            if (structQueue.Count > 0)
            {
                for (int i = 0; i < structQueue.Count; i++) { var it = structQueue[i]; Refresh(it); if (!it.Released) Poll(it); }
                structQueue.Clear();
                RecountOwned();
            }
            if (boundaryDirty) ApplyBoundaries();
            // 활성 GO만 확인한다. 풀의 비활성 GO는 OnEnable/OnDisable로 이 목록에 들어오고 나간다.
            for (int i = polled.Count - 1; i >= 0; i--)
            {
                var lf = polled[i]; SetLeftoverVisible(lf);
                if (!lf.On) continue;
                if (lf.Go.layer != HiddenLayer) { movedLayers[lf.Go] = lf.Go.layer; lf.Go.layer = HiddenLayer; }
                // 배열 getter 대신 준비된 List를 사용해 submesh 재질 교체도 확인한다.
                lf.R.GetSharedMaterials(lf.MaterialScratch);
                bool materialChanged = lf.MaterialScratch.Count != lf.Materials.Length;
                for (int k = 0; !materialChanged && k < lf.Materials.Length; k++)
                {
                    var mat = lf.MaterialScratch[k];
                    if (!ReferenceEquals(mat, lf.Materials[k]) || !mat || mat.passCount != 1) materialChanged = true;
                }
                var m = lf.MaterialScratch.Count > 0 ? lf.MaterialScratch[0] : null;
                bool masked = lf.R is SpriteRenderer sr && sr.maskInteraction != SpriteMaskInteraction.None || lf.R is ParticleSystemRenderer ps && ps.maskInteraction != SpriteMaskInteraction.None;
                if (materialChanged || masked || lf.R.sortingOrder != lf.Key.Order || SortingLayer.GetLayerValueFromID(lf.R.sortingLayerID) != lf.Key.Layer || m && m.renderQueue != lf.Key.Queue || GpuDecoScene.Distance(lf.R, main) != lf.Key.Distance)
                    RefreshLeftover(lf);
            }
            if (boundaryDirty) ApplyBoundaries();
            if (orderDirty) { RebuildOrder(); if (!Active) return; }
            else if (cbDirty) RebuildCommands();
            if (listDirty) BuildList();
            FrameIn f = default(FrameIn);
            Matrix4x4 vp = GL.GetGPUProjectionMatrix(main.projectionMatrix, main.targetTexture != null) * main.worldToCameraMatrix;
            for (int i = 0; i < 16; i++) f.vp[i] = vp[i];
            f.cam[0] = camLogic.x; f.cam[1] = camLogic.y; f.cam[2] = camLogic.z; f.cam[3] = camLogic.w;
            f.ortho[0] = orthoLogic.x; f.ortho[1] = orthoLogic.y; f.ortho[2] = orthoLogic.z; f.ortho[3] = 0;
            f.dirtyCount = (uint)dirtyCount; f.dirtySlots = (uint*)hDirty.AddrOfPinnedObject(); f.channels = pChannels;
            f.listChanged = listSend ? 1u : 0u; f.list = (uint*)hList.AddrOfPinnedObject(); f.runs = (Run*)hRuns.AddrOfPinnedObject(); f.segs = (uint*)hSegs.AddrOfPinnedObject();
            f.listCount = (uint)listCount; f.runCount = (uint)runCount; f.segCount = (uint)segCount; f.clearStencil = anyMask ? 1u : 0u;
            if (sf_dr2_commit(token, &f) == 0) throw new InvalidOperationException("native commit rejected");
            listSend = false; dirtyCount = 0; stamp++; commits++;
        }
        static void BuildList()
        {
            listBuilds++; listDirty = false; listSend = true;
            int n = 0, r = 0; int seg = 0; int runStartOfSeg = 0;
            for (int s = 0; s < totalSegs * 2; s++) segs[s] = 0;
            int lastTex = -1, lastGeom = -1; uint lastFlags = 0; bool openRun = false;
            for (int i = 0; i < nativeEntries.Count; i++)
            {
                var e = nativeEntries[i]; var it = e.It; if (!it.Drawable) continue;
                if (e.Seg != seg) { segs[seg * 2] = (uint)runStartOfSeg; segs[seg * 2 + 1] = (uint)(r - runStartOfSeg); seg = e.Seg; runStartOfSeg = r; openRun = false; }
                uint flags = e.Kind == 1 ? (it.Flags & FPoint) | FEnter : e.Kind == 2 ? (it.Flags & FPoint) | FLeave : it.Flags;
                if (!openRun || it.Tex != lastTex || it.Geom != lastGeom || flags != lastFlags || (flags >> BlendShift) != 0)
                {
                    runs[r].start = (uint)n; runs[r].count = 0; runs[r].texture = it.Tex; runs[r].geometry = it.Geom; runs[r].flags = flags; r++;
                    lastTex = it.Tex; lastGeom = it.Geom; lastFlags = flags; openRun = true;
                }
                runs[r - 1].count++; list[n++] = (uint)it.Slot;
            }
            segs[seg * 2] = (uint)runStartOfSeg; segs[seg * 2 + 1] = (uint)(r - runStartOfSeg);
            listCount = n; runCount = r; segCount = totalSegs;
        }

        // ---------------------------------------------------------------- 끄기
        static void Release()
        {
            foreach (var lf in sequenced)
            {
                lf.Suppressed = false;
                if (lf.Lifetime) { lf.Lifetime.Changed = null; UnityEngine.Object.Destroy(lf.Lifetime); }
            }
            if (token != 0) { sf_dr2_destroy(token); token = 0; }
            if (hChannels.IsAllocated) hChannels.Free(); if (hStatics.IsAllocated) hStatics.Free(); if (hDirty.IsAllocated) hDirty.Free();
            if (hList.IsAllocated) hList.Free(); if (hSegs.IsAllocated) hSegs.Free(); if (hRuns.IsAllocated) hRuns.Free();
            pChannels = pStatics = null; channels = statics = null;
            itemOf.Clear(); leftoverOf.Clear(); items = new Item[0]; entries.Clear(); nativeEntries.Clear(); commandEntries.Reset(0); sequenced.Clear(); polled.Clear(); texSlot.Clear(); texGeom.Clear(); structQueue.Clear(); movedLayers.Clear();
            boundaryWatch.Clear(); boundaryVisible.Clear(); boundaryCanvases.Clear(); boundaryDirty = false;
            leftovers.Clear(); canvasBounds.Clear();
        }
        internal static void Deactivate(string reason)
        {
            if (!Active && token == 0) return;
            bool was = Active; Active = false;
            try
            {
                if (cbBefore != null) { if (main) main.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, cbBefore); cbBefore.Release(); cbBefore = null; }
                if (cbAfter != null) { if (main) main.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, cbAfter); cbAfter.Release(); cbAfter = null; }
                foreach (var kv in movedLayers) if (kv.Key && kv.Key.layer == HiddenLayer) kv.Key.layer = kv.Value;
                var mine = items;
                foreach (var it in mine) if (!it.Released) { if (it.R) it.R.forceRenderingOff = it.BackForceR || InvisibleSkip.Enabled && it.D && InvisibleSkip.IsHidden(it.D); if (it.M) it.M.forceRenderingOff = it.BackForceM; }
                string stats = was ? State() : "";
                Release();
                // 원래 경로가 다시 맡는다: 색(숨김 처리 포함)과 위치를 한 번 다시 쓰게 한다.
                foreach (var it in mine)
                {
                    var d = it.D; if (!d || it.Released) continue;
                    try { d.SetOpacity(d.opacity); if (d.GetVisible()) d.UpdatePosition(); } catch { }
                }
                if (was) Log("꺼짐: " + reason + " | " + stats);
            }
            catch (Exception e) { Log("끄는 중 오류: " + e.Message); Release(); }
            main = null; manager = null; managerTransform = null; level = null;
        }

        // dr2.hlsl VS의 복제. 셰이더 변경 때 이 식과 오프라인 fixture를 함께 바꾼다.
        static Vector3 ShaderCorner(Item it, Vector2 st)
        {
            float* c = pChannels + it.Slot * ChannelFloats, s = pStatics + it.Slot * StaticFloats;
            uint flags = (uint)s[18];
            Vector2 world;
            if ((flags & 128u) != 0)
            {
                Vector2 v = new Vector2(c[0] + c[2], c[1] + c[3]); if ((flags & 256u) != 0) v.x *= orthoLogic.z;
                Vector2 sr = v / 20f + Vector2.one * .5f;
                Vector2 cp = new Vector2((2f * sr.x - 1f) * orthoLogic.x * orthoLogic.y, (2f * sr.y - 1f) * orthoLogic.x);
                float sn = Mathf.Sin(camLogic.z * Mathf.Deg2Rad), cs = Mathf.Cos(camLogic.z * Mathf.Deg2Rad);
                world = new Vector2(camLogic.x + (cs * cp.x - sn * cp.y), camLogic.y + (sn * cp.x + cs * cp.y));
            }
            else world = new Vector2((camLogic.x - c[0]) * c[12] + c[0], (camLogic.y - c[1]) * c[13] + c[1]);
            Vector2 ts = new Vector2(s[8], s[9]); Vector2 cms = ((flags & 1u) != 0 ? camLogic.w : 1f) * ts;
            world += new Vector2(c[14] * cms.x / s[14], c[15] * cms.y / s[15]);
            float angle = s[12] + c[4] + ((flags & 2u) != 0 ? camLogic.z : 0f);
            // ImageRotation: Transform가 정규화한 Quaternion.Euler로 만든 TRS 열을 먼저 합성한다.
            float half = angle * Mathf.Deg2Rad * .5f, qz = Mathf.Sin(half), qw = Mathf.Cos(half);
            float norm = Mathf.Sqrt(qz * qz + qw * qw); qz /= norm; qw /= norm;
            float twice = qz * 2f, snr = qw * twice, csr = 1f - qz * twice;
            float sx = c[5] * cms.x, sy = c[6] * cms.y;
            Vector2 col0 = new Vector2(csr * sx, snr * sx), col1 = new Vector2(-snr * sy, csr * sy);
            Vector2 x = col0 * s[0] + col1 * s[4], y = col0 * s[1] + col1 * s[5];
            Vector2 origin = col0 * ((c[2] + s[10]) / ts.x + s[2]) + col1 * ((c[3] + s[11]) / ts.y + s[6]) + world;
            Vector2 p = (x * st.x + y * st.y) + origin;
            return new Vector3(p.x, p.y, s[13]);
        }
        sealed class VerifyRow { public float World, Uv, Color; public bool Texture; public string Detail; }
        internal static string Verify()
        {
            if (!Active) return "verify: active=0 (dr2 now 뒤 실행)";
            camFrame = -1; CaptureCamera(); var rows = new List<VerifyRow>();
            float maxWorld = 0, maxUv = 0, maxColor = 0; int texErrors = 0, count = 0, errors = 0;
            verifying = true;
            try
            {
                foreach (var it in items)
                {
                    if (it.Released) continue;
                    var d = it.D; var pivot = d.pivotTrans;
                    Vector3 lp = pivot.localPosition, ls = pivot.localScale; Quaternion lr = pivot.localRotation;
                    Vector2 start = parStart(it.P), end = parEnd(it.P); Vector3 posCam = it.P.posCamAtStart;
                    try
                    {
                        // forceRenderingOff는 그대로 유지한다. Unity 원래 경로의 Transform만 갱신한다.
                        d.UpdatePosition(); Poll(it);
                        var sp = it.IsMask ? it.M.sprite : it.R.sprite; if (!sp) continue;
                        var vertices = sp.vertices; var uv = sp.uv; Bounds b = sp.bounds;
                        Matrix4x4 m = (it.IsMask ? it.M.transform : it.R.transform).localToWorldMatrix;
                        float* s = pStatics + it.Slot * StaticFloats, c = pChannels + it.Slot * ChannelFloats;
                        var mat = it.R.sharedMaterial;
                        bool add = !it.IsMask && mat && mat.shader && mat.shader.name == AddShader;
                        Vector2 repeat = Vector2.one;
                        if (!it.IsMask && !add && mat)
                        {
                            if (mat.HasProperty(scrDecorationManager.ShaderProperty_RepeatX)) repeat.x = mat.GetFloat(scrDecorationManager.ShaderProperty_RepeatX);
                            if (mat.HasProperty(scrDecorationManager.ShaderProperty_RepeatY)) repeat.y = mat.GetFloat(scrDecorationManager.ShaderProperty_RepeatY);
                        }
                        var row = new VerifyRow();
                        for (int k = 0; k < vertices.Length; k++)
                        {
                            var v = vertices[k]; Vector2 st = new Vector2((v.x - b.min.x) / b.size.x, (v.y - b.min.y) / b.size.y);
                            Vector3 local = new Vector3(!it.IsMask && it.R.flipX ? -v.x : v.x, !it.IsMask && it.R.flipY ? -v.y : v.y, 0);
                            row.World = Mathf.Max(row.World, Vector3.Distance(m.MultiplyPoint3x4(local), ShaderCorner(it, st)));
                            Vector2 predicted = new Vector2((s[20] + st.x * s[22] + st.y * s[3]) * s[16], (s[21] + st.x * s[7] + st.y * s[23]) * s[17]);
                            if (k < uv.Length) row.Uv = Mathf.Max(row.Uv, Vector2.Distance(predicted, Vector2.Scale(uv[k], repeat)));
                        }
                        Color expected = it.R.color; float[] actual = { c[7], c[8], c[9], c[10] * c[11] * s[28] };
                        for (int k = 0; k < 4; k++) row.Color = Mathf.Max(row.Color, Mathf.Abs(((Color32)expected)[k] / 255f - Mathf.Floor(Mathf.Clamp01(actual[k]) * 255f + .5f) / 255f));
                        Color tint = Color.white;
                        if (add && mat.HasProperty(pTintColor)) tint = mat.GetColor(pTintColor) * 2f;
                        else if (!it.IsMask && !add && mat && mat.HasProperty("_Color")) tint = mat.GetColor("_Color");
                        for (int k = 0; k < 4; k++) row.Color = Mathf.Max(row.Color, Mathf.Abs(tint[k] - s[24 + k]));
                        // SpriteRenderer는 공유 재질의 기본 _MainTex 대신 sprite.texture를 바인딩한다.
                        row.Texture = !texSlot.TryGetValue(sp.texture, out int tx) || tx != it.Tex;
                        row.Detail = "name=" + d.name + " slot=" + it.Slot + " world=" + row.World.ToString("G9") + " uv=" + row.Uv.ToString("G9") + " color=" + row.Color.ToString("G9") + " textureMismatch=" + row.Texture
                            + " placement=" + d.placementType + " parallax=" + new Vector2(c[12], c[13]) + " crop=" + d.croppedPivotOffset + " repeat=" + new Vector2(s[16], s[17])
                            + " flip=" + it.R.flipX + "," + it.R.flipY + " texture=" + sp.texture.name + " " + sp.texture.width + "x" + sp.texture.height + " originalSize=" + new Vector2(sp.texture.width * d.textureScaleMultiplier.x, sp.texture.height * d.textureScaleMultiplier.y)
                            + " bounds=" + b + " trueScale=" + d.trueScaleMultiplier + " tint=" + new Color(s[24], s[25], s[26], s[27]) + " actualRepeat=" + repeat + " colorSpace=" + QualitySettings.activeColorSpace + " dataSRGB=" + sp.texture.isDataSRGB + " shader=" + (mat && mat.shader ? mat.shader.name : "-");
                        rows.Add(row); count++; maxWorld = Mathf.Max(maxWorld, row.World); maxUv = Mathf.Max(maxUv, row.Uv); maxColor = Mathf.Max(maxColor, row.Color); if (row.Texture) texErrors++;
                    }
                    catch (Exception e) { errors++; Log("verify error " + d.name + ": " + e.Message); }
                    finally { pivot.localPosition = lp; pivot.localRotation = lr; pivot.localScale = ls; parStart(it.P) = start; parEnd(it.P) = end; it.P.posCamAtStart = posCam; }
                }
            }
            finally { verifying = false; }
            rows.Sort((a, b) => (b.Texture ? float.MaxValue : Mathf.Max(b.World, Mathf.Max(b.Uv, b.Color))).CompareTo(a.Texture ? float.MaxValue : Mathf.Max(a.World, Mathf.Max(a.Uv, a.Color))));
            for (int i = 0; i < Math.Min(10, rows.Count); i++) Log("verify top" + (i + 1) + " " + rows[i].Detail);
            return "verify count=" + count + " max_world=" + maxWorld.ToString("G9") + " max_uv=" + maxUv.ToString("G9") + " max_color=" + maxColor.ToString("G9") + " texture_mismatch=" + texErrors + " errors=" + errors + " (CPU 식 대조; GPU/화면 차이는 drPix로 별도 측정)";
        }
        internal static string State()
        {
            if (token == 0) return "active=0 enabled=" + Enabled;
            double tick = 1e6 / System.Diagnostics.Stopwatch.Frequency; double fr = Math.Max(1, sf_dr2_stat(token, 0));
            RecountOwned();
            return "active=" + Active + " generation=" + generation + " activationOwned=" + activationOwned + " owned=" + itemOf.Count + " (bg " + ownedBg + ", fg " + ownedFg + ", mask " + ownedMask + ", masked " + ownedMasked + ", add " + ownedAdd + ") released=" + released + " staticFailures=" + staticFailures + " boundaryChanges=" + boundaryChanges + " boundaries=" + WhyText(unseqReasons) + " canvasBounds=" + canvasBounds.Count + " segs=" + totalSegs + " seqRenderers=" + sequenced.Count + " seqAwake=" + polled.Count
                + " preparedSlots=" + items.Length + " rejected=" + rejected + " pollTiming=" + timing
                + " bgBoundary=" + (bgLimit ? bgMin.Layer + "/" + bgMin.Order : "none") + " fgBoundary=" + (fgLimit ? fgMax.Layer + "/" + fgMax.Order : "none")
                + " drawable=" + listCount + " runs=" + runCount + " frames=" + sf_dr2_stat(token, 0) + " draws/frame=" + (sf_dr2_stat(token, 1) / fr).ToString("F1")
                + " instances/frame=" + (sf_dr2_stat(token, 2) / fr).ToString("F1") + " dropped=" + sf_dr2_stat(token, 3) + " errors=" + sf_dr2_stat(token, 4)
                + " upload_bytes/frame=" + (sf_dr2_stat(token, 5) / fr).ToString("F0") + " render_event_us=" + sf_dr2_stat(token, 6).ToString("F1") + " native_commit_us=" + sf_dr2_stat(token, 7).ToString("F1")
                + " grab_copies/frame=" + (sf_dr2_stat(token, 9) / fr).ToString("F1") + " grab_pixels/frame=" + (sf_dr2_stat(token, 10) / fr).ToString("F0")
                + " polls/frame=" + (polls / Math.Max(1.0, commits)).ToString("F1") + " dirty/frame=" + (pollDirty / Math.Max(1.0, commits)).ToString("F1")
                + " commit_us=" + (commitTicks * tick / Math.Max(1.0, commits)).ToString("F1") + " commit_max_us=" + (commitMaxTicks * tick).ToString("F1") + " poll_us/frame=" + (pollTicks * tick / Math.Max(1.0, commits)).ToString("F1") + " prep_us=" + (prepTicks * tick).ToString("F1") + " listBuilds=" + listBuilds + " orderBuilds=" + orderBuilds + " cbBuilds=" + cbBuilds + " structRefresh=" + structRefresh;
        }
        internal static string Command(string arg)
        {
            arg = (arg ?? "").Trim();
            if (arg == "timing on" || arg == "timing off") { timing = arg.EndsWith("on"); pollTicks = 0; return "poll timing=" + timing; }
            if (arg == "on") { Install(); Enabled = true; return "enabled (다음 재생 준비부터)"; }
            if (arg == "off") { Enabled = false; Deactivate("명령"); return "disabled"; }
            if (arg == "now") { Install(); Enabled = true; Deactivate("명령"); Activate(); return State(); }
            if (arg == "verify") return Verify();
            if (arg == "seq on" || arg == "seq off") { Sequence = arg == "seq on"; return "sequence=" + Sequence; }
            if (arg == "seqparticles on" || arg == "seqparticles off") { SeqParticles = arg == "seqparticles on"; return "seqparticles=" + SeqParticles; }
            if (arg == "tight on" || arg == "tight off") { Tight = arg == "tight on"; return "tight=" + Tight; }
            if (arg == "masks on" || arg == "masks off") { Masks = arg == "masks on"; return "masks=" + Masks; }
            if (arg == "force on" || arg == "force off") { Force = arg == "force on"; return "force=" + Force; }
            if (arg == "seqtest on" || arg == "seqtest off") { SeqTest = arg == "seqtest on"; return "seqtest=" + SeqTest; }
            if (arg.StartsWith("only ")) { Only = int.Parse(arg.Substring(5)); return "only=" + Only; }
            return State();
        }
    }
}
