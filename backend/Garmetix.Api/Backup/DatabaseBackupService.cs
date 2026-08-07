using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Garmetix.Api.AppInfo;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Garmetix.Api.Backup;

public sealed record BackupFileDto(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string Source,
    string? Sha256 = null,
    bool HasChecksum = false,
    bool HasManifest = false,
    string? CompanyName = null,
    string? AppVersion = null,
    string? RelativePath = null);

public sealed record BackupVerificationDto(
    string FileName,
    bool Exists,
    bool HeaderValid,
    bool ChecksumPresent,
    bool ChecksumValid,
    bool ManifestPresent,
    long SizeBytes,
    DateTime? CreatedAtUtc,
    string? Sha256,
    string Status,
    string Message);

public sealed record RestorePreflightDto(
    string FileName,
    bool HeaderValid,
    bool PgRestoreReadable,
    long SizeBytes,
    string Status,
    string Message,
    string[] PreviewLines,
    bool RequiredTablesPresent,
    string[] RequiredTablesFound,
    string[] RequiredTablesMissing,
    string? BackupApplication,
    string? BackupStage,
    string? VersionWarning);

public sealed record BackupMaintenanceStatusDto(
    bool Enabled,
    bool RestoreInProgress,
    string Directory,
    bool DirectoryExists,
    bool DirectoryWritable,
    long? FreeSpaceBytes,
    long BackupFolderSizeBytes,
    int BackupCount,
    int ChecksummedBackupCount,
    int ManifestBackupCount,
    DateTime? LatestBackupAtUtc,
    string? LatestBackupFileName,
    long? LatestBackupSizeBytes,
    double? LatestBackupAgeHours,
    bool HasRecentBackup,
    int RetentionCount,
    int RetentionDays,
    int KeepMinimum,
    DateTime? LastRestoreDrillAtUtc,
    string RestoreDrillStatus,
    int OrphanSidecarCount,
    int TemporaryRestoreFileCount,
    string Status,
    string[] Recommendations);

public sealed record BackupCleanupItemDto(
    string FileName,
    long SizeBytes,
    string Reason);

public sealed record BackupCleanupResultDto(
    DateTime CompletedAtUtc,
    int DeletedFileCount,
    long RecoveredBytes,
    BackupCleanupItemDto[] DeletedFiles,
    BackupMaintenanceStatusDto Status);

public sealed record BackupVerifyAllResultDto(
    DateTime CheckedAtUtc,
    int TotalBackups,
    int PassedBackups,
    int FailedBackups,
    BackupVerificationDto[] Results);

public sealed record BackupManifestDto(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string Source,
    string Database,
    string Host,
    int Port,
    string Sha256,
    string Format,
    string Application,
    string Stage,
    string? CompanyName = null,
    string? AppVersion = null,
    string? BackupDateLocal = null,
    int? Sequence = null,
    string? ProofArchiveFileName = null,
    string? ProofArchiveSha256 = null,
    long? ProofArchiveSizeBytes = null);

public sealed class DatabaseBackupService(
    IConfiguration configuration,
    IOptions<BackupOptions> options,
    GoogleDriveBackupService googleDriveBackupService,
    ILogger<DatabaseBackupService> logger)
{
    private readonly BackupOptions options = options.Value;
    private readonly string backupDirectory = ResolveDirectory(options.Value.Directory);
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private volatile bool restoreInProgress;

    /// <summary>A relative Directory resolves against the app's own content root (matching the same
    /// portable-path pattern LicenseActivationService already uses for its activation file) so a
    /// Docker-era default or a bare-metal systemd deployment both land somewhere the process actually
    /// owns and can write to, instead of a hardcoded "/app/..." that only exists inside a container.</summary>
    private static string ResolveDirectory(string configured)
        => Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);

    private static readonly string[] RequiredRestoreTables =
    [
        "Companies",
        "StoreGroups",
        "Stores",
        "Users",
        "Products",
        "Stocks",
        "SalesInvoices",
        "PurchaseInvoices",
        "Ledgers",
        "Vouchers",
        "PettyCashSheets",
        "CashDetails",
        "Employees",
        "PurchaseInvoiceImportBatches",
        "PurchaseInvoiceImportLines",
        "PurchaseInvoiceImportFiles"
    ];

    public bool IsRestoreInProgress => restoreInProgress;

    public IReadOnlyList<BackupFileDto> ListBackups()
    {
        EnsureDirectory();
        return Directory.EnumerateFiles(backupDirectory, "*.dump", SearchOption.AllDirectories)
            .Where(path => !IsTemporaryRestoreFile(path))
            .Select(path =>
            {
                var file = new FileInfo(path);
                var checksum = TryReadChecksum(path);
                var manifest = TryReadManifest(path);
                var relativePath = Path.GetRelativePath(backupDirectory, path);
                return new BackupFileDto(
                    file.Name,
                    file.Length,
                    file.CreationTimeUtc,
                    manifest?.Source ?? SourceFromFileName(file.Name),
                    checksum,
                    !string.IsNullOrWhiteSpace(checksum),
                    File.Exists(ManifestPath(path)),
                    manifest?.CompanyName ?? CompanyNameFromFileName(file.Name),
                    manifest?.AppVersion ?? AppVersionFromFileName(file.Name),
                    relativePath);
            })
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public string? ResolveBackupPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(fileName))
        {
            return null;
        }

        EnsureDirectory();
        var normalized = fileName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(backupDirectory, normalized));
        var backupRoot = Path.GetFullPath(backupDirectory) + Path.DirectorySeparatorChar;
        if (candidate.StartsWith(backupRoot, StringComparison.Ordinal)
            && File.Exists(candidate)
            && string.Equals(Path.GetExtension(candidate), ".dump", StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return null;
        }

        return Directory.EnumerateFiles(backupDirectory, "*.dump", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetFileName(path), safeName, StringComparison.OrdinalIgnoreCase))
            .Where(path => !IsTemporaryRestoreFile(path))
            .OrderByDescending(path => File.GetCreationTimeUtc(path))
            .FirstOrDefault();
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
            DeleteProofArchiveSidecars(path);
            DeleteFileIfExists(ChecksumPath(path));
            DeleteFileIfExists(ManifestPath(path));
            File.Delete(path);
            await Task.CompletedTask;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public BackupVerificationDto VerifyBackup(string fileName)
    {
        var path = ResolveBackupPath(fileName);
        if (path is null)
        {
            return new BackupVerificationDto(
                fileName,
                false,
                false,
                false,
                false,
                false,
                0,
                null,
                null,
                "missing",
                "Backup file was not found.");
        }

        return VerifyBackupPath(path);
    }

    public async Task<RestorePreflightDto> PreviewRestoreAsync(
        Stream uploadedFile,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        string? uploadedPath = null;
        try
        {
            EnsureDirectory();
            uploadedPath = Path.Combine(
                backupDirectory,
                $"restore-preview-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.dump");

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
            return await InspectDumpAsync(uploadedPath, Path.GetFileName(originalFileName), cancellationToken);
        }
        finally
        {
            if (uploadedPath is not null)
            {
                DeleteFileIfExists(uploadedPath);
            }

            operationLock.Release();
        }
    }

    public async Task<RestorePreflightDto> PreviewLocalRestoreAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        var path = ResolveBackupPath(fileName)
            ?? throw new FileNotFoundException("Backup file was not found.");
        ValidateDump(path, fileName);
        var verification = VerifyBackupPath(path);
        if (verification.ChecksumPresent && !verification.ChecksumValid)
        {
            throw new InvalidOperationException("Backup checksum verification failed. Do not restore this file.");
        }

        return await InspectDumpAsync(path, fileName, cancellationToken);
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
                backupDirectory,
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
            _ = await InspectDumpAsync(uploadedPath, Path.GetFileName(originalFileName), cancellationToken);
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

    public BackupMaintenanceStatusDto GetMaintenanceStatus()
    {
        EnsureDirectory();
        var backups = ListBackups();
        var latest = backups.FirstOrDefault();
        var now = DateTime.UtcNow;
        var latestAgeHours = latest is null
            ? (double?)null
            : Math.Round((now - latest.CreatedAtUtc).TotalHours, 2);
        var hasRecentBackup = latestAgeHours is not null && latestAgeHours <= 30;
        var directoryExists = Directory.Exists(backupDirectory);
        var directoryWritable = CanWriteDirectory(backupDirectory);
        long? freeSpaceBytes = null;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(backupDirectory));
            if (!string.IsNullOrWhiteSpace(root))
            {
                freeSpaceBytes = new DriveInfo(root).AvailableFreeSpace;
            }
        }
        catch
        {
            freeSpaceBytes = null;
        }

        var folderSize = directoryExists
            ? Directory.EnumerateFiles(backupDirectory, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .Sum(file => file.Length)
            : 0;
        var orphanSidecars = CountOrphanSidecars();
        var tempRestoreFiles = CountTemporaryRestoreFiles();
        var drillMarker = ResolveRestoreDrillMarkerPath();
        var lastRestoreDrillAtUtc = File.Exists(drillMarker)
            ? File.GetLastWriteTimeUtc(drillMarker)
            : (DateTime?)null;
        var restoreDrillStatus = lastRestoreDrillAtUtc is null
            ? "Not run"
            : (now - lastRestoreDrillAtUtc.Value).TotalDays <= 30 ? "Recent" : "Stale";
        var recommendations = BuildMaintenanceRecommendations(
            backups,
            directoryWritable,
            freeSpaceBytes,
            folderSize,
            orphanSidecars,
            tempRestoreFiles,
            hasRecentBackup,
            restoreDrillStatus);
        var status = !options.Enabled || !directoryExists || !directoryWritable
            ? "Critical"
            : !hasRecentBackup || orphanSidecars > 0 || tempRestoreFiles > 0 || (freeSpaceBytes is not null && freeSpaceBytes < 1_073_741_824)
                ? "Needs attention"
                : "Ready";

        return new BackupMaintenanceStatusDto(
            options.Enabled,
            restoreInProgress,
            backupDirectory,
            directoryExists,
            directoryWritable,
            freeSpaceBytes,
            folderSize,
            backups.Count,
            backups.Count(backup => backup.HasChecksum),
            backups.Count(backup => backup.HasManifest),
            latest?.CreatedAtUtc,
            latest?.FileName,
            latest?.SizeBytes,
            latestAgeHours,
            hasRecentBackup,
            Math.Max(options.RetentionCount, 1),
            Math.Max(options.RetentionDays, 0),
            Math.Max(options.KeepMinimum, 1),
            lastRestoreDrillAtUtc,
            restoreDrillStatus,
            orphanSidecars,
            tempRestoreFiles,
            status,
            recommendations);
    }

    public BackupVerifyAllResultDto VerifyAllBackups()
    {
        var results = ListBackups()
            .Select(backup => VerifyBackup(backup.FileName))
            .ToArray();
        return new BackupVerifyAllResultDto(
            DateTime.UtcNow,
            results.Length,
            results.Count(result => string.Equals(result.Status, "ok", StringComparison.OrdinalIgnoreCase)),
            results.Count(result => !string.Equals(result.Status, "ok", StringComparison.OrdinalIgnoreCase)),
            results);
    }

    public async Task<BackupCleanupResultDto> CleanupBackupDirectoryAsync(CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectory();
            var deleted = new List<BackupCleanupItemDto>();
            DeleteOrphanSidecars(deleted);
            DeleteTemporaryRestoreFiles(deleted);
            await Task.CompletedTask;
            var recovered = deleted.Sum(item => item.SizeBytes);
            logger.LogInformation(
                "Backup maintenance cleanup deleted {DeletedFileCount} files and recovered {RecoveredBytes} bytes.",
                deleted.Count,
                recovered);
            return new BackupCleanupResultDto(
                DateTime.UtcNow,
                deleted.Count,
                recovered,
                deleted.ToArray(),
                GetMaintenanceStatus());
        }
        finally
        {
            operationLock.Release();
        }
    }

    private int CountOrphanSidecars()
    {
        if (!Directory.Exists(backupDirectory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(backupDirectory, "*", SearchOption.AllDirectories)
            .Count(path => IsSidecar(path) && !File.Exists(RemoveSidecarExtension(path)));
    }

    private int CountTemporaryRestoreFiles()
    {
        if (!Directory.Exists(backupDirectory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(backupDirectory, "restore-*.dump", SearchOption.TopDirectoryOnly).Count();
    }

    private void DeleteOrphanSidecars(List<BackupCleanupItemDto> deleted)
    {
        foreach (var path in Directory.EnumerateFiles(backupDirectory, "*", SearchOption.AllDirectories)
            .Where(path => IsSidecar(path) && !File.Exists(RemoveSidecarExtension(path))))
        {
            DeleteMaintenanceFile(path, "orphan sidecar", deleted);
        }
    }

    private void DeleteTemporaryRestoreFiles(List<BackupCleanupItemDto> deleted)
    {
        foreach (var path in Directory.EnumerateFiles(backupDirectory, "restore-*.dump", SearchOption.TopDirectoryOnly))
        {
            DeleteMaintenanceFile(path, "temporary restore upload/preview", deleted);
            DeleteFileIfExists(ChecksumPath(path));
            DeleteFileIfExists(ManifestPath(path));
        }
    }

    private static void DeleteMaintenanceFile(string path, string reason, List<BackupCleanupItemDto> deleted)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var file = new FileInfo(path);
        var size = file.Length;
        var name = file.Name;
        File.Delete(path);
        deleted.Add(new BackupCleanupItemDto(name, size, reason));
    }

    private static bool CanWriteDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $".garmetix-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(path, "ok");
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSidecar(string path)
    {
        return path.EndsWith(".dump.sha256", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".dump.manifest.json", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveSidecarExtension(string path)
    {
        return path.EndsWith(".dump.sha256", StringComparison.OrdinalIgnoreCase)
            ? path[..^7]
            : path.EndsWith(".dump.manifest.json", StringComparison.OrdinalIgnoreCase)
                ? path[..^14]
                : path;
    }

    private static string[] BuildMaintenanceRecommendations(
        IReadOnlyList<BackupFileDto> backups,
        bool directoryWritable,
        long? freeSpaceBytes,
        long folderSize,
        int orphanSidecars,
        int temporaryRestoreFiles,
        bool hasRecentBackup,
        string restoreDrillStatus)
    {
        var recommendations = new List<string>();
        if (!directoryWritable)
        {
            recommendations.Add("Backup directory is not writable. Check Docker volume permissions for ./backups.");
        }

        if (backups.Count == 0)
        {
            recommendations.Add("Create the first manual backup before live billing begins.");
        }
        else if (!hasRecentBackup)
        {
            recommendations.Add("Latest backup is older than 30 hours. Confirm scheduled backup automation is running.");
        }

        if (backups.Any(backup => !backup.HasChecksum || !backup.HasManifest))
        {
            recommendations.Add("Some backup files are missing checksum or manifest sidecars. Create a fresh backup and prefer checked backups for restore.");
        }

        if (orphanSidecars > 0 || temporaryRestoreFiles > 0)
        {
            recommendations.Add("Run backup maintenance cleanup to remove orphan sidecars and stale restore temp files.");
        }

        if (string.Equals(restoreDrillStatus, "Not run", StringComparison.OrdinalIgnoreCase))
        {
            recommendations.Add("Run scripts/linux/backup-restore-drill.sh on the production host before go-live. It restores into a disposable database and writes a drill marker.");
        }
        else if (string.Equals(restoreDrillStatus, "Stale", StringComparison.OrdinalIgnoreCase))
        {
            recommendations.Add("Last restore drill is older than 30 days. Repeat the disposable restore drill after this release update.");
        }

        if (freeSpaceBytes is not null && freeSpaceBytes < 1_073_741_824)
        {
            recommendations.Add("Available disk space is below 1 GB. Free disk space before running more backups or restore operations.");
        }

        if (folderSize > 5L * 1024 * 1024 * 1024)
        {
            recommendations.Add("Backup folder is larger than 5 GB. Review retention and off-site backup policy.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Backup storage, recent backup age, checksums and cleanup state look ready.");
        }

        return recommendations.ToArray();
    }

    private async Task<BackupFileDto> CreateBackupCoreAsync(
        string source,
        CancellationToken cancellationToken)
    {
        EnsureDirectory();
        var safeSource = Slug(source, "manual");
        var companyName = await GetPrimaryCompanyNameAsync(cancellationToken);
        var safeCompany = Slug(companyName, "Garmetix");
        var indiaNow = ToIndiaTime(DateTimeOffset.UtcNow);
        var sequence = NextBackupSequence(safeCompany, AppInfoEndpoints.Version, indiaNow, safeSource);
        var fileName = $"{safeCompany}-Garmetix-v{AppInfoEndpoints.Version}-{indiaNow:yyyyMMdd-HHmmss}-B{sequence:000}-{safeSource}.dump";
        var filePath = Path.Combine(backupDirectory, fileName);
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
        var sha256 = await WriteChecksumSidecarAsync(filePath, cancellationToken);
        var proofArchive = await TryCreatePurchaseImportProofArchiveAsync(filePath, cancellationToken);
        await WriteManifestSidecarAsync(filePath, source, connection, sha256, companyName, indiaNow, sequence, proofArchive, cancellationToken);
        ApplyRetention();
        var file = new FileInfo(filePath);
        logger.LogInformation(
            "Database backup {FileName} created with size {SizeBytes} bytes and SHA256 {Sha256}.",
            file.Name,
            file.Length,
            sha256);

        if (googleDriveBackupService.IsEnabled && googleDriveBackupService.UploadOnBackup)
        {
            try
            {
                await googleDriveBackupService.UploadBackupAsync(filePath, cancellationToken);
                if (proofArchive is not null)
                {
                    await googleDriveBackupService.UploadBackupAsync(proofArchive.Path, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Local backup {FileName} was created, but Google Drive upload failed.",
                    file.Name);
            }
        }

        return new BackupFileDto(file.Name, file.Length, file.CreationTimeUtc, source, sha256, true, true, companyName, AppInfoEndpoints.Version, Path.GetRelativePath(backupDirectory, filePath));
    }

    private void ApplyRetention()
    {
        var keepCount = Math.Max(options.RetentionCount, options.KeepMinimum);
        var keepMinimum = Math.Max(options.KeepMinimum, 1);
        var retentionDays = Math.Max(options.RetentionDays, 0);
        var cutoff = retentionDays > 0 ? DateTime.UtcNow.AddDays(-retentionDays) : DateTime.MinValue;
        var automaticFiles = Directory.EnumerateFiles(backupDirectory, "*.dump", SearchOption.AllDirectories)
            .Where(path => SourceFromFileName(Path.GetFileName(path)).Equals("scheduled", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .ToList();

        for (var index = 0; index < automaticFiles.Count; index++)
        {
            var file = automaticFiles[index];
            var exceedsCount = index >= keepCount;
            var exceedsAge = retentionDays > 0 && file.CreationTimeUtc < cutoff && index >= keepMinimum;
            if (!exceedsCount && !exceedsAge)
            {
                continue;
            }

            DeleteProofArchiveSidecars(file.FullName);
            DeleteFileIfExists(ChecksumPath(file.FullName));
            DeleteFileIfExists(ManifestPath(file.FullName));
            file.Delete();
        }
    }

    private BackupVerificationDto VerifyBackupPath(string path)
    {
        var file = new FileInfo(path);
        var checksum = TryReadChecksum(path);
        var checksumValid = false;
        if (!string.IsNullOrWhiteSpace(checksum))
        {
            checksumValid = string.Equals(
                checksum,
                ComputeSha256(path),
                StringComparison.OrdinalIgnoreCase);
        }

        var headerValid = IsPgDumpHeader(path);
        var manifestPresent = File.Exists(ManifestPath(path));
        var status = headerValid && (!string.IsNullOrWhiteSpace(checksum) ? checksumValid : true)
            ? "ok"
            : "failed";
        var message = status == "ok"
            ? (checksumValid ? "Backup header and checksum are valid." : "Backup header is valid. Checksum sidecar is missing.")
            : "Backup verification failed. Do not restore this file.";

        return new BackupVerificationDto(
            file.Name,
            true,
            headerValid,
            !string.IsNullOrWhiteSpace(checksum),
            checksumValid,
            manifestPresent,
            file.Length,
            file.CreationTimeUtc,
            checksum,
            status,
            message);
    }

    private async Task<RestorePreflightDto> InspectDumpAsync(
        string path,
        string displayFileName,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            "pg_restore",
            ["--list", path],
            string.Empty,
            cancellationToken);

        var allListLines = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var previewLines = allListLines
            .Take(25)
            .ToArray();

        var readable = result.ExitCode == 0 && allListLines.Length > 0;
        var requiredTablesFound = RequiredRestoreTables
            .Where(table => allListLines.Any(line => line.Contains(table, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requiredTablesMissing = RequiredRestoreTables
            .Except(requiredTablesFound, StringComparer.OrdinalIgnoreCase)
            .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var manifest = TryReadManifest(path);
        var versionWarning = manifest is not null
            && !string.IsNullOrWhiteSpace(manifest.Stage)
            && !manifest.Stage.Contains(AppInfoEndpoints.Version, StringComparison.OrdinalIgnoreCase)
                ? $"Backup manifest stage is {manifest.Stage}; current app is {AppInfoEndpoints.Stage}. Review migrations before restoring over production."
                : null;
        var file = new FileInfo(path);
        return new RestorePreflightDto(
            displayFileName,
            IsPgDumpHeader(path),
            readable,
            file.Length,
            readable && requiredTablesMissing.Length == 0 ? "ok" : readable ? "warning" : "failed",
            readable
                ? requiredTablesMissing.Length == 0
                    ? "Backup can be read by pg_restore and contains the required Garmetix core tables."
                    : "Backup can be read by pg_restore, but some expected Garmetix tables were not visible in the preview list."
                : $"pg_restore could not read this backup. {LastUsefulLine(result.Error)}",
            previewLines,
            requiredTablesMissing.Length == 0,
            requiredTablesFound,
            requiredTablesMissing,
            manifest?.Application,
            manifest?.Stage,
            versionWarning);
    }

    private void ValidateDump(string path, string originalFileName)
    {
        var file = new FileInfo(path);
        if (file.Length < 5)
        {
            throw new InvalidOperationException("The uploaded backup is empty or incomplete.");
        }

        if (!IsPgDumpHeader(path))
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(originalFileName)} is not a valid PostgreSQL custom-format backup.");
        }
    }

    private static bool IsPgDumpHeader(string path)
    {
        Span<byte> header = stackalloc byte[5];
        using var stream = File.OpenRead(path);
        _ = stream.Read(header);
        return header.SequenceEqual("PGDMP"u8);
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
        Directory.CreateDirectory(backupDirectory);
    }

    private async Task<ProofArchiveInfo?> TryCreatePurchaseImportProofArchiveAsync(string dumpPath, CancellationToken cancellationToken)
    {
        try
        {
            var root = configuration["PurchaseImport:StorageRoot"];
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(Directory.GetCurrentDirectory(), "data", "purchase-imports");
            }

            if (!Directory.Exists(root) || !Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
            {
                return null;
            }

            var archivePath = ProofArchivePath(dumpPath);
            DeleteFileIfExists(archivePath);
            DeleteFileIfExists(ChecksumPath(archivePath));
            ZipFile.CreateFromDirectory(root, archivePath, CompressionLevel.Fastest, includeBaseDirectory: false);
            var sha = await WriteChecksumSidecarAsync(archivePath, cancellationToken);
            var file = new FileInfo(archivePath);
            logger.LogInformation("Purchase invoice proof archive {FileName} created with {SizeBytes} bytes.", file.Name, file.Length);
            return new ProofArchiveInfo(archivePath, file.Name, file.Length, sha);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database backup was created, but purchase invoice proof archive could not be created.");
            return null;
        }
    }

    private async Task WriteManifestSidecarAsync(
        string path,
        string source,
        ConnectionInfo connection,
        string sha256,
        string companyName,
        DateTimeOffset backupDateLocal,
        int sequence,
        ProofArchiveInfo? proofArchive,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        var manifest = new BackupManifestDto(
            file.Name,
            file.Length,
            file.CreationTimeUtc,
            source,
            connection.Database,
            connection.Host,
            connection.Port,
            sha256,
            "PostgreSQL custom pg_dump",
            AppInfoEndpoints.ProductName,
            $"{AppInfoEndpoints.Stage} / v{AppInfoEndpoints.Version} / {AppInfoEndpoints.BuildCode}",
            companyName,
            AppInfoEndpoints.Version,
            backupDateLocal.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            sequence,
            proofArchive?.FileName,
            proofArchive?.Sha256,
            proofArchive?.SizeBytes);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(ManifestPath(path), json, cancellationToken);
    }

    private static async Task<string> WriteChecksumSidecarAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var hash = ComputeSha256(path);
        await File.WriteAllTextAsync(ChecksumPath(path), $"{hash}  {Path.GetFileName(path)}{Environment.NewLine}", cancellationToken);
        return hash;
    }

    private static string? TryReadChecksum(string dumpPath)
    {
        var path = ChecksumPath(dumpPath);
        if (!File.Exists(path))
        {
            return null;
        }

        var firstToken = File.ReadLines(path)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return firstToken?.Length == 64 ? firstToken : null;
    }

    private BackupManifestDto? TryReadManifest(string dumpPath)
    {
        var path = ManifestPath(dumpPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BackupManifestDto>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private string ResolveRestoreDrillMarkerPath()
    {
        if (!string.IsNullOrWhiteSpace(options.RestoreDrillMarkerPath))
        {
            return options.RestoreDrillMarkerPath;
        }

        return Path.Combine(backupDirectory, "restore-drill-status.json");
    }

    private static string ComputeSha256(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string ChecksumPath(string dumpPath) => $"{dumpPath}.sha256";
    private static string ManifestPath(string dumpPath) => $"{dumpPath}.manifest.json";
    private static string ProofArchivePath(string dumpPath) => $"{dumpPath}.purchase-import-proofs.zip";

    private static void DeleteProofArchiveSidecars(string dumpPath)
    {
        var proofArchive = ProofArchivePath(dumpPath);
        DeleteFileIfExists(ChecksumPath(proofArchive));
        DeleteFileIfExists(proofArchive);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
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

        if (!string.IsNullOrEmpty(password))
        {
            startInfo.Environment["PGPASSWORD"] = password;
        }

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


    private async Task<string> GetPrimaryCompanyNameAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return "Garmetix";
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "select coalesce(nullif(\"Name\", ''), 'Garmetix') from \"Companies\" order by \"CreatedAt\" nulls last limit 1";
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value?.ToString() is { Length: > 0 } name ? name : "Garmetix";
        }
        catch
        {
            return "Garmetix";
        }
    }

    private int NextBackupSequence(string safeCompany, string version, DateTimeOffset indiaNow, string safeSource)
    {
        var prefix = $"{safeCompany}-Garmetix-v{version}-{indiaNow:yyyyMMdd}";
        return Directory.EnumerateFiles(backupDirectory, "*.dump", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Count(name => name is not null
                && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && name.Contains($"-{safeSource}.dump", StringComparison.OrdinalIgnoreCase)) + 1;
    }

    private static DateTimeOffset ToIndiaTime(DateTimeOffset value)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            return TimeZoneInfo.ConvertTime(value, zone);
        }
        catch
        {
            return value.ToOffset(TimeSpan.FromHours(5.5));
        }
    }

    private static string Slug(string? value, string fallback)
    {
        var chars = (string.IsNullOrWhiteSpace(value) ? fallback : value)
            .Normalize()
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray();
        var result = new string(chars);
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    private static bool IsTemporaryRestoreFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("restore-preview-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("restore-upload-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("garmetix-drive-restore-", StringComparison.OrdinalIgnoreCase);
    }

    private static string? CompanyNameFromFileName(string fileName)
    {
        var marker = "-Garmetix-v";
        var index = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index > 0 ? fileName[..index] : null;
    }

    private static string? AppVersionFromFileName(string fileName)
    {
        var marker = "-Garmetix-v";
        var index = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        var after = fileName[(index + marker.Length)..];
        var end = after.IndexOf('-', StringComparison.Ordinal);
        return end > 0 ? after[..end] : null;
    }

    public static string SourceFromFileName(string fileName)
    {
        if (fileName.Contains("-pre-restore-", StringComparison.OrdinalIgnoreCase))
        {
            return "pre-restore";
        }

        if (fileName.Contains("-scheduled", StringComparison.OrdinalIgnoreCase))
        {
            return "scheduled";
        }

        if (fileName.Contains("-admin-data", StringComparison.OrdinalIgnoreCase) || fileName.Contains("-admin-json", StringComparison.OrdinalIgnoreCase))
        {
            return "admin-data";
        }

        if (fileName.Contains("-backup-now", StringComparison.OrdinalIgnoreCase) || fileName.Contains("-manual", StringComparison.OrdinalIgnoreCase))
        {
            return "manual";
        }

        return "copied";
    }

    private static string LastUsefulLine(string value)
    {
        return value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .LastOrDefault() ?? "Check the server log for details.";
    }

    private sealed record ProofArchiveInfo(string Path, string FileName, long SizeBytes, string Sha256);

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
