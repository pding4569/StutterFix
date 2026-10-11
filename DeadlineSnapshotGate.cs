namespace StutterFix {
    // Relative monotonic intervals only. No average FPS, allocation or GPU read.
    // Fast true frames for about half a second admit rest; one near-deadline interval
    // resumes snapshots. An unexpected first hitch cannot be covered while asleep.
    // Each switch moves the shown picture by the interpolation delay, so where the real rate hovers near
    // 1.25x the refresh it must not flap: HELLO 2026 (game screen, 165 Hz) entered rest 139 times in 70 s
    // with the old 8-frame rule. After a resume it stays awake for 3 s.
    internal struct DeadlineSnapshotGate {
        private long previous,resumedAt;
        private int fast, rate;
        internal bool Resting { get; private set; }
        internal int Rests; // times rest began (explicit state command only)
        internal void Reset() { if(Resting) resumedAt=previous; previous=0; fast=rate=0; Resting=false; }
        internal bool Observe(long now,long frequency,int hz) {
            if(hz<=0 || frequency<=0 || now<=0) { Reset(); return false; }
            if(rate!=hz || previous==0 || now<=previous) {
                if(Resting) resumedAt=now;
                previous=now;rate=hz;fast=0;Resting=false;return false;
            }
            double interval=(double)(now-previous)/frequency;
            previous=now;
            int need=hz/2>8?hz/2:8;
            if(interval<.8/hz) {
                if(fast<need) ++fast;
                if(!Resting && fast>=need && (resumedAt==0 || (double)(now-resumedAt)/frequency>=3)) { Resting=true; ++Rests; }
            }
            else { fast=0; if(Resting) { Resting=false; resumedAt=now; } }
            return Resting;
        }
    }
}
