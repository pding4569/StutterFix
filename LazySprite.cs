using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 투명한 장식의 이미지 바꾸기(scrVisualDecoration.SetSprite)를 다시 보일 때까지 미룬다.
    //
    // Arche 곡 258초: 장식 이동 효과 두 개가 투명한 안개 장식 4,962개의 이미지를 한 프레임에 바꿔 10ms(그 프레임 22ms 중).
    // SetSprite 한 번은 스프라이트 3개 넣기 + 편집용 충돌 상자 크기 + 재질 키워드 + 텍스처 감싸기 모드로 엔진 호출이 8번쯤 된다.
    // 안 그리는 장식(InvisibleSkip 이 forceRenderingOff 로 끈 것)은 이 결과를 그 순간 아무도 보지 않는다.
    //
    // 미루는 조건: InvisibleSkip.LazyCan(투명, 히트박스 없음, 마스크 아님, 재생 중) + 이 효과가 이미지 말고 크기·부드럽게·마스크를 안 바꿈
    //   + V15 기능 켜짐(꺼진 옛 방식은 매 프레임 같은 텍스처 장식들의 스프라이트를 읽어 감싸기 모드를 정한다).
    // 미루는 동안: 텍스처 감싸기 모드(Clamp, 텍스처 전체의 값)는 원래 순서를 지키려고 그 자리에서 바로 넣는다. 나머지는 마지막 이미지 하나만 기억.
    // 적용하는 때: 다시 보이게 될 때(InvisibleSkip.Work), 이 장식의 스프라이트를 읽거나 바꾸는 게임 함수가 불리기 직전(아래 목록, IL 로 찾음),
    //   곡 끝·다시 시작(ApplyAllLazy), 모드 끄기.
    internal static class LazySprite
    {
        internal static bool Enabled = true;
        internal static long Deferred, Flushed;
        internal static readonly long[] No = new long[9];   // 못 미룬 이유: 1 재생 아님 2 보임 3 이미지 아님 4 히트박스 5 안 그리는 목록에 없음 6 마스크 8 처음 크기 아직 없음
        private sealed class RefEq : IEqualityComparer<scrVisualDecoration>
        {
            public bool Equals(scrVisualDecoration a, scrVisualDecoration b) { return ReferenceEquals(a, b); }
            public int GetHashCode(scrVisualDecoration o) { return RuntimeHelpers.GetHashCode(o); }
        }
        private static readonly Dictionary<scrVisualDecoration, TextureManager.CustomSprite> pending = new Dictionary<scrVisualDecoration, TextureManager.CustomSprite>(new RefEq());
        private static bool flushing, installed;
        internal static int Count { get { return pending.Count; } }
#if AUTOTEST
        // Inspection must not call Flush or getters patched by OnePrefix.
        internal static bool GpuPeekPending(scrVisualDecoration d,out TextureManager.CustomSprite value)=>pending.TryGetValue(d,out value);
#endif

        internal static void Install(Harmony h)
        {
            try
            {
                var one = new HarmonyMethod(typeof(LazySprite), nameof(OnePrefix)) { priority = Priority.First };
                var all = new HarmonyMethod(typeof(LazySprite), nameof(AllPrefix)) { priority = Priority.First };
                int n = 0;
                // 이 장식의 스프라이트·스프라이트 크기를 읽거나 바꾸는 함수 (IL: SpriteRenderer.get_sprite / _sprite / spriteUnscaledSize)
                foreach (var name in new[] { "SetSprite", "GetDecorationPixelsSizeInUnits", "GetDecorationWorldSize", "SetTextureScaleMultiplier", "SetTile", "UpdateHitbox", "UpdateShader", "ResizeCollider", "get_spriteUnscaledSize", "SetMaskingType", "SetMaskingTarget" })
                    foreach (var m in typeof(scrVisualDecoration).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (m.Name == name && !m.IsAbstract) { h.Patch(m, prefix: one); n++; }
                // 여러 장식을 훑으며 스프라이트를 읽는 관리자 함수: 미룬 것을 모두 먼저
                // 옛 방식(V15 기능 꺼짐)의 타일링 갱신: 바꿀 텍스처 목록이 있을 때만 스프라이트를 읽는다(매 프레임 불리므로 그때만 먼저 적용)
                var tiling = AccessTools.Method(typeof(scrDecorationManager), "UpdateDecorationTilingLateUpdate", Type.EmptyTypes);
                keysField = AccessTools.Field(typeof(scrDecorationManager), "textureKeysToUpdate");
                if (tiling != null && keysField != null) { h.Patch(tiling, prefix: new HarmonyMethod(typeof(LazySprite), nameof(TilingPrefix)) { priority = Priority.First }); n++; }
                foreach (var name in new[] { "ScaleSprite", "ShowEmptyDecorations", "UpdateBordersSizes", "UpdateHitboxSizes" })
                    foreach (var m in typeof(scrDecorationManager).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (m.Name == name && !m.IsAbstract && m.GetMethodBody() != null) { h.Patch(m, prefix: all); n++; }
                installed = true;
                Main.Entry.Logger.Log("[투명 장식] 이미지 바꾸기 미루기 설치 (먼저 적용하는 곳 " + n + "개)");
            }
            catch (Exception ex) { installed = false; Main.Entry.Logger.Log("[투명 장식] 이미지 미루기 설치 실패: " + ex.Message); }
        }

        public static void OnePrefix(scrVisualDecoration __instance) { if (!flushing && pending.Count > 0) Flush(__instance); }
        public static void AllPrefix() { if (!flushing && pending.Count > 0) FlushAll(); }
        private static FieldInfo keysField;
        private static PropertyInfo countProp;
        public static void TilingPrefix(scrDecorationManager __instance)
        {
            if (flushing || pending.Count == 0) return;
            var keys = keysField.GetValue(__instance);
            if (keys == null) return;   // 원래 함수도 목록이 없으면 아무것도 못 한다(예외)
            if (countProp == null) countProp = keys.GetType().GetProperty("Count");
            if (countProp == null || (int)countProp.GetValue(keys, null) > 0) FlushAll();
        }

        // FastMove.ImageBlock 이 묻는다: 이 장식의 이미지 바꾸기를 미뤄도 되나
        internal static bool CanDefer(scrVisualDecoration v)
        {
            if (!Enabled || !installed || flushing || (object)ADOBase.controller == null) { No[7]++; return false; }
            int why = InvisibleSkip.SpriteLazyWhy(v);
            if (why != 0) { No[why]++; return false; }
            // 처음 유효한 이미지가 원래 크기(spriteUnscaledSize)를 정한다(ResizeCollider). 그 첫 번째는 미루지 않는다(미루면 뒤 이미지 크기가 남음).
            if (!pending.ContainsKey(v) && v.spriteUnscaledSize == Vector2.zero) { No[8]++; return false; }
            return true;
        }

        private static TextureManager.CustomSprite lastClamp;
        internal static void BeginEffect() { lastClamp = null; }
        // 원래 SetSprite 끝부분 중 텍스처 전체에 남는 값(감싸기 모드)은 원래 자리에서 넣는다. 한 효과 안에서 같은 이미지는 한 번만(값이 같다).
        internal static void Defer(scrVisualDecoration v, TextureManager.CustomSprite cs)
        {
            pending[v] = cs;
            Deferred++;
            if (!ReferenceEquals(cs, lastClamp))
            {
                lastClamp = cs;
                var s = (object)cs == null ? null : cs.sprite;
                if (s && !ADOBase.controller.disableV15Features) s.texture.wrapMode = TextureWrapMode.Clamp;   // 원래 SetSprite 와 같은 조건
            }
        }

        internal static void Flush(scrVisualDecoration v)
        {
            if (pending.Count == 0 || ReferenceEquals(v, null)) return;
            TextureManager.CustomSprite cs;
            if (!pending.TryGetValue(v, out cs)) return;
            pending.Remove(v);
            if (v == null) return;
            flushing = true;
            try { v.SetSprite(cs, false);
#if AUTOTEST
                DecoBake.GpuImageSpriteFlushed(v);
#endif
                Flushed++; }
            catch (Exception ex) { Main.Entry.Logger.Log("[투명 장식] 미룬 이미지 적용 오류: " + ex.Message); }
            finally { flushing = false; }
        }

        internal static void FlushAll()
        {
            if (pending.Count == 0) return;
            var list = new List<KeyValuePair<scrVisualDecoration, TextureManager.CustomSprite>>(pending);
            pending.Clear();
            flushing = true;
            try
            {
                foreach (var kv in list)
                {
                    if (kv.Key == null) continue;
                    try { kv.Key.SetSprite(kv.Value, false);
#if AUTOTEST
                            DecoBake.GpuImageSpriteFlushed(kv.Key);
#endif
                            Flushed++; }
                    catch (Exception ex) { Main.Entry.Logger.Log("[투명 장식] 미룬 이미지 적용 오류: " + ex.Message); }
                }
            }
            finally { flushing = false; }
        }

        internal static string State { get { return "이미지 미룸 " + Deferred + "번, 적용 " + Flushed + "번, 남음 " + pending.Count + " (못 미룬 이유 재생아님 " + No[1] + " 보임 " + No[2] + " 이미지아님 " + No[3] + " 히트박스 " + No[4] + " 목록에없음 " + No[5] + " 마스크 " + No[6] + " 다른 이미지 설정 " + No[0] + " V15꺼짐·설치 " + No[7] + " 처음크기 " + No[8] + ")"; } }
    }
}
