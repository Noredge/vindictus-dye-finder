# Agent guidance

## Product
- Windows C# / WPF, .NET 10. Keep the UI simple and English.
- Practical, easy-to-align recommendations matter more than exact RGB equality.
- Use only user-opened, dropped or pasted screenshots and the bundled demo. Never capture screens, inspect game processes/assets, inject, overlay, or send game input.
- Import detects geometry; search starts only with Find Matches. Image/rule changes invalidate old results. Preserve cancellation and stale-result protection.
- Keep Color Tolerance visible. Six numbered points share one zoom preview; changing the active point must not change recommendation coordinates.
- Display screenshot RGB as estimates, not guaranteed dye values or material appearance.

## Data and evaluation
- The 0.2 sampler/ranker is the practical baseline; 0.3.2 retains it. Change algorithms separately from UI work and compare on the same inputs.
- Feedback outcomes: Matched here, Found nearby, Not useful. Only user-confirmed original-position six RGB values can label that recommendation. Nearby results cannot.
- Optional result images must be cropped and reviewed. They remain unverified evidence. Demo records are not independent trials.
- Evaluate corrections on held-out panels/sessions; never fit target answers and call them predictive accuracy.

## Repository
- Read HANDOFF.md and TODO.md before starting work; verify Git status and remote state rather than assuming the handoff is current.
- src/DyeFinder.Core is UI-independent; src/DyeFinder.App is WPF; tests/DyeFinder.Tests contains checks.
- Build: scripts/Build.ps1. Test: scripts/Test.ps1, optionally -Samples <private-image-folder>. Publish: scripts/Publish.ps1 -RuntimePackages <offline-cache>.
- Keep private images, feedback, workspace paths, logs, build outputs and internal working notes out of public history. Only the intentional cropped demo is bundled.
- Preserve existing user changes. Update README, TODO and HANDOFF when behavior or project state changes.
- The initial public source push is authorized after local acceptance. Future pushes, tags and GitHub Releases require authorization for the task at hand. A source push does not imply publishing a downloadable Release.
