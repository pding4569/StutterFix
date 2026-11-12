#if AUTOTEST || DRAWCOLLAPSE_CHECK
namespace StutterFix
{
    // A missing or mixed future address permanently rejects the proof.
    // This is not a prediction from the currently displayed image. The upper
    // bound may include registered Texture2D slots after the array bindings.
    internal struct SingleArrayProof
    {
        bool seen,rejected;
        int binding;
        internal int Binding=>seen&&!rejected?binding:-1;
        internal void Unknown(){rejected=true;}
        internal void Observe(int address,int arrays)
        {
            if(address<0 || address>=arrays){rejected=true;return;}
            if(seen && binding!=address){rejected=true;return;}
            seen=true;binding=address;
        }
    }
}
#endif
