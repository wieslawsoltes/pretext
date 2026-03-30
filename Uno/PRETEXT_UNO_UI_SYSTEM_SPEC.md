# Pretext + Uno UI System Specification

Status: draft working specification  
Audience: Pretext contributors, Uno contributors, control authors, performance-focused app teams  
Scope: how the Pretext algorithms and the current `Pretext.Uno` port can be used to build custom layouts, virtualized controls, responsive shells, and a broader Uno UI system

This document supplements `Uno/PRETEXT_TECHNICAL_SPEC.md`. That document explains how the Pretext engine itself works. This document focuses on how that engine should be applied inside the Uno Platform layout system to build controls and application surfaces.

## 1. Purpose

Pretext exists to answer a question that standard UI layout systems answer too late: how tall and how wide will text be before the final live visual tree is measured?

In conventional XAML UI, text-heavy controls often become expensive because the application:

1. creates controls,
2. lets the framework measure them,
3. reads the resulting sizes,
4. rearranges siblings around those results,
5. repeats this work as the viewport changes.

Pretext flips that flow:

1. prepare text once,
2. predict line breaks and heights arithmetically,
3. synthesize geometry for panels and controls up front,
4. arrange live controls only after geometry is already known.

That makes Pretext useful far beyond raw paragraph rendering. It can act as the text geometry engine for an entire UI system.

## 2. Source Basis

This specification is based on:

- the TypeScript reference engine in `src/layout.ts`, `src/analysis.ts`, `src/measurement.ts`, `src/line-break.ts`, and `src/bidi.ts`
- the C# port in `Uno/PretextSamples/Pretext.Uno/*.cs`
- the current Uno integration layer in `Uno/PretextSamples/Pretext.Uno.Controls/*.cs`
- the responsive and virtualized sample surfaces in `Uno/PretextSamples/PretextSamples/Samples/*.cs`
- the Uno framework layout and control sources in:
  - `src/Uno.UI/UI/Xaml/UIElement.Layout.cs`
  - `src/Uno.UI/UI/Xaml/FrameworkElement.Layout.crossruntime.cs`
  - `src/Uno.UI/UI/Xaml/FrameworkElement.EffectiveViewport.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Panel/Panel.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Canvas/Canvas.Layout.cs`
  - `src/Uno.UI/UI/Xaml/Controls/LayoutPanel/LayoutPanel.cs`
  - `src/Uno.UI/UI/Xaml/Controls/LayoutPanel/LayoutPanelLayoutContext.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Repeater/Layout.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Repeater/NonVirtualizingLayout.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Repeater/VirtualizingLayout.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Repeater/VirtualizingLayoutContext.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Repeater/ItemsRepeater.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Repeater/ItemsRepeaterScrollHost.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Repeater/FlowLayout.cs`
  - `src/Uno.UI/UI/Xaml/Controls/Repeater/StackLayout.cs`
  - `src/Uno.UI/UI/Xaml/Controls/ScrollViewer/ScrollViewer.cs`
  - `src/Uno.UI/UI/Xaml/AdaptiveTrigger.cs`
  - `src/Uno.UI/UI/Xaml/VisualStateManager.cs`

## 3. Core Pretext Model Relevant to UI Systems

Pretext has four important properties that matter for Uno control design.

### 3.1 Prepare once, layout many

`prepare()` and `prepareWithSegments()` do the expensive horizontal work up front:

- normalization and whitespace modeling
- segmentation
- script-specific preprocessing
- measurement and metric caching
- chunk metadata generation

`layout()` and `layoutNextLine()` then run as pure arithmetic over prepared data.

This is the main reason Pretext is useful for responsive UI. When width changes, the system does not need to re-measure text through live `TextBlock` instances.

### 3.2 Text is treated as geometry input

Pretext produces:

- total height for a width and line height
- individual line text and widths
- streaming line steps
- cursor ranges
- line ranges without string materialization

Those outputs can be used to determine:

- card heights
- bubble sizes
- note preview heights
- list item extents
- magazine-style column flow
- obstacle-aware routing
- breakpoint-specific panel placement

### 3.3 The fast path avoids framework reads

The engine is deliberately structured to avoid:

- DOM reads in TS
- live `TextBlock` measurement in Uno
- string work in hot resize paths
- allocation-heavy full paragraph reconstruction in common paths

That makes it suitable for resize-heavy responsive shells and large virtualized surfaces.

### 3.4 Rich and opaque paths stay separate

The engine has:

- an opaque fast path for `layout()`
- a richer segmented path for custom rendering and non-rectangular layout

A good Uno integration should preserve that split. Not every control should pay for rich metadata.

## 4. Uno Layout Model and Where Pretext Fits

Uno still follows the standard XAML model:

1. `MeasureOverride` determines desired size.
2. `ArrangeOverride` places children.
3. dirty flags propagate through the tree.

Relevant framework behavior:

- `FrameworkElement.Layout.crossruntime.cs` adjusts for margin, min/max, and template application before calling `MeasureOverride`.
- `UIElement.Layout.cs` tracks measure/arrange invalidation flags, especially on Skia and Wasm.
- `Canvas` measures children with effectively unconstrained size and arranges by attached absolute coordinates.
- `LayoutPanel` delegates measure/arrange to a `Layout` instance.
- `ItemsRepeater` delegates realization and placement to `VirtualizingLayout`.
- `EffectiveViewport` infrastructure can propagate viewport changes down the tree.

Pretext should be plugged in at the point where an app would otherwise need text measurement to choose layout geometry.

### 4.1 What Pretext should own

Pretext should own:

- text preparation
- text-driven height prediction
- text-driven width and line-count prediction
- line-by-line placement for custom paragraph surfaces
- synthesis of panel rectangles when text determines panel size
- viewport-range selection for text-heavy virtualized surfaces

### 4.2 What Uno should still own

Uno should still own:

- control lifetime
- input routing
- focus behavior
- automation peers and accessibility
- template and visual states
- scrolling behavior
- theme resources
- pointer/keyboard interaction
- transitions and compositor animation

The intended architecture is not “replace Uno layout entirely”. It is “replace late text discovery with early arithmetic text geometry”.

## 5. Integration Tiers

There are four practical integration tiers.

### 5.1 Tier 1: Text prediction inside ordinary shells

Use Pretext to compute the size of text blocks, but still render ordinary Uno controls.

Examples:

- todo details panel
- mail preview list
- note editor metadata blocks
- inspector sidebars
- settings pages with variable description text

Current example:

- `Pretext.Uno.Controls/PretextParagraphView.cs`

Pattern:

1. prepare text once per content item
2. compute width-specific height with `PretextLayout.Layout(...)`
3. place a standard `TextBlock` or a pooled line view at exact coordinates
4. use the predicted height to place buttons, chips, dividers, inputs, and other controls

This tier gives most of the benefit with the least framework complexity.

### 5.2 Tier 2: Absolute-positioned surfaces

Use a `Canvas` or Canvas-like surface where all significant rectangles are computed from Pretext output.

Examples:

- editorial spread
- dashboard with irregular cards
- list/detail shell with non-grid panel switching
- a note-taking board with overlapping tool chrome
- document page with obstacle avoidance

Current examples:

- editorial sample
- dynamic layout sample
- responsive todo/mail/notes samples

Pattern:

1. choose viewport mode
2. compute panel rectangles
3. compute text-driven heights inside each panel
4. place controls with absolute coordinates
5. arrange only the controls that are actually needed

In this model, the surface behaves more like a retained scene graph than a nested stack/grid tree.

### 5.3 Tier 3: Virtualized text-heavy controls

Use Pretext to compute a full logical extent without realizing all items.

Examples:

- non-uniform wrap panel
- non-uniform list box
- large inbox with preview snippets
- results view with mixed card heights
- activity feeds

Current examples:

- `PretextVirtualizedWrapPanel`
- `PretextVirtualizedListBox`
- `VerticalOcclusionIndex`

Pattern:

1. precompute template or item heights using Pretext
2. generate absolute placements
3. build a vertical occlusion index
4. ask the scroll host for the local viewport
5. realize only the band range that intersects the viewport plus overscan
6. reuse a small visual pool

This is the most direct control-level application of the masonry strategy.

### 5.4 Tier 4: True custom layout engines

Implement Pretext-backed layouts as first-class Uno layout objects.

Possible future shapes:

- `PretextNonVirtualizingLayout : NonVirtualizingLayout`
- `PretextVirtualizingLayout : VirtualizingLayout`
- `PretextPanel : Panel`
- `PretextLayoutPanel : LayoutPanel` with a dedicated `Layout`

These make sense when the layout itself needs to be reusable as framework infrastructure rather than sample code.

## 6. Recommended Control Architecture

### 6.1 Preferred geometry pipeline

For any Pretext-driven control, the pipeline should be:

1. input model
2. prepared text cache
3. geometry model
4. viewport filter
5. visual pool
6. arrange/update pass

More concretely:

1. Normalize control data into immutable item records.
2. Convert text fields into `PreparedText` or `PreparedTextWithSegments`.
3. Compute widths, line counts, heights, and placements.
4. Use viewport information to select the visible subset.
5. Reuse visuals instead of creating new controls on each frame.
6. Apply geometry to the retained visuals.

### 6.2 Separate the geometry model from the visual tree

Do not let the geometry exist only as live control properties.

Maintain explicit data structures such as:

- `ItemTemplateMetrics`
- `PanelRect`
- `LineSlot`
- `ObstacleBand`
- `Placement`
- `ViewportRange`

This mirrors the current `PretextVirtualizedWrapPanel` and `PretextVirtualizedListBox`, where layout state is computed before visuals are updated.

### 6.3 Use built-in controls as leaves, not as layout authorities

Built-in controls are valuable for:

- semantics
- focus
- keyboard navigation
- styling
- default platform behavior

They are not ideal as geometry oracles in text-dense responsive surfaces. A Pretext surface should treat them as arranged leaves whose final rectangle is already known.

## 7. Which Uno Extension Point to Use

There are several viable extension points in Uno. They should be used intentionally.

### 7.1 `Canvas`

Use when:

- the surface is fully absolute-positioned
- geometry is already known
- you want maximum control
- you want minimal layout overhead

Best for:

- editorial layouts
- obstacle routing
- custom boards
- responsive app mockups with exact placement
- pooled visual layers

Tradeoff:

- the control author must manage everything explicitly

### 7.2 `Panel`

Use when:

- you want a normal child collection
- you still want explicit arrange rules
- virtualization is not required

Best for:

- reusable absolute-layout containers
- form or dashboard surfaces with a bounded child count

Proposed future type:

- `PretextAbsolutePanel`

That panel would:

- hold child descriptors
- compute rectangles from prepared text
- measure children against known rectangles
- arrange children exactly once

### 7.3 `LayoutPanel` + `NonVirtualizingLayout`

Use when:

- you want a pluggable layout algorithm
- the layout should be reusable across different panels
- virtualization is not required

Why it fits:

- `LayoutPanel` already forwards measure and arrange to a `Layout`
- layout state is stored in `LayoutPanelLayoutContext`
- invalidation is already modeled via `Layout.MeasureInvalidated` and `ArrangeInvalidated`

Best for:

- document sections
- adaptive panels
- wrap layouts with precise non-uniform heights
- control libraries where layout should be swappable

Recommended future type:

- `PretextFlowLayout : NonVirtualizingLayout`

That layout would compute placements from text metrics and arrange children into exact slots.

### 7.4 `ItemsRepeater` + `VirtualizingLayout`

Use when:

- you need data virtualization
- the item count is large
- item height depends on text content
- you want item templating and recycling support

Why it fits:

- `VirtualizingLayoutContext` exposes item count, realization rect, anchor index, and element realization
- `ItemsRepeater` already manages factories, recycling, viewport tracking, and anchor semantics
- `FlowLayout` and `StackLayout` show how layout state and extent estimation are expected to work

Best for:

- mail lists
- chat logs
- search results
- mixed-height feed cards
- giant wrap panels

Recommended future types:

- `PretextListLayout : VirtualizingLayout`
- `PretextWrapLayout : VirtualizingLayout`
- `PretextMasonryLayout : VirtualizingLayout`

Those layouts should use Pretext for item sizing and Uno for realization infrastructure.

### 7.5 `UserControl`

Use when:

- the goal is a composite sample or a bounded, self-contained custom control
- you do not need general-purpose layout reusability yet

This is the current shape of `PretextParagraphView`, `PretextVirtualizedWrapPanel`, and `PretextVirtualizedListBox`.

It is acceptable for incubating patterns, but the reusable parts should eventually migrate into `Layout`, `Panel`, or shared primitives.

### 7.6 Measure and arrange contract

For Pretext-driven controls, the layout contract should be:

- `MeasureOverride` computes geometry and returns desired extent
- `ArrangeOverride` applies cached placements
- scrolling and viewport updates should not trigger a full geometry recomputation unless width or item content changed

Recommended rule:

do text math in measure, do placement in arrange, do not rediscover text size from the live tree in either phase.

More specifically:

1. `MeasureOverride` should:
   - resolve available width
   - choose mode or topology if needed
   - run Pretext layout for text-bearing regions
   - compute panel and child rectangles
   - measure leaf controls against their known target slot if they still need framework participation
   - return the final desired size or logical extent

2. `ArrangeOverride` should:
   - consume the geometry produced during measure
   - set final rectangles
   - avoid re-running paragraph preparation or line breaking

3. `InvalidateMeasure` should be used when:
   - width bucket changed
   - text changed
   - font changed
   - collection changed
   - chrome constants changed

4. `InvalidateArrange` should be used when:
   - only final placement changed
   - visual emphasis changed without affecting desired extent

This matches Uno’s layout invalidation model and avoids oscillating measure cycles.

## 8. Viewport and Scrolling Strategy

### 8.1 Single scroll owner

Use one scroll owner per surface whenever possible.

Reasons:

- simpler viewport math
- fewer nested scrolling bugs
- easier anchor and overscan reasoning
- more predictable virtualization

The current `StretchScrollHost` is aligned with this rule.

### 8.2 Local viewport projection

The viewport query must be converted into the local coordinate system of the target surface.

This is essential when:

- the scroller is above the custom surface
- multiple content sections share the same page `ScrollViewer`
- only one section is virtualized

`StretchScrollHost.TryGetLocalViewportBounds(...)` already demonstrates the right pattern.

### 8.3 Effective viewport integration

Uno’s `FrameworkElement.EffectiveViewport` infrastructure provides a more framework-native path for viewport propagation.

A mature Pretext control library should support both:

- direct `ScrollViewer` integration for application-owned scroll hosts
- `EffectiveViewportChanged`-style integration for controls embedded in larger trees

Recommended rule:

- use explicit scroll-host wiring in app-owned pages
- consider `EffectiveViewport` for reusable library controls

### 8.4 Band-based occlusion

The current `VerticalOcclusionIndex` is a strong base abstraction.

Its strengths:

- cheap binary search
- predictable memory profile
- independent from the visual pool
- works with wrap, list, and masonry-style surfaces

This should remain the default visibility structure for Pretext virtualized controls.

## 9. Responsive Design Model

Pretext should not replace adaptive state management. It should supply the geometry data used inside each adaptive state.

### 9.1 Breakpoint resolution

The current responsive samples use a code-side width classifier:

- narrow
- medium
- wide

That is the correct first abstraction. A robust system should expose this as a shared mode resolver.

Recommended future primitive:

- `PretextResponsiveModeResolver`

### 9.2 Visual states for chrome, Pretext for content geometry

Use `AdaptiveTrigger` and `VisualStateManager` to switch:

- visibility of panels
- icon density
- command labels
- selection affordances
- template variants

Use Pretext to compute:

- panel widths
- panel heights
- text block heights
- detail pane offsets
- wrap thresholds

Important Uno-specific rule:

`AdaptiveTrigger` order matters in Uno. States should be declared from largest to smallest condition when using minimum-width triggers. The UI system guidance should explicitly enforce that.

### 9.3 Responsive layout flow

The recommended responsive layout flow is:

1. determine viewport mode
2. choose panel topology for that mode
3. compute text-driven heights inside each panel
4. compute final rectangles for panels and controls
5. arrange controls

This is different from traditional XAML responsive design, which often relies on nested adaptive stacks/grids and lets the layout tree discover heights late.

### 9.4 Pretext as the breakpoint stabilizer

Pretext is especially valuable near breakpoints because it can answer:

- when a title now needs 2 lines instead of 1
- when a preview card now jumps from 3 lines to 5
- when two columns should collapse into one
- when a toolbar label stops fitting next to action buttons

That lets the app choose mode boundaries based on actual content behavior, not only arbitrary width constants.

## 10. Control and Layout Patterns

### 10.1 Text-heavy card and tile surfaces

Use case:

- notes boards
- feed cards
- issue grids
- mixed-height dashboard tiles

Pattern:

- prepare all card text once
- group items by repeated templates when possible
- compute per-template height at a given width
- place tiles absolutely
- virtualize with vertical bands

Recommended reusable primitives:

- `PretextTileTemplate`
- `PretextTileMetrics`
- `PretextOcclusionSurface`

### 10.2 Non-uniform list controls

Use case:

- mail inbox
- notifications
- inspector results
- search results

Pattern:

- title and preview text prepared separately
- per-item body height predicted before realization
- extent derived from text height, padding, and chrome constants
- visible range determined from viewport
- selection and interaction delegated to normal Uno controls

The current `PretextVirtualizedListBox` is already the prototype for this pattern.

### 10.3 Editorial and obstacle-aware layouts

Use case:

- magazine spreads
- poster compositions
- presentation scenes
- animated decorative obstacles with wrapped text

Pattern:

- use `prepareWithSegments()` or rich path handles
- compute free horizontal slots per line
- call `layoutNextLine()` or range walking iteratively
- route text around obstacles without asking the live tree how tall the text became

This is where Pretext’s streaming line walker becomes more important than a simple height result.

### 10.4 App shells: todo, mail, notes

Use case:

- real application chrome with built-in controls

Pattern:

- keep buttons, inputs, list rows, and progress indicators as standard Uno controls
- use Pretext to size the variable text regions
- compute shell rectangles by mode
- avoid grids that must re-measure several nested text blocks to settle

The current responsive samples already validate that this model works.

### 10.5 Chat and shrinkwrap bubbles

Use case:

- messages
- comments
- compact conversational UIs

Pattern:

- use `walkLineRanges()` or rich layout APIs
- derive bubble width from actual line geometry rather than only max width
- compute shrinkwrapped rectangles
- keep avatar, meta, reactions, and status glyphs as separate controls

This is a natural extension of the existing bubbles demo.

## 11. Proposed Reusable Library Surface

To turn the current experiments into a comprehensive UI system, the following shared abstractions are recommended.

### 11.1 Text preparation layer

- `PretextTextHandle`
  - wraps `PreparedText` or `PreparedTextWithSegments`
  - records font and locale
  - can be shared across controls

- `PretextTextCatalog`
  - central cache keyed by text, font, locale, whitespace mode
  - avoids repeated preparation across list rows and shells

### 11.2 Geometry layer

- `PretextMeasuredBlock`
  - width, line height, line count, height

- `PretextPanelRect`
  - x, y, width, height

- `PretextPlacement`
  - item index plus final arrange rectangle

- `PretextViewportRange`
  - visible start/end with overscan metadata

- `PretextObstacleMap`
  - line-band obstruction geometry for editorial and diagram layouts

### 11.3 Rendering layer

- `PretextParagraphView`
  - bounded paragraph renderer using pooled `TextBlock`s

- `PretextInlineLayer`
  - future richer inline fragment renderer for chips, links, code spans, and badges

- `PretextAbsoluteSurface`
  - a reusable Canvas-like host that owns an explicit geometry model

### 11.4 Layout layer

- `PretextPanel`
  - `Panel`-based absolute layout container

- `PretextLayout`
  - `NonVirtualizingLayout` for `LayoutPanel`

- `PretextVirtualizingLayout`
  - `VirtualizingLayout` for `ItemsRepeater`

- `PretextMasonryLayout`
  - masonry-style virtualized layout

- `PretextWrapLayout`
  - non-uniform wrapping virtualized layout

- `PretextListLayout`
  - non-uniform list layout with anchor and extent support

### 11.5 Infrastructure layer

- `PretextScrollContext`
  - wraps `ScrollViewer` or effective viewport input

- `PretextOcclusionIndex`
  - current vertical band logic generalized for multiple surface types

- `PretextRenderScheduler`
  - coalesced invalidation and viewport update scheduling

- `PretextResponsiveShell`
  - breakpoint resolution plus geometry callbacks

## 12. Performance Specification

The main performance objective is to move text-dependent decisions out of the live measure path.

### 12.1 Hot-path rules

On resize, scroll, or mode transitions, do not:

- create hidden `TextBlock`s just to measure content
- ask `DesiredSize` of text-heavy leaves as the main source of truth
- rebuild whole child collections
- materialize entire documents if only a viewport slice is visible
- do repeated string slicing when a prepared handle already exists

### 12.2 Cache levels

Recommended cache levels:

1. segment metrics cache
2. prepared text cache
3. width-bucketed block height cache
4. item-template geometry cache
5. viewport band index
6. visual pool

The current system already has:

- segment metric caching in the engine
- prepared handles
- per-layout-state geometry
- occlusion indexing
- visual pools

The main missing reusable layer is a shared text/catalog cache above individual sample controls.

### 12.3 Width bucketing

For highly dynamic resize surfaces, it is often useful to bucket widths to avoid churn caused by tiny fractional differences.

Examples:

- rounded integer widths
- device-independent pixel buckets
- explicit breakpoints

This is especially relevant on Skia, where tiny width changes can cause frequent reflow without a user-visible difference.

### 12.4 Invalidation discipline

Only invalidate measure when:

- available width class changed materially
- text content changed
- font changed
- padding or chrome constants changed
- item collection changed

Only invalidate arrange when:

- placement changed without extent change
- selection or visual emphasis changed

This matches Uno’s split invalidation model and reduces dirty-path propagation.

### 12.5 Pooling strategy

Visual pooling is required for:

- long lists
- wrap panels
- rich paragraph line rendering
- animated editorial surfaces

Pooled visuals should:

- keep leaf control instances alive
- update content and geometry in place
- avoid reattaching handlers repeatedly
- avoid per-frame template creation

### 12.6 Virtualization strategy

For 100k-item surfaces:

- precompute logical placements
- query visible bands by binary search
- realize only a narrow slice
- support overscan
- keep extent independent from realized count

The current custom list and wrap controls already follow this model, and the same strategy maps directly to `ItemsRepeater` layouts.

## 13. Recommended Architectural Boundaries

### 13.1 What belongs in `Pretext.Uno`

- pure engine
- preparation and layout APIs
- no control dependencies
- no viewport logic
- no `UIElement` knowledge

### 13.2 What belongs in `Pretext.Uno.Controls`

- paragraph views
- scroll-host integration
- occlusion indices
- reusable layout hosts
- virtualized controls
- responsive geometry coordinators

### 13.3 What belongs in app/sample code

- visual styling
- theme constants
- content data
- product-specific mode decisions
- sample-specific compositions

The more the current samples converge on common primitives, the more of their infrastructure should move into `Pretext.Uno.Controls`.

## 14. Risks and Constraints

### 14.1 Font parity across Uno heads

The C# port uses Skia measurement as authority. Different Uno targets may still differ visually at the text rendering layer. A reusable UI system should assume:

- geometry is stable within a given rendering head
- exact cross-head pixel identity is not guaranteed

### 14.2 Locale and segmentation fidelity

The TS engine uses `Intl.Segmenter`. The C# port uses local tokenization logic. That is good enough for the current parity surface, but a future editor-grade UI system may want richer locale integration.

### 14.3 Editing surfaces are harder than read-mostly surfaces

Pretext is strongest for:

- static text
- read-mostly content
- content with infrequent edits
- responsive composition

Fully editable rich text introduces:

- caret geometry
- IME interaction
- incremental reflow around edits
- selection painting
- bidirectional caret rules

Those are possible, but should be treated as a separate subsystem.

### 14.4 Accessibility must not be sacrificed

The more the system moves toward custom absolute surfaces, the more carefully it must preserve:

- logical focus order
- automation peers
- semantic grouping
- readable text scaling behavior
- keyboard navigation

Whenever possible, the UI system should keep real Uno controls in the tree rather than flattening everything into drawing primitives.

## 15. Testing Strategy

The UI system should be validated at three layers.

### 15.1 Engine parity

Already present:

- xUnit parity tests for the C# engine against the TS test surface

### 15.2 Geometry contract tests

Needed for controls:

- width-to-height contract tests
- placement consistency tests
- viewport-range query tests
- extent prediction tests

Examples:

- a note card at width 420 should have the same predicted height before and after a no-op arrange
- a list with known item heights should produce the same visible range for a given viewport

### 15.3 Visual snapshot tests

Needed for the UI layer:

- desktop wide/medium/narrow snapshots
- list virtualization snapshots
- editorial obstacle-routing snapshots
- rich fragment snapshots

These should validate stable geometry, not only pixel-perfect antialiasing.

## 16. Implementation Roadmap

### Phase 1: consolidate current primitives

- keep `PretextParagraphView`
- keep `VerticalOcclusionIndex`
- keep `UiRenderScheduler`
- keep `StretchScrollHost`
- extract shared geometry/cache interfaces

### Phase 2: introduce reusable layout hosts

- add `PretextAbsolutePanel`
- add `PretextResponsiveShell`
- add shared prepared-text catalog

### Phase 3: promote virtualization into framework-style layouts

- add `PretextWrapLayout : VirtualizingLayout`
- add `PretextListLayout : VirtualizingLayout`
- optionally add `PretextMasonryLayout : VirtualizingLayout`

### Phase 4: add non-virtualized pluggable layouts

- add `PretextNonVirtualizingLayout : NonVirtualizingLayout`
- integrate with `LayoutPanel`

### Phase 5: richer inline and document surfaces

- add inline fragment renderer
- add obstacle maps
- add richer paragraph streaming infrastructure

## 17. Recommended Design Rules

1. Treat Pretext as the source of text geometry truth, not the live visual tree.
2. Keep Uno controls as semantic and interactive leaves.
3. Prefer one scroll owner per page section.
4. Keep layout state explicit and separate from visuals.
5. Reuse visuals aggressively.
6. Use Pretext to drive responsive panel geometry, not just paragraph height.
7. Prefer `LayoutPanel` and `ItemsRepeater` when building reusable control-library infrastructure.
8. Prefer `Canvas`-backed retained surfaces when building editorial or scene-like compositions.
9. Keep adaptive state logic simple and let Pretext handle the hard text math.
10. Validate geometry contracts in tests, not only screenshots.

## 18. Final Position

Pretext should be understood inside Uno as a text-aware geometry engine.

Its value is not limited to rendering paragraphs faster. Its real value is that it makes text predictable early enough to build:

- custom layouts
- responsive shells
- non-uniform virtualized lists
- wrap panels
- editorial compositions
- rich card systems
- large text-heavy application surfaces

The correct long-term direction is a layered system:

- `Pretext.Uno` for text preparation and line layout
- `Pretext.Uno.Controls` for reusable geometry, viewport, and control primitives
- Uno framework extension points such as `Panel`, `LayoutPanel`, and `ItemsRepeater` for final integration

That combination can support a comprehensive UI system where text-heavy interfaces remain accurate, responsive, and fast without depending on repeated live measurement of the visual tree.
