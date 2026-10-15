# Configuration owner consolidation (2026-10-15)

- **Physical owner:** `Foundation/Data/Configuration/` for F15 primitives, `Mechanism/Data/Configuration/` for runtime configuration managers, layers, deployment, and session logic.
- **No new project**; historical `MPM/MTC/MVD/MRK` public type names and namespace declarations were retained so existing callers require no source rename. Their defining assembly changes to `CxVisual.Mechanism.Data` and callers have an explicit project reference. This is a **binary compatibility break** for consumers compiled against the old assembly: rebuild all dependent assemblies.
- Old config implementation files in `Mechanism/Core`, `Mechanism/Runtime`, `Mechanism/Lifecycle`, and `Foundation/Data` were removed after moving their exact contents. The WPF editor remains a presentation-layer adapter and reuses the consolidated service; it is **not a second configuration store**.
- `ConfigConsolidationTests` enforces assembly ownership and smoke-tests save/reload, validation and notification; all existing L1/L2-L4/Professional/WPF tests remain enabled.
- This migration itself does **not** establish V1.4 feature completion. Order-preserving JSON, comprehensive validators, arbitrary historical version queries, and multi-scope source resolution need their own implementation and regression evidence.
- Windows verification: `msbuild CxVisual.Unified.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64`; then `Output/Release/x64/CxVisual.Tests.ArchitectureSmoke.exe` and `--config-wpf`.
- No .NET Framework compiler was available in the authoring environment; CI must confirm build and runtime test results before merging.
