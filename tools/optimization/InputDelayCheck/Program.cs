using System;
using System.Threading;
using StutterFix;
int checks=0;
void Check(bool value){checks++;if(!value)throw new Exception("Condition "+checks);}
bool Correct(long received,long poll,long freq=10000000,bool timestamped=false,bool focused=true,bool playing=true)
    =>FrameQueueDelayPolicy.TryCorrect(received,poll,freq,1000000000,timestamped,focused,playing,out _,out _);
Check(FrameQueueDelayPolicy.TryCorrect(1000000,1100000,10000000,1000000000,false,true,true,out long result,out double ms)&&result==999900000&&ms==10);
Check(Correct(100,100));Check(!Correct(0,100));Check(!Correct(200,100));Check(!Correct(1,100,0));
Check(!Correct(1,100,timestamped:true));Check(!Correct(1,100,focused:false));Check(!Correct(1,100,playing:false));
Check(Correct(100,2500100));Check(!Correct(100,2500101));Check(Correct(long.MaxValue-1,long.MaxValue));
Check(!FrameQueueDelayPolicy.TryCorrect(1,1000001,10000000,100,false,true,true,out result,out ms));
var keys=new KeyDeliveryBuffer();long stamp;
keys.Receive(65,false,100);keys.Receive(65,true,110);Check(keys.Take(65,out stamp)&&stamp==100);Check(!keys.Take(65,out stamp));
keys.Receive(65,false,120);keys.Receive(65,false,130);Check(keys.Take(65,out stamp)&&stamp==120);
keys.Receive(65,true,140);keys.Receive(65,false,150);keys.Receive(65,true,160);keys.Receive(65,false,170);Check(!keys.Take(65,out stamp));
keys.Clear();keys.Receive(66,false,200);Check(!keys.Take(65,out stamp));Check(keys.Take(66,out stamp)&&stamp==200);
keys.Receive(67,false,210);keys.Clear();Check(!keys.Take(67,out stamp));keys.Receive(255,false,220);Check(!keys.Take(255,out stamp));
using(var clock=new RawKeyboardClock())
{
    clock.Start();var until=DateTime.UtcNow.AddSeconds(2);
    while(!clock.Ready&&clock.Status=="off"&&DateTime.UtcNow<until)Thread.Sleep(10);
    Check(clock.Ready);Check(!clock.Take(0,out _));Check(!clock.Take(256,out _));
    using(var competing=new RawKeyboardClock())
    {
        competing.Start();var end=DateTime.UtcNow.AddSeconds(2);
        while(competing.Status=="off"&&DateTime.UtcNow<end)Thread.Sleep(10);
        Check(!competing.Ready&&competing.Status.Contains("existing-keyboard-registration"));
        competing.Dispose();Check(clock.Ready);
    }
    Console.WriteLine("Owned message thread: "+clock.Status+"; real keyboard timing not tested");
    clock.Dispose();Check(!clock.Ready);
}
Console.WriteLine("Input delay conditions "+checks+", errors 0; hardware time and judgment quality not measured");
