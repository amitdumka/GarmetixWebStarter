using System.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Garmetix.Api.Backup;

public sealed record BackupFileDto(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string Source);

public sealed class DatabaseBackupService(
    IConfiguration configuration,
    IOptions<BackupOptions> options,
    ILogger<DatabaseBackupService> logger)
{
    private readonly BackupOptions options = options.Value;
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private volatile bool restoreInProgress;

    public bool IsRestoreInProgress => restoreInProgress;

    public IReadOnlyList<BackupFileDto> ListBackups()
    {
        EnsureDirectory();
        return Directory.EnumerateFiles(options.Directory, "garmetix-*.dump", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var file = new FileInfo(path);
                return new BackupFileDto(
                    file.Name,
                    file.Length,
                    file.CreationTimeUtc,
                    SourceFromFileName(file.Name));
            })
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public string? ResolveBackupPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(options.Directory, fileName));
        var backupRoot = Path.GetFullPath(options.Directory) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(backupRoot, StringComparison.Ordinal)
            && File.Exists(fullPath)
            && string.Equals(Path.GetExtension(fullPath), ".dump", StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
    }

    public async Task<BackupFileDto> CreateBackupAsync(
        string source,
        CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            return await CreateBackupCoreAsync(source, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task DeleteBackupAsync(string fileName, CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            var path = ResolveBackupPath(fileName)
                ?? throw new FileNotFoundException("Backup file was not found.");
            File.Delete(path);
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<BackupFileDto> RestoreAsync(
        Stream uploadedFile,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        string? uploadedPath = null;
        try
        {
            restoreInProgress = true;
            EnsureDirectory();
            uploadedPath = Path.Combine(
                options.Directory,
                $"restore-upload-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.dump");

            await using (var target = new FileStream(
                uploadedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await uploadedFile.CopyToAsync(target, cancellationToken);
            }

            ValidateDump(uploadedPath, originalFileName);
            var safetyBackup = await CreateBackupCoreAsync("pre-restore", cancellationToken);
            var connection = GetConnectionInfo();

            var result = await RunProcessAsync(
                "pg_restore",
                [
                    "--clean",
                    "--if-exists",
                    "--no-owner",
                    "--no-privileges",
                    "--exit-on-error",
                    "--single-transaction",
                    "--host", connection.Host,
                    "--port", connection.Port.ToString(),
                    "--username", connection.Username,
                    "--dbname", connection.Database,
                    uploadedPath
                ],
                connection.Password,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Database restore failed. A safety backup was kept as {safetyBackup.FileName}. {LastUsefulLine(result.Error)}");
            }

            logger.LogWarning(
                "Database restore completed from uploaded file {FileName}. Safety backup: {SafetyBackup}.",
                Path.GetFileName(originalFileName),
                safetyBackup.FileName);
            return safetyBackup;
        }
        finally
        {
            restoreInProgress = false;
            if (uploadedPath is not null && File.Exists(uploadedPath))
            {
                File.Delete(uploadedPath);
            }

            operationLock.Release();
        }
    }

    public BackupOptions GetOptions()
    {
        return options;
    }

    private async Task<BackupFileDto> CreateBackupCoreAsync(
        string source,
        CancellationToken cancellationToken)
    {
        EnsureDirectory();
        var safeSource = new string(source
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray())
            .Trim('-');
        var fileName = $"garmetix-{safeSource}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.dump";
        var filePath = Path.Combine(options.Directory, fileName);
        var connection = GetConnectionInfo();

        var result = await RunProcessAsync(
            "pg_dump",
            [
                "--format=custom",
                "--compress=6",
                "--no-owner",
                "--no-privileges",
                "--host", connection.Host,
                "--port", connection.Port.ToString(),
                "--username", connection.Username,
                "--file", filePath,
                connection.Database
            ],
            connection.Password,
            cancellationToken);

        if (result.ExitCode != 0 || !File.Exists(filePath))
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            throw new InvalidOperationException($"Database backup failed. {LastUsefulLine(result.Error)}");
        }

        ValidateDump(filePath, fileName);
        ApplyRetention();
        var file = new FileInfo(filePath);
        logger.LogInformation(
            "Database backup {FileName} created with size {SizeBytes} bytes.",
            file.Name,
            file.Length);
        return new BackupFileDto(file.Name, file.Length, file.CreationTimeUtc, source);
    }

    private void ApplyRetention()
    {
        var keep = Math.Max(options.RetentionCount, 1);
        var automaticFiles = Directory.EnumerateFiles(options.Directory, "garmetix-scheduled-*.dump")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Skip(keep)
            .ToList();

        foreach (var file in automaticFiles)
        {
            file.Delete();
        }
    }

    private void ValidateDump(string path, string originalFileName)
    {
        var file = new FileInfo(path);
        if (file.Length < 5)
        {
            throw new InvalidOperationException("The uploaded backup is empty or incomplete.");
        }

        Span<byte> header = stackalloc byte[5];
        using var stream = File.OpenRead(path);
        _ = stream.Read(header);
        if (!header.SequenceEqual("PGDMP"u8))
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(originalFileName)} is not a valid PostgreSQL custom-format backup.");
        }
    }

    private ConnectionInfo GetConnectionInfo()
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Database connection is not configured.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return new ConnectionInfo(
            builder.Host ?? "localhost",
            builder.Port,
            builder.Database ?? string.Empty,
            builder.Username ?? string.Empty,
            builder.Password ?? string.Empty);
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(options.Directory);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string password,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["PGPASSWORD"] = password;
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static string SourceFromFileName(string fileName)
    {
        if (fileName.Contains("-pre-restore-", StringComparison.OrdinalIgnoreCase))
        {
            return "pre-restore";
        }

        if (fileName.Contains("-scheduled-", StringComparison.OrdinalIgnoreCase))
        {
            return "scheduled";
        }

        return "manual";
    }

    private static string LastUsefulLine(string value)
    {
        return value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .LastOrDefault() ?? "Check the server log for details.";
    }

    private sealed record ConnectionInfo(
        string Host,
        int Port,
        string Database,
        string Username,
        string Password);

    private sealed record ProcessResult(
        int ExitCode,
        string Output,
        string Error);
}
