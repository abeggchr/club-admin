using System.IO.Compression;

namespace backend.Tests.Accounting;

/// <summary>The in-memory zip of CAMT test files, shared by the import tests.</summary>
internal static class CamtTestFiles
{
    public static byte[] Zip(params (string Name, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }
}
