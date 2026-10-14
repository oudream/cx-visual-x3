# V1.0 engineering conventions — confirmed-vs-unverified matrix

> **Reference limitation:** full-text binary for 《CxVisual 统一工程、项目、代码与资源规范 V1.0》 was not present in the available Project/Library and Drive search. Entries below are **user-approved conclusions preserved from the earlier design discussion**, not direct quotations from the absent original.

| Previous approved contract | Implementation | Current evidence |
|---|---|---|
| 28 projects: F8/M9/P6/App2/Intelligence1/Tests2 | Full | `DOCS/ProjectCatalog.csv`, 28 `.csproj`, `CxVisual.Unified.sln` |
| Classical C# project format, ToolsVersion 15.0 and .NET Framework 4.8 | Full source specification | All `.csproj`; Windows MSBuild validation **pending** |
| Single Windows x64 Debug/Release `Output` directory | Defined | `Build/CxVisual.Build.props`, build/test scripts |
| WPF-only GUI / MVVM, no WinForms | Implemented sample | `Applications/Studio`, WPF SceneView + ViewModel |
| CLI and WPF runnable application samples | Sources provided | Runtime.Host / Studio; native Windows execution **pending** |
| Project code follows C# guidelines, static helpers when stateless | Demo scope | C# 7.3, PascalCase / camelCase / `_fields`, 4 spaces; one principal public type/file |
| APP config separate from business JSON | Demo scope | App.config + Resources/Settings/studio.json |
| Themes, Styles, Assets, Localization structure | Demo scope | Studio folders, compiled WPF ResourceDictionaries |
| DLL contains actual native/runtime binaries, not NuGet | Defined | DLL/x64, Build/Stage-RuntimeDlls.ps1 |
| Cases are test-case fixtures | Defined | `Examples/Cases/core-undo-redo.json` |
| App-V1.1/V1.2/V2.3 preserved for rollback | Folder reservations | `Legacy/App-*`; **original historical bytes unavailable** |
| V7.8 old sources preserved separately | Archive included | `Legacy/V7.8/CxVisual-V7.8.zip` |
| Traceable categorized logging and independent testing | Demo scope | Foundation.Infrastructure log sinks, 2 standalone test executables |
| Foundation carries pure reusable data/math; Mechanism owns project semantics | Demonstrated | Mechanism.Core canonical tree and transaction, Foundation geometry+identity |

No claim is made that the complete 135 subareas, 1080 candidate objects, or all 349 historical contracts have been implemented. No claim that full V1.0 content has been line-by-line checked.
