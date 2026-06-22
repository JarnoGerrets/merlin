namespace Merlin.Backend.Services.Feedback;

public sealed class DefaultFeedbackCardProvider : IFeedbackCardProvider
{
    private static readonly IReadOnlyList<FeedbackCard> Cards =
    [
        Card(
            "general_start_01",
            "Got it.",
            priority: 0,
            vector: new()
            {
                ["domain.general"] = 0.7,
                ["phase.executing"] = 0.3,
                ["interaction.voice"] = 0.2
            }),
        Card(
            "general_start_02",
            "Okay.",
            priority: 0,
            vector: new()
            {
                ["domain.general"] = 0.65,
                ["phase.executing"] = 0.25,
                ["interaction.voice"] = 0.2
            }),
        Card(
            "general_checking_01",
            "I'll check.",
            priority: 1,
            vector: new()
            {
                ["domain.general"] = 0.45,
                ["phase.executing"] = 0.4,
                ["duration.medium"] = 0.4
            }),
        Card(
            "conversation_thinking_01",
            "Let me think that through.",
            priority: 1,
            tags: ["conversation", "thinking"],
            vector: new()
            {
                ["domain.conversation"] = 1.0,
                ["phase.executing"] = 0.7,
                ["duration.medium"] = 0.5,
                ["interaction.voice"] = 0.5
            }),
        Card(
            "conversation_thinking_02",
            "I'll work through that.",
            priority: 1,
            tags: ["conversation", "thinking"],
            vector: new()
            {
                ["domain.conversation"] = 0.95,
                ["phase.executing"] = 0.65,
                ["duration.medium"] = 0.45,
                ["interaction.voice"] = 0.45
            }),
        Card(
            "conversation_thinking_03",
            "Give me a moment to reason it out.",
            priority: 0,
            tags: ["conversation", "thinking"],
            vector: new()
            {
                ["domain.conversation"] = 0.95,
                ["phase.executing"] = 0.6,
                ["duration.medium"] = 0.45,
                ["interaction.voice"] = 0.4
            }),
        Card(
            "conversation_checking_01",
            "I'll check that.",
            priority: 0,
            tags: ["conversation"],
            vector: new()
            {
                ["domain.conversation"] = 0.9,
                ["phase.executing"] = 0.55,
                ["duration.medium"] = 0.4,
                ["interaction.voice"] = 0.35
            }),
        Card(
            "external_open_01",
            "Opening that.",
            priority: 5,
            tags: ["external", "open"],
            vector: new()
            {
                ["domain.external_app"] = 1.0,
                ["phase.executing"] = 0.5,
                ["duration.short"] = 0.35,
                ["action.external"] = 0.6
            }),
        Card(
            "external_open_02",
            "Bringing that up.",
            priority: 4,
            tags: ["external", "open"],
            vector: new()
            {
                ["domain.external_app"] = 0.95,
                ["phase.executing"] = 0.45,
                ["duration.short"] = 0.35,
                ["action.external"] = 0.55
            }),
        Card(
            "file_search_01",
            "I'm looking through your files.",
            priority: 5,
            tags: ["file", "search"],
            vector: new()
            {
                ["domain.file_search"] = 1.0,
                ["phase.executing"] = 0.45,
                ["duration.medium"] = 0.55
            }),
        Card(
            "file_search_02",
            "Checking your files now.",
            priority: 4,
            tags: ["file", "search"],
            vector: new()
            {
                ["domain.file_search"] = 0.95,
                ["phase.executing"] = 0.45,
                ["duration.medium"] = 0.5
            }),
        Card(
            "web_search_01",
            "I'm checking that online.",
            priority: 5,
            tags: ["web", "search"],
            vector: new()
            {
                ["domain.web_search"] = 1.0,
                ["phase.executing"] = 0.45,
                ["duration.medium"] = 0.5,
                ["action.external"] = 0.25
            }),
        Card(
            "web_search_02",
            "Looking that up.",
            priority: 4,
            tags: ["web", "search"],
            vector: new()
            {
                ["domain.web_search"] = 0.95,
                ["phase.executing"] = 0.45,
                ["duration.medium"] = 0.45
            }),
        Card(
            "memory_lookup_01",
            "I'll check what I remember.",
            priority: 5,
            tags: ["memory"],
            vector: new()
            {
                ["domain.memory"] = 1.0,
                ["phase.executing"] = 0.45,
                ["duration.medium"] = 0.4
            }),
        Card(
            "memory_update_01",
            "I'll save that.",
            priority: 5,
            tags: ["memory"],
            vector: new()
            {
                ["domain.memory"] = 0.9,
                ["phase.executing"] = 0.35,
                ["duration.short"] = 0.45
            }),
        Card(
            "confirmation_prepare_01",
            "I'll prepare it first and ask before sending.",
            priority: 8,
            tags: ["confirmation"],
            requiresConfirmationContext: true,
            vector: new()
            {
                ["domain.confirmation"] = 1.0,
                ["risk.confirmation"] = 1.0,
                ["phase.needs_confirmation"] = 0.65
            }),
        Card(
            "confirmation_safe_01",
            "I won't send anything yet.",
            priority: 7,
            tags: ["confirmation"],
            requiresConfirmationContext: true,
            vector: new()
            {
                ["domain.confirmation"] = 0.95,
                ["risk.confirmation"] = 1.0,
                ["phase.needs_confirmation"] = 0.55
            }),
        Card(
            "progress_medium_01",
            "This may take a second.",
            priority: 1,
            tags: ["progress"],
            vector: new()
            {
                ["phase.still_working"] = 0.8,
                ["duration.medium"] = 0.6,
                ["domain.general"] = 0.2
            }),
        Card(
            "interruption_correction_01",
            "Oh, I see what you meant. Let me re-organise that.",
            priority: 14,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "correction", "redirect"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.redirecting"] = 1.0,
                ["interruption.correction"] = 1.0,
                ["interruption.redirect"] = 0.8
            }),
        Card(
            "interruption_redirect_01",
            "Okay, let me answer that instead.",
            priority: 13,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "redirect"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.redirecting"] = 1.0,
                ["interruption.redirect"] = 1.0,
                ["interruption.correction"] = 0.5
            }),
        Card(
            "interruption_redirect_02",
            "Got it, I'll switch direction.",
            priority: 12,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "redirect"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.redirecting"] = 1.0,
                ["interruption.redirect"] = 1.0
            }),
        Card(
            "interruption_recompose_01",
            "Good point, let me include that.",
            priority: 13,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "recompose", "clarification"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.recomposing_continuation"] = 1.0,
                ["interruption.recompose"] = 1.0,
                ["interruption.clarification"] = 0.6,
                ["interruption.side_comment"] = 0.5
            }),
        Card(
            "interruption_recompose_02",
            "True, let me fold that into the answer.",
            priority: 12,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "recompose", "clarification"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.recomposing_continuation"] = 1.0,
                ["interruption.recompose"] = 1.0,
                ["interruption.clarification"] = 0.6
            }),
        Card(
            "interruption_recompose_03",
            "That matters too. I'll adjust the answer around it.",
            priority: 11,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "recompose", "additional_context"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.recomposing_continuation"] = 1.0,
                ["interruption.recompose"] = 1.0,
                ["interruption.additional_context"] = 0.6
            }),
        Card(
            "interruption_recompose_wait_01",
            "Let me fold that into the answer.",
            priority: 12,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "recompose", "waiting"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.recomposing_continuation"] = 1.0,
                ["duration.medium"] = 0.6,
                ["duration.long"] = 0.4,
                ["interruption.recompose"] = 1.0,
                ["interruption.waiting"] = 1.0
            }),
        Card(
            "interruption_recompose_wait_02",
            "I'm updating the answer around that.",
            priority: 11,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "recompose", "waiting"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.recomposing_continuation"] = 1.0,
                ["duration.medium"] = 0.6,
                ["duration.long"] = 0.4,
                ["interruption.recompose"] = 1.0,
                ["interruption.waiting"] = 1.0
            }),
        Card(
            "interruption_recompose_wait_03",
            "One moment, I'm reconnecting the thread.",
            priority: 10,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "recompose", "waiting"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.recomposing_continuation"] = 1.0,
                ["duration.medium"] = 0.6,
                ["duration.long"] = 0.4,
                ["interruption.recompose"] = 1.0,
                ["interruption.waiting"] = 1.0
            }),
        Card(
            "interruption_queue_followup_01",
            "Sure, I'll come back to that after this.",
            priority: 12,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "follow_up", "queue"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.queueing_followup"] = 1.0,
                ["interruption.follow_up"] = 1.0,
                ["interruption.queue"] = 1.0
            }),
        Card(
            "interruption_queue_followup_02",
            "Got it. I'll handle that after this part.",
            priority: 11,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "follow_up", "queue"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.queueing_followup"] = 1.0,
                ["interruption.follow_up"] = 1.0,
                ["interruption.queue"] = 1.0
            }),
        Card(
            "interruption_queue_followup_03",
            "I'll park that and return to it after this.",
            priority: 10,
            urgency: FeedbackUrgency.High,
            tags: ["interruption", "follow_up", "queue"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.queueing_followup"] = 1.0,
                ["interruption.follow_up"] = 1.0,
                ["interruption.queue"] = 1.0
            }),
        Card(
            "interruption_unclear_01",
            "Did you want me to change direction, or should I keep going?",
            priority: 8,
            tags: ["interruption", "unclear"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.handling_interruption"] = 1.0,
                ["interruption.unclear"] = 1.0
            }),
        Card(
            "interruption_unclear_02",
            "Do you want me to answer that now, or continue first?",
            priority: 7,
            tags: ["interruption", "unclear"],
            cooldown: TimeSpan.FromSeconds(120),
            requiresInterruptionContext: true,
            vector: new()
            {
                ["domain.interruption"] = 1.0,
                ["phase.handling_interruption"] = 1.0,
                ["interruption.unclear"] = 1.0
            }),
        Card(
            "failure_generic_01",
            "Something went wrong there.",
            priority: 1,
            vector: new()
            {
                ["phase.failed"] = 1.0,
                ["domain.general"] = 0.2
            })
    ];

    public IReadOnlyList<FeedbackCard> GetCards()
    {
        return Cards;
    }

    private static FeedbackCard Card(
        string id,
        string text,
        int priority,
        Dictionary<string, double> vector,
        IReadOnlyList<string>? tags = null,
        FeedbackUrgency urgency = FeedbackUrgency.Normal,
        TimeSpan? cooldown = null,
        bool requiresConfirmationContext = false,
        bool requiresInterruptionContext = false)
    {
        return new FeedbackCard
        {
            Id = id,
            Text = text,
            Urgency = urgency,
            Tags = tags ?? Array.Empty<string>(),
            Vector = vector,
            Cooldown = cooldown ?? TimeSpan.FromSeconds(60),
            Priority = priority,
            RequiresConfirmationContext = requiresConfirmationContext,
            RequiresInterruptionContext = requiresInterruptionContext
        };
    }
}
