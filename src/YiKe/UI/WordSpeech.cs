using System;
using System.Runtime.InteropServices;

namespace YiKe.UI;

internal sealed class WordSpeech : IDisposable
{
    object? voice;
    internal void Speak(string text)
    {
        var type = Type.GetTypeFromProgID("SAPI.SpVoice") ?? throw new InvalidOperationException("Windows 语音服务不可用。");
        voice ??= Activator.CreateInstance(type);
        dynamic speaker = voice!;
        dynamic voices = speaker.GetVoices("Language=409");
        try { if (voices.Count == 0) throw new InvalidOperationException("尚未安装英语语音，请在 Windows 语言设置中添加英语语音。"); speaker.Voice = voices.Item(0); speaker.Speak(text, 3); }
        finally { if (Marshal.IsComObject(voices)) Marshal.ReleaseComObject(voices); }
    }
    internal void Stop() { if (voice is not null) { dynamic speaker = voice; speaker.Speak("", 3); } }
    public void Dispose() { Stop(); if (voice is not null && Marshal.IsComObject(voice)) Marshal.ReleaseComObject(voice); voice = null; }
}
