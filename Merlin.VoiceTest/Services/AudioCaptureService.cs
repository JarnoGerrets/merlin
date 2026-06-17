using Merlin.VoiceTest.Models;
using NAudio.Wave;

namespace Merlin.VoiceTest.Services;

public sealed class AudioCaptureService
{
    public IReadOnlyList<string> ListInputDevices()
    {
        var devices = new List<string>();
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add($"{i}: {caps.ProductName} ({caps.Channels} channels)");
        }

        return devices;
    }

    public int ResolveDeviceNumber(string? configuredDevice)
    {
        if (string.IsNullOrWhiteSpace(configuredDevice))
        {
            return 0;
        }

        if (int.TryParse(configuredDevice, out var deviceNumber))
        {
            return deviceNumber;
        }

        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            if (caps.ProductName.Contains(configuredDevice, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    public async Task<string> RecordFixedWindowAsync(
        string outputPath,
        TimeSpan duration,
        VoiceTestOptions options,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var deviceNumber = ResolveDeviceNumber(options.Device);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(options.TargetSampleRate, 16, options.Channels),
            BufferMilliseconds = 50
        };
        await using var writer = new WaveFileWriter(outputPath, waveIn.WaveFormat);
        waveIn.DataAvailable += (_, args) => writer.Write(args.Buffer, 0, args.BytesRecorded);
        waveIn.RecordingStopped += (_, args) =>
        {
            if (args.Exception is not null)
            {
                tcs.TrySetException(args.Exception);
            }
            else
            {
                tcs.TrySetResult();
            }
        };

        waveIn.StartRecording();
        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            waveIn.StopRecording();
        }

        await tcs.Task.WaitAsync(cancellationToken);
        return outputPath;
    }
}
