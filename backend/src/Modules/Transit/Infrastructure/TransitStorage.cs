using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Transit.Application;
namespace Transit.Infrastructure;

public sealed class TransitStorage(IHostEnvironment env, IConfiguration config)
{
    private readonly string root = Path.GetFullPath(config["Transit:StoragePath"] ?? Path.Combine(env.ContentRootPath, "storage", "transit"));
    public string DirectoryFor(Guid id) => Path.Combine(root, id.ToString("N"));
    public string FileFor(Guid id, string name) => Path.Combine(DirectoryFor(id), Path.GetFileName(name));
    public string[] UploadedFiles(Guid id) => Directory.GetFiles(DirectoryFor(id))
        .Where(f => Path.GetExtension(f).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(f).Equals(".csv", StringComparison.OrdinalIgnoreCase)).ToArray();
    public async Task WriteSnapshot(Guid id, TransitSnapshot snapshot, CancellationToken ct)
    {
        var path = FileFor(id, "validated.json");
        await using (var stream = File.Create(path + ".tmp")) await JsonSerializer.SerializeAsync(stream, snapshot, cancellationToken: ct);
        File.Move(path + ".tmp", path, true);
    }
    public async Task<TransitSnapshot> ReadSnapshot(Guid id, CancellationToken ct)
    {
        await using var stream = File.OpenRead(FileFor(id, "validated.json"));
        return await JsonSerializer.DeserializeAsync<TransitSnapshot>(stream, cancellationToken: ct) ?? throw new InvalidDataException("Không tìm thấy dữ liệu đã kiểm tra.");
    }
}
