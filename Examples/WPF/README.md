# WPF interactive GUI — built executable

```powershell
.\Build\BUILD.cmd -ShowGUI
# Or after build:
.\Output\Debug\x64\CxVisual.Studio.exe
```

Manual GUI acceptance:
1. Startup draws Detector and Source rectangles on a grid with Project/Revision status.
2. Click a rectangle to select; drag it to another position, **Revision increases**.
3. Add rectangle, Undo and Redo, compare canvas and numeric Revision.
4. Save XML and load XML, verify the two initial objects + additions persist.
5. WPF view owns only mouse events and DrawingContext; project model/transaction/Undo reside in Mechanism.Core/Visual. UI uses MVVM for commands/state, code-behind wires only input events.

This is **interactive manual smoke acceptance**, not proof an automated Windows UI test has executed. Current environment cannot start WPF.
