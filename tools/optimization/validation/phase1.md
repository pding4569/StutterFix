# Phase 1 verification

Target: installed Unity 6000.3.21f1 alpha / Windows x64, 2026-10-16.
Baseline source: 2a267f3 (2.7.3). New candidates default false.

| Step | Developer Release errors | Player Release errors | Other evidence |
|---|---:|---:|---|
| 01 | 0 | 0 | 55 policy checks |
| 02 | 0 | 0 | Existing preflight; source change 0 |
| 03 | 0 | 0 | Existing final unreadable texture; source change 0 |
| 04 | 0 | 0 | 20017 parser checks; allocation bytes 7107400 -> 4611752 |
| 05 | 0 | 0 | 22 cache checks; cold/warm live functional runs |
| 06 | 0 | 0 | 9 scope checks; live 16/64 -> 2/4 |

Final LoadCheck: Developer 223 named field references / mismatch 0; Player 222 / mismatch 0.
Dynamic-name field references: 4 per edition; static scanner does not validate those.

Restored installation SHA256 (3/3):
- DLL: 615dd5cd527b7f70de0e7836d7816641b4175219efba2066a6ba14d4c8381f50
- native: 1d595ec64a5d1a6d0447c3837cfb1d709e34bc92bcb48d86713d7b86517b033
- Settings: 0aa88c8fd8b722252213d2d26592416a10ba16173c0a1341980dd3de4c837999

Performance A/B, all-pixel parity, general OOM prevention and old-engine compatibility: not measured.
Raw game logs / installed originals remain private under ignored out/.
Initial 06 load test completed apply/restore and normal exit; its harness expected numeric log fields missing at that time and failed. Final menu API check records actual numeric values.
