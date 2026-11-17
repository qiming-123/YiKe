using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;
using YiKe.Core;

namespace YiKe.Selection;

// Attach to the existing Writer instance only. Never create an application, move
// its selection, read Document.Content, change settings or use the clipboard.
internal static class WpsSelectionAdapter
{
    internal readonly record struct SelectionState(int Type, int Start, int End, int Story);
    internal interface ISelectedRange
    {
        SelectionState State { get; }
        bool IsCurrent { get; }
        bool Bounds(out int left, out int top, out int width, out int height);
        string Text();
    }
    internal static bool IsWriter(string processName) => processName.Equals("wps", StringComparison.OrdinalIgnoreCase);
    internal static SelectionReadResult ReadSelected(ISelectedRange range, Gesture gesture)
    {
        if (!range.IsCurrent) return new(ReadStatus.TargetGone);
        var state = range.State;
        if (state.Start < 0 || state.End < state.Start) return new(ReadStatus.Unsupported);
        // WPS can return the character beside an insertion point from Text.
        // Check type AND endpoints before making any text query.
        if (state.End == state.Start || state.Type is 0 or 1) return new(ReadStatus.NoSelection);
        if (state.Type != 2) return new(ReadStatus.Unsupported); // Normal continuous text only.
        if ((long)state.End - state.Start > 8000) return new(ReadStatus.TooLong);
        if (!range.Bounds(out int left, out int top, out int width, out int height) || width <= 0 || height <= 0) return new(ReadStatus.Unsupported);
        bool Near(int x, int y) => x >= (long)left - 12 && x <= (long)left + width + 12 && y >= (long)top - 12 && y <= (long)top + height + 12;
        if (!Near(gesture.StartX, gesture.StartY) || !Near(gesture.X, gesture.Y)) return new(ReadStatus.NoSelection);
        if (!range.IsCurrent) return new(ReadStatus.TargetGone);
        string text = range.Text();
        if (!range.IsCurrent) return new(ReadStatus.TargetGone);
        if (text.Length != state.End - state.Start || text.IndexOfAny(new[] { '\0', '\uFFFC' }) >= 0) return new(ReadStatus.Unsupported);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\v', '\n');
        return string.IsNullOrWhiteSpace(text) ? new(ReadStatus.NoSelection) : TextLimit.Allowed(text) ? new(ReadStatus.Success, text) : new(ReadStatus.TooLong);
    }
    internal static SelectionReadResult? TryRead(Gesture gesture)
    {
        var owned = new List<object>();
        try
        {
            using var process = Process.GetProcessById(gesture.ProcessId);
            if (!IsWriter(process.ProcessName)) return null;
            if (!CurrentSource(gesture)) return new(ReadStatus.TargetGone);
            foreach (var point in new[] { new Native.Point { X = gesture.StartX, Y = gesture.StartY }, new Native.Point { X = gesture.X, Y = gesture.Y } })
            {
                var hit = Native.WindowFromPoint(point);
                // Writer creates a tool window after mouse-up, sometimes in its helper
                // process. Accept its explicit native owner, then validate BOTH endpoints
                // against the selected range's screen bounds before reading any text.
                if (Native.GetAncestor(hit, 2).ToInt64() != gesture.Window && Native.GetAncestor(hit, 3).ToInt64() != gesture.Window) return new(ReadStatus.NoSelection);
            }
            Guid clsid = new("000209FF-0000-4b30-A977-D214852036FF");
            if (GetActiveObject(ref clsid, IntPtr.Zero, out object app) < 0 || app is null) return null;
            owned.Add(app);
            object window = ((dynamic)app).ActiveWindow; owned.Add(window);
            if (!MatchesWindow(window, gesture)) return null;
            object selection = ((dynamic)window).Selection; owned.Add(selection);
            object range = ((dynamic)selection).Range; owned.Add(range);
            object document = ((dynamic)range).Document; owned.Add(document);
            return ReadSelected(new ComRange(app, window, selection, range, document, gesture), gesture);
        }
        catch (COMException error) when (error.ErrorCode == unchecked((int)0x80070005)) { return new(ReadStatus.AccessDenied); }
        catch (Exception error) when (error is COMException or RuntimeBinderException or ArgumentException or InvalidOperationException or InvalidCastException or OverflowException)
        { return CurrentSource(gesture) ? null : new(ReadStatus.TargetGone); }
        finally { for (int i = owned.Count - 1; i >= 0; i--) Release(owned[i]); }
    }
    static bool CurrentSource(Gesture gesture) => Native.GetForegroundWindow().ToInt64() == gesture.Window && Native.IsWindow(new IntPtr(gesture.Window)) && Native.ProcessId(new IntPtr(gesture.Window)) == gesture.ProcessId;
    static bool MatchesWindow(object window, Gesture gesture)
    {
        long handle = Convert.ToInt64(((dynamic)window).Hwnd);
        var root = Native.GetAncestor(new IntPtr(handle), 2);
        return root.ToInt64() == gesture.Window && Native.ProcessId(root) == gesture.ProcessId;
    }
    static SelectionState StateOf(object selection, object range) => new((int)((dynamic)selection).Type, (int)((dynamic)range).Start, (int)((dynamic)range).End, (int)((dynamic)range).StoryType);
    static bool SameIdentity(object first, object second)
    {
        IntPtr a = Marshal.GetIUnknownForObject(first), b = IntPtr.Zero;
        try { b = Marshal.GetIUnknownForObject(second); return a == b; }
        finally { if (b != IntPtr.Zero) Marshal.Release(b); Marshal.Release(a); }
    }
    static void Release(object? value) { if (value is not null) try { if (Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); } catch (InvalidComObjectException) { } }
    sealed class ComRange(object app, object window, object selection, object range, object document, Gesture gesture) : ISelectedRange
    {
        public SelectionState State { get; } = StateOf(selection, range);
        public bool IsCurrent
        {
            get
            {
                if (!CurrentSource(gesture)) return false;
                object? currentWindow = null, currentSelection = null, currentRange = null, currentDocument = null;
                try
                {
                    currentWindow = ((dynamic)app).ActiveWindow;
                    if (!MatchesWindow(currentWindow, gesture) || !SameIdentity(window, currentWindow)) return false;
                    currentSelection = ((dynamic)currentWindow).Selection;
                    currentRange = ((dynamic)currentSelection).Range;
                    currentDocument = ((dynamic)currentRange).Document;
                    return SameIdentity(document, currentDocument) && State == StateOf(currentSelection, currentRange) && CurrentSource(gesture);
                }
                finally { Release(currentDocument); Release(currentRange); Release(currentSelection); Release(currentWindow); }
            }
        }
        public bool Bounds(out int left, out int top, out int width, out int height)
        {
            left = top = width = height = 0;
            ((dynamic)window).GetPoint(out left, out top, out width, out height, range);
            return true;
        }
        public string Text() => (string?)((dynamic)range).Text ?? "";
    }
    [DllImport("oleaut32.dll")] static extern int GetActiveObject(ref Guid clsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object result);
}
