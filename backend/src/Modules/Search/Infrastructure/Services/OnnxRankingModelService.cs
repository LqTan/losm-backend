using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class OnnxRankingModelService : IOnnxRankingModelService, IDisposable
{
    private readonly ILogger<OnnxRankingModelService> _logger;
    private readonly string _storageDir;
    private readonly string _modelFilePath;
    private readonly string _metadataFilePath;
    private readonly object _lock = new();

    private InferenceSession? _session;
    private bool _isActive;
    private string? _modelName;
    private long _fileSizeBytes;
    private DateTime? _uploadedAt;
    private string? _inputName;
    private int? _featureCount;
    private string? _statusMessage;

    public OnnxRankingModelService(
        ILogger<OnnxRankingModelService> logger,
        Microsoft.Extensions.Hosting.IHostEnvironment? hostEnv = null)
    {
        _logger = logger;

        var rootDir = hostEnv?.ContentRootPath ?? AppDomain.CurrentDomain.BaseDirectory;
        _storageDir = Path.Combine(rootDir, "storage", "models");
        _modelFilePath = Path.Combine(_storageDir, "ranking_lambdamart.onnx");
        _metadataFilePath = Path.Combine(_storageDir, "ranking_metadata.json");

        InitModel();
    }

    private void InitModel()
    {
        try
        {
            if (!Directory.Exists(_storageDir))
            {
                Directory.CreateDirectory(_storageDir);
            }

            if (File.Exists(_metadataFilePath))
            {
                var json = File.ReadAllText(_metadataFilePath);
                var meta = JsonSerializer.Deserialize<StoredModelMetadata>(json);
                if (meta is not null)
                {
                    _isActive = meta.IsActive;
                    _modelName = meta.ModelName;
                    _fileSizeBytes = meta.FileSizeBytes;
                    _uploadedAt = meta.UploadedAt;
                }
            }

            var modelPath = _modelFilePath;
            if (!File.Exists(modelPath))
            {
                var altPath = Path.Combine(_storageDir, "ranker.onnx");
                if (File.Exists(altPath))
                {
                    modelPath = altPath;
                }
            }

            if (File.Exists(modelPath))
            {
                var session = new InferenceSession(modelPath);
                _session = session;
                _fileSizeBytes = new FileInfo(modelPath).Length;
                _uploadedAt ??= File.GetLastWriteTimeUtc(modelPath);
                _modelName ??= Path.GetFileName(modelPath);
                if (!File.Exists(_metadataFilePath))
                {
                    _isActive = true; // Auto-activate if model placed manually
                }

                InspectInputMetadata();

                _statusMessage = _isActive
                    ? "Mô hình LambdaMART ONNX đang hoạt động."
                    : "Mô hình đã tải lên nhưng đang tạm tắt.";
                _logger.LogInformation("Loaded ONNX ranking model '{ModelName}' ({Size} bytes), active={Active}",
                    _modelName, _fileSizeBytes, _isActive);
            }
            else
            {
                _statusMessage = "Chưa có mô hình ONNX. Hệ thống đang sử dụng công thức heuristic mặc định.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ONNX ranking model.");
            _statusMessage = $"Lỗi khi tải mô hình: {ex.Message}";
            _session = null;
            _isActive = false;
        }
    }

    private void InspectInputMetadata()
    {
        if (_session is null) return;

        var inputMeta = _session.InputMetadata;
        if (inputMeta.Count > 0)
        {
            var firstInput = inputMeta.First();
            _inputName = firstInput.Key;

            var dims = firstInput.Value.Dimensions;
            if (dims.Length >= 2 && dims[1] > 0)
            {
                _featureCount = dims[1];
            }
            else
            {
                _featureCount = 10; // 10 features standard
            }
        }
    }

    public OnnxModelStatusDto GetStatus()
    {
        lock (_lock)
        {
            return new OnnxModelStatusDto(
                HasModel: _session is not null && File.Exists(_modelFilePath),
                IsActive: _isActive && _session is not null,
                ModelName: _modelName,
                FileSizeBytes: _fileSizeBytes,
                UploadedAt: _uploadedAt,
                InputName: _inputName,
                FeatureCount: _featureCount,
                StatusMessage: _statusMessage
            );
        }
    }

    public async Task<OnnxModelStatusDto> UploadModelAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("Vui lòng chọn một file hợp lệ.", nameof(file));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".onnx")
        {
            throw new ArgumentException("Chỉ chấp nhận file định dạng .onnx", nameof(file));
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        // Validate by test-loading session from bytes
        InferenceSession newSession;
        try
        {
            newSession = new InferenceSession(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to instantiate ONNX InferenceSession from uploaded file.");
            throw new InvalidOperationException($"File ONNX không hợp lệ hoặc cấu trúc không được hỗ trợ: {ex.Message}", ex);
        }

        lock (_lock)
        {
            try
            {
                if (!Directory.Exists(_storageDir))
                {
                    Directory.CreateDirectory(_storageDir);
                }

                File.WriteAllBytes(_modelFilePath, bytes);

                _session?.Dispose();
                _session = newSession;

                _modelName = file.FileName;
                _fileSizeBytes = file.Length;
                _uploadedAt = DateTime.UtcNow;
                _isActive = true;

                InspectInputMetadata();

                SaveMetadata();

                _statusMessage = $"Đã tải lên và kích hoạt mô hình '{_modelName}' thành công ({_fileSizeBytes / 1024} KB).";
                _logger.LogInformation("Successfully uploaded and activated ONNX model: {FileName}", file.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving ONNX model file.");
                throw new InvalidOperationException($"Lỗi khi lưu file mô hình: {ex.Message}", ex);
            }

            return GetStatus();
        }
    }

    public Task<OnnxModelStatusDto> SetActiveAsync(bool isActive, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_session is null)
            {
                throw new InvalidOperationException("Chưa có mô hình ONNX để kích hoạt.");
            }

            _isActive = isActive;
            _statusMessage = _isActive
                ? "Mô hình LambdaMART ONNX đang hoạt động."
                : "Mô hình đã tắt. Đang sử dụng công thức mặc định.";

            SaveMetadata();
            return Task.FromResult(GetStatus());
        }
    }

    public Task<OnnxModelStatusDto> DeleteModelAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            try
            {
                _session?.Dispose();
                _session = null;
                _isActive = false;

                if (File.Exists(_modelFilePath))
                {
                    File.Delete(_modelFilePath);
                }

                if (File.Exists(_metadataFilePath))
                {
                    File.Delete(_metadataFilePath);
                }

                _modelName = null;
                _fileSizeBytes = 0;
                _uploadedAt = null;
                _inputName = null;
                _featureCount = null;
                _statusMessage = "Đã xóa mô hình ONNX. Hệ thống quay về công thức xếp hạng mặc định.";

                _logger.LogInformation("Deleted ONNX ranking model.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ONNX model file.");
                _statusMessage = $"Lỗi khi xóa file: {ex.Message}";
            }

            return Task.FromResult(GetStatus());
        }
    }

    public float? Predict(RankingFeaturesDto features)
    {
        if (!_isActive || _session is null)
        {
            return null;
        }

        try
        {
            float[] featureValues =
            [
                features.Bm25Norm,
                features.CategoryMatch,
                features.DurationAvgMin,
                features.DurationMaxMin,
                features.DurationSpreadMin,
                features.DistanceAvgKm,
                features.DistanceMaxKm,
                features.DurationAvgRankNorm,
                features.DurationSpreadRankNorm,
                features.ParticipantCount
            ];

            // If model expects fewer features, slice to match expected shape
            var expectedLen = _featureCount.GetValueOrDefault(10);
            if (expectedLen > 0 && expectedLen < featureValues.Length)
            {
                featureValues = featureValues.Take(expectedLen).ToArray();
            }

            var inputName = _inputName ?? "input";
            var dimensions = new[] { 1, featureValues.Length };
            var tensor = new DenseTensor<float>(featureValues, dimensions);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var results = _session.Run(inputs);
            var firstOutput = results.FirstOrDefault();
            if (firstOutput is null) return null;

            var val = firstOutput.AsEnumerable<float>().FirstOrDefault();
            return val;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ONNX inference exception, gracefully falling back to heuristic ranking.");
            return null;
        }
    }

    private void SaveMetadata()
    {
        var meta = new StoredModelMetadata(
            IsActive: _isActive,
            ModelName: _modelName,
            FileSizeBytes: _fileSizeBytes,
            UploadedAt: _uploadedAt
        );

        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_metadataFilePath, json);
    }

    public void Dispose()
    {
        _session?.Dispose();
    }

    private sealed record StoredModelMetadata(
        bool IsActive,
        string? ModelName,
        long FileSizeBytes,
        DateTime? UploadedAt
    );
}
