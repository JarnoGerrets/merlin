using System.Text;
using Merlin.Backend.Configuration;
using Merlin.Backend.Core.Memory.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Core.Memory.Services;

public sealed class PromptRenderer
{
    private readonly CoreMemoryOptions _options;

    private static readonly HashSet<string> ProfileFactBlockTypes = new(StringComparer.Ordinal)
    {
        PromptBlockTypes.ResponsePreferences,
        PromptBlockTypes.CodingPreferences,
        PromptBlockTypes.MerlinBehaviorPreferences,
        PromptBlockTypes.WorkflowPreferences,
        PromptBlockTypes.PersonalFacts
    };

    public PromptRenderer()
        : this(Options.Create(new CoreMemoryOptions()))
    {
    }

    public PromptRenderer(IOptions<CoreMemoryOptions> options)
    {
        _options = options.Value;
    }

    public string Render(IReadOnlyList<PromptBlock> blocks)
    {
        var builder = new StringBuilder();
        var renderedProfileHeader = false;

        foreach (var block in blocks.OrderBy(block => block.SortOrder))
        {
            if (block.Type == PromptBlockTypes.RetrievalNotes && !_options.IncludeRetrievalNotesInPrompt)
            {
                continue;
            }

            if (!block.Required && string.IsNullOrWhiteSpace(block.Content))
            {
                continue;
            }

            if (ProfileFactBlockTypes.Contains(block.Type) && !renderedProfileHeader)
            {
                AppendSection(builder, "USER PROFILE FACTS:", null);
                renderedProfileHeader = true;
            }

            AppendSection(builder, block.Title, block.Content);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendSection(StringBuilder builder, string title, string? content)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.AppendLine(title);
        }

        if (content is not null)
        {
            builder.AppendLine(content);
        }
    }
}
