using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace StutterFix
{
    // Own message thread/window. Never steals an existing process keyboard registration.
    internal sealed class RawKeyboardClock:IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct Device{internal ushort Page,Usage;internal uint Flags;internal IntPtr Target;}
        [StructLayout(LayoutKind.Sequential)] private struct Message{internal IntPtr Window;internal uint Id;internal UIntPtr W;internal IntPtr L;internal uint Time;internal int X,Y;internal uint Private;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct WindowClass{internal uint Style;internal WindowProc Proc;internal int ClassExtra,WindowExtra;internal IntPtr Instance,Icon,Cursor,Background;internal string Menu,Name;}
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProc(IntPtr h,uint m,UIntPtr w,IntPtr l);
        [DllImport("user32.dll",SetLastError=true)] private static extern uint GetRegisteredRawInputDevices([Out]Device[] list,ref uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)] private static extern bool RegisterRawInputDevices(Device[] list,uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)] private static extern uint GetRawInputData(IntPtr raw,uint command,IntPtr data,ref uint size,uint header);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern ushort RegisterClassW(ref WindowClass c);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool UnregisterClassW(string name,IntPtr instance);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateWindowExW(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetMessageW(out Message m,IntPtr h,uint first,uint last);
        [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref Message m);
        [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr h,uint m,UIntPtr w,IntPtr l);
        [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint id,uint m,UIntPtr w,IntPtr l);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string module);
        private readonly KeyDeliveryBuffer keys=new KeyDeliveryBuffer();
        private Thread thread;private volatile bool stopping;private volatile uint threadId;
        private WindowProc proc;private IntPtr buffer;private int bytes;
        private IntPtr keyboardDevice;
        internal volatile bool Ready;internal string Status="off";internal long Received,Rejected;
        internal void Start()
        {
            if(thread!=null)return;
            thread=new Thread(Run){IsBackground=true,Name="StutterFix RawInput clock"};thread.Start();
        }
        private static Device[] Registrations()
        {
            uint count=0,size=(uint)Marshal.SizeOf<Device>();
            if(GetRegisteredRawInputDevices(null,ref count,size)==uint.MaxValue||count>256)throw new InvalidOperationException("RawInput registry unavailable");
            var devices=new Device[count];
            if(count>0&&GetRegisteredRawInputDevices(devices,ref count,size)==uint.MaxValue)throw new InvalidOperationException("RawInput registry changed");
            return devices;
        }
        private static bool Keyboard(Device d)=>d.Page==1&&d.Usage==6;
        private void Run()
        {
            string name="StutterFix.RawClock."+Guid.NewGuid().ToString("N");IntPtr window=IntPtr.Zero,instance=IntPtr.Zero;bool registered=false,cls=false;
            try
            {
                foreach(var d in Registrations())if(Keyboard(d)){Status="existing-keyboard-registration; unchanged";return;}
                threadId=GetCurrentThreadId();instance=GetModuleHandleW(null);proc=Receive;
                var c=new WindowClass{Proc=proc,Instance=instance,Name=name};
                if(RegisterClassW(ref c)==0)throw new InvalidOperationException("Window class rejected");cls=true;
                window=CreateWindowExW(0,name,"",0,0,0,0,0,new IntPtr(-3),IntPtr.Zero,instance,IntPtr.Zero);
                if(window==IntPtr.Zero)throw new InvalidOperationException("Message window rejected");
                foreach(var d in Registrations())if(Keyboard(d)){Status="keyboard-registration-race; unchanged";return;}
                bytes=IntPtr.Size*2+24;buffer=Marshal.AllocHGlobal(bytes);
                // INPUTSINK, never NOLEGACY: retain ordinary game key messages.
                if(!RegisterRawInputDevices(new[]{new Device{Page=1,Usage=6,Flags=0x100,Target=window}},1,(uint)Marshal.SizeOf<Device>()))throw new InvalidOperationException("Keyboard registration rejected");
                registered=true;Ready=true;Status="receiving-software-QPC";
                while(!stopping)
                {
                    int result=GetMessageW(out var m,IntPtr.Zero,0,0);if(result<=0)break;DispatchMessageW(ref m);
                }
            }
            catch(Exception ex){Status="fallback: "+ex.GetType().Name+" / "+ex.Message;}
            finally
            {
                Ready=false;
                if(registered)try
                {
                    // Another library may have replaced ours: do not remove its registration.
                    foreach(var d in Registrations())if(Keyboard(d)&&d.Target==window&&d.Flags==0x100)
                        RegisterRawInputDevices(new[]{new Device{Page=1,Usage=6,Flags=1}},1,(uint)Marshal.SizeOf<Device>());
                }catch{}
                if(window!=IntPtr.Zero)DestroyWindow(window);if(cls)UnregisterClassW(name,instance);
                if(buffer!=IntPtr.Zero){Marshal.FreeHGlobal(buffer);buffer=IntPtr.Zero;}
                keys.Clear();
                threadId=0;
            }
        }
        private IntPtr Receive(IntPtr h,uint message,UIntPtr w,IntPtr l)
        {
            long qpc=Stopwatch.GetTimestamp();
            if(message==0xFF&&!stopping)try
            {
                uint size=(uint)bytes,header=(uint)(8+IntPtr.Size*2);
                uint copied=GetRawInputData(l,0x10000003,buffer,ref size,header);
                if(copied!=uint.MaxValue&&copied>=header+16&&size<=bytes&&Marshal.ReadInt32(buffer)==1)
                {
                    IntPtr device=Marshal.ReadIntPtr(buffer,8);
                    if(device==IntPtr.Zero||keyboardDevice!=IntPtr.Zero&&keyboardDevice!=device)
                    {
                        Status="unidentified-or-multiple-keyboards; correction=0";Ready=false;stopping=true;keys.Clear();Rejected++;
                        return DefWindowProcW(h,message,w,l);
                    }
                    keyboardDevice=device;
                    int key=Marshal.ReadInt16(buffer,(int)header+6)&0xFFFF;
                    int flags=Marshal.ReadInt16(buffer,(int)header+2)&0xFFFF;
                    if(key>0&&key<255){keys.Receive(key,(flags&1)!=0,qpc);Received++;}
                }else Interlocked.Increment(ref Rejected);
            }catch{Interlocked.Increment(ref Rejected);}
            return DefWindowProcW(h,message,w,l);
        }
        internal bool Take(int virtualKey,out long qpc)
        {
            qpc=0;return Ready&&keys.Take(virtualKey,out qpc);
        }
        internal void Clear(){keys.Clear();}
        public void Dispose()
        {
            stopping=true;Ready=false;uint id=threadId;if(id!=0)try{PostThreadMessageW(id,0x12,UIntPtr.Zero,IntPtr.Zero);}catch{}
            if(thread!=null&&thread!=Thread.CurrentThread)thread.Join(250);
            // Thread owns all native cleanup even if GetMessage has not returned yet.
        }
    }
}
