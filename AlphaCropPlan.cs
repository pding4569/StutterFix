using System;

namespace StutterFix
{
    // Pure research plan. Never changes a game-owned texture or Sprite.
    internal readonly struct AlphaCropPlan
    {
        internal readonly int X, Y, Width, Height;
        internal readonly double PivotX, PivotY;
        internal AlphaCropPlan(int x,int y,int width,int height,double pivotX,double pivotY)
        {X=x;Y=y;Width=width;Height=height;PivotX=pivotX;PivotY=pivotY;}

        internal static bool TryCreate(int width,int height,int x,int y,int w,int h,
            double pivotX,double pivotY,int padding,bool pointOrBilinear,bool clamp,
            bool originalSizeKnown,bool mipmapped,bool masked,bool customShader,bool compressed,out AlphaCropPlan plan)
        {
            plan=default;
            if(width<=0||height<=0||x<0||y<0||w<=0||h<=0||padding<1||padding>16
                ||(long)x+w>width||(long)y+h>height||!Finite(pivotX)||!Finite(pivotY)
                ||!pointOrBilinear||!clamp||!originalSizeKnown||mipmapped||masked||customShader||compressed)return false;
            int x0=(int)Math.Max(0L,(long)x-padding),y0=(int)Math.Max(0L,(long)y-padding);
            int x1=(int)Math.Min(width,(long)x+w+padding),y1=(int)Math.Min(height,(long)y+h+padding);
            int cw=x1-x0,ch=y1-y0;
            if(cw==width&&ch==height)return false;
            // An out-of-range normalized pivot is valid mathematically; never
            // clamp it or the visible image would move when the texture shrinks.
            double px=(width*pivotX-x0)/cw,py=(height*pivotY-y0)/ch;
            if(!Finite(px)||!Finite(py))return false;
            plan=new AlphaCropPlan(x0,y0,cw,ch,px,py);return true;
        }
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
