using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Overture.Infrastructure;

public sealed class OvertureOptions
{
    public string ToolImage { get; set; } = "overture-tools:latest";
    public string DockerBin { get; set; } = "docker";
    public string WorkDir { get; set; } = "/tmp";
    public int TimeoutSeconds { get; set; } = 600;
    public int DefaultBatchSize { get; set; } = 5000;
}

public interface IOvertureCliRunner
{
    Task<OvertureDownloadResult> DownloadAsync(
        double minLng, double minLat,
        double maxLng, double maxLat,
        CancellationToken cancellationToken = default);
}

public sealed record OvertureDownloadResult(
    string OutputPath,
    long FileBytes,
    int DurationMs);

public sealed class OvertureCliRunner : IOvertureCliRunner
{
    private readonly OvertureOptions _options;
    private readonly ILogger<OvertureCliRunner> _logger;

    public OvertureCliRunner(
        IOptions<OvertureOptions> options,
        ILogger<OvertureCliRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OvertureDownloadResult> DownloadAsync(
        double minLng, double minLat,
        double maxLng, double maxLat,
        CancellationToken cancellationToken = default)
    {
        var bbox = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3}",
            minLng, minLat, maxLng, maxLat);

        var fileName = $"overture-{Guid.NewGuid():N}.jsonl";
        var containerPath = $"/work/{fileName}";
        var hostPath = Path.Combine(_options.WorkDir, fileName);

        var args = new[]
        {
            "run", "--rm",
            "--network=host",
            "-v", $"{_options.WorkDir}:/work",
            _options.ToolImage,
            "download",
            $"--bbox={bbox}",
            "-f", "geojson",
            "--type=place",
            "-o", containerPath
        };

        _logger.LogInformation(
            "Overture CLI: docker {Args}",
            string.Join(" ", args));

        var psi = new ProcessStartInfo(_options.DockerBin);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        var sw = Stopwatch.StartNew();
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException(
                $"Failed to start '{_options.DockerBin}'.");

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            throw new TimeoutException(
                $"Overture CLI timed out after {_options.TimeoutSeconds}s. " +
                $"Bbox={bbox}.");
        }

        sw.Stop();

        var stderr = await stderrTask;
        var exitCode = proc.ExitCode;

        if (exitCode != 0)
        {
            try { File.Delete(hostPath); } catch { }
            throw new InvalidOperationException(
                $"Overture CLI exited with code {exitCode}. stderr: {stderr}");
        }

        if (!File.Exists(hostPath))
        {
            throw new InvalidOperationException(
                $"Overture CLI exited 0 but output file missing: {hostPath}. " +
                $"stdout: {await stdoutTask} stderr: {stderr}");
        }

        var info = new FileInfo(hostPath);
        _logger.LogInformation(
            "Overture CLI done in {Ms}ms, {Bytes} bytes -> {Path}",
            sw.ElapsedMilliseconds, info.Length, hostPath);

        return new OvertureDownloadResult(
            hostPath, info.Length, (int)sw.ElapsedMilliseconds);
    }
}