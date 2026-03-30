# Pretext Technical Specification

This document describes the current Pretext architecture across:

- the TypeScript reference engine in `src/`
- the C# port in `Uno/PretextSamples/Pretext/`
- the Uno integration layer in `Uno/PretextSamples/Pretext.Uno.Controls/`

It is an internal implementation document, not a public API guide. Use `README.md` for public usage examples and `DEVELOPMENT.md` for the current verification workflow.

## 1. Design goals

Pretext is designed around a strict separation between:

1. `prepare(...)`: analyze and measure text once
2. `layout(...)`: reflow the measured result many times using pure arithmetic

The central problem it solves is that direct UI-tree or DOM measurement is too expensive for hot layout paths. In the browser, `getBoundingClientRect`, `offsetHeight`, and similar APIs force synchronous layout. In UI frameworks, repeated control measurement and templated layout introduce similar costs.

Pretext avoids those costs by:

- normalizing and segmenting text up front
- measuring stable units once per font
- caching those measurements aggressively
- using arithmetic-only line walking during resize and custom layout
- exposing low-level streaming APIs so higher-level layouts can avoid materializing full paragraphs

The engine is optimized for common app text:

- `white-space: normal`
- `overflow-wrap: break-word`
- `word-break: normal`
- `line-break: auto`

It also has an explicit `pre-wrap` mode for editor-like input and preserved whitespace.

## 2. Repository map

### TypeScript reference engine

| Responsibility | File |
| --- | --- |
| Public API, prepare/layout surface, materialization | `src/layout.ts` |
| Text analysis, segmentation, merging, break policy | `src/analysis.ts` |
| Measurement, caching, engine profile, emoji correction | `src/measurement.ts` |
| Internal line walker | `src/line-break.ts` |
| Rich-path bidi metadata | `src/bidi.ts` |
| Durable reference tests | `src/layout.test.ts` |

### C# port

| Responsibility | File |
| --- | --- |
| Public API, shared types, cache control, test hooks | `Uno/PretextSamples/Pretext/PretextLayout.cs` |
| Text analysis and token merging | `Uno/PretextSamples/Pretext/PretextLayout.Analysis.cs` |
| Measurement via Skia, font parsing, segment cache | `Uno/PretextSamples/Pretext/PretextLayout.Measurement.cs` |
| Prepare/materialization pipeline | `Uno/PretextSamples/Pretext/PretextLayout.Preparation.cs` |
| Internal line walker | `Uno/PretextSamples/Pretext/PretextLayout.LineBreak.cs` |
| Rich-path bidi metadata | `Uno/PretextSamples/Pretext/PretextLayout.Bidi.cs` |

### Uno integration

| Responsibility | File |
| --- | --- |
| Paragraph rendering using pooled `TextBlock`s | `Uno/PretextSamples/Pretext.Uno.Controls/PretextParagraphView.cs` |
| Virtualized absolute-layout wrap surface | `Uno/PretextSamples/Pretext.Uno.Controls/PretextVirtualizedWrapPanel.cs` |
| Virtualized non-uniform list surface | `Uno/PretextSamples/Pretext.Uno.Controls/PretextVirtualizedListBox.cs` |
| Binary-search viewport occlusion index | `Uno/PretextSamples/Pretext.Uno.Controls/VerticalOcclusionIndex.cs` |
| Shared scroll host and viewport projection | `Uno/PretextSamples/Pretext.Uno.Controls/StretchScrollHost.cs` |
| Render coalescing | `Uno/PretextSamples/Pretext.Uno.Controls/UiRenderScheduler.cs` |
| Template-indirection data contracts | `Uno/PretextSamples/Pretext.Uno.Controls/VirtualizedTextContracts.cs` |

## 3. Core runtime model

At a high level, Pretext works like this:

```text
raw text
  -> whitespace normalization
  -> segmentation into typed runs
  -> script- and punctuation-specific preprocessing
  -> measurement and cached grapheme metadata
  -> PreparedText / PreparedTextWithSegments
  -> layout() / layoutWithLines() / layoutNextLine() / walkLineRanges()
```

Key properties of the model:

- prepared text is width-independent
- line height is supplied at layout time, not prepare time
- the fast path avoids string materialization
- the rich path exposes line text and cursors for manual rendering
- the streaming path can drive custom editorial and obstacle-aware layouts

## 4. TypeScript reference engine

### 4.1 Public prepared model

The TypeScript engine keeps the hot data in parallel arrays rather than object graphs. `PreparedCore` in `src/layout.ts` stores:

- `widths`
- `lineEndFitAdvances`
- `lineEndPaintAdvances`
- `kinds`
- `breakableWidths`
- `breakablePrefixWidths`
- `discretionaryHyphenWidth`
- `tabStopAdvance`
- `chunks`
- optional `segLevels`

Two public handle shapes sit on top of that:

- `PreparedText`: opaque fast-path handle
- `PreparedTextWithSegments`: rich handle exposing aligned `segments`

This split is deliberate. The hot path does not need exposed strings or bidi metadata, and the engine avoids making the main API accidentally depend on the current internal representation.

### 4.2 Analysis phase

The analysis phase is implemented in `src/analysis.ts`.

### 4.2.1 Whitespace normalization

Pretext supports two explicit whitespace modes:

- `normal`
- `pre-wrap`

In `normal` mode:

- tabs, newlines, carriage returns, and form feeds collapse into ordinary spaces
- repeated spaces collapse
- leading and trailing collapsible spaces are trimmed

In `pre-wrap` mode:

- ordinary spaces are preserved
- hard breaks are preserved
- CRLF is normalized to `\n`
- `\r` and `\f` become `\n`

### 4.2.2 Segment kinds

Each token is classified into one of eight break kinds:

- `text`
- `space`
- `preserved-space`
- `tab`
- `glue`
- `zero-width-break`
- `soft-hyphen`
- `hard-break`

This richer model is critical. It lets the line walker distinguish:

- collapsible spaces that hang and can disappear at line boundaries
- preserved spaces that must remain visible
- tabs that align to absolute tab stops
- glue that must remain visible and non-breaking
- discretionary hyphens that are invisible until chosen
- explicit forced breaks

### 4.2.3 Segmentation and merge passes

The TypeScript reference engine uses `Intl.Segmenter` for word segmentation and then applies a series of merge and carry passes. The important point is that most script-specific behavior lives in preprocessing, not in the line walker.

Representative passes in `src/analysis.ts`:

- merge closing punctuation onto the preceding word
- keep opening quotes attached to the following text
- keep escaped quote clusters together
- carry CJK closing-quote clusters as needed
- enforce kinsoku-like CJK boundary behavior
- preserve `NBSP`, `NNBSP`, and `WJ`-style glue
- preserve `ZWSP` as an explicit zero-width break opportunity
- merge URL-like runs
- split URL query boundaries more conservatively
- merge numeric and time-range runs
- split specific hyphenated numeric runs
- keep Arabic punctuation and punctuation-plus-mark clusters together
- repair `" " + combining marks` before Arabic text
- keep Myanmar medial/punctuation patterns together

The result is a segmentation model that tries to match browser line-breaking behavior closely without turning the hot path into a shaping engine.

### 4.2.4 Hard-break chunks

After segmentation, the analysis phase compiles `AnalysisChunk[]`.

Each chunk represents a line-walk region:

- `startSegmentIndex`
- `endSegmentIndex`
- `consumedEndSegmentIndex`

In `pre-wrap`, hard breaks split the prepared content into explicit chunks so the line walker can treat empty lines and forced boundaries correctly without re-scanning raw text.

### 4.3 Measurement phase

The measurement phase is implemented in `src/measurement.ts` and orchestrated from `measureAnalysis(...)` in `src/layout.ts`.

### 4.3.1 Measurement backend

The browser engine measures text with:

- `OffscreenCanvas` when available
- otherwise a normal canvas 2D context

This is important: `measureText` does not force layout reflow. That is the entire reason Pretext can move layout off the DOM hot path.

### 4.3.2 Cache model

The main measurement cache is:

```text
Map<font, Map<segment, SegmentMetrics>>
```

Measured facts are cached per font and segment, including:

- measured width
- whether the segment contains CJK
- emoji count
- grapheme widths
- grapheme prefix widths

The cache is shared across texts, so repeated words across the app become effectively free after the first measurement.

### 4.3.3 Browser engine profile

The TypeScript engine derives a browser-specific `EngineProfile` from the current runtime:

- `lineFitEpsilon`
- `carryCJKAfterClosingQuote`
- `preferPrefixWidthsForBreakableRuns`
- `preferEarlySoftHyphenBreak`

This is how the engine absorbs small browser differences without forking the whole algorithm.

### 4.3.4 Emoji correction

On some browser/font combinations, canvas emoji width is inflated relative to actual DOM layout. The browser engine compensates by:

1. measuring a reference emoji on canvas
2. measuring the same emoji once in hidden DOM
3. caching the correction per font
4. subtracting that correction per emoji grapheme

This is a browser-accuracy shim, not a general layout feature. It exists because browser text layout is the reference behavior for the TypeScript engine.

### 4.3.5 CJK measured expansion

CJK text is not treated as a single opaque run. During measurement, the engine expands measured CJK runs into smaller units that preserve:

- per-grapheme breakability
- kinsoku start/end constraints
- sticky punctuation behavior
- browser-specific closing-quote carry rules

### 4.3.6 Line-end fit vs line-end paint

The TypeScript engine stores both:

- `lineEndFitAdvances`
- `lineEndPaintAdvances`

That distinction is how it models hanging whitespace and discretionary hyphens correctly:

- some segments contribute to fit but not paint
- some contribute to paint but not fit
- tabs are special because their advance depends on the current line position

### 4.4 Line-breaking algorithm

The TypeScript line walker lives in `src/line-break.ts`.

### 4.4.1 Two execution modes

There are two internal walkers:

- a simple fast path for ordinary text with only `text`, `space`, and `zero-width-break` and no hard-break chunk complexity
- a general path for `pre-wrap`, tabs, preserved spaces, soft hyphens, and hard breaks

The simple path exists because the common case is still normal app text, and that path should remain extremely cheap.

### 4.4.2 High-level algorithm

For each line:

1. normalize the line start by skipping break-only prefixes as appropriate
2. walk segments left to right
3. maintain current width
4. track the latest legal break candidate
5. if the next segment fits, consume it
6. if it overflows:
   - break at the most recent legal candidate, or
   - break within the current segment at grapheme boundaries, or
   - apply discretionary soft-hyphen logic, or
   - emit a forced line in special cases like preserved whitespace and tabs

### 4.4.3 Overflow behavior

Important cases handled by the walker:

- trailing collapsible spaces hang past the edge and do not force an earlier break
- preserved spaces remain visible content
- tabs advance to the next absolute tab stop
- `ZWSP` creates a zero-width break candidate
- `SHY` creates a break candidate that paints a trailing hyphen only if chosen
- overlong word-like runs can break at grapheme boundaries
- chunk boundaries from hard breaks force line termination even when no text is present

### 4.4.4 Streaming and non-materializing APIs

The TypeScript engine exposes three important rich-path variants:

- `walkLineRanges(...)`: geometry only, no text materialization
- `layoutNextLine(...)`: step one line at a time from a cursor
- `layoutWithLines(...)`: batch materialized lines

This is one of the most important design choices in the library. The engine is not just a paragraph height counter; it is also a streaming reflow primitive for userland layout.

### 4.4.5 Text materialization

The TypeScript engine keeps materialization out of the hot path. `layout()` counts lines only. Rich APIs materialize text later and cache grapheme slices in a `WeakMap`.

That is why line text construction can support:

- visible soft hyphens when chosen
- partial grapheme-range materialization
- exact start/end cursors

without forcing those costs onto `layout(...)`.

### 4.5 Bidi metadata

`src/bidi.ts` computes simplified bidi embedding levels. This metadata is:

- computed only for `prepareWithSegments()`
- attached as segment-level metadata
- not consumed by the line-breaking engine itself

This keeps the core layout path simple while still giving custom renderers enough information for mixed-direction text.

## 5. C# port architecture

The C# port in the `Pretext` project, using the `Pretext.Uno` namespace, preserves the TypeScript API shape and most of the semantic behavior, but it is not a literal transliteration of every low-level optimization.

### 5.1 Public API parity

The C# surface mirrors the TypeScript surface closely:

- `Prepare(...)`
- `PrepareWithSegments(...)`
- `ProfilePrepare(...)`
- `Layout(...)`
- `LayoutWithLines(...)`
- `LayoutNextLine(...)`
- `WalkLineRanges(...)`
- `ClearCache()`
- `SetLocale(...)`

It also preserves the same conceptual types:

- `PreparedText`
- `PreparedTextWithSegments`
- `LayoutCursor`
- `LayoutLine`
- `LayoutLineRange`
- `PrepareProfile`

### 5.2 Analysis phase in C#

The ported analysis phase lives in `PretextLayout.Analysis.cs`.

It preserves the same high-level responsibilities:

- whitespace normalization
- text-element segmentation
- segment kind classification
- punctuation and quote merging
- script-specific carry rules
- URL and numeric-run preprocessing
- Arabic and Myanmar special handling

The main implementation difference is segmentation strategy:

- TypeScript uses `Intl.Segmenter`
- C# uses `StringInfo.GetTextElementEnumerator(...)` plus Unicode-category heuristics

This makes the C# port deterministic and framework-local, but it also means it is not currently locale-driven in the same way as the browser engine. `SetLocale(...)` exists for API parity and cache invalidation, but the current C# tokenization path does not yet retarget a locale-specific segmenter.

### 5.3 Measurement phase in C#

The C# measurement backend lives in `PretextLayout.Measurement.cs`.

### 5.3.1 Skia backend

Instead of canvas, the port uses Skia:

- `SKFont`
- `SKTypeface`
- `SKFont.MeasureText(...)`

This makes measurement portable across Uno desktop and Skia-backed heads.

### 5.3.2 Font state cache

The C# port stores a `FontState` per font string. Each `FontState` contains:

- parsed font specification
- `SKFont`
- measured space width
- measured hyphen width
- tab stop advance
- a segment cache

The segment cache key is:

```text
MeasurementCacheKey(Text, Kind, IsBreakableRun)
```

The inclusion of `Kind` and `IsBreakableRun` is important. It prevents accidental cache collisions between visually identical text that participates differently in breaking semantics.

### 5.3.3 Prepared segment model

Instead of parallel arrays as the primary representation, the C# engine uses `PreparedSegment` objects internally. A prepared segment carries:

- `Text`
- `Kind`
- `IsBreakableRun`
- `Width`
- `Graphemes`
- optional `PrefixWidths`

This is simpler and more idiomatic in C#, but it is less allocation-tight than the TypeScript array model.

### 5.3.4 CJK and grapheme expansion

Like the TypeScript engine, the C# port:

- expands CJK runs into measured units
- stores grapheme slices for breakable runs
- precomputes prefix widths so sub-run widths can be calculated cheaply

### 5.3.5 Current measurement differences vs TypeScript

The main intentional differences are:

- browser user-agent detection is not ported; the C# `EngineProfile` is a fixed Chromium-flavored profile
- browser DOM emoji correction is not ported; Skia is the measurement authority
- font family fallback is normalized for portability, for example:
  - `sans-serif` and `system-ui` -> `Arial`
  - `serif` -> `Times New Roman`
  - `monospace` -> `Menlo`

These choices favor stable cross-platform measurement over exact browser-oracle reproduction.

### 5.4 Prepare pipeline in C#

The prepare pipeline is implemented in `PretextLayout.Preparation.cs`.

The flow is:

1. get `FontState`
2. analyze tokens
3. expand tokens into measured prepared segments
4. compute fit and paint advances
5. compute breakable-width collections for the rich path
6. build hard-break chunks
7. optionally compute bidi levels
8. return either `PreparedText` or `PreparedTextWithSegments`

As in TypeScript, line height is not baked into the prepared handle.

### 5.5 Line walker in C#

The C# line walker lives in `PretextLayout.LineBreak.cs`.

It follows the same semantic model as the TypeScript walker:

- normalize line start
- walk segments left to right
- keep the best legal break candidate
- honor hanging spaces
- honor preserved spaces
- honor tabs and tab-stop snapping
- honor zero-width break opportunities
- honor discretionary soft hyphens
- break inside breakable runs at grapheme boundaries when necessary

### 5.5.1 Unified stepping model

The C# port currently uses one main stepping function:

- `TryStepLine(...)`

`Layout(...)`, `LayoutWithLines(...)`, `LayoutNextLine(...)`, and `WalkLineRanges(...)` all build on that same stepper.

This is semantically clean, but it is not identical to the TypeScript optimization structure. The TypeScript engine has a specialized simple fast path and a separate general walker. The C# port computes `SimpleLineWalkFastPath`, but the current line-walk implementation does not branch into a separate optimized execution path yet.

### 5.5.2 Visible end vs consumed end

The C# port models both:

- the visible end of the current line
- the consumed end cursor that the next line should continue from

That distinction is necessary for:

- hanging spaces
- preserved trailing whitespace
- soft-hyphen line endings
- hard-break chunk consumption

### 5.5.3 Materialization

Line text materialization is done by `BuildLineText(...)`, which:

- skips non-painting break markers like hard breaks and zero-width breaks
- inserts `-` when a discretionary soft hyphen is chosen
- slices graphemes for partial overlong-word breaks

### 5.6 Current C# performance profile

The C# port preserves the big architectural win:

- no UI-tree measurement in the layout phase
- no Skia measurement during relayout after `Prepare(...)`

However, it is not yet identical to the TypeScript micro-optimization level. Current differences:

- internal representation is object-heavy compared with TS parallel arrays
- the line walker is unified rather than split into simple and general hot paths
- chunk metadata is preserved but not exploited as aggressively as in TS
- locale selection is stored but not currently used to drive segmentation
- browser-specific measurement shims are intentionally absent

In practice, the port is semantically close and fast enough for the current Uno samples, but the TypeScript engine still remains the stricter reference for browser-accuracy and hot-path specialization.

## 6. Uno integration architecture

The Uno integration does not replace the platform text renderer. Instead, it uses Pretext to externalize layout decisions and then feeds those decisions into retained WinUI/Uno controls.

This is a crucial distinction:

- Pretext computes text geometry
- Uno `TextBlock` and related controls still paint the text

That preserves native rendering and accessibility while avoiding expensive control-tree measurement loops.

### 6.1 Stretch scroll host

`StretchScrollHost` wraps page content in a `ScrollViewer` with a content host that always stretches to the viewport width.

It provides:

- a shared page scroll owner
- a consistent content width
- viewport-to-local coordinate projection via `TryGetLocalViewportBounds(...)`

That viewport projection is the basis for custom occlusion and virtualization.

### 6.2 Render coalescing

`UiRenderScheduler` coalesces repeated invalidations into a single dispatcher pass.

This keeps resize, scroll, and control events from causing redundant rerender work. Multiple upstream events collapse into one render action.

### 6.3 Paragraph rendering

`PretextParagraphView` is the simplest reusable integration primitive.

It:

- calls `LayoutWithLines(...)`
- keeps a pool of `TextBlock`s, one per visible line
- reuses those `TextBlock`s between renders
- places them absolutely on a `Canvas`

This means paragraph layout work happens in Pretext, not in a nested `TextBlock` measure/arrange pipeline.

### 6.4 Absolute-layout virtualization

The high-throughput virtual controls are:

- `PretextVirtualizedWrapPanel`
- `PretextVirtualizedListBox`

Both intentionally avoid built-in Uno items panels and virtualization panels for their core layout behavior.

### 6.4.1 Common strategy

The common strategy is:

1. use Pretext to compute exact text-driven item heights
2. build absolute placements for the whole logical surface
3. compile those placements into vertical bands
4. query the visible band range with binary search
5. reuse a small pool of visuals for only the visible items

The live UI cost therefore scales with visible content, not dataset size.

### 6.4.2 Vertical occlusion index

`VerticalOcclusionIndex` stores vertical bands and answers:

- first band whose bottom is after `top`
- first band whose top is after `bottom`

The queries are binary searches, so viewport lookup is `O(log n)` in band count.

This is the reusable occlusion primitive that lets the wrap panel and list box render only the items intersecting the current viewport plus overscan.

### 6.4.3 Wrap panel

`PretextVirtualizedWrapPanel` lays out non-uniform tiles by arithmetic:

- each tile template already has a Pretext-derived body height
- the control computes row placements across the available width
- each row becomes a vertical band
- only the rows intersecting the viewport are realized

Important optimization: the data source uses `WrapTileTemplate` plus `templateIndices`. Many logical items can point at a smaller template set, so measurement is amortized.

### 6.4.4 List box

`PretextVirtualizedListBox` does the same for a vertically stacked non-uniform list:

- body height comes from `PretextLayout.Layout(...)`
- title and fixed chrome are added arithmetically
- every item gets a known absolute rectangle
- every item rectangle becomes a `VerticalBand`
- only the visible range is realized

Unlike a normal list control, item height is known before live UI realization because Pretext predicts it from text and font settings.

### 6.4.5 Why this is fast

The list and wrap controls avoid the classic expensive pattern:

- create many controls
- let the framework measure them
- read those measurements
- reposition them

Instead they do:

- measure template text once
- compute all placements arithmetically
- binary-search the viewport
- reuse a bounded visual pool

For large datasets, this is the difference between scaling with total item count and scaling with visible item count.

### 6.5 Responsive and editorial samples

The sample app uses the same primitives for more complex surfaces:

- responsive app shells
- editorial obstacle-aware layouts
- dynamic manual text routing
- side-by-side justification demos

The common pattern is:

- compute panel or obstacle geometry first
- call Pretext with the available text width per slot
- place normal Uno controls absolutely
- avoid asking the visual tree how tall text became after the fact

This is the reason the samples can use built-in Uno controls while still showcasing manual, app-defined layout behavior.

## 7. Why Pretext is fast

Pretext gets most of its performance from architecture, not from one micro-optimization.

### 7.1 Prepare once, layout many

The most important optimization is moving expensive work into `prepare(...)`:

- segmentation
- punctuation policy
- script-specific preprocessing
- text measurement
- grapheme subdivision

That turns relayout into arithmetic on cached data.

### 7.2 No live-tree measurement on the hot path

In the browser reference engine:

- no DOM reads during layout
- no canvas calls during layout

In Uno:

- no UI-tree text measurement during layout
- no live control-size probing to discover text height

### 7.3 Cache reuse

Caches exist at multiple levels:

- analysis segmenter reuse
- measurement cache per font
- grapheme width reuse
- prepared-handle reuse across widths
- pooled visuals in Uno controls
- banded occlusion reuse across scroll passes

### 7.4 Streaming geometry APIs

`walkLineRanges(...)` and `layoutNextLine(...)` are performance features, not just convenience APIs.

They let callers:

- route text around obstacles
- compute shrinkwrap or masonry heights
- stop early
- avoid building full paragraph strings when geometry alone is sufficient

### 7.5 Bounded live visuals

The Uno integration layer keeps the number of live controls near the number of visible lines or items.

That is why the 100k-item wrap and list samples are viable:

- logical dataset size can be large
- measured template count can stay small
- live visual count stays bounded

## 8. Validation and parity

The durable unit-test surface in the TypeScript engine is `src/layout.test.ts`.

That suite has been ported to xUnit under:

- `Uno/PretextSamples/Pretext.Uno.Tests/PretextLayoutParityTests.cs`
- `Uno/PretextSamples/Pretext.Uno.Tests/PretextLayoutParityTests.Prepare.cs`
- `Uno/PretextSamples/Pretext.Uno.Tests/PretextLayoutParityTests.Layout.cs`

The xUnit port validates:

- whitespace normalization
- glue handling
- zero-width breaks
- soft hyphens
- punctuation carry rules
- Arabic and Myanmar special cases
- URL-like and numeric-run preprocessing
- line-count monotonicity
- hanging whitespace
- `pre-wrap` behavior
- `layoutNextLine(...)` parity with `layoutWithLines(...)`
- `walkLineRanges(...)` parity with materialized lines

The C# tests use `SetMeasurementOverrideForTests(...)` so widths are deterministic and independent of host font availability.

## 9. Known architectural differences between TS and C#

These are the most important current differences:

1. Browser profile detection
   - TypeScript adapts behavior by runtime browser.
   - C# currently uses a fixed Chromium-flavored profile.

2. Locale-sensitive segmentation
   - TypeScript uses `Intl.Segmenter` and can retarget via `setLocale(...)`.
   - C# keeps `SetLocale(...)` in the API, but the current tokenizer is still based on `StringInfo` plus Unicode-category heuristics.

3. Emoji correction
   - TypeScript includes a DOM-vs-canvas correction shim.
   - C# treats Skia measurement as authoritative.

4. Hot-path specialization
   - TypeScript has explicit simple and general walkers.
   - C# currently uses one main stepper.

5. Internal representation
   - TypeScript is optimized around parallel arrays and lazy line-text caches.
   - C# uses richer objects and read-only collections for clarity and portability.

These differences do not invalidate the port. They describe where semantic parity is already strong and where low-level performance or browser-oracle fidelity still differs.

## 10. Practical guidance for future work

When extending Pretext, keep these rules in mind:

- put script-specific break-policy fixes in analysis, not in the line walker
- keep `layout(...)` free of measurement and string construction
- only compute rich metadata on the rich path
- treat `walkLineRanges(...)` and `layoutNextLine(...)` as first-class APIs
- prefer pooled visuals and absolute positioning for large custom surfaces
- if a feature needs exact browser parity, validate it against the TypeScript engine first
- if a feature needs cross-platform Uno stability, prefer deterministic Skia-backed behavior over browser quirks

## 11. Summary

Pretext is not primarily a text renderer. It is a high-performance text layout engine that precomputes measurement and break metadata so applications can do:

- fast relayout on resize
- predicted text heights
- custom obstacle-aware page composition
- non-uniform virtualization at large scale
- responsive absolute layouts with built-in controls

The TypeScript implementation is the browser-accuracy reference. The C# port preserves the same public model and most of the same break semantics, then the Uno integration layer turns those prepared results into retained, pooled, viewport-aware UI. That combination is what makes the Uno sample set able to show complex editorial and large-data layouts without relying on built-in panel measurement as the source of truth.
