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
