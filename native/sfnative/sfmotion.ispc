// StutterFix original MIT code. Linear scalar animation values in SoA form.
// Keep divide, multiply and add separate; build without fast-math or FMA.
export void sf_motion_linear_ispc(uniform const float start[], uniform const float change[],
    uniform const float position[], uniform const float duration[], uniform float output[], uniform int count, uniform int rounding)
{
    foreach(i=0 ... count)
    {
        float fraction=position[i]/duration[i];
        if(rounding==1)
        {
            // Unity Mono can evaluate the multiply/add in double precision,
            // then round once at the stored float. Runtime calibration selects
            // this path only when it matches that runtime's actual expression.
            double delta=(double)change[i]*(double)fraction;
            output[i]=(float)((double)start[i]+delta);
        }
        else
        {
            float delta=change[i]*fraction;
            output[i]=start[i]+delta;
        }
    }
}
