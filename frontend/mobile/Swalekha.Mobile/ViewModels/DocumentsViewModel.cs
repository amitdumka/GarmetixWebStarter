using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class DocumentsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public DocumentsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] EntityTypes = { "General", "FixedDeposit", "RecurringDeposit", "Loan", "InsurancePolicy" };

    public ObservableCollection<SwalekhaDocumentDto> Documents { get; } = new();

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private string uploadEntityType = "General";

    [ObservableProperty]
    private string? uploadNotes;

    [ObservableProperty]
    private string? statusMessage;

    public bool HasNoDocuments => HasLoadedOnce && Documents.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var documents = await _apiClient.GetDocumentsAsync();
            Documents.Clear();
            foreach (var document in documents)
            {
                Documents.Add(document);
            }
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoDocuments));
        });
    }

    [RelayCommand]
    private async Task PickAndUploadAsync()
    {
        var allowedTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = new[] { "application/pdf", "image/png", "image/jpeg", "image/webp" }
        });

        FileResult? picked;
        try
        {
            picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a document",
                FileTypes = allowedTypes
            });
        }
        catch (Exception)
        {
            ErrorMessage = "Couldn't open the file picker.";
            return;
        }

        if (picked is null)
        {
            return;
        }

        StatusMessage = null;
        await RunAsync(async () =>
        {
            var contentType = picked.ContentType ?? "application/octet-stream";
            await _apiClient.UploadDocumentAsync(picked.FullPath, picked.FileName, contentType, UploadEntityType, UploadNotes);
            UploadNotes = null;
            StatusMessage = $"Uploaded {picked.FileName}.";
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DownloadAsync(SwalekhaDocumentDto document)
    {
        StatusMessage = null;
        await RunAsync(async () =>
        {
            var (bytes, contentType) = await _apiClient.DownloadDocumentAsync(document.Id);

            var localPath = Path.Combine(FileSystem.CacheDirectory, document.FileName);
            await File.WriteAllBytesAsync(localPath, bytes);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = document.FileName,
                File = new ShareFile(localPath, contentType)
            });
        });
    }

    [RelayCommand]
    private async Task DeleteAsync(SwalekhaDocumentDto document)
    {
        await RunAsync(async () =>
        {
            await _apiClient.DeleteDocumentAsync(document.Id);
            await LoadAsync();
        });
    }
}
