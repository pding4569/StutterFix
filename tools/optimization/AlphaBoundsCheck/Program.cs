using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using StutterFix;

unsafe class Program
{
    [DllImport("sfnative",CallingConvention=CallingConvention.Cdecl)]
    private static extern int sf_alpha_bounds_v1(byte* pixels,UIntPtr bytes,int width,int height,int stride,int alpha,int* box);
    static int conditions, pixelsChecked;
    static void Check(bool ok,string reason){conditions++;if(!ok)throw new Exception(reason);}
    static int[] Reference(byte[] data,int w,int h,int stride,int alpha)
    {
        int x0=w,y0=h,x1=-1,y1=-1;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(data[y*stride+x*4+alpha]!=0)
        {x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
        return x1<0?new int[4]:new[]{x0,y0,x1-x0+1,y1-y0+1};
    }
    static void Compare(byte[] data,int w,int h,int stride,int alpha)
    {
        var prior=(byte[])data.Clone();int* box=stackalloc int[4];
        fixed(byte* source=data)
            Check(sf_alpha_bounds_v1(source,new UIntPtr((uint)data.Length),w,h,stride,alpha,box)==1,"valid input accepted");
        Check(new[]{box[0],box[1],box[2],box[3]}.SequenceEqual(Reference(data,w,h,stride,alpha)),"scalar bounds match");
        Check(data.SequenceEqual(prior),"input and row padding unchanged");pixelsChecked+=w*h;
    }
    static bool Plan(int w,int h,int x,int y,int cw,int ch,double px,double py,out AlphaCropPlan p,
        int padding=1,bool sample=true,bool clamp=true,bool known=true,bool mip=false,bool masked=false,bool custom=false,bool compressed=false)
        =>AlphaCropPlan.TryCreate(w,h,x,y,cw,ch,px,py,padding,sample,clamp,known,mip,masked,custom,compressed,out p);
    static void Main(string[] args)
    {
        if(args.Length!=1)throw new ArgumentException("native DLL absolute path required");
        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly,(name,asm,path)=>name=="sfnative"?NativeLibrary.Load(args[0]):IntPtr.Zero);
        var random=new Random(1202026);
        for(int w=1;w<=41;w++)for(int alpha=0;alpha<4;alpha++)for(int variant=0;variant<5;variant++)
        {
            int h=variant+1,stride=w*4+(w%3)*7;var data=new byte[stride*(h-1)+w*4];random.NextBytes(data);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                data[y*stride+x*4+alpha]=(byte)(variant==0?0:variant==1?255:variant==2?(x==w-1&&y==h-1?1:0):variant==3?(x==0&&y==0?128:0):random.Next(0,2));
            Compare(data,w,h,stride,alpha);
        }
        for(int n=0;n<200;n++)
        {
            int w=random.Next(1,257),h=random.Next(1,129),stride=w*4+random.Next(0,17),alpha=random.Next(0,4);
            var data=new byte[stride*(h-1)+w*4];random.NextBytes(data);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)data[y*stride+x*4+alpha]=(byte)(random.Next(1000)==0?1:0);
            Compare(data,w,h,stride,alpha);
        }
        byte[] small=new byte[64];int* output=stackalloc int[4];
        fixed(byte* source=small)
        {
            Check(sf_alpha_bounds_v1(null,new UIntPtr(64),4,4,16,3,output)==0,"null input");
            Check(sf_alpha_bounds_v1(source,new UIntPtr(64),4,4,16,3,null)==0,"null bounds");
            Check(sf_alpha_bounds_v1(source,new UIntPtr(63),4,4,16,3,output)==0,"truncated last pixel");
            Check(sf_alpha_bounds_v1(source,new UIntPtr(64),4,4,15,3,output)==0,"short stride");
            Check(sf_alpha_bounds_v1(source,new UIntPtr(64),int.MaxValue,4,16,3,output)==0,"width overflow");
            Check(sf_alpha_bounds_v1(source,new UIntPtr(64),4,int.MaxValue,int.MaxValue,3,output)==0,"height overflow");
            Check(sf_alpha_bounds_v1(source,new UIntPtr(64),4,4,16,4,output)==0,"alpha offset");
            Check(sf_alpha_bounds_v1(source,new UIntPtr(64),0,4,16,3,output)==0,"zero width");
        }
        Check(Plan(100,80,30,20,10,12,.5,.5,out var p),"crop planned");
        Check(p.X==29&&p.Y==19&&p.Width==12&&p.Height==14,"padding preserved");
        for(int n=0;n<10000;n++)
        {
            int w=random.Next(8,10000),h=random.Next(8,10000),x=random.Next(2,w-3),y=random.Next(2,h-3);
            int cw=random.Next(1,w-x-1),ch=random.Next(1,h-y-1);double px=random.NextDouble()*4-2,py=random.NextDouble()*4-2;
            Check(Plan(w,h,x,y,cw,ch,px,py,out p),"finite crop accepted");
            double a=x-w*px,b=(x-p.X)-p.Width*p.PivotX,c=y-h*py,d=(y-p.Y)-p.Height*p.PivotY;
            Check(Math.Abs(a-b)<1e-9&&Math.Abs(c-d)<1e-9,"visible pixel position unchanged");
        }
        Check(!Plan(10,10,0,0,10,10,.5,.5,out p),"no space saved");
        Check(!Plan(10,10,2,2,1,1,.5,.5,out p,clamp:false),"repeat excluded");
        Check(!Plan(10,10,2,2,1,1,.5,.5,out p,mip:true),"mips excluded");
        Check(!Plan(10,10,2,2,1,1,.5,.5,out p,masked:true),"masks excluded");
        Check(!Plan(10,10,2,2,1,1,.5,.5,out p,custom:true),"unknown shader excluded");
        Check(!Plan(10,10,2,2,1,1,.5,.5,out p,compressed:true),"DXT block changes excluded");
        Check(!Plan(10,10,2,2,1,1,.5,.5,out p,known:false),"unknown original dimensions excluded");
        Check(!Plan(10,10,2,2,1,1,.5,.5,out p,padding:0),"bilinear padding required");
        Check(!Plan(10,10,2,2,1,1,double.NaN,.5,out p),"nonfinite pivot");
        Check(!Plan(10,10,9,9,2,1,.5,.5,out p),"outside crop");
        Console.WriteLine(JsonSerializer.Serialize(new{conditions,errors=0,pixels_checked=pixelsChecked,random_seed=1202026,native=true,game_texture_mutations=0,gpu_pixels_verified=false},new JsonSerializerOptions{WriteIndented=true}));
    }
}
