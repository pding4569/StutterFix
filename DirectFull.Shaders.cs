#if DEV || AUTOTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace StutterFix
{
    // Resource schema is produced by export-original-shaders.py. That tool joins
    // parsed/common parameters with the parameter blob referenced by each stage,
    // and verifies every binding against declarations and link signatures.
    internal static class DirectFullShaders
    {
        internal sealed class Variable { internal string Name, TextureName; internal int Id, Offset, Type, Rows, Cols, Matrix, TextureId; internal bool TextureST; }
        internal sealed class Buffer { internal string Name; internal int Slot, Size, Declared; internal Variable[] Variables; }
        internal sealed class Texture { internal string Name; internal int Id, Slot, Sampler; }
        internal sealed class Input { internal string Name; internal int Index, System, Type, Register, Mask; }
        internal sealed class Stage { internal byte[] Code; internal Buffer[] Buffers; internal Texture[] Textures; internal Input[] Inputs; }
        internal sealed class Variant { internal string[] Keywords; internal Stage Vs, Ps; }
        internal sealed class State { internal float Value; internal string Name; internal int Id; }
        internal sealed class Pass { internal int Index; internal bool Grab; internal string GrabName = ""; internal State[] States; internal Variant[] Variants; }
        internal sealed class Property { internal string Name; internal int Id, Type; internal Vector4 Default; internal string DefaultTexture; }
        internal sealed class ShaderData { internal string Name; internal string[] Keywords; internal Property[] Properties; internal Pass[] Passes; }
        static Dictionary<string, ShaderData> all;
        static int Count(BinaryReader r, int max = 10000) { int n = r.ReadInt32(); if (n < 0 || n > max) throw new InvalidDataException("DRF resource count"); return n; }
        static string Text(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(Count(r, 1048576)));
        internal static Dictionary<string, ShaderData> Load()
        {
            if (all != null) return all;
            var result = new Dictionary<string, ShaderData>(StringComparer.Ordinal);
            using (var stream = typeof(DirectFull).Assembly.GetManifestResourceStream("StutterFix.drfull.original"))
            using (var r = new BinaryReader(stream ?? throw new InvalidDataException("DRF original resource missing")))
            {
                if (r.ReadInt32() != 0x34465244) throw new InvalidDataException("DRF magic");
                int schema = r.ReadInt32(); if (schema != 1 && schema != 2) throw new InvalidDataException("DRF schema");
                int n = Count(r);
                for (int i = 0; i < n; i++)
                {
                    var s = new ShaderData { Name = Text(r), Keywords = Strings(r) };
                    s.Properties = new Property[Count(r)];
                    for (int k = 0; k < s.Properties.Length; k++)
                    {
                        var p = new Property { Name = Text(r), Type = r.ReadInt32(), Default = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), DefaultTexture = Text(r) };
                        p.Id = Shader.PropertyToID(p.Name); s.Properties[k] = p;
                    }
                    s.Passes = new Pass[Count(r, 32)];
                    for (int k = 0; k < s.Passes.Length; k++)
                    {
                        var p = new Pass { Index = r.ReadInt32(), Grab = r.ReadInt32() != 0, States = new State[29] };
                        if (schema >= 2) p.GrabName = Text(r);
                        for (int j = 0; j < p.States.Length; j++) { var st = new State { Value = r.ReadSingle(), Name = Text(r) }; st.Id = Shader.PropertyToID(st.Name); p.States[j] = st; }
                        p.Variants = new Variant[Count(r)];
                        for (int j = 0; j < p.Variants.Length; j++) p.Variants[j] = new Variant { Keywords = Strings(r), Vs = ReadStage(r), Ps = ReadStage(r) };
                        s.Passes[k] = p;
                    }
                    result.Add(s.Name, s);
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("DRF resource trailing bytes");
            }
            all = result; return all;
        }
        static string[] Strings(BinaryReader r) { var a = new string[Count(r)]; for (int i = 0; i < a.Length; i++) a[i] = Text(r); return a; }
        static Stage ReadStage(BinaryReader r)
        {
            var s = new Stage { Code = r.ReadBytes(Count(r, 1048576)), Buffers = new Buffer[Count(r, 14)] };
            for (int i = 0; i < s.Buffers.Length; i++)
            {
                var b = new Buffer { Name = Text(r), Slot = r.ReadInt32(), Size = r.ReadInt32(), Declared = r.ReadInt32(), Variables = new Variable[Count(r)] };
                if (b.Slot < 0 || b.Slot >= 14 || b.Size <= 0 || b.Size > 65536 || b.Size % 16 != 0 || b.Declared > b.Size) throw new InvalidDataException("DRF CB extent");
                for (int k = 0; k < b.Variables.Length; k++)
                {
                    var v = new Variable { Name = Text(r), Offset = r.ReadInt32(), Type = r.ReadInt32(), Rows = r.ReadInt32(), Cols = r.ReadInt32(), Matrix = r.ReadInt32() };
                    v.Id = Shader.PropertyToID(v.Name);
                    if (v.Name.EndsWith("_TexelSize", StringComparison.Ordinal) || v.Name.EndsWith("_ST", StringComparison.Ordinal))
                    { v.TextureST = v.Name.EndsWith("_ST", StringComparison.Ordinal); v.TextureName = v.Name.Substring(0, v.Name.Length - (v.TextureST ? 3 : 10)); v.TextureId = Shader.PropertyToID(v.TextureName); }
                    if (v.Offset < 0 || v.Offset + 4 * v.Rows * v.Cols > b.Size) throw new InvalidDataException("DRF variable extent");
                    b.Variables[k] = v;
                }
                s.Buffers[i] = b;
            }
            s.Textures = new Texture[Count(r, 32)];
            for (int i = 0; i < s.Textures.Length; i++) { var t = new Texture { Name = Text(r), Slot = r.ReadInt32(), Sampler = r.ReadInt32() }; t.Id = Shader.PropertyToID(t.Name); s.Textures[i] = t; }
            s.Inputs = new Input[Count(r, 32)];
            for (int i = 0; i < s.Inputs.Length; i++) s.Inputs[i] = new Input { Name = Text(r), Index = r.ReadInt32(), System = r.ReadInt32(), Type = r.ReadInt32(), Register = r.ReadInt32(), Mask = r.ReadInt32() };
            return s;
        }
        internal static Variant Select(ShaderData shader, Pass pass, Material material)
        {
            foreach (var v in pass.Variants)
            {
                bool matches = true;
                foreach (string k in shader.Keywords)
                {
                    // Explicit DrawIndexed is never an instanced Unity batch.
                    bool active = k != "INSTANCING_ON" && (material && material.IsKeywordEnabled(k) || Shader.IsKeywordEnabled(k));
                    bool included = Array.IndexOf(v.Keywords, k) >= 0;
                    if (active != included) { matches = false; break; }
                }
                if (matches) return v;
            }
            throw new InvalidOperationException("no verified original variant: " + shader.Name);
        }
    }
}
#endif
