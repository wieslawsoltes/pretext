# Pretext Technical Specification

This document describes the current Pretext architecture across:

- the TypeScript reference engine in `src/`
- the C# port in `Uno/PretextSamples/Pretext/`
- the Uno sample browser in `Uno/PretextSamples/PretextSamples/`

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

### Uno sample browser

| Responsibility | File |
| --- | --- |
| Shared sample host and viewport projection | `Uno/PretextSamples/PretextSamples/Samples/SampleHostControls.cs` |
| Sample shell helpers and obstacle geometry | `Uno/PretextSamples/PretextSamples/Samples/SampleInfrastructure.cs` |
| Original Pretext demo views | `Uno/PretextSamples/PretextSamples/Samples/SampleViews.cs` |
| Justification comparison demo | `Uno/PretextSamples/PretextSamples/Samples/JustificationComparisonSampleView.cs` |

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

This is semantically clean and now follows the same broad optimization shape as the TypeScript engine. The C# port computes `SimpleLineWalkFastPath`, branches into simple and general walkers, and keeps the rich text materialization work off the hot count path.

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

The remaining differences are narrower:

- the C# port uses desktop-local Skia measurement instead of browser DOM/canvas calibration
- browser-specific measurement shims are intentionally absent
- the C# implementation stays optimized for framework-local determinism rather than browser-oracle fidelity

In practice, the port is semantically close to the TypeScript engine and preserves the same major hot-path structure, while the TypeScript implementation remains the stricter browser-accuracy reference.

## 6. Uno sample browser architecture

The remaining Uno integration is intentionally narrow: it exists to host the original Pretext demos, not to provide a generalized UI-layout framework.

### 6.1 Shared sample host

`StretchScrollHost` wraps page content in a `ScrollViewer` with a content host that always stretches to the viewport width.

It provides:

- a shared page scroll owner
- a consistent content width
- viewport-to-local coordinate projection via `TryGetLocalViewportBounds(...)`

These helpers now live in `SampleHostControls.cs` as sample-local infrastructure rather than a reusable controls library.

### 6.2 Render coalescing

`UiRenderScheduler` coalesces repeated invalidations into a single dispatcher pass.

This keeps resize, scroll, drag, and animation events from causing redundant rerender work in the heavier demos.

### 6.3 Original demo surfaces

The original Pretext demos that remain ported in Uno are:

- accordion
- bubbles
- masonry
- rich text
- dynamic layout
- editorial engine
- justification comparison
- variable typographic ASCII

They use Pretext in the same general way as the TypeScript demos:

- prepare text once
- reflow it arithmetically as widths or obstacle geometry change
- keep page composition in userland instead of asking the framework to discover text height after the fact

### 6.4 Sample-specific helpers

Some demo pages still use sample-local pooling and viewport helpers inside `SampleViews.cs` and `SampleInfrastructure.cs`.

That code is intentionally treated as demo implementation detail, not shared platform abstraction:

- masonry predicts card heights before placement
- editorial engine routes lines around live obstacles
- dynamic layout fits title and body slots against changing geometry
- justification comparison materializes custom columns from Pretext line walks

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
- sample-local visual reuse in the heavier demos

### 7.4 Streaming geometry APIs

`walkLineRanges(...)` and `layoutNextLine(...)` are performance features, not just convenience APIs.

They let callers:

- route text around obstacles
- compute shrinkwrap or masonry heights
- stop early
- avoid building full paragraph strings when geometry alone is sufficient

### 7.5 Bounded live visuals in demos

The heavier Uno demos still keep live visuals bounded:

- masonry reuses a limited card pool
- editorial engine reuses text and orb layers
- dynamic layout reuses logo and paragraph surfaces

That keeps the sample browser responsive without carrying a separate generalized controls framework in the port.

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
   - C# uses a desktop-local profile backed by Skia measurement rather than browser runtime detection.

2. Measurement authority
   - TypeScript includes a DOM-vs-canvas correction shim.
   - C# treats Skia measurement as authoritative.

3. Rendering environment
   - TypeScript is validated directly against browser layout behavior.
   - C# is validated against the ported invariant tests and the Uno sample surfaces, not against a live browser rendering engine.

These differences do not invalidate the port. They describe where semantic parity is already strong and where browser-oracle fidelity still differs by design.

## 10. Practical guidance for future work

When extending Pretext, keep these rules in mind:

- put script-specific break-policy fixes in analysis, not in the line walker
- keep `layout(...)` free of measurement and string construction
- only compute rich metadata on the rich path
- treat `walkLineRanges(...)` and `layoutNextLine(...)` as first-class APIs
- prefer sample-local pooling and absolute positioning only when a demo actually needs it
- if a feature needs exact browser parity, validate it against the TypeScript engine first
- if a feature needs cross-platform Uno stability, prefer deterministic Skia-backed behavior over browser quirks

## 11. Summary

Pretext is not primarily a text renderer. It is a high-performance text layout engine that precomputes measurement and break metadata so applications can do:

- fast relayout on resize
- predicted text heights
- custom obstacle-aware page composition
- userland editorial composition
- browser- or framework-local demo rendering without repeated live text measurement

The TypeScript implementation is the browser-accuracy reference. The C# port preserves the same public model and most of the same break semantics, then the Uno sample browser uses those prepared results to drive the original demo set without relying on built-in text measurement as the source of truth.
