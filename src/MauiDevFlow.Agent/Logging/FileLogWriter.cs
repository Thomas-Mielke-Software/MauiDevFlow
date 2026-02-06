using System.Text.Json;

namespace MauiDevFlow.Agent.Logging;

/// <summary>
/// Writes log entries to rotating JSONL files.
/// Thread-safe. Each file is capped at a configurable size.
/// </summary>
public class FileLogWriter : IDisposable
{
    private readonly string _logDir;
    private readonly long _maxFileSize;
    private readonly int _maxFiles;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private long _currentSize;
    private bool _disposed;

    private const string CurrentFileName = "log-current.jsonl";

    public string LogDirectory => _logDir;

    public FileLogWriter(string logDir, long maxFileSizeBytes = 1_048_576, int maxFiles = 5)
    {
        _logDir = logDir;
        _maxFileSize = maxFileSizeBytes;
        _maxFiles = maxFiles;
        Directory.CreateDirectory(_logDir);
        OpenCurrentFile();
    }

    public void Write(FileLogEntry entry)
    {
        if (_disposed) return;

        var json = JsonSerializer.Serialize(entry);

        lock (_lock)
        {
            if (_disposed) return;

            _writer!.WriteLine(json);
            _writer.Flush();
            _currentSize += json.Length + Environment.NewLine.Length;

            if (_currentSize >= _maxFileSize)
                Rotate();
        }
    }

    private void OpenCurrentFile()
    {
        var path = Path.Combine(_logDir, CurrentFileName);
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        _currentSize = stream.Length;
        _writer = new StreamWriter(stream) { AutoFlush = false };
    }

    private void Rotate()
    {
        _writer?.Dispose();

        var currentPath = Path.Combine(_logDir, CurrentFileName);

        // Shift existing rotated files up by 1
        for (int i = _maxFiles - 1; i >= 1; i--)
        {
            var src = Path.Combine(_logDir, $"log-{i:D3}.jsonl");
            var dst = Path.Combine(_logDir, $"log-{i + 1:D3}.jsonl");
            if (File.Exists(src))
            {
                if (i + 1 >= _maxFiles)
                    File.Delete(src);
                else
                    File.Move(src, dst, true);
            }
        }

        // Move current to log-001
        if (File.Exists(currentPath))
            File.Move(currentPath, Path.Combine(_logDir, "log-001.jsonl"), true);

        OpenCurrentFile();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            _writer?.Dispose();
        }
    }
}
