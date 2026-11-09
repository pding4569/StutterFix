#if DEV || AUTOTEST || GPUDECO
using System;
using UnityEngine;

namespace StutterFix
{
    // ToggleVisible in the game's image prefab only toggles its own decoration's
    // Behaviour.enabled. The manager calls LogicUpdate regardless of that flag.
    // Keep the callback unchanged; never whitelist by short type name or allow
    // it to toggle another script (which may consume the stale Transform).
    internal static class LiveDecoScripts
    {
        internal static string Reason(MonoBehaviour[] scripts, scrVisualDecoration d, SpriteRenderer renderer)
        {
            foreach (var component in scripts)
            {
                if (!component || !component.isActiveAndEnabled || ReferenceEquals(component, d)
                    || ReferenceEquals(component, d.parallax) || ReferenceEquals(component, d.blendModeEffect)
                    || ReferenceEquals(component, d.meshBlendModeEffect) || ReferenceEquals(component, d.spriteAlphaMask))
                    continue;
                // RunLod 의 보임 알림(OnBecameVisible 만 받음)은 장식 그림·변환에 손대지 않는다. 이걸 막으면 RunLod 를 켠 판에서
                // GPU 장식이 맡을 장식이 사라졌다(Windflower 4,010개 거부, Arche 는 붙는 시점에 따라 판마다 달라짐).
                if (RunLod.IsOwnTag(component)) continue;
                if (component.GetType() == typeof(ToggleVisible))
                {
                    var toggle = (ToggleVisible)component;
                    if (toggle.transform != renderer.transform) return "visibility-node";
                    if (!ReferenceEquals(toggle.scriptToToggle, d))
                        return "visibility-target:" + (toggle.scriptToToggle ? toggle.scriptToToggle.GetType().FullName : "missing");
                    continue;
                }
                return "foreign-child-script:" + component.GetType().FullName;
            }
            return null;
        }
    }
}
#endif
