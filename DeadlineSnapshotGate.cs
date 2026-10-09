namespace StutterFix {
    // Relative monotonic intervals only. No average FPS, allocation or GPU read.
    // Eight consecutive fast true frames admit rest; one near-deadline interval
    // resumes snapshots. An unexpected first hitch cannot be covered while asleep.
    internal struct DeadlineSnapshotGate {
        private long previous;
        private int fast, rate;
        internal bool Resting { get; private set; }
        internal void Reset() { previous=0; fast=rate=0; Resting=false; }
        internal bool Observe(long now,long frequency,int hz) {
            if(hz<=0 || frequency<=0 || now<=0) { Reset(); return false; }
            if(rate!=hz || previous==0 || now<=previous) {
                previous=now;rate=hz;fast=0;Resting=false;return false;
            }
            double interval=(double)(now-previous)/frequency;
            previous=now;
            if(interval<.8/hz) { if(fast<8)++fast;Resting=fast>=8; }
            else {fast=0;Resting=false;}
            return Resting;
        }
    }
}
