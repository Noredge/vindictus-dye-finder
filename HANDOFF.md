# Development handoff

## Current state

Preview 0.3.2, locally accepted by the owner on 2026-09-12. Initial public source destination: Noredge/vindictus-dye-finder, branch main, MIT. The initial public source push completed on 2026-09-12 (initial commit 7bbe571). Remote: https://github.com/Noredge/vindictus-dye-finder.git. Documentation updates may follow that initial commit; verify the actual remote HEAD when resuming. The owner has now authorized a downloadable 0.3.2 Release. Windows CI builds, tests and packages the application; verify its run and Release status before assuming publication has completed.

README is intentionally brief. TODO contains the next work. AGENTS contains implementation and data boundaries. Internal historical notes and local packages remain ignored rather than published.

## Behavior and architecture

The WPF app detects a random dye board and six original crosses, then searches screenshot pixels for up to three recommended positions. It compares sRGB-derived Lab colors with CIEDE2000. Recommendations are approximate; no learned correction is active. The 0.2 sampling/ranking baseline is retained.

Core contains color math, geometry, search and calibration records. App contains import, UI, local preferences and feedback ZIP export. Custom names are optional and backward-compatible with old preferences. No-match warnings clear when options change.

Feedback has three outcomes; exact RGB labels require confirmation at the original position. A nearby outcome cannot label the original spot. The real demo is deliberately cropped to board/crosses/RGB labels, and its feedback is flagged separately.

## Verification

The last local 0.3.2 run passed 128 checks with the owner's optional private screenshot set plus the WPF smoke test. The packaged ZIP was extracted and its executable passed smoke testing. The public checkout does not include those private screenshots; the default test command uses deterministic checks and the bundled demo. Re-run it on Windows before new changes are published.

Use scripts/Build.ps1 and scripts/Test.ps1 with a .NET 10 SDK. The verified local toolchain was SDK 10.0.400 and runtime 10.0.11. NuGet.Config disables network feeds; portable publishing supports -Online or an existing offline runtime cache. The app has no third-party application packages.

## Resume here

1. Check git status, remote and HEAD; read TODO.
2. Verify the Windows CI run and v0.3.2 Release assets; publication uses the exact downloaded CI ZIP after local smoke verification.
3. Collect real trial feedback, then make an isolated easier-alignment experiment. Do not change color math and ranking together.

Keep new UI text English. Preserve the visible tolerance slider, compact markers, six RGB summaries, custom names and local-only feedback flow.

Public-checkout verification on 2026-09-12: 36 deterministic checks passed with no private samples, plus the WPF smoke test using the bundled real demo. No application algorithm changes were made during repository cleanup.
