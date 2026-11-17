using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Accessibility;
using YiKe.Core;

namespace YiKe.Selection;

// A read-only fallback for applications exposing IAccessible2 but no UIA TextPattern.
// Only explicit selection offsets are read. No accName/accValue or document-wide text queries.
internal static class LegacyTextReader
{
    internal interface ISelectedText
    {
        int Count { get; }
        bool Range(int index, out int start, out int end);
        bool Bounds(int offset, out int x, out int y, out int width, out int height);
        string Text(int start, int end);
    }
    internal static SelectionReadResult ReadSelected(ISelectedText provider, Gesture gesture)
    {
        int count = provider.Count;
        if (count is < 0 or > 1) return new(ReadStatus.Unsupported);
        var parts = new List<string>();
        for (int index = 0; index < count; index++)
        {
            if (!provider.Range(index, out int start, out int end) || start < 0 || end < start) return new(ReadStatus.Unsupported);
            if (start == end) continue;
            if ((long)end - start > 8000) return new(ReadStatus.TooLong);
            if (!provider.Bounds(start, out int x1, out int y1, out int w1, out int h1) ||
                !provider.Bounds(end - 1, out int x2, out int y2, out int w2, out int h2) || w1 <= 0 || w2 <= 0 || h1 <= 0 || h2 <= 0) continue;
            long left = Math.Min(x1, x2) - 6L, top = Math.Min(y1, y2) - 6L;
            long right = Math.Max((long)x1 + w1, (long)x2 + w2) + 6, bottom = Math.Max((long)y1 + h1, (long)y2 + h2) + 6;
            bool Contains(int x, int y) => x >= left && x <= right && y >= top && y <= bottom;
            // Requiring both endpoints avoids accepting one paragraph of a multi-paragraph selection.
            if (!Contains(gesture.StartX, gesture.StartY) || !Contains(gesture.X, gesture.Y)) continue;
            string text = provider.Text(start, end);
            if (text.Length != end - start) return new(ReadStatus.Unsupported);
            if (text.Contains('\uFFFC')) return new(ReadStatus.Unsupported); // Embedded objects are not complete text.
            if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
        }
        string value = string.Join("\n", parts);
        return string.IsNullOrWhiteSpace(value) ? new(ReadStatus.NoSelection) : TextLimit.Allowed(value) ? new(ReadStatus.Success, value) : new(ReadStatus.TooLong);
    }
    internal static SelectionReadResult? TryRead(Gesture gesture)
    {
        var owned = new List<object>();
        SelectionReadResult? best = null;
        try
        {
            foreach (var point in new[] { new Native.Point { X = gesture.StartX, Y = gesture.StartY }, new Native.Point { X = gesture.X, Y = gesture.Y } })
            {
                if (!CurrentSource(gesture) || Native.GetAncestor(Native.WindowFromPoint(point), 2).ToInt64() != gesture.Window) continue;
                if (AccessibleObjectFromPoint(point, out var accessible, out object child) < 0 || accessible is null) continue;
                owned.Add(accessible);
                if ((Convert.ToInt32(accessible.get_accState(child)) & 0x20000000) != 0) return new(ReadStatus.AccessDenied);
                if (child is int childId && childId != 0 && accessible.get_accChild(child) is IAccessible nested) { accessible = nested; owned.Add(nested); }
                for (int depth = 0; accessible is not null && depth < 12; depth++)
                {
                    if (WindowFromAccessibleObject(accessible, out IntPtr hwnd) < 0 || Native.GetAncestor(hwnd, 2).ToInt64() != gesture.Window) break;
                    if ((Convert.ToInt32(accessible.get_accState(0)) & 0x20000000) != 0) return new(ReadStatus.AccessDenied);
                    int role = Convert.ToInt32(accessible.get_accRole(0));
                    if (role is 1 or 3 or 51) break; // Title bar, scrollbar, slider.
                    if (accessible is IServiceProvider service)
                    {
                        Guid sid = typeof(IAccessible).GUID, iid = typeof(IAccessibleText).GUID;
                        if (service.QueryService(ref sid, ref iid, out object result) >= 0 && result is IAccessibleText text)
                        {
                            owned.Add(result);
                            var selected = ReadSelected(new ComText(text), gesture);
                            if (selected.Status is ReadStatus.Success or ReadStatus.TooLong) best = selected;
                            else if (best is null && selected.Status == ReadStatus.NoSelection) best = selected;
                        }
                    }
                    if (accessible.accParent is not IAccessible parent) break;
                    accessible = parent; owned.Add(parent);
                }
            }
            return CurrentSource(gesture) ? best : new(ReadStatus.TargetGone);
        }
        catch (Exception error) when (error is COMException or InvalidCastException or ArgumentException or OverflowException or InvalidComObjectException)
        { return null; }
        finally
        {
            for (int i = owned.Count - 1; i >= 0; i--) try { if (Marshal.IsComObject(owned[i])) Marshal.ReleaseComObject(owned[i]); } catch (InvalidComObjectException) { }
        }
    }
    static bool CurrentSource(Gesture gesture) => Native.GetForegroundWindow().ToInt64() == gesture.Window && Native.ProcessId(new IntPtr(gesture.Window)) == gesture.ProcessId;
    sealed class ComText(IAccessibleText text) : ISelectedText
    {
        public int Count => text.Selections(out int count) >= 0 ? count : 0;
        public bool Range(int index, out int start, out int end) => text.Selection(index, out start, out end) >= 0;
        public bool Bounds(int offset, out int x, out int y, out int width, out int height) => text.CharacterExtents(offset, 0, out x, out y, out width, out height) >= 0;
        public string Text(int start, int end) => text.Text(start, end, out string value) >= 0 ? value ?? "" : "";
    }
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromPoint(Native.Point point, [MarshalAs(UnmanagedType.Interface)] out IAccessible accessible, [MarshalAs(UnmanagedType.Struct)] out object child);
    [DllImport("oleacc.dll")] static extern int WindowFromAccessibleObject(IAccessible accessible, out IntPtr window);
    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IServiceProvider
    {
        [PreserveSig] int QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result);
    }
    // Vtable order through get_text follows Linux Foundation's AccessibleText.idl.
    // The unused methods preserve COM slots and are never called.
    [ComImport, Guid("24FD2FFB-3AAD-4A08-8335-A3AD89C0FB4B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAccessibleText
    {
        [PreserveSig] int AddSelection(int start, int end);
        [PreserveSig] int Attributes(int offset, out int start, out int end, [MarshalAs(UnmanagedType.BStr)] out string attributes);
        [PreserveSig] int Caret(out int offset);
        [PreserveSig] int CharacterExtents(int offset, int coordinateType, out int x, out int y, out int width, out int height);
        [PreserveSig] int Selections(out int count);
        [PreserveSig] int OffsetAtPoint(int x, int y, int coordinateType, out int offset);
        [PreserveSig] int Selection(int index, out int start, out int end);
        [PreserveSig] int Text(int start, int end, [MarshalAs(UnmanagedType.BStr)] out string value);
    }
}
