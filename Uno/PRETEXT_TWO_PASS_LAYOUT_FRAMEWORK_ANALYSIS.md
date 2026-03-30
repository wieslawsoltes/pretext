# Pretext-Style Two-Pass Layout Analysis for Uno and Avalonia

Status: draft design analysis  
Audience: framework contributors, control authors, application architects  
Scope: how the prepare-and-cache ideas used by Pretext can be generalized into a two-pass layout system for custom panels, custom controls, responsive shells, and virtualized text-heavy UI in Uno and Avalonia

This document extends:

- `Uno/PRETEXT_TECHNICAL_SPEC.md`
- `Uno/PRETEXT_UNO_UI_SYSTEM_SPEC.md`

Those documents explain the current Pretext engine and the current Uno integration. This document focuses on a broader architectural question:

can the same kind of parsing, caching, and staged layout that Pretext uses for text be used to build reusable panel layouts and control layouts in Uno or Avalonia?

The answer is yes. In fact, both frameworks already contain the right lifecycle shape for it.

## 1. Executive Summary

Pretext works because it splits layout into two different kinds of work:

1. expensive semantic preparation
2. cheap repeated geometric solving

For text, the semantic preparation phase includes:

- normalization
- segmentation
- script-specific break fixes
- measurement caching
- chunk metadata

The repeated solve phase then answers:

- how many lines fit?
- what is the height?
- where does each line begin and end?

The same pattern can be generalized to UI layout:

1. parse controls or items into layout tokens and intrinsic metrics
2. cache the prepared representation
3. solve geometry repeatedly for different available sizes
4. arrange live controls from the solved geometry

That gives a two-pass layout system:

- pass A: semantic prepare pass
- pass B: geometric solve pass

inside the normal framework measure/arrange lifecycle.

This is feasible in both:

- Uno, through `MeasureOverride`, `ArrangeOverride`, `LayoutPanel`, and `ItemsRepeater` / `VirtualizingLayout`
- Avalonia, through `MeasureOverride`, `ArrangeOverride`, `Panel`, `VirtualizingPanel`, `LayoutManager`, and `EffectiveViewportChanged`

## 2. Why Pretext’s Architecture Generalizes Beyond Text

### 2.1 What Pretext is really doing

At a deeper level, Pretext is not “just a text layout engine”. It is an example of a more general architecture:

- content is transformed into a stable prepared model
- expensive derivations are cached
- width-dependent solves do not repeat semantic work
- rendering consumes solved geometry rather than discovering it late

The text case happens to be:

- tokens are segments and graphemes
- constraints are width and line height
- outputs are line ranges and heights

The same idea can be applied to panel layout:

- tokens are children, content groups, chrome sections, or item templates
- constraints are width, height, mode, viewport, and spacing rules
- outputs are child rectangles, extents, and virtualization ranges

### 2.2 The key abstraction

The key abstraction is:

`PreparedLayoutModel + Solve(constraints) -> LayoutGeometry`

For text:

- `PreparedText`
- `Layout(width, lineHeight)`

For UI:

- `PreparedPanelModel`
- `SolvePanel(width, height, viewport, mode)`

For controls:

- `PreparedControlModel`
- `SolveControl(width, height, state)`

## 3. Framework Evidence That This Fits

## 3.1 Uno evidence

Uno already separates framework measure and arrange clearly.

Relevant code paths:

- `UIElement.Layout.cs`
  - invalidation flags
  - measure/arrange dirtiness
- `FrameworkElement.Layout.crossruntime.cs`
  - the actual `MeasureCore` / `ArrangeCore` pipeline
- `LayoutPanel`
  - pluggable `Layout` object
- `VirtualizingLayout`
  - pluggable virtualizing layout
- `ItemsRepeater`
  - realization, recycling, viewport, anchor, extent
- `AdaptiveTrigger` and `VisualStateManager`
  - responsive state switching

Uno also already contains a control-level example of the same pattern in `TextBlockMeasureCache`:

- it extracts a stable measure key from `TextBlock` properties
- caches measured sizes for available sizes
- reuses compatible measures instead of recomputing

That is not Pretext, but it is the same architectural idea.

## 3.2 Avalonia evidence

Avalonia also exposes the right lifecycle.

Relevant code paths:

- `Layoutable`
  - measure/arrange invalidation
  - `EffectiveViewportChanged`
  - `LayoutUpdated`
- `LayoutManager`
  - queued layout passes
  - separate measure and arrange queues
  - effective viewport listeners
- `Panel`
  - child collection and parent invalidation helpers
- `Canvas`
  - absolute arrangement reference pattern
- `VirtualizingPanel`
  - item generation and recycling contract
- `VirtualizingStackPanel`
  - viewport-driven realization
- `TextBlock` / `TextPresenter`
  - cached `TextLayout`
  - recreated only when constraint or text-affecting state changes

Avalonia’s `TextBlock` is especially instructive:

- it holds a cached `_textLayout`
- invalidates that cache only when measure-affecting inputs change
- reuses the same text layout during render and arrange

That is effectively a local prepared-model cache.

## 4. Generalized Two-Pass Layout Model

The proposed generalized model is:

### Phase 0: input fingerprinting

Compute a stable fingerprint for the layout-affecting inputs.

Examples:

- text
- font
- inline content
- child visibility
- spacing and padding
- template mode
- responsive state
- item count
- per-item content hashes

This decides whether the prepared model is reusable.

### Phase 1: semantic prepare pass

Convert raw control or panel inputs into a prepared model.

Examples:

- segment text once
- precompute fixed chrome sizes
- classify children into layout roles
- group repeated item templates
- derive intrinsic dimensions for icons, badges, buttons, and chips
- build obstacle maps
- precompute title/body handles

This phase should avoid dependence on the current viewport except where unavoidable.

### Phase 2: constraint solve pass

Given available width, height, viewport, and mode:

- solve the actual geometry
- compute panel rectangles
- compute child slots
- compute extents
- compute realization range
- compute arrangement of internal control parts

This phase should be arithmetic-first and should not do expensive parsing.

### Phase 3: framework arrange / realization pass

Consume the solved geometry:

- arrange children
- realize only the visible item range
- update retained visuals
- keep interaction and accessibility with the framework controls

This phase should not re-solve the model.

## 5. Prepared Models for Panels

To apply the model to panel layouts, the panel needs a prepared representation that plays the same role as `PreparedText`.

### 5.1 Prepared panel model

A `PreparedPanelModel` should capture:

- child roles
- intrinsic child metrics
- prepared text handles for text-bearing children
- chrome constants
- groupings or template classes
- optional viewport indexing support

Example conceptual shape:

```csharp
public sealed record PreparedPanelModel(
    IReadOnlyList<PreparedChildNode> Children,
    IReadOnlyDictionary<string, PreparedTextHandle> TextHandles,
    PanelChromeMetrics Chrome,
    object? DerivedState);
```

### 5.2 Prepared child node

Each prepared child node should capture what the geometric solve needs, not the live control itself.

Examples:

- fixed-size child
- aspect-ratio child
- text-driven child
- intrinsic leaf with cached size
- fill child
- anchored child
- obstacle child

Example:

```csharp
public enum PreparedChildKind
{
    Fixed,
    TextBlock,
    Fill,
    Aspect,
    Overlay,
    Obstacle,
}
```

This is analogous to Pretext’s segment kinds.

### 5.3 Why panel parsing matters

Without a prepare pass, a custom panel often has to rediscover:

- which children affect height
- which children are fixed versus elastic
- which children need text measurement
- how different children are coupled

That discovery ends up repeated inside every measure pass.

A prepared panel model eliminates that repeated interpretation.

## 6. Prepared Models for Controls

The same idea applies to a single custom control, especially templated controls with text-heavy chrome.

### 6.1 Prepared control model

A `PreparedControlModel` should capture:

- control parts and their roles
- text-bearing regions
- fixed chrome metrics
- alignment rules
- responsive variants
- internal dependencies between parts

Example use cases:

- command button with title, subtitle, badge, icon, accelerator
- navigation row with multi-line label and trailing status
- mail row with sender, subject, snippet, timestamp, unread marker
- note card with title, body excerpt, tags, collaborator chips

### 6.2 Why this is useful

A conventional templated control often measures:

- title `TextBlock`
- subtitle `TextBlock`
- badge
- icon
- stack containers

and only then discovers the final size.

A Pretext-style control can instead:

1. prepare title and subtitle text once
2. prepare chrome metrics once
3. solve title/subtitle heights for the current width
4. derive final rectangles for all template parts
5. arrange the existing elements

This is the same idea as Pretext, but applied to a control template.

## 7. Measure and Arrange Mapping in Uno

The best mapping for Uno is:

### 7.1 In `MeasureOverride`

Do:

- validate or rebuild the prepared model if the semantic fingerprint changed
- derive width class or responsive mode
- solve geometry for the current available size
- optionally measure leaf children against their known target slot
- return desired size or logical extent

Do not:

- repeatedly parse content
- rebuild the prepared model unnecessarily
- discover text sizes through hidden live controls

### 7.2 In `ArrangeOverride`

Do:

- apply the geometry solved during measure
- reuse cached placements
- update leaf positions

Do not:

- run paragraph preparation again
- repeat expensive text layout work

### 7.3 In `LayoutPanel`

`LayoutPanel` is the cleanest path for reusable non-virtualized Pretext-style layouts in Uno.

Recommended shape:

- `PretextPanelLayout : NonVirtualizingLayout`

Responsibilities:

- maintain `LayoutState`
- hold prepared panel data
- solve geometry in `MeasureOverride`
- apply placement in `ArrangeOverride`
- expose narrow invalidation channels through `InvalidateMeasure()` and `InvalidateArrange()`

### 7.4 In `ItemsRepeater`

For large item sets, use:

- `PretextVirtualizingLayout : VirtualizingLayout`

Responsibilities:

- own per-item or per-template prepared text handles
- own extent model
- own visible-range solve
- ask `VirtualizingLayoutContext` for elements only when needed

This is where the current custom wrap/list controls should eventually evolve if they become framework-grade reusable controls.

### 7.5 Responsive states in Uno

Uno should use:

- `AdaptiveTrigger`
- `VisualStateManager`

for:

- choosing topology
- showing and hiding chrome
- switching between narrow, medium, and wide visual states

Then use the Pretext-style solver for:

- exact panel rectangles
- text heights
- content-dependent widths

Important Uno-specific note:

because adaptive trigger order matters in Uno, state declarations should be ordered from largest constraint to smallest when using minimum-width triggers.

## 8. Measure and Arrange Mapping in Avalonia

The best mapping for Avalonia is similar, but it should be aligned with `LayoutManager` and `Layoutable`.

### 8.1 In `MeasureOverride`

Do:

- validate the prepared model
- solve geometry for the current constraint
- measure leaf children against target slots where necessary
- return desired size

Avalonia’s `TextBlock` and `TextPresenter` already demonstrate the local version of this pattern by invalidating cached `TextLayout` only when needed.

### 8.2 In `ArrangeOverride`

Do:

- consume the solved geometry
- reuse placements from the last measure
- avoid new semantic work

### 8.3 In `Panel`

For reusable non-virtualized Pretext layouts in Avalonia, the natural shape is:

- `PretextPanel : Panel`

This panel would:

- parse children into prepared layout roles
- cache prepared text or intrinsic metrics
- solve child rectangles in measure
- arrange children in arrange

Avalonia’s `Canvas` is the simplest reference implementation for absolute placement, but a Pretext panel would be content-aware instead of purely coordinate-driven.

### 8.4 In `VirtualizingPanel`

For large data surfaces:

- `PretextVirtualizingPanel : VirtualizingPanel`

Responsibilities:

- listen to `EffectiveViewportChanged`
- maintain realization range
- recycle item containers
- keep a prepared template and geometry cache
- realize only what is needed

Avalonia’s `VirtualizingStackPanel` already shows the right lifecycle:

- maintain viewport state
- realize a range
- recycle outside the realized viewport
- compute desired extent

The Pretext-style addition is that per-item text size should come from prepared handles and width solves, not from repeatedly constructing the same child subtree to discover its size.

### 8.5 Effective viewport in Avalonia

Avalonia’s `EffectiveViewportChanged` is especially well-suited for this architecture.

Recommended use:

- use `EffectiveViewportChanged` to trigger realization-range updates
- use overscan or buffer factors to reduce churn
- keep geometry solve separate from realization

This maps very cleanly to Pretext’s occlusion and viewport-band logic.

## 9. Control Layout vs Panel Layout

The design differs slightly depending on whether the target is a control or a panel.

### 9.1 Control layout

A control layout is internal to a single reusable component.

Examples:

- mail row control
- conversation bubble control
- note card control
- rich command item

Recommended design:

- prepare internal text fields once
- prepare chrome metrics once
- solve internal part rectangles for current width
- arrange template parts

This is similar to a mini text engine for the control.

### 9.2 Panel layout

A panel layout is responsible for multiple children or items.

Examples:

- wrap panel
- masonry panel
- dashboard board
- document section layout

Recommended design:

- prepare per-child or per-template metrics
- compute full geometry map
- compute extent
- compute viewport visibility if virtualized
- arrange children from solved placements

### 9.3 Shared principle

In both cases, the framework should not be asked to discover text size late if the solver can know it early.

## 10. Generalized Parsing Model for UI Layout

To mimic Pretext’s prepare phase, a UI layout system needs a parsing model for content.

### 10.1 Parse UI into layout tokens

Possible tokens:

- fixed box
- text block
- optional section
- separator
- chip run
- icon slot
- detail region
- expandable body
- overlay
- obstruction

The point is not to make everything a drawing primitive. The point is to create a semantic model that can be solved without interrogating the final visual tree.

### 10.2 Parse repeated item templates

For lists and wrap surfaces, many items share the same structural shape.

A Pretext-style prepare phase should:

- deduplicate repeated template types
- cache prepared title/body handles
- precompute template chrome
- classify items by template kind

This is exactly what the current Pretext wrap/list samples already do informally through template arrays and template indices.

### 10.3 Parse breakpoints as layout topology

Responsive layout should not only change properties. It should also change topology.

Examples:

- wide: list + detail + inspector
- medium: list + detail
- narrow: stacked list then detail

The parse/prep layer should know these topology options so the solve phase can quickly choose one.

## 11. Caching Strategy

The important lesson from Pretext is not just “cache measurements”. It is “cache at the right semantic levels”.

### 11.1 Recommended cache layers

1. input fingerprint cache
2. prepared text cache
3. prepared control/panel model cache
4. width-bucket geometry cache
5. viewport range cache
6. visual pool

### 11.2 Semantic fingerprint keys

Keys should include only layout-affecting inputs.

For control text:

- string content
- font family
- size
- weight
- line-height
- wrap mode
- trimming mode
- locale

For panel or control models:

- visible child set
- template variant
- spacing/padding values
- icon presence
- tag counts
- mode

### 11.3 Width-bucket solves

Small width changes often do not justify a full semantic rebuild.

Recommended strategy:

- keep semantic prepare caches exact
- allow solve caches to be width-bucketed or rounded
- invalidate the solve cache when the bucket changes materially

### 11.4 Framework-specific cache placement

In Uno:

- cache prepared models in the control or layout object
- keep layout state in `LayoutState` for `LayoutPanel` and `ItemsRepeater`

In Avalonia:

- cache prepared models on the control or panel instance
- let `LayoutManager` continue to drive invalidation and pass execution

## 12. Virtualization and Pretext-Style Layout

The best large-data architecture is:

1. semantic prepare of templates or item classes
2. solve item heights from prepared text
3. compute placements and extent
4. build viewport index
5. realize only the visible slice

This is already present in principle in:

- Uno `ItemsRepeater`
- Avalonia `VirtualizingPanel`

What Pretext adds is better intrinsic sizing before realization.

### 12.1 Why this matters

Without a prepare phase, many variable-height lists do one of two bad things:

- estimate poorly and correct after live measure
- realize too much just to discover height

Pretext-style sizing lets the virtualization system start with much more accurate geometry.

## 13. Proposed Reusable Abstractions

### 13.1 Shared, framework-agnostic abstractions

- `PreparedLayoutModel`
- `PreparedControlModel`
- `PreparedPanelModel`
- `LayoutSolveInput`
- `LayoutGeometryResult`
- `ViewportQuery`
- `RealizationRange`

### 13.2 Uno-specific abstractions

- `PretextPanelLayout : NonVirtualizingLayout`
- `PretextWrapLayout : VirtualizingLayout`
- `PretextListLayout : VirtualizingLayout`
- `PretextResponsiveShell`
- `PretextLayoutState`

### 13.3 Avalonia-specific abstractions

- `PretextPanel : Panel`
- `PretextVirtualizingPanel : VirtualizingPanel`
- `PretextResponsiveLayoutRoot`
- `PreparedLayoutCache`

### 13.4 Control-level abstractions

- `PreparedChromeMetrics`
- `PreparedTextSlot`
- `ControlLayoutSlot`
- `ControlLayoutFingerprint`

## 14. Design Rules for a Two-Pass System

1. Parse semantic content once.
2. Separate preparation from geometry solving.
3. Make geometry solving pure and arithmetic-first.
4. Keep live controls as interactive leaves, not measurement oracles.
5. Cache at semantic layers, not only final sizes.
6. Invalidate narrowly.
7. Use viewport change as a realization signal, not a semantic rebuild signal.
8. Keep arrange cheap.
9. Prefer width-only or width-dominant solves when possible.
10. Build reusable prepared models for both controls and panels.

## 15. Example Design: A Pretext Mail Row

To make the idea concrete, consider a mail row.

Raw inputs:

- sender
- subject
- snippet
- timestamp
- unread state
- attachment icon

Prepared control model:

- prepared sender text
- prepared subject text
- prepared snippet text
- fixed metrics for timestamp, icon slot, padding, badge
- responsive variants for wide and narrow row modes

Solve result for a given width:

- sender rect
- subject rect
- snippet rect
- timestamp rect
- icon rect
- final row height

Framework step:

- arrange the existing parts into those slots

No hidden `TextBlock`s are needed to discover row height after the fact.

## 16. Example Design: A Pretext Wrap Panel

Raw inputs:

- 100k items
- each item has title/body/tags

Prepared panel model:

- template classes
- prepared title/body handles
- chrome metrics
- per-template intrinsic width rules

Solve result:

- tile heights
- tile placements
- content extent
- band index
- visible range

Framework step:

- realize only visible containers
- assign geometry

This is the generalized form of the current Pretext masonry and virtual wrap samples.

## 17. What Is Harder

Some problems still need separate work.

### 17.1 Fully editable text

If the surface needs:

- caret movement
- selection
- IME composition
- incremental edits
- bidirectional caret rules

then the solver needs a richer incremental model.

### 17.2 Arbitrary child intrinsic sizing

If every child is an arbitrary complex subtree with no stable intrinsic model, a semantic prepare pass becomes harder.

In those cases:

- require children to expose intrinsic metrics
- or limit the scope to text-heavy or partly-constrained children

### 17.3 Animation-heavy rearrangement

If the layout is continuously animating topology, the solver still helps, but the animation layer should interpolate solved rectangles rather than re-parsing content every frame.

## 18. Final Position

Pretext’s main architectural lesson is not only about text. It is about how to structure layout work:

- parse first
- cache second
- solve geometry third
- realize visuals last

Both Uno and Avalonia already provide the right two-pass host lifecycle for this:

- measure
- arrange
- narrow invalidation
- viewport signaling
- virtualization hooks

So yes, there is a practical way to do something similar for panel layouts and control layouts.

The right direction is a framework-integrated prepared-layout architecture where:

- text preparation is one kind of semantic prepare pass
- control and panel parsing are additional prepare passes
- measure performs geometry solving over prepared state
- arrange consumes solved geometry
- virtualization uses the same prepared state for accurate extents and realized ranges

That is the natural path from “Pretext as a text engine” to “Pretext-style layout as a broader UI-system architecture”.
