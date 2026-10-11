using System;
using StutterFix;
class DeadlineGateCheck {
 static int tests;
 static void Check(bool value,string label){++tests;if(!value)throw new Exception(label);}
 static void Main(){
  var gate=new DeadlineSnapshotGate();long now=1;
  Check(!gate.Observe(now,1000000,100),"first frame saves");
  for(int i=1;i<=50;i++)Check(gate.Observe(now+=7999,1000000,100)==(i==50),"half a second of fast frames (hz/2)");
  Check(!gate.Observe(now+=8000,1000000,100),"exact80% wakes");
  for(int i=1;i<=50;i++)Check(!gate.Observe(now+=7000,1000000,100),"no rest within 3 s of a wake");
  long woke=now-50*7000;
  while(now+7000-woke<3000000) Check(!gate.Observe(now+=7000,1000000,100),"hold until 3 s");
  Check(gate.Observe(now+=7000,1000000,100),"rest again after the hold");
  Check(gate.Rests==2,"two rests counted");
  Check(!gate.Observe(now+=100000,1000000,100),"hitch wakes");
  Check(!gate.Observe(now+=1000,1000000,165),"refresh change resets");
  Check(!gate.Observe(now-10,1000000,165),"clock regression resets");
  Check(!gate.Observe(0,1000000,165),"zero clock resets");
  Check(!gate.Observe(1,0,165),"invalid frequency resets");
  Check(!gate.Observe(1,1000000,0),"invalid refresh resets");
  gate.Reset();Check(!gate.Resting,"unload reset");
  Console.WriteLine("conditions="+tests+" failures=0");
 }
}
