# Initial verification report — V1.0 preview

| Verification | Result | Evidence / limitation |
|---|---|---|
| Project and Solution counts | **PASS (static)** | 28 `.csproj`; Unified.sln 28; Foundation.sln 9 |
| Category distribution | **PASS (static)** | Foundation 8 / Mechanism 9 / Professional 6 / Applications 2 / Intelligence 1 / Tests 2 |
| ProjectRef targets resolve | **PASS (static)** | every referenced relative `.csproj` exists |
| Project dependency cycles | **PASS (static)** | no cycles in project dependency graph |
| Project TargetFrameworkVersion | **PASS (static)** | all v4.8 |
| `.csproj`/`.xaml` XML syntax | **PASS (static)** | all parsed as XML |
| Included `.cs`/resource files | **PASS (static)** | all paths exist |
| UI dependency isolation in Core/Visual | **PASS (source audit)** | no `using System.Windows*` in Foundation.Core / Mechanism.Core / Mechanism.Visual |
| Build with Windows MSBuild | **NOT RUN** | Linux environment has no .NET Framework 4.8 targeting pack / MSBuild |
| CLI smoke test executable | **NOT RUN** | scripts and source provided for Windows |
| WPF GUI startup/manual interaction | **NOT RUN** | Windows desktop/WPF unavailable |
| Professional domain functionality | **NOT IMPLEMENTED** | intentionally project skeletons only |

## Windows acceptance commands

Run in a VS Developer PowerShell / PowerShell console:

```powershell
.\Build\Verify-Structure.ps1
.\Build\BUILD.cmd
.\Build\BUILD.cmd -ShowGUI
```

The scripts stop on non-zero MSBuild or test executable exit codes; see `Output/Debug/x64/Build.log`. Neither this report nor static checks claim actual `MSBuild` compilation passed.

## Functional checks implemented in code

- FoundationSmoke: stable ID equality, numeric finite guard, rectangle contains/intersect, affine coordinates, category memory logging.
- ArchitectureSmoke: UI dependency isolation, tree ownership, multi-op rollback, revision conflicts, XML roundtrip, add/move/undo/redo/branching, invalid change rejection.
- CLI: demo prints locations, applies Add → Move → Undo → Redo, save/load XML.
- GUI: canvas selection & drag, toolbar Add / Move / Undo / Redo / Save / Load, view revision and status.
