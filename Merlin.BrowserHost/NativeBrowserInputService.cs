using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Merlin.BrowserHost;

internal static class NativeBrowserInputService
{
    private const uint InputMouse = 0;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;

    public static void LeftClick(int screenX, int screenY)
    {
        if (!SetCursorPos(screenX, screenY))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetCursorPos failed.");
        }

        Span<Input> inputs =
        [
            MouseInput(MouseEventLeftDown),
            MouseInput(MouseEventLeftUp)
        ];

        var sent = SendInput((uint)inputs.Length, ref inputs[0], Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed.");
        }
    }

    private static Input MouseInput(uint flags) =>
        new()
        {
            Type = InputMouse,
            Mouse = new MouseInputData
            {
                Flags = flags
            }
        };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, ref Input inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInputData Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
