using System;
namespace StutterFix
{
    // One outstanding down per VK. A second press before polling is ambiguous, not guessed.
    internal sealed class KeyDeliveryBuffer
    {
        private readonly object gate=new object();
        private readonly long[] down=new long[256];private readonly bool[] held=new bool[256],ambiguous=new bool[256];
        internal void Receive(int key,bool up,long qpc)
        {
            if(key<=0||key>=255||qpc<=0)return;
            lock(gate)
            {
                if(up){held[key]=false;return;} // Keep short taps delivered before the next Update.
                if(held[key])return;held[key]=true;
                if(down[key]!=0||ambiguous[key]){ambiguous[key]=true;down[key]=0;}
                else down[key]=qpc;
            }
        }
        internal bool Take(int key,out long qpc)
        {
            qpc=0;if(key<=0||key>=255)return false;
            lock(gate)
            {
                bool uncertain=ambiguous[key];qpc=down[key];down[key]=0;ambiguous[key]=false;
                return !uncertain&&qpc!=0;
            }
        }
        internal void Clear(){lock(gate){Array.Clear(down,0,down.Length);Array.Clear(held,0,held.Length);Array.Clear(ambiguous,0,ambiguous.Length);}}
    }
}
