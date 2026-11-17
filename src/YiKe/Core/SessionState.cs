using System;

namespace YiKe.Core;

// Owned by the UI thread. A selection lease is distinct from the network request generation.
public sealed class SessionState
{
    public bool Enabled { get; private set; }
    public bool Pinned { get; private set; }
    public long SelectionEpoch { get; private set; }
    public SelectionSnapshot? Current { get; private set; }
    public long DisplayedTranslation { get; private set; }
    public void ShowTranslation(long sequence) => DisplayedTranslation = sequence;
    public bool AcceptsTranslation(long sequence, long current) => Enabled && DisplayedTranslation != 0 && sequence == DisplayedTranslation && sequence == current;
    public bool CanReceiveSelection => Enabled && !Pinned;

    public SessionState(bool enabled) => Enabled = enabled;
    public long BeginSelection()
    {
        if (!CanReceiveSelection) throw new InvalidOperationException("当前不接收自动选区。");
        Current = null;
        DisplayedTranslation = 0;
        return ++SelectionEpoch;
    }
    public bool Accepts(long epoch) => CanReceiveSelection && epoch == SelectionEpoch;
    public void SetCurrent(SelectionSnapshot snapshot) => Current = snapshot;
    public void SetPinned(bool pinned)
    {
        if (Pinned == pinned) return;
        Pinned = pinned;
        SelectionEpoch++; // Pinning or unpinning never revives a pending UIA read.
    }
    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (!enabled) Close();
    }
    public void Close()
    {
        SelectionEpoch++;
        Current = null;
        DisplayedTranslation = 0;
        Pinned = false;
    }
    public bool ShouldDismissForForeground(long window, bool ownedByThisApp) =>
        !Pinned && !ownedByThisApp && Current is { Window: not 0 } && Current.Window != window;
}
