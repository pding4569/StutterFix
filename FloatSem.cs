#if DEV || AUTOTEST
using System;
using System.Runtime.CompilerServices;

namespace StutterFix
{
    // (자동 시험 floatsem) 이 게임 Mono 가 float 식 `s + c*e` 를 어떻게 계산하는지: 곱셈 뒤 반올림하는 엄격한 단정밀도인지, 곱셈+덧셈을 한 번만 반올림하는지(FMA 같은),
    // 식 모양(지역 변수·필드·메서드 반환)에 따라 달라지는지를 같은 입력으로 비교한다.
    internal static class FloatSem
    {
        private sealed class Holder { internal float s, c; internal float Calc(float e) { return s + c * e; } internal float CalcLocal(float e) { float m = c * e; return s + m; } }
        private struct Pair { internal float s, c; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float s, float c, float e) { return s + c * e; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float B(float s, float c, float e) { float m = c * e; return s + m; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float Strict(float s, float c, float e)
        {   // 곱셈 결과를 float 로 확정(비트 변환)한 뒤 더한다
            float m = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(c * e));
            return BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(s + m));
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float Dbl(float s, float c, float e) { return (float)((double)s + (double)c * (double)e); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float Fma(float s, float c, float e) { return (float)Math.FusedMultiplyAdd((double)c, (double)e, (double)s); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float ViaStruct(Pair p, float e) { return p.s + p.c * e; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float t, float d) { return t / d; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float DivStrict(float t, float d) { return BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(t / d)); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float Cubic(float t, float d) { t = t / d - 1f; return t * t * t + 1f; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static float CubicStrict(float t, float d)
        {
            float q = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(t / d)); q = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(q - 1f));
            float a = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(q * q)); float b = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(a * q));
            return BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(b + 1f));
        }
        private static bool Eq(float a, float b) { return BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b); }

        internal static string Run()
        {
            var rnd = new Random(777); const int N = 2000000;
            long aB = 0, aStrict = 0, aDbl = 0, aFma = 0, aField = 0, aFieldLocal = 0, aStruct = 0, strictDbl = 0, strictFma = 0, dblFma = 0, divS = 0, cubS = 0;
            var h = new Holder();
            for (int i = 0; i < N; i++)
            {
                float s = (float)((rnd.NextDouble() * 2 - 1) * 4000), c = (float)((rnd.NextDouble() * 2 - 1) * 4000), e = (float)rnd.NextDouble();
                float a = A(s, c, e), b = B(s, c, e), st = Strict(s, c, e), d = Dbl(s, c, e), f = Fma(s, c, e);
                h.s = s; h.c = c; var p = new Pair { s = s, c = c };
                if (Eq(a, b)) aB++; if (Eq(a, st)) aStrict++; if (Eq(a, d)) aDbl++; if (Eq(a, f)) aFma++;
                if (Eq(a, h.Calc(e))) aField++; if (Eq(a, h.CalcLocal(e))) aFieldLocal++; if (Eq(a, ViaStruct(p, e))) aStruct++;
                if (Eq(st, d)) strictDbl++; if (Eq(st, f)) strictFma++; if (Eq(d, f)) dblFma++;
                float t = (float)(rnd.NextDouble() * 40), dur = (float)(rnd.NextDouble() * 40 + 0.1);
                if (Eq(Div(t, dur), DivStrict(t, dur))) divS++; if (Eq(Cubic(t, dur), CubicStrict(t, dur))) cubS++;
            }
            return "N=" + N + " | s+c*e: 식A==식B(지역변수) " + aB + ", A==엄격단정밀도 " + aStrict + ", A==double 한 번 반올림 " + aDbl + ", A==FMA " + aFma
                + ", A==필드식 " + aField + ", A==필드+지역 " + aFieldLocal + ", A==구조체식 " + aStruct + " | 엄격==double " + strictDbl + ", 엄격==FMA " + strictFma + ", double==FMA " + dblFma
                + " | 나눗셈 일반==엄격 " + divS + " | OutCubic 일반==엄격 " + cubS + " (전부 N 이면 같은 의미)";
        }
    }
}
#endif
