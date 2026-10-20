using StutterFix;
int checks = 0;
void Check(bool value) { checks++; if (!value) throw new Exception("check " + checks); }
Check(MatSharePolicy.Runs(new[]{"A","B","A"},new bool[3]).SequenceEqual(new[]{0,1,2}));
Check(MatSharePolicy.Runs(new[]{"A","A","A"},new[]{false,true,false}).SequenceEqual(new[]{0,1,2}));
Check(MatSharePolicy.Runs(Array.Empty<string>(),Array.Empty<bool>()).Length == 0);
var rng = new Random(20261021);
for (int t = 0; t < 1000; t++) {
    int n = rng.Next(1,50); var keys = Enumerable.Range(0,n).Select(_=>rng.Next(3).ToString()).ToArray();
    var barriers = Enumerable.Range(0,n).Select(_=>rng.Next(4)==0).ToArray();
    var runs = MatSharePolicy.Runs(keys,barriers);
    for (int i = 1; i < n; i++) Check((runs[i]==runs[i-1]) == (!barriers[i] && !barriers[i-1] && keys[i]==keys[i-1]));
    var ids = Enumerable.Range(0,n).Select(i=>unchecked(int.MinValue+i*7919)).OrderBy(_=>rng.Next()).ToArray();
    var order = MatSharePolicy.MaterialOrder(ids);
    Check(order.Select(i=>ids[i]).SequenceEqual(ids.OrderBy(x=>x)));
}
Console.WriteLine($"MatShareCheck checks={checks} errors=0; pure policy only; Unity sorting fixture requires runtime");
