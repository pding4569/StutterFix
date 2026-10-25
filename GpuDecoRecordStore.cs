#if GPUDECO && !DEV && !AUTOTEST
using System.Runtime.CompilerServices;

namespace StutterFix
{
    // Keep GPU metadata out of the normal player's Rec object layout. The table
    // and its entries exist only once GPU preparation/ownership actually uses them.
    // Weak keys do not retain pooled or discarded animation records across maps.
    internal static class GpuDecoRecordStore
    {
        internal sealed class Stamp
        {
            public Stamp() { }
            internal int Source, StartFrame=-1, Generation=-1, CaptureEpoch=-1;
            internal bool OrderObserved;
            internal long Order;
            internal double StartSong;
        }
        static ConditionalWeakTable<DecoAnim.Rec,Stamp> stamps;
        internal static Stamp For(DecoAnim.Rec record)
        {
            if(stamps==null)stamps=new ConditionalWeakTable<DecoAnim.Rec,Stamp>();
            return stamps.GetOrCreateValue(record);
        }
        internal static void Clear() { stamps=null; }
    }
}
#endif
