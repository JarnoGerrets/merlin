# 03 - Email Capability

## Goal

Implement email as a privacy-first capability: search/read email, summarize threads, create drafts, and eventually send emails after explicit confirmation. Email is high-value but high-risk because it contains private data and sending mail affects other people.

## Current state

`email` exists as a missing capability domain. There is no email provider, OAuth flow, mailbox search, draft creation, send action, or email-specific permission model yet.

## User value

Example requests:

- "Check if school emailed me today."
- "Find the email about my internship deadline."
- "Summarize unread emails from this morning."
- "Draft a reply saying I can attend."
- "Send Lisa the notes from our meeting."

## Scope

### Phase 1: Metadata/search only

- Connect one provider.
- Search mailbox metadata: sender, subject, date, labels, snippet.
- No body reading unless user asks or grants permission.

### Phase 2: Read and summarize

- Read selected email/thread body.
- Summarize emails.
- Extract dates/tasks cautiously.
- Respect privacy and avoid memory storage by default.

### Phase 3: Draft replies

- Create reviewable drafts.
- Never send automatically.
- Support tone/length instructions.

### Phase 4: Confirmed send

- Send only after hard confirmation.
- Confirmation includes recipients, subject, and body summary.

## Non-goals

- No mass email sending.
- No deleting email in the first version.
- No automatic unsubscribe in the first version.
- No hidden reading of all email.
- No training memory on email contents by default.

## Safety model

Email requires private-data permissions.

Recommended safety levels:

- Search metadata: `private_readonly`.
- Read body: `private_readonly` with explicit or remembered permission.
- Create draft: `confirmation_required` or at least explicit user request.
- Send email: `high_risk_confirmation`.
- Delete/archive/label: later, confirmed write only.

## Provider choice

Start with one provider. For a local Windows personal assistant, the practical first target is Gmail or Microsoft Graph/Outlook. Do not attempt all providers at once.

Provider interface:

```csharp
public interface IEmailProvider
{
    Task<EmailSearchResult> SearchAsync(EmailSearchRequest request, CancellationToken cancellationToken);
    Task<EmailThread> ReadThreadAsync(string threadId, CancellationToken cancellationToken);
    Task<EmailDraft> CreateDraftAsync(CreateEmailDraftRequest request, CancellationToken cancellationToken);
    Task<SendEmailResult> SendDraftAsync(string draftId, CancellationToken cancellationToken);
}
```

## Suggested models

```csharp
public sealed record EmailSearchRequest(
    string Query,
    EmailSearchScope Scope,
    DateTimeOffset? Since,
    DateTimeOffset? Before,
    int MaxResults,
    bool IncludeBodySnippets);

public sealed record EmailMessageSummary(
    string MessageId,
    string ThreadId,
    string From,
    IReadOnlyList<string> To,
    string Subject,
    DateTimeOffset ReceivedAt,
    string Snippet,
    bool HasAttachments,
    bool IsUnread);

public sealed record CreateEmailDraftRequest(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body,
    string? ReplyToThreadId);
```

## Suggested files

```text
Merlin.Backend/
  Configuration/EmailOptions.cs
  Models/EmailSearchRequest.cs
  Models/EmailMessageSummary.cs
  Models/EmailThread.cs
  Models/EmailDraft.cs
  Services/Interfaces/IEmailProvider.cs
  Services/EmailPermissionService.cs
  Services/EmailSearchService.cs
  Services/EmailDraftService.cs
  Tools/EmailSearchTool.cs
  Tools/EmailReadTool.cs
  Tools/EmailDraftTool.cs
  Tools/EmailSendTool.cs
Merlin.Backend.Tests/
  EmailCapabilityRoutingTests.cs
  EmailSearchToolTests.cs
  EmailReadToolTests.cs
  EmailDraftToolTests.cs
  EmailSendConfirmationTests.cs
```

## Configuration

```json
"Email": {
  "Enabled": true,
  "Provider": "Gmail",
  "DefaultMaxResults": 10,
  "ReadBodyRequiresPermission": true,
  "SendingEnabled": false,
  "DraftOnlyMode": true,
  "AllowDelete": false,
  "AllowArchive": false,
  "CacheMetadataSeconds": 60
}
```

## Permission flow

First use:

1. User asks: "Check my email."
2. Merlin responds: "Email is private. I need permission to connect your mailbox before I can search it."
3. User completes provider setup.
4. Merlin stores credential reference only.
5. Merlin confirms search scope.

Example private read confirmation:

> "I can search your email subjects and snippets for messages about internship deadlines. Should I do that now?"

Example body read confirmation:

> "I found a likely email from Fontys. Do you want me to open and summarize the full message?"

## Sending confirmation

Never use a vague confirmation like "send it?" Use exact confirmation:

```text
I'm about to send this email:
To: lisa@example.com
Subject: Meeting notes
Summary: Shares the notes and says you can discuss them tomorrow.
Say "send this email" to send it, or "cancel" to stop.
```

For voice mode, require an explicit phrase such as:

- "send this email"
- "yes, send the email"

Do not accept weak confirmations like "sure" for first implementation.

## Routing examples

Should route to `email`:

- "check my unread email"
- "did I get an email from school"
- "find the email about the invoice"
- "draft a reply to Mark"
- "send an email to Lisa"

Should not route to `email`:

- "what is my email address" -> maybe memory/contact later.
- "open Gmail" -> URL/application opening.
- "search webmail setup docs" -> web search.

## Privacy and memory

Email content must not automatically enter long-term memory. Add a policy:

- Summaries can live in the current conversation.
- Long-term memory extraction ignores email bodies unless user explicitly says "remember this".
- Audit logs do not store full body text.
- Search query logs should be optional.

## Attachments

Phase 1: show attachment names and sizes only.

Phase 2: allow user to open/read supported attachments through file/document reading pipeline.

Never execute attachments.

## Tests

- [ ] Email queries route to email domain.
- [ ] Open Gmail routes to URL/open app, not mailbox access.
- [ ] Missing credentials produces setup response.
- [ ] Metadata search does not read full body.
- [ ] Body read requires permission.
- [ ] Draft creation does not send.
- [ ] Send requires exact hard confirmation.
- [ ] Ambiguous recipient triggers clarification.
- [ ] Multiple matching threads are presented safely.
- [ ] Audit log excludes body content.
- [ ] Tool discovery shows read/draft/send distinction.

## Phased TODO

### Phase 1

- [ ] Add options and provider abstraction.
- [ ] Add fake provider tests.
- [ ] Implement metadata search.
- [ ] Add routing examples.
- [ ] Add permission prompt for first use.

### Phase 2

- [ ] Implement thread body read.
- [ ] Add summarization service.
- [ ] Add privacy-safe memory exclusion.

### Phase 3

- [ ] Create drafts.
- [ ] Add reply draft support.
- [ ] Add visual draft preview.

### Phase 4

- [ ] Confirmed send.
- [ ] Add send audit log.
- [ ] Add stricter voice confirmation tests.

## Acceptance criteria

Merlin can search email metadata, read one selected thread after permission, draft a reply, and only send after a clear explicit confirmation containing recipient and subject.
