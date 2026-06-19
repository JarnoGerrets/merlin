# 04 - Calendar Capability

## Goal

Implement calendar access so Merlin can check availability, summarize schedules, create events, update events, and respond to invitations safely. Calendar is less risky than email but still private and externally visible.

## Current state

`calendar` exists as a missing capability domain. There is no calendar provider, calendar permission flow, event model, scheduling assistant, or event creation tool yet.

## User value

Example requests:

- "What is on my calendar today?"
- "Am I free tomorrow afternoon?"
- "Schedule a study block for Friday morning."
- "Move my gym reminder to six."
- "Create a meeting with Lisa next Monday."

## Scope

### Phase 1: Read-only schedule

- Connect one calendar provider.
- List events by date range.
- Answer availability questions.
- Summarize today's/tomorrow's schedule.

### Phase 2: Create events

- Create simple events after confirmation.
- Support title, date, start, end, location, description.
- Support no-attendee personal events first.

### Phase 3: Attendees and invites

- Add attendees.
- Create meeting invites.
- Add video meeting links if provider supports it.

### Phase 4: Update/RSVP/delete

- Move events.
- Cancel/delete events.
- RSVP to invitations.
- All require confirmation.

## Non-goals

- No autonomous rescheduling.
- No automatic invite acceptance.
- No complex recurring-event editing in first version.
- No deleting/canceling events until create/update is reliable.

## Safety model

- Read schedule: `private_readonly`.
- Check availability: `private_readonly`.
- Create event without attendees: `confirmation_required`.
- Create invite with attendees: `high_risk_confirmation`.
- Update/delete event: `high_risk_confirmation`.

## Provider abstraction

```csharp
public interface ICalendarProvider
{
    Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(CalendarQuery query, CancellationToken cancellationToken);
    Task<CalendarAvailability> GetAvailabilityAsync(AvailabilityQuery query, CancellationToken cancellationToken);
    Task<CalendarEvent> CreateEventAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken);
    Task<CalendarEvent> UpdateEventAsync(UpdateCalendarEventRequest request, CancellationToken cancellationToken);
    Task DeleteEventAsync(string eventId, CancellationToken cancellationToken);
}
```

## Suggested models

```csharp
public sealed record CalendarQuery(
    DateTimeOffset Start,
    DateTimeOffset End,
    string? CalendarId,
    int MaxResults);

public sealed record CalendarEvent(
    string Id,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Location,
    string? Description,
    IReadOnlyList<string> Attendees,
    CalendarEventStatus Status);

public sealed record CreateCalendarEventRequest(
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Location,
    string? Description,
    IReadOnlyList<string> Attendees,
    bool AddVideoMeeting);
```

## Suggested files

```text
Merlin.Backend/
  Configuration/CalendarOptions.cs
  Models/CalendarQuery.cs
  Models/CalendarEvent.cs
  Models/CalendarAvailability.cs
  Models/CreateCalendarEventRequest.cs
  Services/Interfaces/ICalendarProvider.cs
  Services/CalendarPermissionService.cs
  Services/CalendarQueryParser.cs
  Services/CalendarAvailabilityService.cs
  Tools/CalendarListTool.cs
  Tools/CalendarAvailabilityTool.cs
  Tools/CalendarCreateEventTool.cs
  Tools/CalendarUpdateEventTool.cs
Merlin.Backend.Tests/
  CalendarCapabilityRoutingTests.cs
  CalendarListToolTests.cs
  CalendarAvailabilityToolTests.cs
  CalendarCreateConfirmationTests.cs
  CalendarQueryParserTests.cs
```

## Configuration

```json
"Calendar": {
  "Enabled": true,
  "Provider": "GoogleCalendar",
  "DefaultCalendarId": "primary",
  "DefaultEventDurationMinutes": 60,
  "CreateEventsEnabled": true,
  "UpdateEventsEnabled": false,
  "DeleteEventsEnabled": false,
  "AttendeeInvitesEnabled": false,
  "ReadRequiresPermission": true
}
```

## Date/time parsing

Calendar is dangerous if date parsing is sloppy.

Rules:

- Always resolve relative dates using the user's local timezone.
- Use explicit dates in confirmation.
- If time is missing, ask a clarification unless a default is obvious and harmless.
- If AM/PM ambiguity exists, ask a clarification.
- For dayparts:
  - morning: default 09:00
  - afternoon: default 15:00
  - evening: default 19:00
- Confirm exact start and end before creating.

## Availability UX

User: "Am I free tomorrow afternoon?"

Merlin should:

1. Resolve tomorrow's absolute date.
2. Interpret afternoon window.
3. Check events.
4. Answer clearly.

Example:

> "Tomorrow afternoon, June 19, you look free from 15:30 onward. You have one event from 14:00 to 15:00."

## Event creation confirmation

```text
I can create this calendar event:
Title: Study block
When: Friday, June 19, 2026, 09:00-11:00
Calendar: Primary
Attendees: none
Say "create this event" to confirm.
```

For attendees:

```text
This will invite Lisa at lisa@example.com. She will receive a calendar invitation. Say "send the invite" to confirm.
```

## Routing examples

Should route to `calendar`:

- "what's on my calendar today"
- "am I free after lunch"
- "schedule a meeting tomorrow at 3"
- "move my appointment"
- "accept my meeting invite"

Should not route to `calendar`:

- "what day is it" -> system date/time.
- "open Google Calendar" -> URL/app opening.
- "find the email with the invite" -> email.

## Tests

- [ ] Calendar queries route to calendar domain.
- [ ] Date/time-only queries route to system time/date where appropriate.
- [ ] Relative dates resolve using local timezone.
- [ ] Missing time triggers clarification for event creation.
- [ ] Read schedule requires private read permission.
- [ ] Create event stages confirmation.
- [ ] Confirmation includes absolute date/time.
- [ ] Attendee invite requires stricter confirmation.
- [ ] Update/delete disabled until enabled in config.
- [ ] Tool discovery separates read/create/update abilities.

## Phased TODO

### Phase 1

- [ ] Add provider abstraction and fake provider.
- [ ] Add list-events tool.
- [ ] Add availability tool.
- [ ] Add date/time parser tests.

### Phase 2

- [ ] Add create event request model.
- [ ] Add confirmation flow.
- [ ] Add personal event creation.

### Phase 3

- [ ] Add attendees.
- [ ] Add invites.
- [ ] Add video meeting option.

### Phase 4

- [ ] Add update/delete/RSVP with strict confirmation.
- [ ] Add recurring-event safeguards.

## Acceptance criteria

Merlin can read today's schedule and create a personal event after confirming the exact title, date, start, end, and calendar. It must not create ambiguous events.
