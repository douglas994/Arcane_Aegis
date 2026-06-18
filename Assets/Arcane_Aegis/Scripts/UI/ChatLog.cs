using System;
using System.Collections.Generic;

namespace Arcane_Aegis.UI
{
    /// <summary>Client-side rolling chat log (recent lines, capped). Channel-agnostic — handlers add already-formatted
    /// lines (e.g. party). The chat panel subscribes to <see cref="OnChanged"/> and re-renders. Foundation for the wider
    /// chat system (global/zone/whisper land as more line sources).</summary>
    public static class ChatLog
    {
        private const int MaxLines = 100;
        private static readonly List<string> _lines = new();

        public static event Action OnChanged;
        public static IReadOnlyList<string> Lines => _lines;

        public static void AddLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _lines.Add(line);
            if (_lines.Count > MaxLines) _lines.RemoveAt(0);
            OnChanged?.Invoke();
        }
    }
}
