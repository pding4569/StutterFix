using System;
using StutterFix;
using UnityEngine;
using Object = UnityEngine.Object;

int checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
void Reset()
{
    ShaderWarm.Shutdown(); RenderTexture.Acquired.Clear(); RenderTexture.Released.Clear();
    Material.Created.Clear(); Object.Destroyed.Clear(); Material.Fail = false;
    RenderTexture.Allocations = RenderTexture.FailAt = RenderTexture.NullAt = 0;
    RenderTexture.active = new RenderTexture(); Graphics.Calls = Graphics.FailAt = 0;
    GL.Clears = 0; GL.Fail = false; Shader.Missing = false; Shader.FindCalls = 0;
    Shader.Filter = new Shader { name = "fixture" }; Resources.Missing = false; Resources.Loads = 0;
    ADOBase.levelPath = "fixture"; ADOBase.customLevel = null; scrVfxPlus.instance = null;
    Hitch.Playing = false; ShaderWarm.Enabled = true;
}
void Balanced(RenderTexture previous, string label)
{
    Check(ReferenceEquals(RenderTexture.active, previous), label + " active RT restored");
    Check(RenderTexture.Acquired.Count == RenderTexture.Released.Count, label + " temporary RT released");
    Check(Material.Created.Count == Object.Destroyed.Count, label + " materials destroyed");
}
for (int fault = 0; fault < 9; fault++)
{
    Reset(); var previous = RenderTexture.active;
    if (fault == 1) RenderTexture.FailAt = 1;
    if (fault == 2) RenderTexture.FailAt = 2;
    if (fault == 3) Material.Fail = true;
    if (fault == 4) Graphics.FailAt = 2;
    if (fault == 5) Shader.Filter.Passes = 0;
    if (fault == 6) GL.Fail = true;
    if (fault == 7) RenderTexture.NullAt = 1;
    if (fault == 8) Shader.Filter.isSupported = false;
    Check(ShaderWarmPasses.TryWarm(Shader.Filter) == (fault == 0), "pass outcome " + fault);
    Balanced(previous, "fault " + fault);
    if (fault == 0) Check(Graphics.Calls == 3 && GL.Clears == 2, "all passes / initialized sources");
}
Reset(); var caller = RenderTexture.active;
scrVfxPlus.instance = new scrVfxPlus { filterToComp = new() { ["fixture"] = new FixtureFilter() } };
Graphics.FailAt = 2;
ShaderWarm.AfterLoad();
Check(Graphics.Calls == 2, "first pass attempt failed at second pass");
Balanced(caller, "initial failed preparation");
Graphics.FailAt = 0;
ShaderWarm.AfterLoad();
Check(Graphics.Calls == 5, "failed shader retried through pending cache");
Check(Resources.Loads == 1, "retry does not duplicate held resource");
ShaderWarm.AfterLoad();
Check(Graphics.Calls == 5, "successful shader not redrawn");
Balanced(caller, "retry preparation");
ShaderWarm.Shutdown();
Check(ShaderWarm.ResourcesLoaded == 0 && ShaderWarm.LevelChanged, "unload clears lifetime state");
ShaderWarm.AfterLoad();
Check(Graphics.Calls == 8 && Resources.Loads == 2, "reload prepares resources again");
Reset(); scrVfxPlus.instance = new scrVfxPlus { filterToComp = new() { ["fixture"] = new FixtureFilter() } };
Shader.Missing = true; ShaderWarm.AfterLoad(); Check(Graphics.Calls == 0, "missing shader not rendered");
Shader.Missing = false; ShaderWarm.AfterLoad(); Check(Graphics.Calls == 3, "missing shader resolved later");
Reset(); scrVfxPlus.instance = new scrVfxPlus { filterToComp = new() { ["fixture"] = new FixtureFilter() } };
Resources.Missing = true; ShaderWarm.AfterLoad(); Check(ShaderWarm.ResourcesLoaded == 0, "missing resource not held");
Resources.Missing = false; ShaderWarm.AfterLoad(); Check(ShaderWarm.ResourcesLoaded == 1, "missing resource retried");
Check(Graphics.Calls == 3, "resource retry does not rewarm completed shader");
Reset(); ShaderWarm.Enabled = false; ShaderWarm.AfterLoad();
Check(RenderTexture.Allocations == 0 && Shader.FindCalls == 0, "off has no allocation or scan");
Reset(); Hitch.Playing = true; ShaderWarm.AfterLoad();
Check(RenderTexture.Allocations == 0 && Shader.FindCalls == 0, "AfterLoad does not run during play");
Console.WriteLine($"Shader warm ownership/retry conditions {checks}, errors 0; real GPU variants/performance unverified");
