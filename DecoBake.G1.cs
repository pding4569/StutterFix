#if DEV || AUTOTEST || GPUDECO
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace StutterFix
{
    internal static partial class DecoBake
    {
        // Captured at Setup, before scrubbing or native animation can mutate it.
        sealed class G1Initial
        {
            internal bool Visible, Smooth, CustomRange;
            internal int Depth, FrontLayer, FrontOrder, BackLayer, BackOrder, Width, Height;
            internal Vector2 Crop, TextureScale;
            internal MaskingType Mask;
            internal string Target;
        }
        static readonly Dictionary<scrDecoration,G1Initial> g1Initial = new Dictionary<scrDecoration,G1Initial>();
        static readonly AccessTools.FieldRef<scrDecoration,bool> g1RendererEnabled = AccessTools.FieldRefAccess<scrDecoration,bool>("rendererEnabled");
        static readonly AccessTools.FieldRef<scrVisualDecoration,MaskingType> g1Mask = AccessTools.FieldRefAccess<scrVisualDecoration,MaskingType>("maskingType");
        static void CaptureG1Initial(scrDecoration d)
        {
            if(!(d is scrVisualDecoration v) || !v.spriteRenderer || !v.spriteMask || !v.spriteAlphaMask)return;
            var r=v.spriteRenderer;var m=v.spriteMask;var tex=r.sprite?r.sprite.texture:null;
            g1Initial[d]=new G1Initial { Visible=g1RendererEnabled(d), Smooth=v.smoothing, Mask=g1Mask(v), Target=v.spriteAlphaMask.targetTag,
                Depth=-r.sortingOrder, Width=tex?tex.width:0, Height=tex?tex.height:0, Crop=d.croppedPivotOffset, TextureScale=d.textureScaleMultiplier, CustomRange=m.isCustomRangeActive,
                FrontLayer=m.frontSortingLayerID, FrontOrder=m.frontSortingOrder, BackLayer=m.backSortingLayerID, BackOrder=m.backSortingOrder };
        }
        // Once any value changes, the decoration is excluded for the entire song.
        // Thus comparing all writes against Setup is also a complete constant-value proof.
        static void AddG1ValueReasons(ffxMoveDecorationsPlus f,scrDecoration d,DecoBakeCore.Decoration row,bool alwaysVisible,bool hasImageWrites)
        {
            if(!g1Initial.TryGetValue(d,out var s)){row.Reasons.Add("초기 부가 상태 없음");return;}
            var v=(scrVisualDecoration)d;
            if(f.depthUsed && (d.syncFloorDepth || f.targetDepth!=s.Depth))row.Reasons.Add("깊이 변경");
            if(f.cropPivotOffsetUsed && f.targetCropPivotOffset/100f!=s.Crop)row.Reasons.Add("크롭 변경");
            if(f.smoothingUsed && f.targetSmoothing!=s.Smooth)row.Reasons.Add("부드럽게 변경");
            if(f.originalSizeUsed)
            {
                // An image write can change dimensions before SetTextureScaleMultiplier.
                if(f.targetOriginalSize.x!=0 && f.targetOriginalSize.y!=0 && (hasImageWrites || s.Width==0 || s.Height==0
                    || new Vector2(f.targetOriginalSize.x/s.Width,f.targetOriginalSize.y/s.Height)!=s.TextureScale)
                    )
                    row.Reasons.Add("원래크기 변경/이미지 의존");
            }
            if(f.maskingTypeUsed && (f.targetMaskingType!=s.Mask || !alwaysVisible))row.Reasons.Add("마스크 종류/숨김 재활성화");
            if(f.maskingTargetUsed && f.targetmaskingTarget!=s.Target)row.Reasons.Add("마스크 대상 변경");
            if(f.useMaskingDepthUsed && f.targetUseMaskingDepth!=s.CustomRange)row.Reasons.Add("마스크 범위 사용 변경");
            if(f.maskingFrontDepthUsed || f.maskingBackDepthUsed)
            {
                // Reproduce SetMaskingDepth, including its unusual old front inverse.
                int front=f.maskingFrontDepthUsed?f.targetMaskingFrontDepth:-s.FrontOrder-1;
                int back=f.maskingBackDepthUsed?f.targetMaskingBackDepth:-s.BackOrder;
                if(back<front){int t=front;front=back;back=t;}
                if(s.FrontLayer!=SortingLayer.NameToID(front>=0?"Bg":"Default") || s.FrontOrder!=-front+1
                    || s.BackLayer!=SortingLayer.NameToID(back>=0?"Bg":"Default") || s.BackOrder!=-back)
                    row.Reasons.Add("마스크 깊이 변경");
            }
        }
        static bool[] g1DrawVisible=Array.Empty<bool>();
        static bool[] g1ImageEmpty=Array.Empty<bool>(),g1ImageExpectedEmpty=Array.Empty<bool>();
        static readonly AccessTools.FieldRef<scrDecoration,bool> g1ForceHide = AccessTools.FieldRefAccess<scrDecoration,bool>("forceHide");
        static void CaptureG1DrawState()
        {
            g1DrawVisible=new bool[gpuLiveOwnedIndices.Length];
            g1ImageEmpty=new bool[g1DrawVisible.Length];g1ImageExpectedEmpty=new bool[g1DrawVisible.Length];
            for(int m=0;m<g1DrawVisible.Length;m++)g1DrawVisible[m]=gpuLiveOwnedRenderers[m].enabled;
        }
        static bool G1DrawHidden(int master)=>(uint)master<(uint)g1DrawVisible.Length && (!g1DrawVisible[master] || g1ImageEmpty[master]);
        static bool G1ExpectedEmpty(int master)=>(uint)master<(uint)g1ImageEmpty.Length && g1ImageEmpty[master] && g1ImageExpectedEmpty[master];
        static void RefreshG1Draw(int master)
        {
            for(int s=0;s<gpuLiveSlices.Count;s++)
            {int item=gpuLiveMasterToSliceItem[master][s];if(item>=0)gpuLiveSlices[s].RefreshItem(item);}
        }
        internal static bool G1VisibilityConsistent(scrVisualDecoration d)=>d.spriteRenderer.enabled==g1RendererEnabled(d);
        static void SyncG1CollapseItem(int item)
        {
            RequireLive(!gpuCollapseOn || DrawCollapsePolicy.GlobalVerts,"G1 discrete visibility requires global collapse map");
            if(!gpuCollapseOn || gpuCollapseMapCpu==null || gpuCollapseSlotOfItem==null || !gpuCollapseIncluded[item])return;
            int slot=gpuCollapseSlotOfItem[item];if(slot<0)return;
            uint value=G1DrawHidden(gpuLiveSlices[0].MasterItems[item])?uint.MaxValue:(uint)item;
            if(gpuCollapseMapCpu[slot]==value)return;
            gpuCollapseMapCpu[slot]=value;
            gpuCollapseAllMap?.SetData(gpuCollapseMapCpu,slot,slot,1);
        }
        static void ApplyG1Visible(ffxMoveDecorationsPlus f,int master,scrDecoration d)
        {
            if(!f.visibleUsed)return;
            bool visible=f.visible && !g1ForceHide(d);
            d.SetVisible(visible); // Keep rendererEnabled separate from the physically suppressed CPU renderer.
            g1DrawVisible[master]=visible;
            var r=gpuLiveOwnedRenderers[master];r.forceRenderingOff=true;
            if(DrawCollapsePolicy.DisableOwnedRenderers){gpuLiveEnabledOff[master]=true;r.enabled=false;}
            RefreshG1Draw(master);
        }
    }
}
#endif
