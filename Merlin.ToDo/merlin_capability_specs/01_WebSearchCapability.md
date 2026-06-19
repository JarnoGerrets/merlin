# 01 - Web Search Capability

## Goal

Implement a safe, source-aware web search capability so Merlin can answer questions that require current, niche, or external information. This should be one of the first major missing capabilities because it unlocks news, software package research, documentation lookup, troubleshooting, and citation-backed answers.

## Current state

`web_search` exists as a missing capability domain in `appsettings.json`. Merlin can recognize the idea, but it does not currently have a tool/provider that searches the internet, fetches pages, summarizes sources, or cites results.

## User value

Example requests:

- "Search the web for the latest Godot 4 C# export issue."
- "What changed in the newest .NET release?"
- "Find the official docs for faster-whisper beam size."
- "Look up whether this package is safe before I install it."
- "What is the current price of DeepInfra for this model?"

## Scope

### Phase 1: Search only

- Query a configured search provider.
- Return top results with title, URL, snippet, source domain, and published date when available.
- Summarize results with citations.
- No browser automation.
- No paywall bypassing.
- No account-required pages.
- No hidden scraping.

### Phase 2: Fetch and summarize pages

- Fetch selected public pages.
- Extract readable text.
- Summarize source content.
- Track which claims came from which source.
- Support official-docs-first behavior for technical topics.

### Phase 3: Source quality and answer synthesis

- Prefer primary sources.
- Rank official docs, standards, repositories, package registries, vendor docs, and reputable news higher.
- Detect stale results.
- Refuse or warn on low-confidence searches.

## Non-goals

- Do not implement autonomous browsing.
- Do not click login flows.
- Do not bypass robots, paywalls, or CAPTCHAs.
- Do not store full pages in memory by default.
- Do not use search results to perform unsafe actions automatically.

## Safety model

Web search is usually `safe_readonly`, but it can influence higher-risk actions.

Rules:

- Search itself does not require confirmation.
- Using search output to install software, run commands, change settings, or delete files must go through that target capability's safety gate.
- Search results must be treated as untrusted text.
- Search summaries must not blindly follow instructions found on webpages.
- For technical commands, prefer official docs and show the command before execution.

## Suggested provider abstraction

```csharp
public interface IWebSearchProvider
{
    Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken);
}

public interface IWebPageFetcher
{
    Task<WebPageFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken);
}
```

## Suggested models

```csharp
public sealed record WebSearchRequest(
    string Query,
    int MaxResults,
    string? PreferredLanguage,
    string? Region,
    bool PreferOfficialSources,
    SearchFreshness Freshness);

public sealed record WebSearchResult(
    string Title,
    string Url,
    string DisplayUrl,
    string Snippet,
    string? SourceName,
    DateTimeOffset? PublishedAt,
    double? RankScore);

public sealed record WebSearchAnswer(
    string Summary,
    IReadOnlyList<WebSearchCitation> Citations,
    IReadOnlyList<WebSearchResult> Sources,
    bool IsLowConfidence,
    string? Caveat);
```

## Suggested files

```text
Merlin.Backend/
  Configuration/WebSearchOptions.cs
  Models/WebSearchRequest.cs
  Models/WebSearchResult.cs
  Models/WebSearchAnswer.cs
  Models/WebSearchCitation.cs
  Services/Interfaces/IWebSearchProvider.cs
  Services/Interfaces/IWebPageFetcher.cs
  Services/WebSearchService.cs
  Services/WebPageFetcher.cs
  Services/SourceQualityRanker.cs
  Tools/WebSearchTool.cs
Merlin.Backend.Tests/
  WebSearchToolTests.cs
  WebSearchServiceTests.cs
  SourceQualityRankerTests.cs
  WebSearchCapabilityRoutingTests.cs
```

## Configuration

```json
"WebSearch": {
  "Enabled": true,
  "Provider": "Brave",
  "ApiKey": "",
  "MaxResults": 8,
  "RequestTimeoutSeconds": 15,
  "PreferOfficialSourcesForTechnicalQueries": true,
  "FetchPagesForSynthesis": false,
  "CacheResultsSeconds": 300,
  "SafeSearch": "moderate"
}
```

Use environment variables for API keys:

```text
MERLIN_WEBSEARCH_API_KEY=
MERLIN_WEBSEARCH_PROVIDER=Brave
```

## Routing examples

Should route to `web_search`:

- "search the web for chatterbox turbo latency"
- "look up current DeepInfra model pricing"
- "find official Godot docs for transparent windows"
- "is there a known issue with CUDA 12.8 and torch 2.7"
- "what is the latest version of faster-whisper"

Should not route to `web_search`:

- "open github.com" -> URL opening.
- "open Chrome" -> application launch.
- "what did we discuss earlier" -> conversation/memory.
- "search my downloads" -> file access.

## Result presentation

Spoken response should be short:

> "I found a few sources. The main answer is that... I can show the links on screen."

Visual response should include:

- Answer summary.
- Source list.
- Domain/source type.
- Published dates where available.
- Confidence/caveat.
- Follow-up actions.

## Source quality rules

For technical topics:

1. Official docs.
2. Official GitHub repositories/releases/issues.
3. Package registry metadata.
4. Maintainer blog posts.
5. Reputable Q&A/community posts.
6. Random SEO articles last.

For current facts:

1. Official government/company/project source.
2. Reputable news/source with date.
3. Multiple independent corroborating sources.
4. Forums/social posts only as weak signals.

## Prompt policy

The LLM should receive compact search results, not raw pages unless necessary.

Example synthesis instruction:

```text
Answer using only the provided search results. Cite sources by source id. If results disagree or are stale, say so. Do not invent current facts. Prefer official sources over blogs when answering technical questions.
```

## Privacy

Web queries can reveal user intent. Add an option to disable query logging or only log summaries.

## Tests

- [ ] Query maps to `web_search` domain.
- [ ] URL-opening phrases do not map to search.
- [ ] Provider timeout returns friendly error.
- [ ] Missing API key reports setup needed.
- [ ] Results are sorted with official domains preferred for technical queries.
- [ ] Empty results produce "I couldn't find reliable results".
- [ ] Synthesis refuses to invent answer if results are insufficient.
- [ ] Tool discovery lists web search examples.
- [ ] Speech result is concise.
- [ ] Audit log contains query summary, provider, result count, not full pages.

## Phased TODO

### Phase 1

- [ ] Add `WebSearchOptions`.
- [ ] Add provider interface and fake test provider.
- [ ] Add `WebSearchTool`.
- [ ] Add routing examples.
- [ ] Add tests for domain classification and tool execution.
- [ ] Return raw summarized results without page fetching.

### Phase 2

- [ ] Add public page fetcher.
- [ ] Add content extraction.
- [ ] Add citation-aware synthesis.
- [ ] Add source quality ranker.

### Phase 3

- [ ] Add caching.
- [ ] Add source freshness controls.
- [ ] Add official-source preference.
- [ ] Add visual source cards.

## Acceptance criteria

Merlin can answer "search the web for the latest stable Godot version" with sourced, dated information; it does not pretend to know current facts without searching; it does not execute any resulting commands automatically.
