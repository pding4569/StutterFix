#if AUTOTEST
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    // 측정 전용(화면 순서가 틀림): 화면에서 많이 확대된 Sprites/Tile 장식만 1/N 해상도 보조 카메라로 그리고, 화면 맨 앞에 한 번 늘려 붙인다.
    // 스크린 공간 LOD 의 GPU·FPS 상한을 본다. 정식 구현은 원래 그리기 순서 자리에 합쳐야 한다.
    internal static class LodProbe
    {
        const int Layer = 31;
        static Camera lodCam, mainCam;
        static RenderTexture rt;
        static GameObject quad;
        static readonly List<KeyValuePair<GameObject, int>> moved = new List<KeyValuePair<GameObject, int>>();
        static int savedMask;

        sealed class Follow : MonoBehaviour
        {
            void LateUpdate()
            {
                if (!mainCam || !lodCam) return;
                lodCam.orthographic = mainCam.orthographic; lodCam.orthographicSize = mainCam.orthographicSize;
                lodCam.fieldOfView = mainCam.fieldOfView; lodCam.nearClipPlane = mainCam.nearClipPlane; lodCam.farClipPlane = mainCam.farClipPlane;
                lodCam.transform.SetPositionAndRotation(mainCam.transform.position, mainCam.transform.rotation);
            }
        }

        internal static string Run(string arg)
        {
            if (arg == "off") return Off();
            Off();
            int div = 4; float minMag = 4f;
            var p = arg.Split(' ');
            if (p.Length > 0 && int.TryParse(p[0], out var d)) div = d;
            if (p.Length > 1) float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out minMag);
            mainCam = Camera.allCameras.FirstOrDefault(c => c.name == "Camera");
            if (!mainCam) return "본 카메라 없음";
            int w = mainCam.targetTexture ? mainCam.targetTexture.width : Screen.width, h = mainCam.targetTexture ? mainCam.targetTexture.height : Screen.height;
            int n = 0;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!sr.sprite || !sr.sharedMaterial || sr.sharedMaterial.shader.name != "Sprites/Tile" || !sr.GetComponentInParent<scrVisualDecoration>()) continue;
                // 지금 화면 확대 배율(화면 픽셀 / 텍스처 픽셀)
                var b = sr.bounds; Vector3 a0 = mainCam.WorldToScreenPoint(b.min), a1 = mainCam.WorldToScreenPoint(b.max);
                float mag = Mathf.Min(Mathf.Abs(a1.x - a0.x) / sr.sprite.textureRect.width, Mathf.Abs(a1.y - a0.y) / sr.sprite.textureRect.height);
                if (mag < minMag) continue;
                moved.Add(new KeyValuePair<GameObject, int>(sr.gameObject, sr.gameObject.layer)); sr.gameObject.layer = Layer; n++;
            }
            savedMask = mainCam.cullingMask; mainCam.cullingMask &= ~(1 << Layer);
            rt = new RenderTexture(w / div, h / div, 24, RenderTextureFormat.ARGB32) { name = "SF.lodprobe" }; rt.Create();
            var go = new GameObject("StutterFix.LodCam"); Object.DontDestroyOnLoad(go);
            lodCam = go.AddComponent<Camera>(); lodCam.CopyFrom(mainCam);
            lodCam.cullingMask = 1 << Layer; lodCam.targetTexture = rt; lodCam.clearFlags = CameraClearFlags.SolidColor; lodCam.backgroundColor = new Color(0, 0, 0, 0);
            lodCam.depth = mainCam.depth - 0.5f;
            go.AddComponent<Follow>();
            // 합치기: 본 카메라 앞에 붙은 화면 크기 사각형(맨 앞 정렬), 미리 곱한 알파 섞기
            quad = new GameObject(); var qm = new Mesh(); qm.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) }; qm.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }; qm.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quad.AddComponent<MeshFilter>().sharedMesh = qm; quad.AddComponent<MeshRenderer>();
            quad.name = "StutterFix.LodQuad"; quad.layer = mainCam.gameObject.layer == Layer ? 0 : 0;
            quad.transform.SetParent(mainCam.transform, false);
            float dist = mainCam.nearClipPlane + 1f; quad.transform.localPosition = new Vector3(0, 0, dist);
            float hh = mainCam.orthographic ? mainCam.orthographicSize * 2 : 2 * dist * Mathf.Tan(mainCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            quad.transform.localScale = new Vector3(hh * w / (float)h, hh, 1);
            var mr = quad.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = rt };
            mat.renderQueue = 4000; mr.sharedMaterial = mat; mr.sortingLayerName = "Default"; mr.sortingOrder = 32767;
            return "저해상도로 보낸 장식 " + n + " (1/" + div + ", 확대 " + minMag + "배 이상)";
        }

        static string Off()
        {
            int n = moved.Count;
            foreach (var kv in moved) if (kv.Key) kv.Key.layer = kv.Value;
            moved.Clear();
            if (mainCam && savedMask != 0) mainCam.cullingMask = savedMask;
            savedMask = 0;
            if (lodCam) Object.Destroy(lodCam.gameObject);
            if (quad) Object.Destroy(quad);
            if (rt) { rt.Release(); Object.Destroy(rt); }
            return "되돌림 " + n;
        }
    }
}
#endif
