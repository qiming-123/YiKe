using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using YiKe.Core;

namespace YiKe.Selection;

internal static class PdfAutomaticCopy
{
    internal static async Task<SelectionReadResult?> Run(Gesture gesture, string title, Func<bool> active, IntPtr clipboardWindow)
    {
        if (!active()) return null;
        var snapshot = await Task.Run(NativeClipboardSnapshot.Capture);
        if (snapshot is null) return null; // Preserve an unsupported or oversized clipboard; manual Ctrl+C still works.
        var modifiers = new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C };
        var timer = Stopwatch.StartNew();
        while (Array.Exists(modifiers, key => GetAsyncKeyState(key) < 0) && timer.ElapsedMilliseconds < 350)
            await Task.Delay(20);
        if (!active() || snapshot.Sequence != Native.GetClipboardSequenceNumber() || Array.Exists(modifiers, key => GetAsyncKeyState(key) < 0)) return null;
        var inputs = new[] { Key(0x11), Key(0x43), Key(0x43, true), Key(0x11, true) };
        uint sent = SendInput(4, inputs, Marshal.SizeOf<Input>());
        if (sent != 4)
        {
            if (sent > 0) SendInput(2, new[] { Key(0x43, true), Key(0x11, true) }, Marshal.SizeOf<Input>());
            return null;
        }
        uint copied = 0;
        try
        {
            timer.Restart();
            // Finish the clipboard transaction even if the selection is canceled;
            // only publishing its text depends on the current selection lease.
            while (timer.ElapsedMilliseconds < 1500)
            {
                await Task.Delay(20);
                uint sequence = Native.GetClipboardSequenceNumber();
                if (sequence == snapshot.Sequence || sequence == 0) continue;
                if (!PdfCopyFallback.SourceOwnsClipboard(gesture)) return null;
                copied = sequence;
                if (!active()) return null;
                var result = await Task.Run(() => PdfCopyFallback.ReadFresh(gesture, title, snapshot.Sequence, sequence));
                if (result is not null) return result;
            }
            return null;
        }
        finally { await Task.Run(() => snapshot.Restore(copied, clipboardWindow)); }
    }
    // YiKe ships only as win-x64: INPUT's native keyboard union begins at byte 8.
    [StructLayout(LayoutKind.Explicit, Size=40)] struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort VirtualKey;
        [FieldOffset(12)] public uint Flags;
    }
    static Input Key(ushort key, bool up=false) => new() { Type=1, VirtualKey=key, Flags=up?2u:0u };
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern uint SendInput(uint count, Input[] inputs, int size);
}
