using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class CapabilityRegistry
{
    private readonly IReadOnlyList<CapabilityDefinition> _capabilities =
    [
        new("assistant.stop", IntentDomain.ConversationControl, "Stop current assistant speech or output.", "Assistant.Stop", ["stop", "stop talking", "shut up"], [], 0.9),
        new("assistant.cancel", IntentDomain.ConversationControl, "Cancel the current pending action.", "Assistant.Cancel", ["cancel", "never mind", "abort"], [], 0.9),
        new("assistant.repeat", IntentDomain.ConversationControl, "Repeat the last assistant response.", "Assistant.Repeat", ["repeat", "say that again"], [], 0.8),

        new("system.get_time", IntentDomain.LocalSystem, "Get the current local clock time.", "SystemResourceTool.GetTime", ["time", "clock", "current time"], ["time complexity", "great time", "last time", "remember the time"], 0.72),
        new("system.get_date", IntentDomain.LocalSystem, "Get the current local date.", "SystemResourceTool.GetDate", ["date", "today", "current date"], [], 0.72),
        new("system.get_timezone", IntentDomain.LocalSystem, "Get the current local or system timezone.", "SystemResourceTool.GetTimezone", ["timezone", "time zone", "tz"], [], 0.72),
        new("system.get_cpu", IntentDomain.LocalSystem, "Get current CPU usage.", "SystemResourceTool.GetCpu", ["cpu", "processor"], [], 0.78),
        new("system.get_memory", IntentDomain.LocalSystem, "Get current system memory or RAM usage.", "SystemResourceTool.GetMemory", ["memory", "ram", "system memory", "pc memory"], ["memory of backpacking", "memory of childhood"], 0.78),
        new("system.get_disk", IntentDomain.LocalSystem, "Get current disk usage or available drive space.", "SystemResourceTool.GetDisk", ["disk", "drive space", "storage"], [], 0.78),
        new("system.get_battery", IntentDomain.LocalSystem, "Get current battery status.", "SystemResourceTool.GetBattery", ["battery"], [], 0.78),
        new("system.get_network", IntentDomain.LocalSystem, "Get current network status.", "SystemResourceTool.GetNetwork", ["network", "internet connection"], [], 0.78),

        new("audio.get_volume", IntentDomain.Audio, "Get current system or application volume.", "AudioTool.GetVolume", ["current volume", "system volume", "speaker volume"], ["volume of a cylinder", "volume of a sphere"], 0.78),
        new("audio.set_volume", IntentDomain.Audio, "Set or adjust current volume.", "AudioTool.SetVolume", ["set volume", "volume to", "louder", "quieter"], ["calculate volume", "formula for volume"], 0.78),
        new("audio.mute", IntentDomain.Audio, "Mute audio.", "AudioTool.Mute", ["mute", "mute sound"], [], 0.8),
        new("audio.unmute", IntentDomain.Audio, "Unmute audio.", "AudioTool.Unmute", ["unmute"], [], 0.8),

        new("app.open", IntentDomain.AppControl, "Open or launch an application.", "OpenApplicationTool.Open", ["open", "launch", "start"], [], 0.72),
        new("app.close", IntentDomain.AppControl, "Close an application.", "ApplicationTool.Close", ["close", "quit"], [], 0.78),
        new("app.focus", IntentDomain.AppControl, "Focus or switch to an application.", "ApplicationTool.Focus", ["focus", "switch to"], [], 0.78),

        new("url.open", IntentDomain.WebSearch, "Open a safe HTTP or HTTPS URL in the default browser.", "OpenUrlTool.Open", ["open", "go to", "take me to", "browse", "visit"], [], 0.72),

        new("memory.search", IntentDomain.Memory, "Search personal or long-term memory.", "MemoryTool.Search", ["remember", "memory of", "last time we talked"], ["ram", "system memory"], 0.76),
        new("memory.save", IntentDomain.Memory, "Save information to memory.", "MemoryTool.Save", ["remember this", "save this"], [], 0.76),
        new("memory.forget", IntentDomain.Memory, "Forget saved memory.", "MemoryTool.Forget", ["forget this", "forget that"], [], 0.76),

        new("chat.general", IntentDomain.GeneralChat, "Use normal chat.", "DeepInfra.Chat", ["chat", "question"], [], 0.65),
        new("chat.recommendation", IntentDomain.GeneralChat, "Use chat for recommendations.", "DeepInfra.Chat", ["recommend", "best", "what gear"], [], 0.65),
        new("chat.reasoning", IntentDomain.GeneralChat, "Use chat for reasoning and explanations.", "DeepInfra.Chat", ["explain", "design", "time complexity"], [], 0.65)
    ];

    public IReadOnlyList<CapabilityDefinition> GetAll()
    {
        return _capabilities;
    }

    public CapabilityDefinition? Find(string id)
    {
        return _capabilities.FirstOrDefault(capability =>
            string.Equals(capability.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
