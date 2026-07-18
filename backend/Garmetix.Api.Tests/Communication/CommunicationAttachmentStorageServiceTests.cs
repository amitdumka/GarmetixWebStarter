using Garmetix.Api.Communication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

public sealed class CommunicationAttachmentStorageServiceTests
{
    [Theory]
    [InlineData("invoice.pdf")]
    [InlineData("photo.PNG")] // extension check must be case-insensitive
    [InlineData("scan.jpg")]
    [InlineData("data.xlsx")]
    public void ValidateUpload_AcceptsAllowListedExtensions(string fileName)
    {
        var service = CreateService();
        var (isValid, error) = service.ValidateUpload(fileName, 1024);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.sh")]
    [InlineData("archive.zip")]
    [InlineData("page.html")]
    [InlineData("noextension")]
    public void ValidateUpload_RejectsDisallowedOrMissingExtensions(string fileName)
    {
        var service = CreateService();
        var (isValid, error) = service.ValidateUpload(fileName, 1024);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUpload_RejectsZeroOrNegativeSize()
    {
        var service = CreateService();
        Assert.False(service.ValidateUpload("file.pdf", 0).IsValid);
        Assert.False(service.ValidateUpload("file.pdf", -1).IsValid);
    }

    [Fact]
    public void ValidateUpload_RejectsOversizedFile()
    {
        var service = CreateService();
        var (isValid, error) = service.ValidateUpload("huge.pdf", 20 * 1024 * 1024); // 20 MB > 15 MB cap

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateUpload_AcceptsFileAtExactlyTheSizeCap()
    {
        var service = CreateService();
        var (isValid, _) = service.ValidateUpload("borderline.pdf", 15 * 1024 * 1024);
        Assert.True(isValid);
    }

    [Fact]
    public void StorageRoot_FallsBackToContentRootDataFolder_WhenNotConfigured()
    {
        var service = CreateService(storagePath: null);
        var root = service.StorageRoot();

        Assert.Contains("communication-attachments", root);
    }

    [Fact]
    public void StorageRoot_UsesConfiguredPath_WhenProvided()
    {
        var service = CreateService(storagePath: "/custom/attachment/path");
        Assert.Equal("/custom/attachment/path", service.StorageRoot());
    }

    private static CommunicationAttachmentStorageService CreateService(string? storagePath = null)
    {
        var configValues = new Dictionary<string, string?>();
        if (storagePath is not null)
        {
            configValues["Communication:AttachmentStorage:StoragePath"] = storagePath;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var environment = new TestWebHostEnvironment { ContentRootPath = Path.GetTempPath() };
        return new CommunicationAttachmentStorageService(configuration, environment);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Garmetix.Api.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
