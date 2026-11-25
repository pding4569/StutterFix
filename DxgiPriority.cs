#if DEV || AUTOTEST
using System;
using System.Runtime.InteropServices;

namespace StutterFix
{
    // Diagnostic: IDXGIResource::SetEvictionPriority through the native D3D11 texture pointer (COM vtable).
    internal static class DxgiPriority
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int QueryInterfaceFn(IntPtr self, ref Guid iid, out IntPtr obj);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint ReleaseFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetEvictionPriorityFn(IntPtr self, uint priority);
        static Guid iidDxgiResource = new Guid("035f3ab4-482e-4e50-b41f-8a7f8bd8960b");

        static IntPtr Slot(IntPtr obj, int index) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), index * IntPtr.Size);

        internal static int Set(IntPtr texture, uint priority)
        {
            if (texture == IntPtr.Zero) return unchecked((int)0x80004003);
            var qi = Marshal.GetDelegateForFunctionPointer<QueryInterfaceFn>(Slot(texture, 0));
            int hr = qi(texture, ref iidDxgiResource, out IntPtr res);
            if (hr != 0 || res == IntPtr.Zero) return hr != 0 ? hr : unchecked((int)0x80004002);
            try
            {
                // IUnknown 3 + IDXGIObject 4 + IDXGIDeviceSubObject 1 + GetSharedHandle, GetUsage → SetEvictionPriority = 10
                return Marshal.GetDelegateForFunctionPointer<SetEvictionPriorityFn>(Slot(res, 10))(res, priority);
            }
            finally { Marshal.GetDelegateForFunctionPointer<ReleaseFn>(Slot(res, 2))(res); }
        }
    }
}
#endif
