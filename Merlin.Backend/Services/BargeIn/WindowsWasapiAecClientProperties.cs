using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Merlin.Backend.Services.BargeIn;

internal static class WindowsWasapiAecClientProperties
{
    public const string ModeNAudio = "NAudio";
    public const string ModeCustomInterop = "CustomInterop";
    public const string ModeDisabledForDiagnostics = "DisabledForDiagnostics";

    public static WindowsAecClientPropertiesDiagnostics CreateNAudioDiagnostics()
    {
        var properties = CreateNAudioProperties();
        return new WindowsAecClientPropertiesDiagnostics(
            ModeNAudio,
            Marshal.SizeOf<AudioClientProperties>(),
            properties.bIsOffload,
            (int)properties.eCategory,
            properties.eCategory.ToString(),
            (int)properties.Options);
    }

    public static AudioClientProperties CreateNAudioProperties()
    {
        return new AudioClientProperties
        {
            cbSize = (uint)Marshal.SizeOf<AudioClientProperties>(),
            bIsOffload = 0,
            eCategory = AudioStreamCategory.Communications,
            Options = AudioClientStreamOptions.None
        };
    }

    public static WindowsAecClientPropertiesDiagnostics CreateCustomInteropDiagnostics()
    {
        var properties = CreateCustomInteropProperties();
        return new WindowsAecClientPropertiesDiagnostics(
            ModeCustomInterop,
            Marshal.SizeOf<NativeAudioClientProperties>(),
            properties.bIsOffload,
            properties.eCategory,
            ((AudioStreamCategory)properties.eCategory).ToString(),
            properties.Options);
    }

    public static NativeAudioClientProperties CreateCustomInteropProperties()
    {
        return new NativeAudioClientProperties
        {
            cbSize = (uint)Marshal.SizeOf<NativeAudioClientProperties>(),
            bIsOffload = 0,
            eCategory = (int)AudioStreamCategory.Communications,
            Options = 0
        };
    }

    public static string NormalizeMode(string? mode)
    {
        if (string.Equals(mode, ModeCustomInterop, StringComparison.OrdinalIgnoreCase))
        {
            return ModeCustomInterop;
        }

        if (string.Equals(mode, ModeDisabledForDiagnostics, StringComparison.OrdinalIgnoreCase))
        {
            return ModeDisabledForDiagnostics;
        }

        return ModeNAudio;
    }

    public static string GetHResultName(int hresult)
    {
        return unchecked((uint)hresult) switch
        {
            0x00000000 => "S_OK",
            0x88890001 => "AUDCLNT_E_NOT_INITIALIZED",
            0x88890002 => "AUDCLNT_E_ALREADY_INITIALIZED",
            0x88890003 => "AUDCLNT_E_WRONG_ENDPOINT_TYPE",
            0x88890004 => "AUDCLNT_E_DEVICE_INVALIDATED",
            0x88890008 => "AUDCLNT_E_UNSUPPORTED_FORMAT",
            0x8889000A => "AUDCLNT_E_EXCLUSIVE_MODE_NOT_ALLOWED",
            0x8889000E => "AUDCLNT_E_ENDPOINT_CREATE_FAILED",
            0x88890017 => "AUDCLNT_E_SERVICE_NOT_RUNNING",
            0x80004002 => "E_NOINTERFACE",
            0x80070057 => "E_INVALIDARG",
            _ => "UNKNOWN_HRESULT"
        };
    }

    public static string FormatSetClientPropertiesFailureReason(int hresult)
    {
        return $"Windows WASAPI AEC unavailable: SetClientProperties failed. HRESULT: 0x{unchecked((uint)hresult):X8} ({GetHResultName(hresult)}).";
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeAudioClientProperties
    {
        public uint cbSize;
        public int bIsOffload;
        public int eCategory;
        public int Options;
    }
}

internal sealed record WindowsAecClientPropertiesDiagnostics(
    string Mode,
    int CbSize,
    int BIsOffload,
    int ECategory,
    string ECategoryName,
    int Options);
