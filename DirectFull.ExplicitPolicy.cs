namespace StutterFix
{
    // The verified unlit forward path. Unknown modes reject activation rather
    // than silently substituting a different lighting model.
    internal static class DirectFullExplicitPolicy
    {
        internal static bool CameraPass(string mode) => string.IsNullOrEmpty(mode) || mode == "Always" || mode == "ForwardBase";
        internal static bool NonCameraPass(string mode) => mode == "ShadowCaster" || mode == "Meta" || mode == "MotionVectors"
            || mode == "DepthOnly" || mode == "DepthNormals" || mode == "Deferred" || mode == "PrepassBase" || mode == "PrepassFinal";
    }
}
