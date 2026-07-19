using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Swalekha.Mobile.Models;
using Swalekha.Mobile.Services;
using Swalekha.Mobile.Views;

namespace Swalekha.Mobile.ViewModels;

[QueryProperty(nameof(LoanId), "id")]
public sealed partial class LoanDetailViewModel : BaseViewModel
{
    private readonly SwalekhaApiClient _apiClient;

    public LoanDetailViewModel(SwalekhaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public static readonly string[] PaymentTypes = { "Emi", "Prepayment" };

    [ObservableProperty]
    private string loanId = string.Empty;

    [ObservableProperty]
    private SwalekhaLoanDto? loan;

    [ObservableProperty]
    private bool isAddingPayment;

    [ObservableProperty]
    private string newPaymentType = "Emi";

    [ObservableProperty]
    private string newAmountText = string.Empty;

    [ObservableProperty]
    private string newNarration = string.Empty;

    [ObservableProperty]
    private bool isShowingSchedule;

    public ObservableCollection<SwalekhaLoanPaymentDto> Payments { get; } = new();

    public ObservableCollection<SwalekhaAmortizationRow> Schedule { get; } = new();

    partial void OnLoanIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!Guid.TryParse(LoanId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            Loan = await _apiClient.GetLoanAsync(id);
            NewAmountText = Loan.EmiAmount.ToString();

            var page = await _apiClient.GetLoanPaymentsAsync(id, 1, 50);
            Payments.Clear();
            foreach (var row in page.Rows)
            {
                Payments.Add(row);
            }
        });
    }

    [RelayCommand]
    private void ToggleAddPayment() => IsAddingPayment = !IsAddingPayment;

    [RelayCommand]
    private async Task AddPaymentAsync()
    {
        if (!Guid.TryParse(LoanId, out var id))
        {
            return;
        }

        if (!decimal.TryParse(NewAmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        var payload = new SwalekhaLoanPaymentPayload(
            NewPaymentType,
            DateTime.Today,
            amount,
            string.IsNullOrWhiteSpace(NewNarration) ? null : NewNarration.Trim());

        await RunAsync(async () =>
        {
            await _apiClient.AddLoanPaymentAsync(id, payload);
            NewNarration = string.Empty;
            IsAddingPayment = false;
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task DeletePaymentAsync(SwalekhaLoanPaymentDto payment)
    {
        if (!Guid.TryParse(LoanId, out var id))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _apiClient.DeleteLoanPaymentAsync(id, payment.Id);
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task ToggleScheduleAsync()
    {
        if (!Guid.TryParse(LoanId, out var id))
        {
            return;
        }

        if (IsShowingSchedule)
        {
            IsShowingSchedule = false;
            return;
        }

        await RunAsync(async () =>
        {
            var schedule = await _apiClient.GetAmortizationScheduleAsync(id);
            Schedule.Clear();
            foreach (var row in schedule)
            {
                Schedule.Add(row);
            }
            IsShowingSchedule = true;
        });
    }

    [RelayCommand]
    private async Task EditLoanAsync()
        => await Shell.Current.GoToAsync($"{nameof(LoanEditPage)}?id={LoanId}");
}
