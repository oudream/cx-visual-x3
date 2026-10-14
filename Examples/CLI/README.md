# CLI sample — built executable

From a **Windows x64 developer environment** with .NET Framework 4.8 targeting pack:

```powershell
.\Build\BUILD.cmd
.\Output\Debug\x64\CxVisual.Runtime.Host.exe demo
.\Output\Debug\x64\CxVisual.Runtime.Host.exe save .\Output\Debug\x64\Data\demo.xml
.\Output\Debug\x64\CxVisual.Runtime.Host.exe load .\Output\Debug\x64\Data\demo.xml
```

Expected: lists two initial Elements, adds a third ROI, moves/undoes/redoes it, prints final Revision; save/load round trips Project/Page/Content/Symbol/Group/Element.
