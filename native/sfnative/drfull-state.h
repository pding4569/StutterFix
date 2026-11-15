// Unity serialized values are a wire format, never D3D enum ordinals.
namespace unityState {
inline void valid(bool b){if(!b)throw E_INVALIDARG;}
inline D3D11_BLEND blend(int x){
    static const D3D11_BLEND t[]={D3D11_BLEND_ZERO,D3D11_BLEND_ONE,D3D11_BLEND_DEST_COLOR,D3D11_BLEND_SRC_COLOR,D3D11_BLEND_INV_DEST_COLOR,D3D11_BLEND_SRC_ALPHA,D3D11_BLEND_INV_SRC_COLOR,D3D11_BLEND_DEST_ALPHA,D3D11_BLEND_INV_DEST_ALPHA,D3D11_BLEND_SRC_ALPHA_SAT,D3D11_BLEND_INV_SRC_ALPHA};
    valid(x>=0&&x<11);return t[x];
}
inline D3D11_BLEND_OP blendOp(int x){static const D3D11_BLEND_OP t[]={D3D11_BLEND_OP_ADD,D3D11_BLEND_OP_SUBTRACT,D3D11_BLEND_OP_REV_SUBTRACT,D3D11_BLEND_OP_MIN,D3D11_BLEND_OP_MAX};valid(x>=0&&x<5);return t[x];}
inline D3D11_BLEND blendAlpha(int x){valid(x>=0&&x<11);if(x==2)x=7;else if(x==3)x=5;else if(x==4)x=8;else if(x==6)x=10;else if(x==9)x=1;return blend(x);}
inline UINT8 colorMask(int x){valid(x>=0&&x<16);return UINT8(((x&8)?1:0)|((x&4)?2:0)|((x&2)?4:0)|((x&1)?8:0));}
inline D3D11_COMPARISON_FUNC compare(int x,bool reverse=false){
    static const D3D11_COMPARISON_FUNC t[]={D3D11_COMPARISON_ALWAYS,D3D11_COMPARISON_NEVER,D3D11_COMPARISON_LESS,D3D11_COMPARISON_EQUAL,D3D11_COMPARISON_LESS_EQUAL,D3D11_COMPARISON_GREATER,D3D11_COMPARISON_NOT_EQUAL,D3D11_COMPARISON_GREATER_EQUAL,D3D11_COMPARISON_ALWAYS};
    valid(x>=0&&x<9);if(reverse){if(x==2)x=5;else if(x==5)x=2;else if(x==4)x=7;else if(x==7)x=4;}return t[x];
}
inline D3D11_STENCIL_OP stencil(int x){static const D3D11_STENCIL_OP t[]={D3D11_STENCIL_OP_KEEP,D3D11_STENCIL_OP_ZERO,D3D11_STENCIL_OP_REPLACE,D3D11_STENCIL_OP_INCR_SAT,D3D11_STENCIL_OP_DECR_SAT,D3D11_STENCIL_OP_INVERT,D3D11_STENCIL_OP_INCR,D3D11_STENCIL_OP_DECR};valid(x>=0&&x<8);return t[x];}
inline D3D11_CULL_MODE cull(int x){static const D3D11_CULL_MODE t[]={D3D11_CULL_NONE,D3D11_CULL_FRONT,D3D11_CULL_BACK};valid(x>=0&&x<3);return t[x];}
inline bool boolean(int x){valid(x==0||x==1);return x!=0;}
inline D3D11_TEXTURE_ADDRESS_MODE address(int x){static const D3D11_TEXTURE_ADDRESS_MODE t[]={D3D11_TEXTURE_ADDRESS_WRAP,D3D11_TEXTURE_ADDRESS_CLAMP,D3D11_TEXTURE_ADDRESS_MIRROR,D3D11_TEXTURE_ADDRESS_MIRROR_ONCE};valid(x>=0&&x<4);return t[x];}
inline D3D11_FILTER filter(int x,int aniso){valid(x>=0&&x<3&&aniso>=0&&aniso<=16);return aniso>1?D3D11_FILTER_ANISOTROPIC:x==0?D3D11_FILTER_MIN_MAG_MIP_POINT:x==2?D3D11_FILTER_MIN_MAG_MIP_LINEAR:D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT;}
}
