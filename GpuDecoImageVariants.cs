#if AUTOTEST
using System;
using UnityEngine;

namespace StutterFix
{
    internal sealed class GpuDecoImageVariant
    {
        internal enum Kind { ReadyQuad,NullArgument,Missing,NonQuad,SplitAlpha,Invalid }
        internal Kind Status;
        internal int SpriteId,TextureId;
        internal TextureManager.CustomSprite Source;
        internal float PixelsPerUnit;
        internal Vector2[] Vertices,UV;
        internal ushort[] Triangles;
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        internal static GpuDecoImageVariant Capture(string selector)
        {
            var p=new GpuDecoImageVariant();
            if(!FastMove.TryGpuImageSprite(selector,out var cs)||cs==null)
            {p.Status=string.IsNullOrEmpty(selector)?Kind.NullArgument:Kind.Missing;return p;}
            p=CaptureSprite(cs.sprite);p.Source=cs;return p;
        }
        internal static GpuDecoImageVariant CaptureSprite(Sprite sprite)
        {
            var p=new GpuDecoImageVariant();
            if(!sprite){p.Status=Kind.Invalid;return p;}
            var texture=sprite.texture;
            p.SpriteId=sprite.GetInstanceID();p.TextureId=texture?texture.GetInstanceID():0;p.PixelsPerUnit=sprite.pixelsPerUnit;
            p.Vertices=sprite.vertices;p.UV=sprite.uv;p.Triangles=sprite.triangles;
            if(!texture||!Finite(p.PixelsPerUnit)||p.PixelsPerUnit<=0){p.Status=Kind.Invalid;return p;}
            if(sprite.associatedAlphaSplitTexture){p.Status=Kind.SplitAlpha;return p;}
            if(p.Vertices.Length!=4||p.UV.Length!=4||p.Triangles.Length!=6){p.Status=Kind.NonQuad;return p;}
            foreach(var v in p.Vertices)if(!Finite(v.x)||!Finite(v.y)){p.Status=Kind.Invalid;return p;}
            foreach(var v in p.UV)if(!Finite(v.x)||!Finite(v.y)){p.Status=Kind.Invalid;return p;}
            foreach(var t in p.Triangles)if(t>=4){p.Status=Kind.Invalid;return p;}
            for(int i=0;i<6;i+=3)
            {
                int a=p.Triangles[i],b=p.Triangles[i+1],c=p.Triangles[i+2];
                var ab=p.Vertices[b]-p.Vertices[a];var ac=p.Vertices[c]-p.Vertices[a];
                float area=ab.x*ac.y-ab.y*ac.x;
                if(!Finite(area)||area==0){p.Status=Kind.Invalid;return p;}
            }
            p.Status=Kind.ReadyQuad;return p;
        }
        internal GpuDecoImageVariant WithGeometry(Vector2[] vertices,Vector2[] uv){var c=(GpuDecoImageVariant)MemberwiseClone();c.Vertices=vertices;c.UV=uv;return c;}
        internal bool SameAsset(GpuDecoImageVariant other)
        {
            if(other==null||!ReferenceEquals(Source,other.Source)||Status!=other.Status||SpriteId!=other.SpriteId||TextureId!=other.TextureId||PixelsPerUnit!=other.PixelsPerUnit)return false;
            if(Status!=Kind.ReadyQuad)return true;
            for(int i=0;i<4;i++)if(!Vertices[i].Equals(other.Vertices[i])||!UV[i].Equals(other.UV[i]))return false;
            for(int i=0;i<6;i++)if(Triangles[i]!=other.Triangles[i])return false;
            return true;
        }
    }
}
#endif
