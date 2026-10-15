# Config V1.4 real replacement — 2026-10-15

This branch replaces the prototype configuration implementation. **Source of truth:** previous V1.4 Code Patch; two projects only for configuration backend:
- `Foundation/Data/Configuration`: FCM schema, attributes, validation, JSON codec, ordered document editor and paths.
- `Mechanism/Data/Configuration`: MHS service/sections/versions/transactions, MVB scope selector, MNM notifications.

Deleted the previous `FCMConfigJsonFile/FCMConfigKey`, `MPMConfigDescription/MPMConfigLayeredProfile/MTCConfigManager`, `MVDConfigRelease`, `MRKConfigRuntimeSession` implementations and obsolete config L1/L2/L4 test suites. The WPF visual adapter is distinct from the backend and now invokes `MHSConfigSection<T>`. Existing Vision pixel algorithm is preserved, but its runtime obtains versioned, task-pinned MHS config. Automation, Vision, CT3D and Studio sample models now declare FCM metadata.

**Breaking change:** old configuration namespaces/classes are intentionally removed. These are prototype APIs; callers outside the repository need migration to MHS/FCM. The legacy release/stage/activate CLI commands were removed rather than falsely claiming L4 support; Studio publishing/rollback actions give explicit not-supported messages.

**Tests:** `Tests/ArchitectureSmoke/ConfigurationIntegrationTests.cs` is the V1.4 golden integration suite. Windows CI builds the full solution, runs ArchitectureSmoke, real CPU Vision demo, save/load. The V1.4 WPF dialog is a new UI adapter and still requires manual STA/GUI acceptance; hardware application, distribution, YAML/INI and cross-process notification remain unimplemented.

**What is not claimed:** build/test/physical device success without Windows CI. No vendor device safety/ACK is supplied by the V1.4 software-only engine. Pending version switching and industrial device actuation must be integrated with professional runtime safety gates.
