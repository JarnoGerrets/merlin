---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/merlin_capability_specs/02_NewsCapability.md
classification: architecture-plan
related_features:
  - External App Control
status: future
imported_to_vault: true
---

# 02 - News Capability

## Goal

Implement a current-news capability that reuses web search/fetch infrastructure but adds news-specific behavior: recency, source diversity, headline grouping, topic tracking, and careful uncertainty handling.

## Current state

`news` exists as a missing capability domain. The config message says Merlin would need a `NewsTool` or web search capability. This file assumes the web search capability is implemented first, then news builds on top.

## User value

Example requests:

- "What's the latest AI news today?"
- "Give me a quick news briefing."
- "What happened with OpenAI this week?"
- "Summarize the latest tech headlines."
- "Any important news in the Netherlands today?"

## Scope

### Phase 1: Topic news lookup

- Search recent news for a user-provided topic.
- Return headline groups.
- Summarize the main developments.
- Include source names and dates.

### Phase 2: Personalized briefings

- User-configurable topics.
- Morning/evening briefing modes.
- Region/source preferences.
- Avoid duplicates across sources.

### Phase 3: Monitoring hooks

- Watch topics manually or through a future automation/scheduler layer.
- Notify only for meaningful changes.
- Store last-seen story fingerprints.

## Non-goals

- No political persuasion.
- No paywall bypassing.
- No endless doom-scrolling feed.
- No background monitoring unless the automation system explicitly schedules it.
- No treating one source as absolute truth on contested events.

## Safety and trust rules

News is public read-only, but it can shape user beliefs. Apply stricter quality rules:

- Always show dates.
- Separate confirmed facts from early reports.
- Prefer multiple reputable sources for breaking news.
- Say when an event is developing.
- Avoid overconfident summaries for very recent events.
- Do not infer motive or blame unless sources support it.

## Suggested provider abstraction

```csharp
public interface INewsProvider
{
    Task<NewsSearchResponse> SearchAsync(NewsSearchRequest request, CancellationToken cancellationToken);
}

public interface INewsBriefingService
{
    Task<NewsBriefing> BuildBriefingAsync(NewsBriefingRequest request, CancellationToken cancellationToken);
}
```

## Suggested models

```csharp
public sealed record NewsSearchRequest(
    string? Topic,
    string? Region,
    string? Language,
    DateTimeOffset? Since,
    int MaxArticles,
    NewsMode Mode);

public sealed record NewsArticle(
    string Title,
    string Url,
    string Source,
    DateTimeOffset? PublishedAt,
    string Snippet,
    string? Author,
    string? ImageUrl);

public sealed record NewsStoryCluster(
    string StoryTitle,
    string Summary,
    IReadOnlyList<NewsArticle> Articles,
    bool IsDeveloping,
    double ConfidenceScore);
```

## Suggested files

```text
Merlin.Backend/
  Configuration/NewsOptions.cs
  Models/NewsSearchRequest.cs
  Models/NewsArticle.cs
  Models/NewsStoryCluster.cs
  Models/NewsBriefing.cs
  Services/Interfaces/INewsProvider.cs
  Services/NewsService.cs
  Services/NewsStoryClusterer.cs
  Services/NewsBriefingService.cs
  Tools/NewsTool.cs
Merlin.Backend.Tests/
  NewsToolTests.cs
  NewsServiceTests.cs
  NewsStoryClustererTests.cs
  NewsCapabilityRoutingTests.cs
```

## Configuration

```json
"News": {
  "Enabled": true,
  "Provider": "WebSearch",
  "DefaultRegion": "NL",
  "DefaultLanguage": "en",
  "MaxArticles": 12,
  "DefaultLookbackHours": 24,
  "PreferSourceDiversity": true,
  "IncludeOpinion": false,
  "CacheResultsSeconds": 300
}
```

## Routing examples

Should route to `news`:

- "what's in the news today"
- "latest AI news"
- "news about Microsoft"
- "what happened overnight"
- "give me a tech news briefing"

Should not route to `news`:

- "search the web for a tutorial" -> web search.
- "open NOS.nl" -> URL opening.
- "what did I write in my notes" -> file access/memory.

## UX modes

### Quick headline mode

User: "What's the news?"

Merlin speaks:

> "Here are the three biggest stories I found: first..., second..., third..."

Visual output:

- Grouped headlines.
- Sources.
- Times.
- Links.

### Topic deep mode

User: "What's the latest about AI regulation?"

Merlin:

- Finds recent articles.
- Groups into story clusters.
- Summarizes timeline.
- Shows caveats.

### Briefing mode

User: "Give me my morning briefing."

Merlin:

- Uses saved topics later.
- Gives compact spoken digest.
- Shows expandable cards.

## Source diversity

For a single story, prefer at least two independent sources when possible. If only one source is found, say that the summary is based on one source.

## Bias handling

Do not label sources politically unless a reliable source database is explicitly added. Instead use neutral labels:

- official statement,
- wire/reporting outlet,
- local outlet,
- company/project source,
- analysis/opinion,
- social/unverified.

## Tests

- [ ] News queries route to `news`.
- [ ] Web tutorial searches do not route to `news`.
- [ ] Recent articles are preferred over old ones.
- [ ] Duplicate articles are clustered.
- [ ] Multiple sources are shown for one story.
- [ ] Developing story caveat appears when articles are very recent.
- [ ] Opinion pieces are excluded by default.
- [ ] Missing provider produces setup message.
- [ ] Spoken result stays concise.
- [ ] Tool discovery lists news examples.

## Phased TODO

### Phase 1

- [ ] Implement `NewsTool` on top of `IWebSearchProvider`.
- [ ] Add news-specific routing examples.
- [ ] Return recent source list and brief summary.

### Phase 2

- [ ] Add clustering.
- [ ] Add source diversity scoring.
- [ ] Add briefing output format.

### Phase 3

- [ ] Add saved topics.
- [ ] Add last-seen story fingerprints.
- [ ] Add future automation integration.

## Acceptance criteria

Merlin can answer "latest AI news today" with recent, dated, sourced stories and a developing-story caveat when appropriate. It should not present old search results as today's news.
