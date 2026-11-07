#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        static string PerItemPrepareReason(GpuLiveRow row,GpuDecoData.Item item)
        {
            var sprite=row?.R as SpriteRenderer;
            if(!(sprite && sprite.sprite && row.M && row.M.shader.name=="Sprites/Tile"
                && sprite.maskInteraction==SpriteMaskInteraction.None))return "material/mask";
            if(!row.M.shaderKeywords.All(k=>k=="USE_POINT_SAMPLING"))return "keywords";
            if(!((!row.M.HasProperty("_Color") || row.M.GetColor("_Color")==Color.white)
                && (!row.M.HasProperty("_Opacity") || row.M.GetFloat("_Opacity")==1)))return "tint/opacity";
            var t=sprite.sprite.texture;
            if(!t || t.GetInstanceID()!=item.TextureId)return "texture-changed";
            if(t.mipmapCount!=1)return "mipmap";
            if((t.wrapModeU!=TextureWrapMode.Clamp || t.wrapModeV!=TextureWrapMode.Clamp) && !GpuImageCollapseUvInUnitRange(item.UV))return "wrap";
            return null;
        }
        // 원본 인덱스/프로토타입은 유지한다. 아래 기존 소유 배열/슬라이스 생성만
        // 살아남은 행을 밀집 인덱스로 매핑하며 CPU 행은 정렬 장벽으로 남는다.
        static void DemotePerItemPrepareRows(List<GpuLiveRow> rows,Dictionary<int,GpuDecoData.Item> items,
            Action<int,string> rejected)
        {
            var counts=new Dictionary<string,int>();var cpu=new HashSet<int>();
            var samples=new List<string>();var sampled=new HashSet<int>();
            foreach(var row in rows)
            {
                if(row.Item<0)continue;
                string reason=PerItemPrepareReason(row,items[row.Item]);if(reason==null)continue;
                counts.TryGetValue(reason,out int count);counts[reason]=count+1;
                cpu.Add(row.Item);rejected?.Invoke(row.Item,reason);row.Item=-1;
                if(reason!="mipmap" && reason!="wrap" && reason!="texture-changed")continue;
                var t=(row.R as SpriteRenderer)?.sprite?.texture;
                if(samples.Count<3 && sampled.Add(t?t.GetInstanceID():0))
                    samples.Add(t?(t.name??"").Replace('\r',' ').Replace('\n',' ')+" "+t.width+"x"+t.height
                        +" mipmapCount="+t.mipmapCount+" wrap="+t.wrapModeU+"/"+t.wrapModeV:"null texture");
            }
            // 다른 CPU 유지 강등과 동일하게, 제외 장식의 보조 렌더러도 장벽으로 돌린다.
            foreach(var row in rows)if(cpu.Contains(row.AuxiliaryOwner))row.AuxiliaryOwner=-1;
            int Count(string reason)=>counts.TryGetValue(reason,out int n)?n:0;
            Main.Entry.Logger.Log("[GPU 장식] 준비 검사 장식 제외: 머티리얼/마스크 "+Count("material/mask")
                +" / 키워드 "+Count("keywords")+" / 색조·불투명도 "+Count("tint/opacity")
                +" / 텍스처 밉맵 "+Count("mipmap")+" / 텍스처 랩 "+Count("wrap")+" / 텍스처 바뀜 "+Count("texture-changed"));
            Main.Entry.Logger.Log("[GPU 장식] 준비 검사 위반 텍스처 샘플: "+(samples.Count==0?"없음":string.Join(" | ",samples)));
        }
    }
}
#endif
