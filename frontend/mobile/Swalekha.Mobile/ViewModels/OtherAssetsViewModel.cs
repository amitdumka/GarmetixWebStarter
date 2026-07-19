using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;

namespace Swalekha.Mobile.ViewModels;

public sealed partial class OtherAssetsViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public OtherAssetsViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] AssetTypes = { "PPF", "EPF", "NPS", "Gold", "Other" };

    public ObservableCollection<SwalekhaOtherAssetDto> Assets { get; } = new();

    [ObservableProperty]
    private decimal totalValue;

    [ObservableProperty]
    private bool hasLoadedOnce;

    [ObservableProperty]
    private bool isEditingForm;

    [ObservableProperty]
    private Guid? editingId;

    [ObservableProperty]
    private string formAssetType = "PPF";

    [ObservableProperty]
    private string formName = string.Empty;

    [ObservableProperty]
    private string formValueText = string.Empty;

    public bool HasNoAssets => HasLoadedOnce && Assets.Count == 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var assets = await _apiClient.GetOtherAssetsAsync(false);
            Assets.Clear();
            foreach (var asset in assets)
            {
                Assets.Add(asset);
            }
            TotalValue = assets.Sum(a => a.CurrentValue);
            HasLoadedOnce = true;
            OnPropertyChanged(nameof(HasNoAssets));
        });
    }

    [RelayCommand]
    private void ShowAddForm()
    {
        EditingId = null;
        FormAssetType = "PPF";
        FormName = string.Empty;
        FormValueText = string.Empty;
        IsEditingForm = true;
    }

    [RelayCommand]
    private void EditAsset(SwalekhaOtherAssetDto asset)
    {
        EditingId = asset.Id;
        FormAssetType = asset.AssetType;
        FormName = asset.Name;
        FormValueText = asset.CurrentValue.ToString();
        IsEditingForm = true;
    }

    [RelayCommand]
    private void CancelForm() => IsEditingForm = false;

    [RelayCommand]
    private async Task SaveAssetAsync()
    {
        if (string.IsNullOrWhiteSpace(FormName))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        if (!decimal.TryParse(FormValueText, out var value) || value < 0)
        {
            ErrorMessage = "Enter a current value.";
            return;
        }

        var payload = new SwalekhaOtherAssetPayload(FormAssetType, FormName.Trim(), value, DateTime.Today, true, null);

        await RunAsync(async () =>
        {
            if (EditingId.HasValue)
            {
                await _apiClient.UpdateOtherAssetAsync(EditingId.Value, payload);
            }
            else
            {
                await _apiClient.CreateOtherAssetAsync(payload);
            }

            IsEditingForm = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeleteAssetAsync(SwalekhaOtherAssetDto asset)
    {
        await RunAsync(async () =>
        {
            await _apiClient.DeleteOtherAssetAsync(asset.Id);
            await LoadAsync();
        });
    }
}
