using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Merlin.Backend.Services.BargeIn;

internal static class BargeInAudioFrameConverter
{
    public static float[] ResampleMono(ReadOnlySpan<float> input, int inputSampleRate, int outputSampleRate)
    {
        if (input.IsEmpty)
        {
            return [];
        }

        if (inputSampleRate <= 0 || outputSampleRate <= 0 || inputSampleRate == outputSampleRate)
        {
            return input.ToArray();
        }

        var outputLength = Math.Max(1, (int)Math.Round(input.Length * (double)outputSampleRate / inputSampleRate));
        var output = new float[outputLength];
        var ratio = (input.Length - 1) / (double)Math.Max(1, outputLength - 1);
        for (var index = 0; index < output.Length; index++)
        {
            var sourcePosition = index * ratio;
            var left = (int)Math.Floor(sourcePosition);
            var right = Math.Min(input.Length - 1, left + 1);
            var fraction = sourcePosition - left;
            output[index] = (float)(input[left] + ((input[right] - input[left]) * fraction));
        }

        return output;
    }

    public static float[] ConvertCaptureBufferToMonoFloat(IntPtr buffer, int framesAvailable, WaveFormat format, bool isSilent)
    {
        var output = new float[Math.Max(0, framesAvailable)];
        if (isSilent || framesAvailable <= 0)
        {
            return output;
        }

        var channels = Math.Max(1, format.Channels);
        var bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        var bytes = framesAvailable * channels * bytesPerSample;
        var raw = new byte[bytes];
        Marshal.Copy(buffer, raw, 0, raw.Length);

        for (var frame = 0; frame < framesAvailable; frame++)
        {
            double mixed = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                mixed += ReadSample(raw, (frame * channels + channel) * bytesPerSample, format);
            }

            output[frame] = (float)(mixed / channels);
        }

        return output;
    }

    public static float[] ConvertPcm16ToMonoFloat(ReadOnlySpan<byte> pcm, int channels)
    {
        channels = Math.Max(1, channels);
        var frameCount = pcm.Length / (sizeof(short) * channels);
        var output = new float[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            double mixed = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = (frame * channels + channel) * sizeof(short);
                if (offset + 1 >= pcm.Length)
                {
                    break;
                }

                var sample = (short)(pcm[offset] | (pcm[offset + 1] << 8));
                mixed += sample / 32768.0;
            }

            output[frame] = (float)(mixed / channels);
        }

        return output;
    }

    private static double ReadSample(byte[] raw, int offset, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            return BitConverter.ToSingle(raw, offset);
        }

        return format.BitsPerSample switch
        {
            16 => BitConverter.ToInt16(raw, offset) / 32768.0,
            24 => ReadInt24(raw, offset) / 8388608.0,
            32 => BitConverter.ToInt32(raw, offset) / 2147483648.0,
            _ => 0.0
        };
    }

    private static int ReadInt24(byte[] raw, int offset)
    {
        var value = raw[offset] | (raw[offset + 1] << 8) | (raw[offset + 2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xff000000);
        }

        return value;
    }
}
