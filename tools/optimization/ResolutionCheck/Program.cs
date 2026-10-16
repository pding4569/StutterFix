using System;
using StutterFix;
int n=0;
void Check(float s,float b,int direction){n++;if(ResolutionBudgetPolicy.Direction(s,b)!=direction)throw new Exception($"sample {s}, budget {b}");}
Check(8,10,0);Check(8.001f,10,-1);Check(5.5f,10,0);Check(5.499f,10,1);Check(7,10,0);
Check(float.NaN,10,0);Check(float.PositiveInfinity,10,0);Check(0,10,0);Check(-1,10,0);Check(8,0,0);Check(8,float.NaN,0);Check(8,float.PositiveInfinity,0);
Console.WriteLine($"Resolution budget checks {n}, errors 0; GPU availability and runtime response separately required");
