using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    // 게임 3.4 알파에만 있는 scrDecoration.croppedPivotOffset 을 직접 읽으면, 이 필드가 없는 일반 버전에서는 그 코드를 포함한 메서드가
    // JIT 될 때 MissingFieldException 이 난다(DecoAnim.UpdatePrefix 에 인라인된 연구 코드 때문에 DecoAnim 전체가 꺼졌던 사고, 2026-10-19).
    // 필드 존재 여부를 이름으로 확인하고, 없으면 영벡터를 돌려준다. 연구 코드는 Exists 가 거짓이면 스스로 꺼진다.
    internal static class CropField
    {
        private static readonly FieldInfo field = AccessTools.Field(typeof(scrDecoration), "croppedPivotOffset");
        internal static bool Exists { get { return field != null; } }
        internal static Vector2 Get(scrDecoration d)
        {
            return field == null || d == null ? Vector2.zero : (Vector2)field.GetValue(d);
        }
    }
}
