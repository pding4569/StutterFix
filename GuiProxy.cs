using System;
using UnityEngine;

namespace StutterFix
{
    // OnGUI 만 따로 받는 작은 컴포넌트. 유니티는 OnGUI 가 있는 컴포넌트마다 프레임마다(Layout·Repaint) BeginGUI/EndGUI·클립 설정을 하므로,
    // 할 일이 없을 때(곡 중 창 닫힘 등) 이 컴포넌트를 꺼 두면 그 비용이 없다(2026-11-24 HELLO: IMGUI 1.4%, 그 대부분이 빈 OnGUI 준비).
    internal sealed class GuiProxy : MonoBehaviour
    {
        internal Action Body;
        private void OnGUI() { Body?.Invoke(); }
    }
}
