using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace YiKe.Selection;

// Copy common native clipboard formats eagerly, without retaining an OLE proxy
// or deserializing another application's private objects. Never write a new copy
// over intervening clipboard data: compare the sequence while holding its lock.
internal sealed class NativeClipboardSnapshot
{
    const ulong MaximumBytes = 64UL * 1024 * 1024;
    readonly List<(uint Format, byte[] Data)> entries;
    internal uint Sequence { get; }
    NativeClipboardSnapshot(uint sequence, List<(uint, byte[])> entries) { Sequence = sequence; this.entries = entries; }
    internal static NativeClipboardSnapshot? Capture()
    {
        if (!Open(IntPtr.Zero)) return null;
        try
        {
            uint format = 0;
            var entries = new List<(uint, byte[])>(); ulong total = 0; int count = 0;
            while ((format = EnumClipboardFormats(format)) != 0)
            {
                if (++count > 128) return null;
                bool common = format is 1 or 7 or 8 or 13 or 15 or 16 or 17;
                if (!common)
                {
                    var name = new StringBuilder(128); GetClipboardFormatName(format, name, name.Capacity);
                    common = name.ToString() is "HTML Format" or "Rich Text Format" or "CSV" or "PNG" or "Preferred DropEffect";
                }
                if (!common) continue;
                IntPtr handle = GetClipboardData(format); ulong size = GlobalSize(handle).ToUInt64();
                if (handle == IntPtr.Zero || size == 0) continue;
                if (size > MaximumBytes || total + size > MaximumBytes) return null;
                IntPtr pointer = GlobalLock(handle); if (pointer == IntPtr.Zero) return null;
                try { var bytes = new byte[(int)size]; Marshal.Copy(pointer, bytes, 0, bytes.Length); entries.Add((format, bytes)); total += size; }
                finally { GlobalUnlock(handle); }
            }
            return count == 0 || entries.Count > 0 ? new(Native.GetClipboardSequenceNumber(), entries) : null;
        }
        finally { CloseClipboard(); }
    }
    internal bool Restore(uint copiedSequence, IntPtr owner)
    {
        if (copiedSequence == 0) return false;
        var handles = new IntPtr[entries.Count];
        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                handles[i] = GlobalAlloc(2, (UIntPtr)entries[i].Data.Length);
                if (handles[i] == IntPtr.Zero) return false;
                IntPtr pointer = GlobalLock(handles[i]); if (pointer == IntPtr.Zero) return false;
                try { Marshal.Copy(entries[i].Data, 0, pointer, entries[i].Data.Length); }
                finally { GlobalUnlock(handles[i]); }
            }
            if (!Open(owner)) return false;
            try
            {
                if (Native.GetClipboardSequenceNumber() != copiedSequence || !EmptyClipboard()) return false;
                bool complete = true;
                for (int i = 0; i < entries.Count; i++)
                    if (SetClipboardData(entries[i].Format, handles[i]) != IntPtr.Zero) handles[i] = IntPtr.Zero;
                    else complete = false;
                return complete;
            }
            finally { CloseClipboard(); }
        }
        finally { foreach (var handle in handles) if (handle != IntPtr.Zero) GlobalFree(handle); }
    }
    static bool Open(IntPtr owner)
    {
        for(int attempt=0;attempt<5;attempt++){if(OpenClipboard(owner))return true;Thread.Sleep(10);}
        return false;
    }
    [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    [DllImport("user32.dll")] static extern bool EmptyClipboard();
    [DllImport("user32.dll")] static extern uint EnumClipboardFormats(uint format);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClipboardFormatName(uint format, StringBuilder name, int maximum);
    [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr data);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags, UIntPtr size);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr data);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr data);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr data);
}
