# Solution grouping, ownership and dependencies

## F/M/P organization

Foundation stays UI/device-independent; Mechanism owns shared engineering capabilities; Professional projects are entry points for the six domain Workbenches. Professional cross-domain collaboration should use future public contracts, not direct ownership.

There are **22 F**, **40 M**, **73 P** subareas. The **17 F/M project sheets** correspond to 8+9 code projects, not 62 code projects. See `DOCS/ProjectCatalog.csv` for exact ID assignment.

### Critical sample layering

```text
Foundation.Core ──────────┐
        │                 │
Foundation.Geometry       ├─> Mechanism.Core ───> Mechanism.Visual
                          │                              │
Foundation.Infrastructure │                              v
        └─────────────────┼────────> Applications.Studio <- Mechanism.Presentation.Wpf
                          └────────> Applications.Runtime.Host
```

The diagram summarizes logical calls, not every formal project reference.

- Canonical engineering Definition owner: `CxVisual.Mechanism.Core` (`CxProjectDocument`, `CxNode` and six concrete node kinds).
- Data mutation / optimistic revision / rollback: `CxVisual.Mechanism.Core`.
- Visual command and undo/redo history: `CxVisual.Mechanism.Visual`; it **does not** depend on WPF or WinForms.
- Mouse, visual selection hit UX, drag preview, 2D `DrawingContext`: `CxVisual.Mechanism.Presentation.Wpf`, no second persisted engineering tree.
- Windows shell/VM: `CxVisual.Studio`; CLI: `CxVisual.Runtime.Host`.
- `M18–M25` Orchestration belongs in dedicated project and is **not** substituted by drawing simple lines in the sample.

## Engineering sample transaction chain

```text
WPF pointer move → CxMoveEventArgs → StudioViewModel
  → CxVisualEditor.Move → CxEngineeringSession.Apply
  → CxProjectDocument.Commit(expectedRevision, changes)
  → mutation or atomic rollback → revision update
  → CxVisualEditor history → CxSceneProjection → WPF OnRender
```

For undo/redo the Visual history replays Core inverse/forward batches. No `Visual` code directly modifies a `CxElement`'s set-private coordinates.

## Known simplifications

This sample intentionally omits complete reusable Symbol library/reference resolution, all project resource types, professional Provider/Driver, async orchestrators, version migration beyond schema=1, industrial safety, live diagnostics, and high-volume stream handles. Do not treat absence in this small demonstrator as absence from the full planned specification.
