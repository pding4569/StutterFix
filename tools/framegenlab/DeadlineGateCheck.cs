using System;
using StutterFix;
class DeadlineGateCheck {
 static int tests;
 static void Check(bool value,string label){++tests;if(!value)throw new Exception(label);}
 static void Main(){
  var gate=new DeadlineSnapshotGate();long now=1;
  Check(!gate.Observe(now,1000000,100),"first frame saves");
  for(int i=1;i<=8;i++)Check(gate.Observe(now+=7999,1000000,100)==(i==8),"eight consecutive fast frames");
  Check(!gate.Observe(now+=8000,1000000,100),"exact80% wakes");
  for(int i=1;i<=8;i++)Check(gate.Observe(now+=7000,1000000,100)==(i==8),"reentry requires eight");
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
