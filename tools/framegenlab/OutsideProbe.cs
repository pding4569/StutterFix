namespace StutterFix
{
    // The research harness uses the same runtime as the Player build.
    internal static class FrameGenOutsideProbe
    {
        internal static void Install() { FrameGen.InstallResearch(); }
        internal static void Finish() { FrameGen.FinishResearch(); }
        internal static void Uninstall() { FrameGen.Shutdown(); }
        internal static void DrawGUI() { UnityEngine.GUILayout.Label("프레임 늘리기 (실험): StutterFix 설정 → 그래픽"); }
    }
}
