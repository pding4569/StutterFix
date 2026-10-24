using System;
using System.Linq;

namespace StutterFix
{
    // No Unity dependencies. Missing/invalid evidence can never become PASS.
    internal sealed class RenderMergeGatePolicy
    {
        internal readonly bool[] Seen=new bool[4],Pixels=new bool[2],Stable=new bool[4],StabilitySeen=new bool[4];
        internal readonly double[] Fps=new double[4];
        internal bool Error,PixelFailure,StabilityFailure,FpsFailure;
        internal long Changed;
        internal int Renderers,Draws;
        internal void Window(int i,bool merged,double fps,bool valid,int spikes,int renderers,int draws)
        {
            if(i<0 || i>=4 || Seen[i] || merged!=(i==1||i==2) || double.IsNaN(fps) || double.IsInfinity(fps) || fps<=0
                || spikes<0 || renderers<0 || draws<0 || !valid){Error=true;return;}
            Seen[i]=true;Fps[i]=fps;
            if(merged)
            {
                Renderers=Math.Max(Renderers,renderers);Draws=Math.Max(Draws,draws);
                if(spikes>0)StabilityFailure=true;
                if(fps<220 || renderers>150 || draws>600 || renderers==0 || draws==0)FpsFailure=true;
            }
        }
        internal void Pixel(int i,long changed,int max,long oldChanged,int oldMax,long newChanged,int newMax,bool stable)
        {
            if(i<0 || i>=2 || Pixels[i] || changed<0 || max<0 || max>255 || oldChanged<0 || newChanged<0
                || oldMax<0 || oldMax>255 || newMax<0 || newMax>255){Error=true;return;}
            Pixels[i]=true;Changed=Math.Max(Changed,changed);
            // Exact independent/merged equivalence is deliberately stricter
            // than merely accepting the same aggregate error against originals.
            if(changed!=0 || max!=0 || newChanged>oldChanged || newMax>oldMax)PixelFailure=true;
            if(!stable)StabilityFailure=true;
        }
        internal void Stability(int i,bool valid)
        {
            if(i<0 || i>=4 || StabilitySeen[i]){Error=true;return;}
            StabilitySeen[i]=true;Stable[i]=valid;
            if(!valid)StabilityFailure=true;
        }
        internal string Verdict()
        {
            if(Error || !Seen.All(x=>x) || !Pixels.All(x=>x) || !StabilitySeen.All(x=>x))return "ERROR";
            if(PixelFailure)return "PIXEL_FAIL";
            if(StabilityFailure)return "STABILITY_FAIL";
            if(FpsFailure)return "FPS_FAIL";
            return "PASS";
        }
    }
}
