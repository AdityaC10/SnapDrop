using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SnapDrop.Windows;

// Requests Windows' own shortcut in response to an explicit user command. No shortcut is registered or replaced.
public static class SnippingToolLauncher
{
    public static void Launch()
    {
        foreach (var key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
            if ((GetAsyncKeyState(key) & 0x8000) != 0) throw new InvalidOperationException("Release the modifier keys and try capture again.");
        var input = new[] { Key(0x5B), Key(0x10), Key(0x53), Key(0x53, true), Key(0x10, true), Key(0x5B, true) };
        if (SendInput((uint)input.Length, input, Marshal.SizeOf<Input>()) != input.Length)
        {
            var release = new[] { Key(0x53, true), Key(0x10, true), Key(0x5B, true) };
            _ = SendInput((uint)release.Length, release, Marshal.SizeOf<Input>());
            throw new Win32Exception("Windows did not accept the capture shortcut. Try Win + Shift + S directly.");
        }
    }
    private static Input Key(ushort code, bool up = false) => new() { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = code, Flags = up ? 2u : 0u } } };
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort Scan; public uint Flags; public uint Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X; public int Y; public uint Data; public uint Flags; public uint Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
