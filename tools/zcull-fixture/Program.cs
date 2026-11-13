using System;
using StutterFix;
static class Program
{
    static int checks;
    static void Check(bool ok, string name) { if(!ok)throw new Exception(name);checks++; }
    static SceneRenderOrder.Key K(int order=0,int renderer=1,int shader=2,int material=3)
        => new SceneRenderOrder.Key(0,order,3000,1,material,renderer,0,shader);
    static void Main()
    {
        Check(ZCullPolicy.MayOcclude(1,true,false),"alpha 255");
        Check(!ZCullPolicy.MayOcclude(254f/255,true,false),"color alpha 254");
        Check(!ZCullPolicy.MayOcclude(float.NaN,true,false),"nonfinite color alpha");
        Check(!ZCullPolicy.MayOcclude(1,false,false),"additive rejected");
        Check(!ZCullPolicy.MayOcclude(1,true,true),"ambiguous rejected");
        var keys=new[]{K(renderer:1),K(renderer:2),K(renderer:3),K(order:1)};
        Array.Sort(keys);var ties=ZCullPolicy.Ambiguous(keys);
        Check(ties[0]&&ties[1]&&ties[2]&&!ties[3],"entire tie group rejected");
        Check(K(shader:1,material:9).CompareTo(K(shader:2,material:1))<0,"shader precedes material");
        Check(K(material:1).CompareTo(K(material:2))<0,"material order");
        // Two partially overlapping rectangles on an 8x8 pixel grid. Front is opaque.
        int total=0,alive=0;
        for(int y=0;y<8;y++)for(int x=0;x<8;x++) {
            bool back=x<6&&y<6,front=x>=2&&y>=2;
            float depth=front?ZCullPolicy.Depth(1,2,false):1;
            if(back){total++;if(ZCullPolicy.Depth(0,2,true)<depth)alive++;}
            if(front){total++;if(ZCullPolicy.Depth(1,2,true)<depth)alive++;}
        }
        Check(total==72&&alive==56,"two quads: overlap 16, front self retained");
        // 24-bit depth precision at the largest supported draw count.
        for(int i=0;i<200000;i++) {
            int pre=(int)Math.Round(ZCullPolicy.Depth(i,200000,false)*16777215d);
            int own=(int)Math.Round(ZCullPolicy.Depth(i,200000,true)*16777215d);
            if(!(own<pre))throw new Exception("depth self precision "+i);
            if(i+1<200000 && !(own>(int)Math.Round(ZCullPolicy.Depth(i+1,200000,false)*16777215d)))throw new Exception("depth occlusion precision "+i);
        }
        checks++;
        Console.WriteLine("ZCULL_FIXTURE=PASS checks="+checks+" depth_cases=200000");
    }
}
