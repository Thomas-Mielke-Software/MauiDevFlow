using System.Text.Json;

namespace MauiDevFlow.Tests;

/// <summary>
/// Tests for the JSONL file logging (writer rotation and reader pagination).
/// These tests exercise the file format and rotation logic directly
/// without depending on MAUI-specific types.
/// </summary>
public class FileLoggingTests : IDisposable
{
    private readonly string _logDir;

    public FileLoggingTests()
    {
        _logDir = Path.Combine(Path.GetTempPath(), $"mauidevflow-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_logDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDir))
            Directory.Delete(_logDir, true);
    }

    [Fact]
    public void WriteAndReadEntries()
    {
        WriteEntries(5);
        var entries = ReadEntries(100, 0);

        Assert.Equal(5, entries.Count);
        // Newest first
        Assert.Equal("Message 4", entries[0].GetProperty("m").GetString());
        Assert.Equal("Message 0", entries[4].GetProperty("m").GetString());
    }

    [Fact]
    public void ReadWithLimitAndSkip()
    {
        WriteEntries(20);

        var page1 = ReadEntries(5, 0);
        Assert.Equal(5, page1.Count);
        Assert.Equal("Message 19", page1[0].GetProperty("m").GetString());

        var page2 = ReadEntries(5, 5);
        Assert.Equal(5, page2.Count);
        Assert.Equal("Message 14", page2[0].GetProperty("m").GetString());
    }

    [Fact]
    public void RotationCreatesNewFiles()
    {
        // Write enough to trigger rotation (small max size)
        var currentFile = Path.Combine(_logDir, "log-current.jsonl");

        // Write entries one by one, checking for rotation
        for (int i = 0; i < 100; i++)
        {
            var entry = new
            {
                t = DateTime.UtcNow.ToString("O"),
                l = "Information",
                c = "Test",
                m = $"Message {i} with padding to increase file size {new string('x', 200)}",
                e = (string?)null
            };
            File.AppendAllText(currentFile, JsonSerializer.Serialize(entry) + "\n");

            // Simulate rotation at ~5KB
            if (new FileInfo(currentFile).Length > 5000)
            {
                RotateFiles(3);
            }
        }

        var files = Directory.GetFiles(_logDir, "log-*.jsonl");
        Assert.True(files.Length > 1, "Expected rotation to create multiple files");

        // All entries across all files should be readable
        var allEntries = ReadAllEntries();
        Assert.True(allEntries.Count > 0);
    }

    [Fact]
    public void ReadEmptyDirectory()
    {
        var entries = ReadEntries(100, 0);
        Assert.Empty(entries);
    }

    [Fact]
    public void SkipBeyondAvailable()
    {
        WriteEntries(5);
        var entries = ReadEntries(100, 100);
        Assert.Empty(entries);
    }

    // Helper: write N entries to log-current.jsonl
    private void WriteEntries(int count)
    {
        var currentFile = Path.Combine(_logDir, "log-current.jsonl");
        for (int i = 0; i < count; i++)
        {
            var entry = new
            {
                t = DateTime.UtcNow.AddSeconds(i).ToString("O"),
                l = "Information",
                c = "TestCategory",
                m = $"Message {i}",
                e = (string?)null
            };
            File.AppendAllText(currentFile, JsonSerializer.Serialize(entry) + "\n");
        }
    }

    // Helper: read entries from log files (newest first)
    private List<JsonElement> ReadEntries(int limit, int skip)
    {
        var allEntries = ReadAllEntries();

        return allEntries
            .Skip(skip)
            .Take(limit)
            .ToList();
    }

    // Helper: read all entries across all files, newest first
    private List<JsonElement> ReadAllEntries()
    {
        var entries = new List<JsonElement>();

        // Read current file first (newest)
        var currentFile = Path.Combine(_logDir, "log-current.jsonl");
        if (File.Exists(currentFile))
            entries.AddRange(ReadFile(currentFile));

        // Then rotated files in order (001 = newest rotated)
        var rotatedFiles = Directory.GetFiles(_logDir, "log-*.jsonl")
            .Where(f => !f.EndsWith("log-current.jsonl"))
            .OrderBy(f => f)
            .ToList();

        foreach (var file in rotatedFiles)
            entries.AddRange(ReadFile(file));

        return entries;
    }

    // Read a single JSONL file, return entries in reverse order (newest first)
    private static List<JsonElement> ReadFile(string path)
    {
        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var entries = new List<JsonElement>();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            try { entries.Add(JsonDocument.Parse(lines[i]).RootElement.Clone()); }
            catch { /* skip malformed */ }
        }
        return entries;
    }

    // Simulate rotation: rename current → rotate numbered files
    private void RotateFiles(int maxFiles)
    {
        var currentFile = Path.Combine(_logDir, "log-current.jsonl");
        if (!File.Exists(currentFile)) return;

        // Shift existing rotated files
        for (int i = maxFiles - 1; i >= 1; i--)
        {
            var src = Path.Combine(_logDir, $"log-{i:D3}.jsonl");
            var dst = Path.Combine(_logDir, $"log-{i + 1:D3}.jsonl");
            if (File.Exists(src))
            {
                if (i + 1 > maxFiles) File.Delete(src);
                else File.Move(src, dst, true);
            }
        }

        File.Move(currentFile, Path.Combine(_logDir, "log-001.jsonl"), true);
    }
}
