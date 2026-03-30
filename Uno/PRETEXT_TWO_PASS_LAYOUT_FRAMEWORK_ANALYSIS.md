# Pretext-Style Two-Pass Layout Framework Analysis

Status: working design analysis  
Audience: framework contributors, control authors, application architects  
Scope: how the prepare-and-cache ideas used by Pretext can be generalized into a reusable two-pass layout architecture, then integrated cleanly into Avalonia and Uno for custom panels, custom controls, responsive shells, and virtualized UI

This document extends:

- `Uno/PRETEXT_TECHNICAL_SPEC.md`
- `Uno/PRETEXT_UNO_UI_SYSTEM_SPEC.md`

Those documents describe the current Pretext engine and the current Uno sample integration. This document is broader. It asks:

can the same parsing, caching, and staged solving strategy be lifted out of text layout and turned into a general framework for UI layout?

The answer is yes, but only if the system is treated as a framework architecture rather than a one-off panel optimization.

## 1. Executive Summary

Pretext is valuable because it separates two kinds of work:

1. semantic preparation
2. cheap repeated geometric solving

In the text engine, `prepare()` does the expensive semantic work once:

- normalization
- segmentation
- script-aware preprocessing
- measurement caching
- chunk metadata creation

Then `layout()` and `layoutNextLine()` repeatedly solve geometry without redoing that semantic work.

The same pattern applies to UI layout:

1. parse UI inputs into stable prepared nodes
2. cache the prepared representation behind a layout fingerprint
3. solve geometry for width, height, viewport, and state
4. realize or arrange live controls from solved geometry

The important shift is this:

the framework should not discover layout by repeatedly asking live controls what they want when the meaning of the layout has not changed.

Instead, it should:

- prepare once when semantic inputs change
- solve many times when only geometric constraints change

That is the core of a Pretext-style two-pass UI architecture.

## 2. What Pretext Is Really Teaching

Pretext is not only a text layout library. It is a concrete proof that a UI engine can be fast when it obeys four rules:

### 2.1 Separate semantic and geometric work

Semantic work includes:

- parsing content
- grouping tokens
- building derived metadata
- measuring intrinsic units
- classifying break opportunities

Geometric work includes:

- choosing widths
- selecting rows or columns
- assigning rectangles
- computing extents
- deriving visible ranges

### 2.2 Cache prepared state behind stable fingerprints

If text, font, or break policy does not change, the prepared model should survive many width changes.

The same applies to UI:

- if item content does not change, the prepared item model should survive resize
- if control chrome does not change, the prepared control structure should survive state toggles
- if a responsive shell only changes column widths, the app should not rebuild all content semantics

### 2.3 Keep the solve phase arithmetic-first

Pretext hot paths avoid:

- UI-tree reads
- fresh measurement
- string reconstruction
- avoidable allocations

A generalized layout solver should do the same:

- work from prepared metrics and constraints
- avoid inspecting live children during hot resize or scroll
- emit geometry, not ask the tree what geometry should be

### 2.4 Treat realization as a separate concern

Pretext can answer geometry without materializing line text for every consumer.

Likewise, a UI system should be able to:

- compute rectangles for 100k items
- realize only the visible slice
- keep total extent independent from realized element count

## 3. Core Framework Model

This section is framework-neutral. It is the reusable architecture that Avalonia and Uno should both map onto.

### 3.1 Canonical shape

The generalized abstraction is:

`PreparedLayoutModel + Solve(LayoutConstraints) -> SolvedLayout`

For text today:

- `PreparedText`
- `Layout(width, lineHeight)`

For UI:

- `PreparedPanelModel`
- `PreparedControlModel`
- `PreparedItemsModel`
- `Solve(width, height, viewport, state, density)`

### 3.2 Phase model

The architecture should be described as four phases.

#### Phase 0: fingerprint inputs

Build a stable fingerprint from layout-affecting inputs.

Examples:

- text and inline content
- font, typography, and density
- child role definitions
- padding, spacing, borders, separators
- item count and per-item content hashes
- responsive mode or layout preset
- visibility and collapsed state

This phase decides whether the prepared model can be reused.

#### Phase 1: prepare semantics

Convert raw input into a stable prepared model.

Examples:

- parse content into role nodes
- prepare text handles
- compute intrinsic metrics for icons, chips, badges, and fixed chrome
- classify children into zones such as header, sidebar, body, footer, overlay
- build obstacle maps or slot groups
- build item metadata arrays for virtualization

This is where the expensive structural work belongs.

#### Phase 2: solve geometry

Given current constraints:

- available width
- available height
- viewport
- responsive state
- interaction state

compute:

- desired size
- child rectangles
- item offsets
- extents
- occlusion bands
- realization range

This should be mostly arithmetic and index walking.

#### Phase 3: realize and arrange

Consume solved geometry:

- arrange existing children
- realize the visible slice
- recycle non-visible elements
- update retained visuals
- preserve framework-native input, focus, automation, and accessibility

This phase should not redo the solve.

## 4. Data Structures Needed for a Real Two-Pass UI System

The text engine already has a stable `PreparedText` handle. A general UI system needs equivalent prepared models.

### 4.1 Prepared panel model

A panel-level model should contain:

- child role table
- prepared text handles for text-bearing regions
- intrinsic metrics for fixed-content regions
- group metadata for repeated item classes
- optional viewport index structures

Conceptually:

```csharp
public sealed record PreparedPanelModel(
    LayoutFingerprint Fingerprint,
    IReadOnlyList<PreparedNode> Nodes,
    PanelChromeMetrics Chrome,
    object? DerivedState);
```

### 4.2 Prepared control model

A control-level model should describe control structure, not live elements.

Examples:

- template slots
- optional text regions
- icon and accessory metrics
- state-specific chrome variants
- alignment rules

Conceptually:

```csharp
public sealed record PreparedControlModel(
    LayoutFingerprint Fingerprint,
    ControlSlotMap Slots,
    IReadOnlyList<PreparedTextHandle> TextRegions,
    ControlChromeMetrics Chrome,
    object? DerivedState);
```

### 4.3 Prepared items model

For virtualization, the item model should be array-first and cheap to scan.

Examples:

- per-item intrinsic width and height
- classification or template kind
- per-item prepared text handle
- estimated and solved offsets
- stable keys for recycling

Conceptually:

```csharp
public sealed record PreparedItemsModel(
    LayoutFingerprint Fingerprint,
    int Count,
    ReadOnlyMemory<ItemKind> Kinds,
    ReadOnlyMemory<float> EstimatedWidths,
    ReadOnlyMemory<float> EstimatedHeights,
    ReadOnlyMemory<PreparedTextHandle> TextHandles);
```

This is where Pretext’s current lessons matter most:

- keep hot-path data flat
- avoid object-heavy per-item structures
- reserve object graphs for the semantic layer, not the scroll hot path

## 5. Invalidation Model

The system is only useful if invalidation is disciplined.

### 5.1 Semantic invalidation

Rebuild the prepared model when inputs such as these change:

- actual text or item content
- font family, size, style, weight
- template structure
- role classification
- visibility or collapse of structural regions
- item count or item identity

### 5.2 Geometric invalidation

Reuse the prepared model and only rerun solve when these change:

- available width or height
- viewport
- spacing and gap values
- container padding
- responsive state selection
- scroll position

### 5.3 Visual invalidation

Do not rebuild semantics or solve geometry when only paint changes:

- foreground or background brushes
- hover visuals
- selection colors
- animation-only effects that do not affect desired size

The core framework should explicitly separate those invalidation classes.

## 6. Caching Strategy

Pretext succeeds because the preparation cache is not an incidental optimization. It is the architecture.

The generalized UI version needs the same attitude.

### 6.1 Cache keys

Prepared models should be keyed by semantic fingerprints.

Solved layouts should be keyed by:

- prepared fingerprint
- width bucket or exact width
- height bucket if relevant
- responsive state
- viewport bucket for virtualized scenarios

### 6.2 Width buckets

For text-heavy UI, exact-width caches can become too fine-grained.

A practical pattern is:

- exact cache for active width
- bounded LRU of recent widths
- optional width buckets for coarse responsive bands

Examples:

- mobile narrow
- tablet medium
- desktop wide

### 6.3 Viewport caches

Virtualized surfaces should not cache realized controls as the primary model.

Instead cache:

- solved extents
- cumulative offsets
- occlusion bands
- visible range indices

and treat realized UI elements as a thin reusable layer on top.

## 7. Responsive Design in a Pretext-Style System

The system should not confuse responsiveness with templating.

Responsive behavior can be expressed as:

1. choose mode
2. choose constraints
3. solve geometry inside that mode

The mode switch can depend on:

- width thresholds
- height thresholds
- aspect ratio
- density or input modality
- app state

But once the mode is selected, geometry should still be solved from prepared models rather than hardcoded by imperative tree mutations.

Examples:

- a mail app can switch from three-pane to two-pane to single-pane
- a note editor can collapse sidebars but keep prepared note content
- a dashboard can promote or demote tiles into new slot groups

## 8. Core Framework Responsibilities

Before talking about Avalonia or Uno, it helps to define what a shared core framework would own.

### 8.1 What belongs in the shared core

- fingerprints
- prepared model builders
- solve algorithms
- array-based metric tables
- viewport and occlusion indices
- layout result objects
- responsive mode selection policy
- diagnostic and profiling hooks

### 8.2 What does not belong in the shared core

- framework controls
- dependency properties
- direct `UIElement` access
- dispatcher subscriptions
- template loading
- accessibility peer creation
- renderer-specific drawing

The core should look much more like Pretext itself and much less like a control library.

## 9. Avalonia Integration

Avalonia already has the lifecycle shape required for this architecture.

### 9.1 Relevant framework evidence

The critical sources are:

- `Avalonia.Base/Layout/Layoutable.cs`
- `Avalonia.Base/Layout/LayoutManager.cs`
- `Avalonia.Controls/Panel.cs`
- `Avalonia.Controls/VirtualizingPanel.cs`
- `Avalonia.Controls/VirtualizingStackPanel.cs`
- `Avalonia.Controls/TextBlock.cs`

These files show the exact integration seams:

- `Layoutable` tracks desired size, previous constraints, measure and arrange validity, and exposes `InvalidateMeasure()` / `InvalidateArrange()`
- `LayoutManager` queues separate measure and arrange work and raises `EffectiveViewportChanged`
- `VirtualizingPanel` explicitly tells custom panels to drive realization through item generation and viewport awareness
- `VirtualizingStackPanel` already uses `EffectiveViewportChanged`, cache length, realized element tracking, and recycling
- `TextBlock` keeps a cached `_textLayout`, which is a direct precedent for prepared-model reuse inside a control

### 9.2 Avalonia mapping

The clean mapping is:

- `Prepared*Model` lives outside the visual tree
- `MeasureOverride` ensures the prepared model exists and solves for current size
- `ArrangeOverride` applies solved rectangles and avoids semantic rebuilds
- `EffectiveViewportChanged` updates viewport-driven solve state for virtualized surfaces
- `VirtualizingPanel` owns realization and recycling

### 9.3 Avalonia panel pattern

For a custom panel:

1. fingerprint children and panel-affecting properties
2. rebuild `PreparedPanelModel` only when semantic inputs change
3. in `MeasureOverride`, solve geometry for `availableSize`
4. return desired size from solved extent
5. in `ArrangeOverride`, arrange children using cached solved rectangles

This works well for:

- masonry
- wrap panels with non-uniform items
- dense dashboard tiles
- editorial surfaces
- geometry-aware forms

### 9.4 Avalonia virtualized pattern

For an item surface:

1. prepare item metrics into flat arrays
2. maintain cumulative offsets or column assignment arrays
3. build an occlusion index for quick viewport lookup
4. use `EffectiveViewportChanged` to update only the visible range
5. realize containers through `VirtualizingPanel`
6. recycle controls aggressively

The current `VirtualizingStackPanel` proves the host framework supports:

- viewport-driven realization
- cache-length overscan
- recycling pools
- estimated size fallback

A Pretext-style virtualized wrap or masonry panel would follow the same contract, but replace stack-specific geometry with a custom solver.

### 9.5 Avalonia control pattern

For a custom control:

1. parse control content and chrome into a `PreparedControlModel`
2. prepare text-bearing regions once
3. solve slot rectangles in measure
4. arrange template parts or retained child controls in arrange

This is especially attractive for:

- mail rows
- note cards
- chips and tags
- inspector property rows
- list items with rich inline structure

### 9.6 Avalonia work packages

Recommended implementation order:

1. Build a framework-agnostic core package with prepared models, solve results, and viewport indices.
2. Add an Avalonia `PreparedLayoutHost` base that owns fingerprints, prepared caches, and solved results.
3. Add `PretextPanelBase : Panel` for non-virtualized custom geometry panels.
4. Add `PretextVirtualizingPanelBase : VirtualizingPanel` with:
   - prepared item arrays
   - viewport solve
   - realized range management
   - recycle-key integration
5. Add `PretextVirtualizingItemsControl : ItemsControl, ILogicalScrollable` as the scroll-host bridge for `ScrollViewer`.
6. Add `PretextControlBase : TemplatedControl` for slot-based control layout.
7. Add diagnostics:
   - fingerprint hits and misses
   - prepare duration
   - solve duration
   - realized element count
   - viewport range

## 10. Uno Integration

Uno also has the right lifecycle shape, but the extension points differ from Avalonia.

### 10.1 Relevant framework evidence

The critical sources are:

- `Uno.UI/UI/Xaml/UIElement.Layout.cs`
- `Uno.UI/UI/Xaml/FrameworkElement.Layout.crossruntime.cs`
- `Uno.UI/UI/Xaml/Controls/LayoutPanel/LayoutPanel.cs`
- `Uno.UI/UI/Xaml/Controls/Repeater/ItemsRepeater.cs`
- `Uno.UI/UI/Xaml/Controls/Repeater/VirtualizingLayout.cs`
- `Uno.UI/UI/Xaml/Controls/TextBlock/TextBlockMeasureCache.cs`
- `Uno.UI/UI/Xaml/AdaptiveTrigger.cs`
- `Uno.UI/UI/Xaml/VisualStateManager.cs`

Those files show:

- Uno explicitly tracks dirty flags and dirty paths on `UIElement`
- `FrameworkElement.MeasureCore` and arrange logic already separate lifecycle phases
- `LayoutPanel` is the native hook for pluggable layout algorithms
- `ItemsRepeater` and `VirtualizingLayout` provide a framework-approved path for large-data realization and viewport-aware layout
- `TextBlockMeasureCache` is a direct precedent for extracting stable measure keys and caching compatible results
- `AdaptiveTrigger` and `VisualStateManager` provide responsive and stateful mode selection around a layout engine

### 10.2 Uno mapping

The clean mapping is:

- shared prepared models stay outside `UIElement`
- `MeasureOverride` or `Layout.Measure(...)` builds or reuses prepared state
- `ArrangeOverride` or `Layout.Arrange(...)` consumes solved rectangles
- `ItemsRepeater + VirtualizingLayout` owns large item realization
- `AdaptiveTrigger` or direct width checks choose high-level responsive mode
- solved geometry then handles the actual placement inside that mode

### 10.3 Uno panel pattern

The most natural hook for Uno is `LayoutPanel`.

That gives a built-in place to host:

- `Measure(context, availableSize)`
- `Arrange(context, finalSize)`
- layout-driven invalidation through `InvalidateMeasure` and `InvalidateArrange`

A Pretext-style layout object for Uno could:

1. fingerprint semantic inputs
2. build or reuse a prepared model
3. solve geometry in `Measure`
4. cache solved rectangles
5. apply them in `Arrange`

This is a better fit than embedding all logic directly in a panel subclass when the intent is to author reusable layout algorithms.

### 10.4 Uno repeater pattern

For large item surfaces, `ItemsRepeater` is the right host.

`ItemsRepeater` already manages:

- view generation
- layout state
- visible and realization windows
- anchor behavior
- layout reentrancy checks

`VirtualizingLayout` already expects layout authors to supply:

- measure behavior
- arrange behavior
- items-changed handling
- viewport significance policy

That is exactly where a Pretext-style item geometry solver belongs.

### 10.5 Uno control pattern

For controls that must remain built from normal Uno controls:

1. prepare semantic slots and text handles
2. solve rectangles from current size and control state
3. arrange retained child controls on a `Canvas` or template root
4. use `VisualStateManager` only for paint/state differences, not as the primary geometry solver

This avoids turning responsive geometry into large numbers of imperative visual-tree mutations.

### 10.6 Uno responsive pattern

Responsive design should be layered:

1. `AdaptiveTrigger` or equivalent width-state logic selects mode
2. the mode feeds constraints into the two-pass solver
3. the solver computes actual child rectangles
4. arrange consumes those rectangles

This is more robust than encoding the entire responsive geometry inside visual-state setters.

### 10.7 Uno work packages

Recommended implementation order:

1. Build a framework-agnostic core package shared with Avalonia work.
2. Add a `PretextLayout`-style general geometry package for non-text prepared models.
3. Add `PretextLayoutPanelLayout : Layout` for Uno `LayoutPanel`.
4. Add `PretextVirtualizingLayout : VirtualizingLayout` for `ItemsRepeater`.
5. Add `PretextControlLayoutHost` for slot-based control geometry using retained child controls.
6. Add diagnostics:
   - prepare count
   - solve count
   - viewport update count
   - realized range
   - cache hit rate

Current status in this workspace:

- `PretextPanelLayout` is implemented for `LayoutPanel`
- `PretextVirtualizingLayout` is implemented for `ItemsRepeater`
- `PretextControlLayoutHost` is implemented for retained child-control slot layout
- shared diagnostics are implemented for prepare, solve, viewport, and realized-range tracking
- the main remaining Uno framework work is broader validation and reusable sample coverage built on top of those adapters

## 11. Shared Problems Both Frameworks Need to Solve

The two frameworks differ, but the hard problems are mostly the same.

### 11.1 Fingerprint correctness

If the fingerprint misses a semantic input, cached prepared state becomes unsafe.

### 11.2 Object churn

If prepared item models are too object-heavy, the semantic pass becomes allocation-heavy and loses the main benefit.

### 11.3 Partial invalidation

Large item collections need range-level updates, not full rebuilds, when only a few items change.

### 11.4 Viewport synchronization

Virtualized panels must keep total extent, visible range, and realized controls consistent even when scroll and size changes interleave.

### 11.5 Framework-native behavior

The system must preserve:

- keyboard navigation
- focus behavior
- automation
- hit testing
- bring-into-view behavior
- pointer routing

That is why solved geometry should usually feed native controls instead of replacing them with a custom rendering world unless absolutely necessary.

## 12. Recommended Architecture

The long-term architecture should be layered like this:

### Layer 1: core prepared-layout engine

Framework-independent:

- fingerprints
- prepared models
- solve algorithms
- occlusion indices
- reusable diagnostics

### Layer 2: framework adapters

Framework-specific:

- Avalonia base classes and virtualizing hosts
- Uno `Layout`, `LayoutPanel`, and `VirtualizingLayout` adapters
- template-part and child-control arrangement helpers

### Layer 3: app-facing controls and panels

Reusable controls:

- masonry panels
- wrap panels
- non-uniform lists
- mail rows
- note cards
- dashboards
- responsive shells

### Layer 4: app composition

Application-specific mode selection, theming, and interaction design.

## 13. Initial Delivery Plan

The user asked to start with the core framework model, then do Avalonia and Uno integration work. The right staged plan is:

### Phase A: core framework

Deliver a framework-neutral design and prototype around:

- `LayoutFingerprint`
- `PreparedPanelModel`
- `PreparedControlModel`
- `PreparedItemsModel`
- `LayoutConstraints`
- `SolvedLayout`
- `VerticalOcclusionIndex`
- diagnostic counters and tracing hooks

### Phase B: Avalonia integration

Deliver:

- `PretextPanelBase : Panel`
- `PretextVirtualizingPanelBase : VirtualizingPanel`
- `PretextVirtualizingItemsControl : ItemsControl, ILogicalScrollable`
- `PretextTemplatedLayoutControl : TemplatedControl`
- `EffectiveViewportChanged` integration
- logical-scroll / `ScrollViewer` integration
- realized-range and recycle support

### Phase C: Uno integration

Deliver:

- `PretextLayoutPanelLayout : Layout`
- `PretextVirtualizingLayout : VirtualizingLayout`
- `PretextControlLayoutHost`
- `ItemsRepeater` adapter layer
- responsive mode integration with `AdaptiveTrigger` or width-state selection

Current implementation state:

- the `LayoutPanel`, `VirtualizingLayout`, and control-host layers are in place in the Uno sample workspace
- responsive mode selection is already exercised in the sample browser through width-driven solve logic
- diagnostics plumbing is now in place on the shared controller and the virtualized adapters
- the remaining planned Uno work is mostly expanded reusable sample coverage and sample-level validation

### Phase D: sample and validation layer

Deliver identical sample classes across both frameworks where practical:

- non-uniform wrap
- masonry
- mail list row
- note card
- responsive mail shell
- responsive notes shell

Current implementation state:

- shared `mail list row` and `note card` sample definitions now exist in the core framework layer
- shared `responsive mail shell` and `responsive notes shell` definitions now exist in the core framework layer
- shared `non-uniform wrap` and `masonry` item-surface definitions now exist in the core framework layer through `IPreparedItemsLayoutDefinition`
- the shared occlusion model now supports sparse viewport selections, which is required for shortest-column masonry and is exercised by both the Uno and Avalonia virtualization adapters
- all shared control, shell, wrap, and masonry definitions are validated directly in core tests and exercised through Avalonia control-host or virtualization tests
- the Uno sample browser now hosts shared control, shell, wrap, and masonry samples through `PretextControlLayoutHost` and the reusable `VirtualizingLayout` adapter layer
- an actual Avalonia visual sample browser now exists in `Uno/PretextSamples/Pretext.Avalonia.Samples`
- the Avalonia browser hosts the same shared `mail row`, `note card`, `responsive mail shell`, `responsive notes shell`, `wrap`, and `masonry` samples through `Pretext.Avalonia.Controls`
- the Avalonia browser includes a live diagnostics pane for prepare, solve, cache-hit, viewport, and realization state inspection
- headless screenshot validation now captures the real Avalonia browser for all shared samples and emits PNG plus manifest artifacts under `Uno/PretextSamples/artifacts/headless-screenshots/avalonia-sample-browser`
- the remaining sample-layer work is now optional polish: richer screenshot diffing, more interaction scenarios, or a broader gallery beyond the initial shared sample set

## 14. Final Position

Pretext’s architecture generalizes well because the important lesson is not about text specifically. It is about staging:

- prepare semantics once
- solve geometry often
- realize minimally

Avalonia and Uno already expose the right lifecycle hooks to support this:

- explicit measure and arrange phases
- invalidation systems
- viewport-aware virtualization hooks
- retained native controls

So the correct direction is not to treat Pretext as only a text engine.

The correct direction is to treat it as the first finished example of a broader prepared-layout architecture that can power:

- text layout
- custom panels
- virtualized non-uniform item surfaces
- slot-based controls
- responsive shells

across both Avalonia and Uno.
