using System.Text.Json;

namespace Merlin.Backend.Services.Vision;

public sealed class VisionSidecarClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string SerializeCommand(object command)
    {
        return JsonSerializer.Serialize(command, JsonOptions);
    }

    public bool TryParseMessage(string line, out VisionSidecarMessage? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize<VisionSidecarMessage>(line, JsonOptions);
            return message is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
