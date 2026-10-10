// dupcap: 화면에 실제로 나간 그림이 새 그림인지 본다 (DXGI Desktop Duplication).
//   dupcap.exe <초> <출력 csv> [가운데 가로 세로 = 1280 720] [출력 번호 = 자동(3440x1440)]
// 합성기(DWM)가 새 화면을 만들 때마다(= 게임이 제출한 그림이 화면에 쓰일 때마다) 가운데 영역을 읽어
// 바로 앞 그림과 비교해 바뀐 픽셀 비율·평균 차이를 적는다. PresentMon 은 "표시된 횟수"만 알려 주고 그림이 같은지는 모른다.
// 성능 시험이 아니다(읽기 복사가 GPU 를 쓴다). 체감 문제의 "새 그림 비율"만 본다.
#include <d3d11.h>
#include <dxgi1_2.h>
#include <wrl/client.h>
#include <windows.h>
#include <cstdio>
#include <cstdlib>
#include <cstdint>
#include <vector>
#include <cmath>
using Microsoft::WRL::ComPtr;

int main(int argc, char** argv) {
    if (argc < 3) { std::fprintf(stderr, "dupcap <초> <csv> [w h] [output]\n"); return 2; }
    double seconds = std::atof(argv[1]);
    int cw = argc > 4 ? std::atoi(argv[3]) : 1280, ch = argc > 4 ? std::atoi(argv[4]) : 720;
    int wantOut = argc > 5 ? std::atoi(argv[5]) : -1;
    int dumpN = argc > 6 ? std::atoi(argv[6]) : 0; const char* dumpDir = argc > 7 ? argv[7] : ".";
    ComPtr<IDXGIFactory1> factory; CreateDXGIFactory1(IID_PPV_ARGS(&factory));
    ComPtr<IDXGIAdapter1> adapter; ComPtr<IDXGIOutput> output; int idx = 0;
    for (UINT a = 0; factory->EnumAdapters1(a, &adapter) == S_OK; ++a) {
        ComPtr<IDXGIOutput> o;
        for (UINT i = 0; adapter->EnumOutputs(i, &o) == S_OK; ++i, ++idx) {
            DXGI_OUTPUT_DESC d; o->GetDesc(&d);
            int w = d.DesktopCoordinates.right - d.DesktopCoordinates.left;
            std::fprintf(stderr, "output %d: %dx%d\n", idx, w, d.DesktopCoordinates.bottom - d.DesktopCoordinates.top);
            if ((wantOut < 0 && w == 3440) || wantOut == idx) { output = o; goto found; }
            o.Reset();
        }
        adapter.Reset();
    }
found:
    if (!output) { std::fprintf(stderr, "출력을 못 찾음\n"); return 3; }
    DXGI_OUTPUT_DESC od; output->GetDesc(&od);
    int W = od.DesktopCoordinates.right - od.DesktopCoordinates.left, H = od.DesktopCoordinates.bottom - od.DesktopCoordinates.top;
    ComPtr<ID3D11Device> dev; ComPtr<ID3D11DeviceContext> ctx;
    D3D_FEATURE_LEVEL fl;
    if (FAILED(D3D11CreateDevice(adapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &dev, &fl, &ctx))) { std::fprintf(stderr, "device\n"); return 4; }
    ComPtr<IDXGIOutput1> o1; output.As(&o1);
    ComPtr<IDXGIOutputDuplication> dup;
    if (FAILED(o1->DuplicateOutput(dev.Get(), &dup))) { std::fprintf(stderr, "DuplicateOutput 실패\n"); return 5; }
    if (cw > W) cw = W; if (ch > H) ch = H;
    int x0 = (W - cw) / 2, y0 = (H - ch) / 2;
    D3D11_TEXTURE2D_DESC sd{}; sd.Width = cw; sd.Height = ch; sd.MipLevels = 1; sd.ArraySize = 1; sd.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    sd.SampleDesc.Count = 1; sd.Usage = D3D11_USAGE_STAGING; sd.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Texture2D> staging; dev->CreateTexture2D(&sd, nullptr, &staging);
    LARGE_INTEGER freq; QueryPerformanceFrequency(&freq);
    FILE* f = std::fopen(argv[2], "w");
    std::fprintf(f, "qpc_s,accumulated,changed_pct,mad,hash,mad_inner\n");
    std::vector<uint8_t> prev((size_t)cw * ch * 4), cur((size_t)cw * ch * 4);
    bool have = false;
    LARGE_INTEGER t0; QueryPerformanceCounter(&t0);
    long long updates = 0, frozen = 0; int dumped = 0;
    while (true) {
        LARGE_INTEGER now; QueryPerformanceCounter(&now);
        if ((now.QuadPart - t0.QuadPart) / double(freq.QuadPart) > seconds) break;
        DXGI_OUTDUPL_FRAME_INFO info{}; ComPtr<IDXGIResource> res;
        HRESULT hr = dup->AcquireNextFrame(50, &info, &res);
        if (hr == DXGI_ERROR_WAIT_TIMEOUT) continue;
        if (FAILED(hr)) { std::fprintf(stderr, "Acquire 0x%08x\n", (unsigned)hr); break; }
        if (info.LastPresentTime.QuadPart != 0) {
            ComPtr<ID3D11Texture2D> tex; res.As(&tex);
            D3D11_BOX box{ (UINT)x0, (UINT)y0, 0, (UINT)(x0 + cw), (UINT)(y0 + ch), 1 };
            ctx->CopySubresourceRegion(staging.Get(), 0, 0, 0, 0, tex.Get(), 0, &box);
            D3D11_MAPPED_SUBRESOURCE m{};
            if (SUCCEEDED(ctx->Map(staging.Get(), 0, D3D11_MAP_READ, 0, &m))) {
                for (int y = 0; y < ch; ++y) std::memcpy(&cur[(size_t)y * cw * 4], (uint8_t*)m.pData + (size_t)y * m.RowPitch, (size_t)cw * 4);
                ctx->Unmap(staging.Get(), 0);
                double changed = 0, mad = 0, madInner = 0; uint64_t h = 1469598103934665603ull;
                if (have) {
                    long long n = 0, c = 0, sum = 0, ni = 0, si = 0;
                    int ix0 = cw / 2 - 200, ix1 = cw / 2 + 200, iy0 = ch / 2 - 200, iy1 = ch / 2 + 200;   // 가운데 400x400: 공이 도는 자리
                    for (int y = 0; y < ch; y += 2) {
                        for (int x = 0; x < cw; x += 4) {
                            size_t i = ((size_t)y * cw + x) * 4;
                            int d = std::abs((int)cur[i] - prev[i]) + std::abs((int)cur[i + 1] - prev[i + 1]) + std::abs((int)cur[i + 2] - prev[i + 2]);
                            sum += d; if (d > 12) ++c; ++n;
                            if (x >= ix0 && x < ix1 && y >= iy0 && y < iy1) { si += d; ++ni; }
                        }
                    }
                    changed = 100.0 * c / n; mad = double(sum) / n / 3.0; madInner = ni ? double(si) / ni / 3.0 : 0;
                }
                for (size_t i = 0; i < cur.size(); i += 64) { h ^= cur[i]; h *= 1099511628211ull; }
                std::fprintf(f, "%.6f,%u,%.4f,%.4f,%llx,%.4f\n", info.LastPresentTime.QuadPart / double(freq.QuadPart), info.AccumulatedFrames, changed, mad, (unsigned long long)h, madInner);
                if (have && mad == 0) ++frozen;
                if (dumpN > 0 && dumped < dumpN && (now.QuadPart - t0.QuadPart) / double(freq.QuadPart) > 3.0) {
                    char name[512]; std::snprintf(name, sizeof name, "%s/d%03d_%.6f.bgra", dumpDir, dumped, info.LastPresentTime.QuadPart / double(freq.QuadPart));
                    FILE* g = std::fopen(name, "wb"); if (g) { std::fwrite(cur.data(), 1, cur.size(), g); std::fclose(g); } ++dumped;
                }
                ++updates; prev.swap(cur); have = true;
            }
        }
        dup->ReleaseFrame();
    }
    std::fclose(f);
    std::printf("updates=%lld frozen=%lld\n", updates, frozen);
    return 0;
}
