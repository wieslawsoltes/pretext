# Pretext Locale Segmentation Specification and Implementation Plan

Status: draft design and rollout plan  
Audience: Pretext contributors, Uno integration maintainers, Unicode/text-layout implementers  
Scope: adding real locale-sensitive segmentation behavior to the C# `Pretext.Uno` port while preserving the current fast-path design

This document supplements:

- `Uno/PRETEXT_TECHNICAL_SPEC.md`
- `Uno/PRETEXT_UNO_UI_SYSTEM_SPEC.md`
- `Uno/PRETEXT_TWO_PASS_LAYOUT_FRAMEWORK_ANALYSIS.md`

It focuses specifically on the current locale gap in the C# port.

## 1. Problem Statement

Today, the TypeScript engine and the C# port do not behave the same way with respect to locale-sensitive word segmentation.

### 1.1 TypeScript today

The TypeScript reference engine uses:

- `Intl.Segmenter(locale, { granularity: "word" })` for word segmentation in `src/analysis.ts`
- `Intl.Segmenter(undefined, { granularity: "grapheme" })` for grapheme segmentation in `src/measurement.ts` and `src/layout.ts`

`setLocale(...)` in the TypeScript engine is real behavior, not only cache invalidation:

- it stores the locale
- recreates the shared word segmenter
- changes how the next `prepare(...)` call is segmented

That means locale affects the analysis phase before merge and carry passes run.

### 1.2 C# today

The C# port currently:

- stores `_locale`
- clears caches when `SetLocale(...)` is called
- does **not** actually use `_locale` during tokenization

The current analysis path is framework-local and deterministic:

- it enumerates text elements via `StringInfo.GetTextElementEnumerator(...)`
- classifies each element by Unicode-category heuristics
- merges adjacent text elements and then runs the existing preprocessing passes

This means:

- the C# port is stable and local
- the C# port is not yet locale-driven the way the browser engine is
- `SetLocale(...)` currently exists mostly for API parity and cache invalidation

### 1.3 What is missing

What is missing is **not** a big block of TypeScript logic that was accidentally skipped.

The missing piece is the runtime primitive that the TypeScript engine gets from `Intl.Segmenter`:

- locale-sensitive word boundary segmentation
- `isWordLike` metadata attached to those boundaries
- correct behavior for scripts where dictionary-like segmentation matters

So this is a genuine implementation task, not a mechanical source-port task.

## 2. Why This Matters

Locale-sensitive segmentation matters most for:

- Thai
- Lao
- Khmer
- Myanmar
- mixed-script app text where word-like runs and punctuation boundaries depend on more than simple Unicode categories

It also matters for correctness discipline:

- the public API exposes `SetLocale(...)`
- the TypeScript reference engine uses locale materially
- the C# port should not silently imply stronger parity than it actually has

Without a locale-sensitive segmenter, the C# engine still works, but its segmentation center of gravity is different from the reference implementation.

## 3. Goals

### 3.1 Primary goals

1. Make `SetLocale(...)` materially affect future C# `Prepare(...)` calls.
2. Keep the current public C# API unchanged.
3. Preserve the existing prepare/layout split and hot-path discipline.
4. Keep the existing preprocessing rules in `PretextLayout.Analysis.cs`.
5. Make locale-aware segmentation pluggable by runtime/backend.
6. Keep a deterministic fallback when a locale-aware provider is unavailable.

### 3.2 Secondary goals

1. Reuse existing Unicode infrastructure when possible.
2. Add explicit parity tests for locale-sensitive segmentation.
3. Avoid tying the engine permanently to a single UI framework head.

### 3.3 Non-goals

This work is not intended to:

- replace the current grapheme segmentation approach
- replace Skia measurement logic
- introduce shaping-aware layout
- solve every browser- and engine-specific line-break nuance
- depend on the live UI tree for segmentation

## 4. Current Architecture Constraint

Pretext is deliberately designed so that:

- `prepare(...)` does expensive semantic work
- `layout(...)` is pure arithmetic

Locale-sensitive segmentation must stay in the prepare phase.

It must not:

- leak locale branching into the line walker
- introduce runtime DOM or UI-tree reads
- make `layout()` depend on global platform state

The locale design should therefore look like:

```text
raw text
  -> locale-aware word segmentation provider
  -> split by break kinds
  -> merge/carry/preprocess passes
  -> measurement
  -> PreparedText
```

## 5. What Should Remain Unchanged

The following parts of the C# engine should remain semantically unchanged:

- whitespace normalization
- segment break kinds
- merge rules for punctuation and sticky clusters
- Arabic preprocessing fixes
- URL/query/numeric-run rules
- CJK boundary carry rules
- Myanmar-specific keep/split rules already in the port
- measurement and grapheme-width caching
- line-break and streaming APIs

The locale-sensitive segmenter should replace only the **initial word-boundary seed**, not the later policy passes.

## 6. Recommended Architecture

## 6.1 Add a word segmentation abstraction

Introduce an internal abstraction in `Pretext.Uno`:

```csharp
internal interface IWordSegmentationProvider
{
    IReadOnlyList<WordSegment> Segment(string text, string? locale);
}

internal readonly record struct WordSegment(
    string Text,
    bool IsWordLike,
    int Start);
```

This mirrors the information the TS engine gets from `Intl.Segmenter`:

- `segment`
- `isWordLike`
- `index`

The rest of the analysis pipeline can then stay structurally similar to the current one.

## 6.2 Keep the current heuristic path as a fallback provider

The current C# path should become:

- `HeuristicWordSegmentationProvider`

It would:

- continue using `StringInfo.GetTextElementEnumerator(...)`
- continue using Unicode-category heuristics for `IsWordLike`
- continue merging adjacent compatible elements

This preserves:

- deterministic fallback behavior
- compatibility on heads where a richer provider is unavailable
- a clear correctness baseline

## 6.3 Add a locale-aware provider

Add a second implementation:

- `LocaleWordSegmentationProvider`

This provider should be the preferred path when available.

### Required behavior

For a given `text` and `locale`, it must return:

- locale-sensitive word boundaries
- `isWordLike` metadata for each boundary unit
- stable start offsets in UTF-16 code units

Those values must be usable by the existing `splitSegmentByBreakKind`-style logic in the C# analysis pipeline.

## 6.4 Provider selection

Add an internal resolver:

```csharp
internal static class WordSegmentationProviderResolver
{
    public static IWordSegmentationProvider Resolve();
}
```

Recommended policy:

1. explicit test override, if present
2. platform/provider-specific locale-aware implementation, if available
3. heuristic fallback provider

This keeps the engine:

- testable
- deterministic under fallback
- portable

## 7. Backend Options

There are several implementation options.

### 7.1 Option A: keep the heuristic tokenizer only

Pros:

- zero new dependencies
- zero platform-specific work
- fully deterministic

Cons:

- does not close the locale gap
- does not make `SetLocale(...)` behaviorally meaningful
- leaves Thai/Lao/Khmer/Myanmar parity weaker than the TS design

Conclusion:

- not sufficient if the goal is real locale parity

### 7.2 Option B: rely on `StringInfo` and `CultureInfo`

Pros:

- entirely in-box

Cons:

- `StringInfo` is grapheme-oriented, not locale-aware word segmentation
- does not provide browser-like word boundaries
- does not provide `isWordLike`

Conclusion:

- insufficient

### 7.3 Option C: add a dedicated ICU word-break provider

Pros:

- closest conceptual match to `Intl.Segmenter`
- strong Unicode coverage
- already aligned with how Uno’s Skia text stack handles Unicode boundary work

Cons:

- implementation complexity
- backend availability differences across platforms
- packaging and initialization work

Conclusion:

- the best long-term architecture

### 7.4 Option D: per-platform native word-break APIs

Pros:

- may use platform-native language services

Cons:

- fragmented behavior
- different semantics across heads
- harder to keep deterministic
- more complex test matrix

Conclusion:

- acceptable only as backend-specific adapters under the common provider interface

### 7.5 Option E: external Unicode segmentation library

Pros:

- potentially cross-platform

Cons:

- new dependency and maintenance burden
- may still diverge from `Intl.Segmenter`
- may not expose the exact `isWordLike` shape we want

Conclusion:

- possible, but not the first recommendation

## 8. Recommended Technical Direction

The recommended direction is:

1. create a provider abstraction
2. preserve the current heuristic tokenizer as fallback
3. add an ICU-backed locale-aware provider
4. use that provider in analysis before all current merge passes
5. expand tests from “locale reset does not break later prepares” to real locale-sensitive parity

## 9. Reuse Opportunity in Uno

There is a concrete reuse opportunity in the Uno codebase.

Uno’s Skia text stack already contains ICU boundary iteration infrastructure in:

- `src/Uno.UI/UI/Xaml/Documents/UnicodeText.ICU.skia.cs`
- `src/Uno.UI/UI/Xaml/Documents/UnicodeText.skia.cs`

In particular:

- `UnicodeText.skia.cs` uses ICU break iterators in `AppendBoundaries(...)`
- the current Uno code passes a locale pointer derived from `CultureInfo.CurrentUICulture.Name`
- Uno already handles ICU loading and symbol resolution on supported Skia heads

This means there is already proven local code for:

- ICU loading
- boundary iterator calls
- locale-based boundary extraction

### Important constraint

That Uno implementation is:

- internal
- tied to Uno.UI internals
- Skia-oriented

So `Pretext.Uno` should **not** directly depend on internal Uno text formatting types.

Instead, we should do one of the following:

### Preferred long-term path

Extract or duplicate the minimal ICU boundary-iterator layer into a reusable internal helper for `Pretext.Uno`.

### Acceptable short-term path

Vendor a small ICU boundary adapter into `Pretext.Uno` based on the Uno implementation shape, but keep it isolated behind the segmentation provider interface.

## 10. Detailed Design

## 10.1 New analysis entry flow

Change the analysis flow from:

```text
normalize
-> BuildInitialTokens via text-element enumeration
-> BuildMergedTokens
```

to:

```text
normalize
-> SegmentWords(locale-aware provider)
-> SplitProviderSegmentsByBreakKind
-> BuildMergedTokens
```

The provider should only define the initial segmentation boundaries.

The existing C# merge/carry passes should still run after that.

## 10.2 New internal data shape

Recommended internal shape:

```csharp
internal readonly record struct ProviderSeedSegment(
    string Text,
    bool IsWordLike,
    int Start);
```

Then:

```csharp
private static List<AnalysisToken> BuildInitialTokens(
    string text,
    WhiteSpaceProfile whiteSpaceProfile,
    IReadOnlyList<ProviderSeedSegment> seedSegments)
```

This lets the later break-kind split preserve the existing eight-kind model.

## 10.3 Locale storage

Keep the current public API:

```csharp
public static void SetLocale(string? locale = null)
```

But make `_locale` feed into provider calls during prepare.

The locale should affect only **future** prepare calls, matching the TS design.

## 10.4 Cache invalidation

`SetLocale(...)` should continue to invalidate caches, but the cache story needs to be clarified.

### Required

- invalidate any future analysis-seed caches
- invalidate any prepared-text caches if such caches are added later

### Not required

- font measurement caches do not fundamentally depend on locale

Today `ClearCache()` clears `FontStates`. That is safe but broader than necessary for locale changes.

Recommended future split:

- `ClearMeasurementCache()`
- `ClearAnalysisCache()`
- `ClearAllCaches()`

Public API can stay as `ClearCache()`, but internally the locale path should be able to invalidate analysis state without conceptually claiming that measurement is locale-dependent.

## 10.5 Grapheme segmentation

Do **not** treat grapheme segmentation as part of this locale project.

Why:

- TS grapheme segmentation uses `Intl.Segmenter(..., { granularity: "grapheme" })`
- grapheme boundaries are not locale-driven in the same way as word segmentation
- the current C# `StringInfo` path is already the correct design center for the port’s grapheme use

So this locale work should focus on **word segmentation only**.

## 10.6 `isWordLike` semantics

Browser `Intl.Segmenter` exposes `isWordLike`.

The ICU/provider path must supply equivalent information.

If the backend cannot expose it directly, derive it conservatively using Unicode-category logic over each returned boundary segment.

That fallback is acceptable because:

- the hard part is the locale-sensitive boundaries
- `isWordLike` can be derived more reliably once the boundaries are right

## 11. Platform Strategy

## 11.1 Uno Skia/Desktop first

The first real locale-aware implementation should target the environment we already use for validation:

- Uno desktop / Skia

Reasons:

- Uno already has ICU infrastructure there
- this is the current verified sample/build environment
- it closes the biggest practical gap first

## 11.2 Browser/Wasm option

If `Pretext.Uno` is later used in browser-hosted Uno heads, a browser-specific provider could use JS interop to call `Intl.Segmenter` directly.

That would be the closest possible parity path for WebAssembly/browser execution.

This is optional for phase 1.

## 11.3 Non-Skia native heads

For iOS/Android/macOS/native heads, backend-specific providers are acceptable if they stay behind the shared interface.

The design should not require all heads to ship the same backend on day one.

## 11.4 Fallback behavior

If no locale-aware provider is available:

- use the current heuristic provider
- keep behavior deterministic
- do not throw

This preserves current library robustness.

## 12. Public API Position

The public API does **not** need to change for phase 1.

Keep:

- `SetLocale(...)`
- `Prepare(...)`
- `PrepareWithSegments(...)`
- `ClearCache()`

Add internal-only hooks if needed:

- `SetWordSegmentationProviderForTests(...)`
- `ResetWordSegmentationProviderForTests()`

This will make provider-level tests precise without exposing backend selection publicly before we need to.

## 13. Test Plan

The current parity suite is not enough. It verifies that locale reset does not disturb later prepares, but it does not verify that locale actually changes segmentation.

We need three layers of tests.

## 13.1 Unit tests for provider behavior

Add provider-specific tests for:

- Thai segmentation
- Lao segmentation
- Khmer segmentation
- Myanmar segmentation
- mixed Latin + Southeast Asian text
- fallback behavior when provider is unavailable

These tests should validate returned seed segments:

- segment text
- offsets
- `isWordLike`

## 13.2 Analysis parity tests

Add `prepareWithSegments(...)` tests that assert actual segment arrays for locale-sensitive samples.

Recommended fixture styles:

- plain Thai sentence with no spaces
- Thai with punctuation
- Khmer and Lao short sentences
- Myanmar phrase where locale-sensitive boundaries matter

The expected values should come from:

- TS `prepareWithSegments(...)` using explicit locale
- captured reference fixtures committed into the repo

## 13.3 Layout parity tests

Add layout-level tests to ensure locale-sensitive segmentation changes actual results in narrow widths.

Examples:

- same Thai string under locale-aware provider vs heuristic fallback
- line count and segment boundaries change as expected
- resetting locale back to `undefined` restores default behavior

## 13.4 Regression tests

Protect existing solved paths:

- Latin
- Arabic punctuation rules
- CJK punctuation attachment
- URL/query runs
- numeric/time-range runs
- `pre-wrap`

The locale project must not regress those areas.

## 14. Rollout Plan

## Phase 1: architecture refactor

Goal:

- make room for a pluggable provider without changing behavior yet

Tasks:

1. Add `IWordSegmentationProvider`.
2. Convert the current tokenizer into `HeuristicWordSegmentationProvider`.
3. Refactor analysis to consume seed segments from a provider.
4. Keep default provider = heuristic.
5. Keep all existing tests green.

Expected result:

- no user-visible change
- cleaner seam for locale-aware backends

## Phase 2: ICU-backed implementation

Goal:

- add a real locale-sensitive backend on the primary supported environment

Tasks:

1. Implement a minimal ICU boundary adapter in `Pretext.Uno`.
2. Feed explicit `SetLocale(...)` locale into ICU, falling back to ambient current UI culture only when locale is not set.
3. Return word boundary segments and offsets.
4. Derive `isWordLike` conservatively if ICU does not provide it directly through the chosen API surface.
5. Select ICU provider when available, else fallback to heuristic.

Expected result:

- `SetLocale("th")` and similar calls materially affect segmentation

## Phase 3: parity tests and fixtures

Goal:

- make locale parity measurable

Tasks:

1. Add committed reference fixtures from the TS engine.
2. Add provider tests.
3. Add analysis tests.
4. Add layout behavior tests.

Expected result:

- we can state locale-aware parity with evidence, not only API resemblance

## Phase 4: cache cleanup and optional backend expansion

Goal:

- improve correctness and maintenance after the core behavior lands

Tasks:

1. Split analysis cache invalidation from measurement cache invalidation internally.
2. Add optional browser/Wasm provider if needed.
3. Evaluate whether a shared reusable Unicode boundary helper should be extracted from Uno-side ICU infrastructure.

Expected result:

- cleaner cache model
- broader runtime coverage

## 15. Risks

### 15.1 Backend availability risk

Not every target head will have the same easy access to ICU or equivalent word-boundary APIs.

Mitigation:

- provider abstraction
- deterministic heuristic fallback

### 15.2 Semantic drift risk

ICU word boundaries may still not be identical to browser `Intl.Segmenter` for every locale and runtime version.

Mitigation:

- keep TS as the reference
- capture fixture-based parity tests
- treat backend differences explicitly instead of assuming exactness

### 15.3 Packaging risk

If ICU binaries or initialization are not available, the provider may fail.

Mitigation:

- lazy capability probe
- fallback provider
- keep failures non-fatal

### 15.4 Regression risk in preprocessing

Changing the initial segmentation seed can perturb later merge passes unexpectedly.

Mitigation:

- preserve all current merge passes
- add regression coverage for Latin, Arabic, CJK, URL, numeric, and whitespace cases

## 16. Recommended First Implementation Slice

The smallest good first slice is:

1. refactor current C# analysis behind `IWordSegmentationProvider`
2. keep heuristic provider as default
3. add internal provider override hook for tests
4. add one locale-aware backend for Uno Skia/Desktop using ICU
5. add fixture-based Thai segmentation tests

This slice gives:

- a clean architecture seam
- a real locale-sensitive implementation
- bounded scope
- measurable parity improvement

## 17. Final Recommendation

Implement locale support in the C# port as a **pluggable word-segmentation provider system**, not as ad hoc locale checks inside the current tokenizer.

The recommended stack is:

- current heuristic tokenizer preserved as fallback
- ICU-backed provider added for locale-aware segmentation
- `SetLocale(...)` wired into provider selection and segmentation calls
- all current preprocessing rules kept after provider segmentation
- explicit parity fixtures and tests added for Southeast Asian and mixed-script cases

That keeps the engine architecture honest:

- locale work stays in the prepare phase
- the line walker remains simple
- the hot path stays arithmetic
- the C# port moves from API-level locale parity to real behavioral locale parity
