// 직통 렌더러(DR2): 장식 상태를 GPU 버퍼로 받아 순서대로 인스턴스 그리기. Unity 객체·API 를 쓰지 않는다.
// 메인 스레드: sf_dr2_commit 이 바뀐 채널·그리기 목록을 프레임 묶음으로 복사해 큐에 넣는다.
// 렌더 스레드: 카메라 CommandBuffer 의 플러그인 이벤트가 묶음을 꺼내 버퍼를 올리고 그린다(지연 컨텍스트 -> 명령 목록, Unity 상태 복원).
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <wrl/client.h>
#include <vector>
#include <array>
#include <mutex>
#include <memory>
#include <atomic>
#include <algorithm>
#include <cstring>
#include <cstdint>
#include <cmath>
#include "dr2_shader_bytes.h"
#define DR2_API extern "C" __declspec(dllexport)
namespace {
using Microsoft::WRL::ComPtr;
const unsigned kChannelFloats = 16, kStaticFloats = 32;
struct Run { uint32_t start, count; int32_t texture, geometry; uint32_t flags; };   // flags: 1 점 샘플링, 2 더하기, 4 마스크 들어감(스텐실+1), 8 마스크 나옴(-1), 16 마스크 안에서만, 32 마스크 밖에서만
struct FrameIn {
    float vp[16]; float cam[4]; float ortho[4];
    uint32_t dirtyCount; uint32_t listChanged; const uint32_t* dirtySlots; const float* channels;
    const uint32_t* list; const Run* runs; const uint32_t* segs; uint32_t listCount, runCount, segCount, clearStencil;
};
struct Packet {
    float vp[16], cam[4], ortho[4]; uint32_t clearStencil = 0;
    std::vector<uint32_t> dirtySlots; std::vector<float> dirtyValues;
    bool hasList = false; std::vector<uint32_t> list, segs; std::vector<Run> runs;
};
struct Texture { ComPtr<ID3D11ShaderResourceView> view; ComPtr<ID3D11SamplerState> sampler; };
struct Geometry { uint32_t indexStart = 0, indexCount = 0; int32_t baseVertex = 0; };
double nowUs() { LARGE_INTEGER c, f; QueryPerformanceCounter(&c); QueryPerformanceFrequency(&f); return double(c.QuadPart) * 1e6 / double(f.QuadPart); }
DXGI_FORMAT viewFormat(DXGI_FORMAT f) { switch (f) {
    case DXGI_FORMAT_R8G8B8A8_TYPELESS: return DXGI_FORMAT_R8G8B8A8_UNORM;
    case DXGI_FORMAT_B8G8R8A8_TYPELESS: return DXGI_FORMAT_B8G8R8A8_UNORM;
    case DXGI_FORMAT_BC1_TYPELESS: return DXGI_FORMAT_BC1_UNORM;
    case DXGI_FORMAT_BC2_TYPELESS: return DXGI_FORMAT_BC2_UNORM;
    case DXGI_FORMAT_BC3_TYPELESS: return DXGI_FORMAT_BC3_UNORM;
    case DXGI_FORMAT_BC7_TYPELESS: return DXGI_FORMAT_BC7_UNORM;
    case DXGI_FORMAT_R16G16B16A16_TYPELESS: return DXGI_FORMAT_R16G16B16A16_FLOAT;
    default: return f; } }

struct Renderer {
    int token; uint32_t maxItems;
    ComPtr<ID3D11Device> device; ComPtr<ID3D11DeviceContext> context, deferred;
    ComPtr<ID3D11Buffer> channelBuffer, staticBuffer, frameBuffer, listBuffer, vertexBuffer, indexBuffer;
    ComPtr<ID3D11ShaderResourceView> channelView, staticView;
    ComPtr<ID3D11VertexShader> vs; ComPtr<ID3D11PixelShader> ps[8]; ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11Texture2D> grabTexture; ComPtr<ID3D11ShaderResourceView> grabView;
    D3D11_TEXTURE2D_DESC grabDesc{}; DXGI_FORMAT grabFormat = DXGI_FORMAT_UNKNOWN;
    ComPtr<ID3D11BlendState> blend[3]; ComPtr<ID3D11DepthStencilState> depth[5]; ComPtr<ID3D11RasterizerState> raster;
    std::vector<float> channelShadow, staticShadow;
    // 메인 -> 렌더 큐
    std::mutex mutex;
    std::array<Packet, 8> ring; uint64_t readIndex = 0, writeIndex = 0;
    uint32_t staticDirtyLo = UINT32_MAX, staticDirtyHi = 0;
    std::vector<float> staticPending; bool staticPendingAny = false;
    std::vector<Texture> textures; std::vector<Geometry> geometries;
    std::vector<float> geomVertices; std::vector<uint32_t> geomIndices; bool geomDirty = true; uint32_t vbCapacity = 0, ibCapacity = 0;
    // 렌더 스레드 상태
    float vp[16]{}, cam[4]{}, ortho[4]{}; bool clearStencil = false;
    std::vector<Run> runs; std::vector<uint32_t> segs; uint32_t listCount = 0, listCapacity = 0;
    std::vector<uint32_t> drawList;
    std::vector<Texture> texCopy; std::vector<Geometry> geomCopy;
    bool haveFrame = false;
    // 통계
    std::atomic<uint64_t> frames{0}, draws{0}, instances{0}, dropped{0}, errors{0}, uploadedBytes{0}, renderCalls{0}, commits{0};
    std::atomic<uint64_t> grabCopies{0}, grabPixels{0};
    double renderUsSum = 0, commitUsSum = 0;

    static void check(HRESULT hr) { if (FAILED(hr)) throw hr; }
    Renderer(ID3D11Device* dev, uint32_t n) : token(0), maxItems(n), device(dev) {
        device->GetImmediateContext(&context); check(device->CreateDeferredContext(0, &deferred));
        channelShadow.assign(size_t(n) * kChannelFloats, 0.f); staticShadow.assign(size_t(n) * kStaticFloats, 0.f); staticPending.assign(size_t(n) * kStaticFloats, 0.f);
        D3D11_BUFFER_DESC b{}; b.Usage = D3D11_USAGE_DEFAULT; b.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        b.ByteWidth = n * kChannelFloats * 4; check(device->CreateBuffer(&b, nullptr, &channelBuffer));
        b.ByteWidth = n * kStaticFloats * 4; check(device->CreateBuffer(&b, nullptr, &staticBuffer));
        D3D11_SHADER_RESOURCE_VIEW_DESC sv{}; sv.Format = DXGI_FORMAT_R32G32B32A32_FLOAT; sv.ViewDimension = D3D11_SRV_DIMENSION_BUFFER;
        sv.Buffer.NumElements = n * kChannelFloats / 4; check(device->CreateShaderResourceView(channelBuffer.Get(), &sv, &channelView));
        sv.Buffer.NumElements = n * kStaticFloats / 4; check(device->CreateShaderResourceView(staticBuffer.Get(), &sv, &staticView));
        b = {}; b.ByteWidth = 96; b.Usage = D3D11_USAGE_DEFAULT; b.BindFlags = D3D11_BIND_CONSTANT_BUFFER; check(device->CreateBuffer(&b, nullptr, &frameBuffer));
        check(device->CreateVertexShader(dr2_vs, sizeof(dr2_vs), nullptr, &vs));
        check(device->CreatePixelShader(dr2_ps, sizeof(dr2_ps), nullptr, &ps[0]));
        check(device->CreatePixelShader(dr2_pspoint, sizeof(dr2_pspoint), nullptr, &ps[1]));
        check(device->CreatePixelShader(dr2_psadd, sizeof(dr2_psadd), nullptr, &ps[2]));
        check(device->CreatePixelShader(dr2_psaddpoint, sizeof(dr2_psaddpoint), nullptr, &ps[3]));
        check(device->CreatePixelShader(dr2_psmask, sizeof(dr2_psmask), nullptr, &ps[4]));
        check(device->CreatePixelShader(dr2_psmaskpoint, sizeof(dr2_psmaskpoint), nullptr, &ps[5]));
        check(device->CreatePixelShader(dr2_psgrab, sizeof(dr2_psgrab), nullptr, &ps[6]));
        check(device->CreatePixelShader(dr2_psgrabpoint, sizeof(dr2_psgrabpoint), nullptr, &ps[7]));
        D3D11_INPUT_ELEMENT_DESC el[2] = {
            { "POSITION", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
            { "ITEM", 0, DXGI_FORMAT_R32_UINT, 1, 0, D3D11_INPUT_PER_INSTANCE_DATA, 1 } };
        check(device->CreateInputLayout(el, 2, dr2_vs, sizeof(dr2_vs), &layout));
        D3D11_BLEND_DESC bd{}; auto& rt = bd.RenderTarget[0]; rt.BlendEnable = TRUE; rt.BlendOp = rt.BlendOpAlpha = D3D11_BLEND_OP_ADD; rt.RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
        rt.SrcBlend = rt.SrcBlendAlpha = D3D11_BLEND_ONE; rt.DestBlend = rt.DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA; check(device->CreateBlendState(&bd, &blend[0]));
        // 더하기: Blend SrcAlpha One, ColorMask RGB
        rt.SrcBlend = rt.SrcBlendAlpha = D3D11_BLEND_SRC_ALPHA; rt.DestBlend = rt.DestBlendAlpha = D3D11_BLEND_ONE;
        rt.RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_RED | D3D11_COLOR_WRITE_ENABLE_GREEN | D3D11_COLOR_WRITE_ENABLE_BLUE; check(device->CreateBlendState(&bd, &blend[1]));
        // 마스크: 색을 쓰지 않는다
        rt.BlendEnable = FALSE; rt.RenderTargetWriteMask = 0; check(device->CreateBlendState(&bd, &blend[2]));
        // 깊이·스텐실: 0 끔, 1 마스크 들어감(+1), 2 마스크 나옴(-1), 3 스텐실 >= 1 일 때만, 4 스텐실 == 0 일 때만
        D3D11_DEPTH_STENCIL_DESC dd{}; dd.DepthEnable = FALSE; dd.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ZERO; dd.DepthFunc = D3D11_COMPARISON_ALWAYS; dd.StencilEnable = FALSE; check(device->CreateDepthStencilState(&dd, &depth[0]));
        dd.StencilEnable = TRUE; dd.StencilReadMask = 0xFF; dd.StencilWriteMask = 0xFF;
        auto face = [&dd](D3D11_COMPARISON_FUNC f, D3D11_STENCIL_OP pass) { D3D11_DEPTH_STENCILOP_DESC o{}; o.StencilFunc = f; o.StencilPassOp = pass; o.StencilFailOp = D3D11_STENCIL_OP_KEEP; o.StencilDepthFailOp = D3D11_STENCIL_OP_KEEP; dd.FrontFace = dd.BackFace = o; };
        face(D3D11_COMPARISON_ALWAYS, D3D11_STENCIL_OP_INCR_SAT); check(device->CreateDepthStencilState(&dd, &depth[1]));
        face(D3D11_COMPARISON_ALWAYS, D3D11_STENCIL_OP_DECR_SAT); check(device->CreateDepthStencilState(&dd, &depth[2]));
        dd.StencilWriteMask = 0;
        face(D3D11_COMPARISON_LESS_EQUAL, D3D11_STENCIL_OP_KEEP); check(device->CreateDepthStencilState(&dd, &depth[3]));   // 기준 1 <= 값
        face(D3D11_COMPARISON_GREATER, D3D11_STENCIL_OP_KEEP); check(device->CreateDepthStencilState(&dd, &depth[4]));      // 기준 1 > 값
        D3D11_RASTERIZER_DESC rd{}; rd.FillMode = D3D11_FILL_SOLID; rd.CullMode = D3D11_CULL_NONE; rd.DepthClipEnable = FALSE; check(device->CreateRasterizerState(&rd, &raster));
        // 도형 0 = 사각형 전체
        const float quad[8] = { 0,0, 1,0, 0,1, 1,1 }; const uint32_t qi[6] = { 0,2,1, 1,2,3 };
        geomVertices.assign(quad, quad + 8); geomIndices.assign(qi, qi + 6); geometries.push_back(Geometry{ 0, 6, 0 });
    }
    bool setTexture(int slot, ID3D11Texture2D* tex, int filter, int wrapU, int wrapV, int aniso) {
        if (slot < 0 || slot > 65535 || !tex) return false;
        ComPtr<ID3D11Device> owner; tex->GetDevice(&owner); if (owner != device) return false;
        D3D11_TEXTURE2D_DESC td{}; tex->GetDesc(&td); if (td.SampleDesc.Count != 1 || !(td.BindFlags & D3D11_BIND_SHADER_RESOURCE)) return false;
        Texture t; D3D11_SHADER_RESOURCE_VIEW_DESC sv{}; sv.Format = viewFormat(td.Format);
        if (filter & 256) switch (td.Format) {
            case DXGI_FORMAT_R8G8B8A8_TYPELESS: sv.Format = DXGI_FORMAT_R8G8B8A8_UNORM_SRGB; break;
            case DXGI_FORMAT_B8G8R8A8_TYPELESS: sv.Format = DXGI_FORMAT_B8G8R8A8_UNORM_SRGB; break;
            case DXGI_FORMAT_BC1_TYPELESS: sv.Format = DXGI_FORMAT_BC1_UNORM_SRGB; break;
            case DXGI_FORMAT_BC2_TYPELESS: sv.Format = DXGI_FORMAT_BC2_UNORM_SRGB; break;
            case DXGI_FORMAT_BC3_TYPELESS: sv.Format = DXGI_FORMAT_BC3_UNORM_SRGB; break;
            case DXGI_FORMAT_BC7_TYPELESS: sv.Format = DXGI_FORMAT_BC7_UNORM_SRGB; break;
            default: break;
        }
        filter &= 255;
        sv.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D; sv.Texture2D.MipLevels = td.MipLevels;
        if (FAILED(device->CreateShaderResourceView(tex, &sv, &t.view))) return false;
        D3D11_SAMPLER_DESC sd{}; sd.Filter = filter == 0 ? D3D11_FILTER_MIN_MAG_MIP_POINT : filter == 2 ? D3D11_FILTER_MIN_MAG_MIP_LINEAR : D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT;
        if (aniso > 1 && filter != 0) { sd.Filter = D3D11_FILTER_ANISOTROPIC; sd.MaxAnisotropy = (UINT)std::min(aniso, 16); } else sd.MaxAnisotropy = 1;
        auto wrap = [](int w) { return w == 0 ? D3D11_TEXTURE_ADDRESS_WRAP : w == 2 ? D3D11_TEXTURE_ADDRESS_MIRROR : w == 3 ? D3D11_TEXTURE_ADDRESS_MIRROR_ONCE : D3D11_TEXTURE_ADDRESS_CLAMP; };
        sd.AddressU = wrap(wrapU); sd.AddressV = wrap(wrapV); sd.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP; sd.MaxLOD = D3D11_FLOAT32_MAX; sd.ComparisonFunc = D3D11_COMPARISON_NEVER;
        if (FAILED(device->CreateSamplerState(&sd, &t.sampler))) return false;
        std::lock_guard<std::mutex> lock(mutex); if (textures.size() <= size_t(slot)) textures.resize(size_t(slot) + 1); textures[slot] = std::move(t); return true;
    }
    bool setGeometry(int id, const float* st, uint32_t vcount, const uint32_t* idx, uint32_t icount) {
        if (id <= 0 || id > 65535 || !st || !idx || vcount == 0 || icount == 0 || icount % 3 != 0) return false;
        for (uint32_t i = 0; i < icount; i++) if (idx[i] >= vcount) return false;
        std::lock_guard<std::mutex> lock(mutex);
        if (geometries.size() <= size_t(id)) geometries.resize(size_t(id) + 1);
        Geometry g; g.baseVertex = int32_t(geomVertices.size() / 2); g.indexStart = uint32_t(geomIndices.size()); g.indexCount = icount;
        geomVertices.insert(geomVertices.end(), st, st + size_t(vcount) * 2); geomIndices.insert(geomIndices.end(), idx, idx + icount);
        geometries[id] = g; geomDirty = true; return true;
    }
    bool setStatics(uint32_t first, uint32_t count, const float* data) {
        if (!data || first >= maxItems || count == 0 || count > maxItems - first) return false;
        std::lock_guard<std::mutex> lock(mutex);
        std::memcpy(staticPending.data() + size_t(first) * kStaticFloats, data, size_t(count) * kStaticFloats * 4);
        staticDirtyLo = std::min(staticDirtyLo, first); staticDirtyHi = std::max(staticDirtyHi, first + count); staticPendingAny = true; return true;
    }
    bool commit(const FrameIn& f) {
        double t0 = nowUs();
        std::lock_guard<std::mutex> lock(mutex);
        bool full = writeIndex - readIndex == ring.size();
        // 큐가 차면 가장 새 묶음에 이어 붙인다(렌더가 멈춘 동안에도 상태를 잃지 않게).
        Packet& p = ring[(full ? writeIndex - 1 : writeIndex) % ring.size()];
        if (!full) { p.dirtySlots.clear(); p.dirtyValues.clear(); p.hasList = false; }
        else dropped++;
        std::memcpy(p.vp, f.vp, sizeof p.vp); std::memcpy(p.cam, f.cam, sizeof p.cam); std::memcpy(p.ortho, f.ortho, sizeof p.ortho); p.clearStencil = f.clearStencil;
        if (f.dirtyCount && f.dirtySlots && f.channels) {
            size_t base = p.dirtySlots.size(); p.dirtySlots.resize(base + f.dirtyCount); p.dirtyValues.resize((base + f.dirtyCount) * kChannelFloats);
            for (uint32_t i = 0; i < f.dirtyCount; i++) {
                uint32_t slot = f.dirtySlots[i]; if (slot >= maxItems) { errors++; slot = 0; }
                p.dirtySlots[base + i] = slot; std::memcpy(p.dirtyValues.data() + (base + i) * kChannelFloats, f.channels + size_t(slot) * kChannelFloats, kChannelFloats * 4);
            }
        }
        if (f.listChanged && f.list && f.runs && f.segs) {
            p.hasList = true; p.list.assign(f.list, f.list + f.listCount); p.runs.assign(f.runs, f.runs + f.runCount); p.segs.assign(f.segs, f.segs + size_t(f.segCount) * 2);
        }
        if (!full) writeIndex++;
        commits++; commitUsSum += nowUs() - t0;
        return true;
    }
    void applyPacket(Packet& p, uint32_t& lo, uint32_t& hi) {
        std::memcpy(vp, p.vp, sizeof vp); std::memcpy(cam, p.cam, sizeof cam); std::memcpy(ortho, p.ortho, sizeof ortho); clearStencil = p.clearStencil != 0;
        for (size_t i = 0; i < p.dirtySlots.size(); i++) {
            uint32_t slot = p.dirtySlots[i]; std::memcpy(channelShadow.data() + size_t(slot) * kChannelFloats, p.dirtyValues.data() + i * kChannelFloats, kChannelFloats * 4);
            lo = std::min(lo, slot); hi = std::max(hi, slot + 1);
        }
        if (p.hasList) {
            uint32_t n = uint32_t(p.list.size());
            if (n > listCapacity || !listBuffer) {
                listCapacity = std::max<uint32_t>(1024, n + n / 2); D3D11_BUFFER_DESC d{}; d.ByteWidth = listCapacity * 4; d.Usage = D3D11_USAGE_DEFAULT; d.BindFlags = D3D11_BIND_VERTEX_BUFFER;
                listBuffer.Reset(); check(device->CreateBuffer(&d, nullptr, &listBuffer));
            }
            if (n) { D3D11_BOX box{ 0, 0, 0, n * 4, 1, 1 }; context->UpdateSubresource(listBuffer.Get(), 0, &box, p.list.data(), 0, 0); uploadedBytes += n * 4; }
            listCount = n; drawList = p.list; runs.swap(p.runs); segs.swap(p.segs);
        }
    }
    void prepareGrab(ID3D11Texture2D* source, DXGI_FORMAT format) {
        D3D11_TEXTURE2D_DESC td{}; source->GetDesc(&td);
        if (td.SampleDesc.Count != 1 || td.ArraySize != 1) throw E_INVALIDARG;
        if (grabTexture && grabDesc.Width == td.Width && grabDesc.Height == td.Height && grabDesc.Format == td.Format && grabFormat == format) return;
        grabView.Reset(); grabTexture.Reset();
        td.MipLevels = 1; td.ArraySize = 1; td.Usage = D3D11_USAGE_DEFAULT; td.BindFlags = D3D11_BIND_SHADER_RESOURCE; td.CPUAccessFlags = 0; td.MiscFlags = 0;
        check(device->CreateTexture2D(&td, nullptr, &grabTexture));
        D3D11_SHADER_RESOURCE_VIEW_DESC sv{}; sv.Format = format; sv.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D; sv.Texture2D.MipLevels = 1;
        check(device->CreateShaderResourceView(grabTexture.Get(), &sv, &grabView)); grabDesc = td; grabFormat = format;
    }
    // VS와 같은 식으로 사각형만 계산한다. 여백 2px로 GPU/CPU 마지막 자리 오차를 덮는다.
    D3D11_BOX grabBox(uint32_t slot, const D3D11_VIEWPORT& viewport) const {
        const float* c = channelShadow.data() + size_t(slot) * kChannelFloats;
        const float* s = staticShadow.data() + size_t(slot) * kStaticFloats; uint32_t f = uint32_t(s[18]);
        float wx, wy;
        if (f & 128u) {
            float x = c[0] + c[2], y = c[1] + c[3]; if (f & 256u) x *= ortho[2];
            x = (2.f * (x / 20.f + .5f) - 1.f) * ortho[0] * ortho[1]; y = (2.f * (y / 20.f + .5f) - 1.f) * ortho[0];
            float sn = std::sin(cam[2] * 0.01745329238474369f), cs = std::cos(cam[2] * 0.01745329238474369f);
            wx = cam[0] + (cs * x - sn * y); wy = cam[1] + (sn * x + cs * y);
        } else { wx = (cam[0] - c[0]) * c[12] + c[0]; wy = (cam[1] - c[1]) * c[13] + c[1]; }
        float mx = ((f & 1u) ? cam[3] : 1.f) * s[8], my = ((f & 1u) ? cam[3] : 1.f) * s[9];
        wx += c[14] * mx / s[14]; wy += c[15] * my / s[15];
        float half = (s[12] + c[4] + ((f & 2u) ? cam[2] : 0.f)) * 0.01745329238474369f * .5f;
        float qz = std::sin(half), qw = std::cos(half), norm = std::sqrt(qz * qz + qw * qw); qz /= norm; qw /= norm;
        float sn = qw * (qz * 2.f), cs = 1.f - qz * (qz * 2.f), sx = c[5] * mx, sy = c[6] * my;
        float p0x = cs * sx, p0y = sn * sx, p1x = -sn * sy, p1y = cs * sy;
        float q0x = p0x * s[0] + p1x * s[4], q0y = p0y * s[0] + p1y * s[4];
        float q1x = p0x * s[1] + p1x * s[5], q1y = p0y * s[1] + p1y * s[5];
        float ox = (c[2] + s[10]) / s[8] + s[2], oy = (c[3] + s[11]) / s[9] + s[6];
        float q3x = (p0x * ox + p1x * oy) + wx, q3y = (p0y * ox + p1y * oy) + wy;
        float loX = float(grabDesc.Width), loY = float(grabDesc.Height), hiX = 0, hiY = 0;
        for (unsigned k = 0; k < 4; k++) {
            float px = (q0x * float(k & 1u) + q1x * float(k >> 1)) + q3x, py = (q0y * float(k & 1u) + q1y * float(k >> 1)) + q3y;
            float cx = ((vp[0] * px + vp[4] * py) + vp[8] * s[13]) + vp[12], cy = ((vp[1] * px + vp[5] * py) + vp[9] * s[13]) + vp[13];
            float cw = ((vp[3] * px + vp[7] * py) + vp[11] * s[13]) + vp[15];
            float tx = viewport.TopLeftX + (cx / cw + 1.f) * .5f * viewport.Width, ty = viewport.TopLeftY + (1.f - cy / cw) * .5f * viewport.Height;
            if (!std::isfinite(tx) || !std::isfinite(ty)) return D3D11_BOX{ 0, 0, 0, grabDesc.Width, grabDesc.Height, 1 };
            loX = std::min(loX, tx); loY = std::min(loY, ty); hiX = std::max(hiX, tx); hiY = std::max(hiY, ty);
        }
        auto clamp = [](float v, UINT limit) { return UINT(std::max(0.f, std::min(float(limit), v))); };
        return D3D11_BOX{ clamp(std::floor(loX) - 2, grabDesc.Width), clamp(std::floor(loY) - 2, grabDesc.Height), 0, clamp(std::ceil(hiX) + 2, grabDesc.Width), clamp(std::ceil(hiY) + 2, grabDesc.Height), 1 };
    }
    // 렌더 스레드. 구간 0 이 프레임의 첫 이벤트: 큐에 쌓인 묶음을 모두 적용한다.
    void render(unsigned seg) {
        double t0 = nowUs();
        try {
            if (seg == 0) {
                uint32_t lo = UINT32_MAX, hi = 0; bool any = false;
                {
                    std::lock_guard<std::mutex> lock(mutex);
                    while (readIndex != writeIndex) { applyPacket(ring[readIndex % ring.size()], lo, hi); readIndex++; any = true; }
                    if (staticPendingAny) {
                        std::memcpy(staticShadow.data() + size_t(staticDirtyLo) * kStaticFloats, staticPending.data() + size_t(staticDirtyLo) * kStaticFloats, size_t(staticDirtyHi - staticDirtyLo) * kStaticFloats * 4);
                        D3D11_BOX box{ staticDirtyLo * kStaticFloats * 4, 0, 0, staticDirtyHi * kStaticFloats * 4, 1, 1 };
                        context->UpdateSubresource(staticBuffer.Get(), 0, &box, staticShadow.data() + size_t(staticDirtyLo) * kStaticFloats, 0, 0);
                        uploadedBytes += (staticDirtyHi - staticDirtyLo) * kStaticFloats * 4; staticPendingAny = false; staticDirtyLo = UINT32_MAX; staticDirtyHi = 0;
                    }
                    if (geomDirty) {
                        uint32_t vb = uint32_t(geomVertices.size() * 4), ib = uint32_t(geomIndices.size() * 4);
                        if (vb > vbCapacity || !vertexBuffer) { vbCapacity = std::max<uint32_t>(4096, vb + vb / 2); D3D11_BUFFER_DESC d{}; d.ByteWidth = vbCapacity; d.Usage = D3D11_USAGE_DEFAULT; d.BindFlags = D3D11_BIND_VERTEX_BUFFER; vertexBuffer.Reset(); check(device->CreateBuffer(&d, nullptr, &vertexBuffer)); }
                        if (ib > ibCapacity || !indexBuffer) { ibCapacity = std::max<uint32_t>(4096, ib + ib / 2); D3D11_BUFFER_DESC d{}; d.ByteWidth = ibCapacity; d.Usage = D3D11_USAGE_DEFAULT; d.BindFlags = D3D11_BIND_INDEX_BUFFER; indexBuffer.Reset(); check(device->CreateBuffer(&d, nullptr, &indexBuffer)); }
                        D3D11_BOX box{ 0, 0, 0, vb, 1, 1 }; context->UpdateSubresource(vertexBuffer.Get(), 0, &box, geomVertices.data(), 0, 0);
                        box.right = ib; context->UpdateSubresource(indexBuffer.Get(), 0, &box, geomIndices.data(), 0, 0); geomDirty = false;
                    }
                    texCopy = textures; geomCopy = geometries;
                }
                if (any) haveFrame = true;
                if (hi > lo) {
                    D3D11_BOX box{ lo * kChannelFloats * 4, 0, 0, hi * kChannelFloats * 4, 1, 1 };
                    context->UpdateSubresource(channelBuffer.Get(), 0, &box, channelShadow.data() + size_t(lo) * kChannelFloats, 0, 0); uploadedBytes += (hi - lo) * kChannelFloats * 4;
                }
                if (any) { float fb[24]; std::memcpy(fb, vp, 64); std::memcpy(fb + 16, cam, 16); std::memcpy(fb + 20, ortho, 16); context->UpdateSubresource(frameBuffer.Get(), 0, nullptr, fb, 0, 0); frames++; }
            }
            // 배경 구간이 비어도 앞 구간의 마스크가 쓰는 스텐실은 매 프레임 초기화한다.
            if (seg == 0 && haveFrame && clearStencil) {
                ComPtr<ID3D11DepthStencilView> stencil; context->OMGetRenderTargets(0, nullptr, &stencil);
                if (stencil) context->ClearDepthStencilView(stencil.Get(), D3D11_CLEAR_STENCIL, 1.0f, 0);
            }
            if (!haveFrame || listCount == 0 || size_t(seg) * 2 + 1 >= segs.size() || segs[size_t(seg) * 2 + 1] == 0 || !vertexBuffer || !indexBuffer || !listBuffer || geomCopy.empty()) { finish(t0); return; }
            uint32_t firstRun = segs[size_t(seg) * 2], runN = segs[size_t(seg) * 2 + 1];
            if (firstRun >= runs.size() || runN > runs.size() - firstRun) { errors++; finish(t0); return; }
            ComPtr<ID3D11RenderTargetView> rtv; ComPtr<ID3D11DepthStencilView> dsv; context->OMGetRenderTargets(1, &rtv, &dsv);
            if (!rtv) { errors++; finish(t0); return; }
            ComPtr<ID3D11Texture2D> source;
            for (uint32_t ri = firstRun; ri < firstRun + runN; ri++) if (runs[ri].flags >> 8) {
                ComPtr<ID3D11Resource> resource; rtv->GetResource(&resource); check(resource.As(&source));
                D3D11_RENDER_TARGET_VIEW_DESC vd{}; rtv->GetDesc(&vd);
                if (vd.ViewDimension != D3D11_RTV_DIMENSION_TEXTURE2D || vd.Texture2D.MipSlice != 0) throw E_INVALIDARG;
                prepareGrab(source.Get(), vd.Format); break;
            }
            UINT vpCount = 1; D3D11_VIEWPORT viewport{}; context->RSGetViewports(&vpCount, &viewport); if (vpCount == 0) { errors++; finish(t0); return; }
            auto* d = deferred.Get();
            ID3D11RenderTargetView* rt = rtv.Get(); d->OMSetRenderTargets(1, &rt, dsv.Get()); d->RSSetViewports(1, &viewport);
            d->RSSetState(raster.Get());
            d->IASetInputLayout(layout.Get()); d->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            ID3D11Buffer* vbs[2] = { vertexBuffer.Get(), listBuffer.Get() }; UINT strides[2] = { 8, 4 }, offsets[2] = { 0, 0 };
            d->IASetVertexBuffers(0, 2, vbs, strides, offsets); d->IASetIndexBuffer(indexBuffer.Get(), DXGI_FORMAT_R32_UINT, 0);
            d->VSSetShader(vs.Get(), nullptr, 0); ID3D11Buffer* cb = frameBuffer.Get(); d->VSSetConstantBuffers(0, 1, &cb);
            ID3D11ShaderResourceView* vsv[2] = { channelView.Get(), staticView.Get() }; d->VSSetShaderResources(0, 2, vsv);
            int lastPs = -1, lastBlend = -1, lastTexture = -1, lastDepth = -1; uint64_t drawCount = 0, instanceCount = 0;
            for (uint32_t ri = firstRun; ri < firstRun + runN; ri++) {
                const Run& r = runs[ri];
                if (r.count == 0 || r.start >= listCount || r.count > listCount - r.start) continue;
                if (r.texture < 0 || size_t(r.texture) >= texCopy.size() || !texCopy[r.texture].view) continue;
                const Geometry& g = (r.geometry > 0 && size_t(r.geometry) < geomCopy.size() && geomCopy[r.geometry].indexCount) ? geomCopy[r.geometry] : geomCopy[0];
                bool mask = (r.flags & 12u) != 0;
                bool grab = (r.flags >> 8) != 0;
                int wantPs = (mask ? 4 : grab ? 6 : (r.flags & 2u) ? 2 : 0) + ((r.flags & 1u) ? 1 : 0), wantBlend = mask ? 2 : (r.flags & 2u) ? 1 : 0;
                int wantDepth = (r.flags & 4u) ? 1 : (r.flags & 8u) ? 2 : (r.flags & 16u) ? 3 : (r.flags & 32u) ? 4 : 0;
                if (wantPs != lastPs) { d->PSSetShader(ps[wantPs].Get(), nullptr, 0); lastPs = wantPs; }
                if (wantBlend != lastBlend) { d->OMSetBlendState(blend[wantBlend].Get(), nullptr, ~0u); lastBlend = wantBlend; }
                if (wantDepth != lastDepth) { d->OMSetDepthStencilState(depth[wantDepth].Get(), 1); lastDepth = wantDepth; }
                if (r.texture != lastTexture) { ID3D11ShaderResourceView* v = texCopy[r.texture].view.Get(); ID3D11SamplerState* sm = texCopy[r.texture].sampler.Get(); d->PSSetShaderResources(0, 1, &v); d->PSSetSamplers(0, 1, &sm); lastTexture = r.texture; }
                if (grab) {
                    // 각 장식은 직전 장식까지 그려진 화면을 읽는다. 스냅샷을 여러 인스턴스가 공유하지 않는다.
                    for (uint32_t k = 0; k < r.count; k++) {
                        uint32_t slot = 0;
                        // 목록의 CPU 사본은 applyPacket에서 유지한다.
                        if (r.start + k >= drawList.size()) throw E_INVALIDARG;
                        slot = drawList[r.start + k]; if (slot >= maxItems) throw E_INVALIDARG;
                        D3D11_BOX box = grabBox(slot, viewport); if (box.right <= box.left || box.bottom <= box.top) continue;
                        ID3D11ShaderResourceView* nil = nullptr; d->PSSetShaderResources(1, 1, &nil); d->OMSetRenderTargets(0, nullptr, nullptr);
                        d->CopySubresourceRegion(grabTexture.Get(), 0, box.left, box.top, 0, source.Get(), 0, &box);
                        d->OMSetRenderTargets(1, &rt, dsv.Get()); ID3D11ShaderResourceView* bg = grabView.Get(); d->PSSetShaderResources(1, 1, &bg);
                        d->DrawIndexedInstanced(g.indexCount, 1, g.indexStart, g.baseVertex, r.start + k); drawCount++; instanceCount++;
                        grabCopies++; grabPixels += uint64_t(box.right - box.left) * (box.bottom - box.top);
                    }
                } else { d->DrawIndexedInstanced(g.indexCount, r.count, g.indexStart, g.baseVertex, r.start); drawCount++; instanceCount += r.count; }
            }
            ComPtr<ID3D11CommandList> commands; check(d->FinishCommandList(FALSE, &commands));
            context->ExecuteCommandList(commands.Get(), TRUE);   // Unity 의 즉시 컨텍스트 상태를 그대로 되돌린다
            draws += drawCount; instances += instanceCount;
        } catch (...) { errors++; try { deferred->ClearState(); ComPtr<ID3D11CommandList> discard; deferred->FinishCommandList(FALSE, &discard); } catch (...) {} }
        finish(t0);
    }
    void finish(double t0) { renderUsSum += nowUs() - t0; renderCalls++; }
};
std::mutex gate; std::shared_ptr<Renderer> current; int nextToken = 0;
std::shared_ptr<Renderer> get(int token) { std::lock_guard<std::mutex> lock(gate); return current && current->token == token ? current : nullptr; }
void __stdcall onEvent(int id) noexcept { try { auto r = get(id >> 12); if (r) r->render(unsigned(id & 4095)); } catch (...) {} }
}
DR2_API int sf_dr2_create(void* anyResource, unsigned maxItems) noexcept { try {
    if (!anyResource || maxItems == 0 || maxItems > 1000000) return 0;
    ComPtr<ID3D11Device> device; static_cast<ID3D11DeviceChild*>(anyResource)->GetDevice(&device); if (!device) return 0;
    auto r = std::make_shared<Renderer>(device.Get(), maxItems);
    std::lock_guard<std::mutex> lock(gate); r->token = ++nextToken; current = r; return r->token;
} catch (...) { return 0; } }
DR2_API void sf_dr2_destroy(int token) noexcept { try { std::lock_guard<std::mutex> lock(gate); if (current && current->token == token) current.reset(); } catch (...) {} }
DR2_API int sf_dr2_texture(int token, int slot, void* texture, int filter, int wrapU, int wrapV, int aniso) noexcept { try { auto r = get(token); return r && r->setTexture(slot, static_cast<ID3D11Texture2D*>(texture), filter, wrapU, wrapV, aniso) ? 1 : 0; } catch (...) { return 0; } }
DR2_API int sf_dr2_geometry(int token, int id, const float* st, unsigned vcount, const uint32_t* indices, unsigned icount) noexcept { try { auto r = get(token); return r && r->setGeometry(id, st, vcount, indices, icount) ? 1 : 0; } catch (...) { return 0; } }
DR2_API int sf_dr2_statics(int token, unsigned first, unsigned count, const float* data) noexcept { try { auto r = get(token); return r && r->setStatics(first, count, data) ? 1 : 0; } catch (...) { return 0; } }
DR2_API int sf_dr2_commit(int token, const FrameIn* frame) noexcept { try { auto r = get(token); return r && frame && r->commit(*frame) ? 1 : 0; } catch (...) { return 0; } }
DR2_API void* sf_dr2_event() noexcept { return reinterpret_cast<void*>(&onEvent); }
// 0 프레임, 1 그리기 호출 합, 2 인스턴스 합, 3 밀린 묶음, 4 오류, 5 올린 바이트 합, 6 렌더 이벤트 평균 us, 7 commit 평균 us, 8 렌더 이벤트 수
DR2_API double sf_dr2_stat(int token, int index) noexcept { try { auto r = get(token); if (!r) return -1; switch (index) {
    case 0: return double(r->frames.load()); case 1: return double(r->draws.load()); case 2: return double(r->instances.load()); case 3: return double(r->dropped.load());
    case 4: return double(r->errors.load()); case 5: return double(r->uploadedBytes.load());
    case 6: return r->renderCalls.load() ? r->renderUsSum / double(r->renderCalls.load()) : 0; case 7: return r->commits.load() ? r->commitUsSum / double(r->commits.load()) : 0;
    case 8: return double(r->renderCalls.load()); case 9: return double(r->grabCopies.load()); case 10: return double(r->grabPixels.load()); default: return -1; } } catch (...) { return -1; } }
