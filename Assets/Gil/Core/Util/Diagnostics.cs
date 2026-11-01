using System.Collections.Generic;
using System.Linq;

namespace Gil.Core.Util
{
    public enum Severity
    {
        Warning,
        Error,
    }

    public readonly struct Diagnostic
    {
        public readonly Severity Severity;
        public readonly string Path;
        public readonly string Message;

        public Diagnostic(Severity severity, string path, string message)
        {
            Severity = severity;
            Path = path;
            Message = message;
        }

        public override string ToString() =>
            $"[{(Severity == Severity.Error ? "오류" : "경고")}] {Path}: {Message}";
    }

    public sealed class DiagnosticList
    {
        readonly List<Diagnostic> _items = new List<Diagnostic>();

        public IReadOnlyList<Diagnostic> Items => _items;
        public bool HasErrors => _items.Any(d => d.Severity == Severity.Error);
        public int ErrorCount => _items.Count(d => d.Severity == Severity.Error);
        public int WarningCount => _items.Count(d => d.Severity == Severity.Warning);

        public void Error(string path, string message) => _items.Add(new Diagnostic(Severity.Error, path, message));
        public void Warning(string path, string message) => _items.Add(new Diagnostic(Severity.Warning, path, message));

        public override string ToString() => string.Join("\n", _items);
    }
}
