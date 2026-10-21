// Managed role/binding adapter; no Unity lifecycle or game methods execute.
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public bool isActiveAndEnabled = true, Destroyed;
        public Transform transform = new Transform();
        public static implicit operator bool(MonoBehaviour value) => value != null && !value.Destroyed;
    }
    public class SpriteRenderer { public Transform transform = new Transform(); }
}
public class scrVisualDecoration : UnityEngine.MonoBehaviour
{
    public UnityEngine.MonoBehaviour parallax, blendModeEffect, meshBlendModeEffect, spriteAlphaMask;
}
public class ToggleVisible : UnityEngine.MonoBehaviour
{
    public UnityEngine.MonoBehaviour scriptToToggle;
    public bool ignoreIfDisabled;
}
