using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Swalekha.Mobile.Models;

namespace Swalekha.Mobile.Services;

public sealed class SwalekhaApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public SwalekhaApiClient(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("SwalekhaAuthenticated");
    }

    public Task<SwalekhaDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaDashboardDto>(HttpMethod.Get, "api/swalekha/dashboard", null, cancellationToken);

    public Task<List<SwalekhaAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaAccountDto>>(HttpMethod.Get, "api/swalekha/accounts", null, cancellationToken);

    public Task<SwalekhaAccountDto> GetAccountAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaAccountDto>(HttpMethod.Get, $"api/swalekha/accounts/{id}", null, cancellationToken);

    public Task<SwalekhaAccountDto> CreateAccountAsync(SwalekhaAccountPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaAccountDto>(HttpMethod.Post, "api/swalekha/accounts", payload, cancellationToken);

    public Task<SwalekhaAccountDto> UpdateAccountAsync(Guid id, SwalekhaAccountPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaAccountDto>(HttpMethod.Put, $"api/swalekha/accounts/{id}", payload, cancellationToken);

    public Task DeleteAccountAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/accounts/{id}", null, cancellationToken);

    public Task<SwalekhaTransactionList> GetAccountTransactionsAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTransactionList>(HttpMethod.Get, $"api/swalekha/accounts/{accountId}/transactions?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<SwalekhaTransactionDto> AddTransactionAsync(Guid accountId, SwalekhaTransactionPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTransactionDto>(HttpMethod.Post, $"api/swalekha/accounts/{accountId}/transactions", payload, cancellationToken);

    public Task DeleteTransactionAsync(Guid accountId, Guid transactionId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/accounts/{accountId}/transactions/{transactionId}", null, cancellationToken);

    public Task<SwalekhaTransferResult> CreateTransferAsync(SwalekhaTransferPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTransferResult>(HttpMethod.Post, "api/swalekha/transfers", payload, cancellationToken);

    public Task<List<SwalekhaContactDto>> GetContactsAsync(CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaContactDto>>(HttpMethod.Get, "api/swalekha/contacts", null, cancellationToken);

    public Task<SwalekhaContactDto> GetContactAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaContactDto>(HttpMethod.Get, $"api/swalekha/contacts/{id}", null, cancellationToken);

    public Task<SwalekhaContactDto> CreateContactAsync(SwalekhaContactPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaContactDto>(HttpMethod.Post, "api/swalekha/contacts", payload, cancellationToken);

    public Task<SwalekhaContactDto> UpdateContactAsync(Guid id, SwalekhaContactPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaContactDto>(HttpMethod.Put, $"api/swalekha/contacts/{id}", payload, cancellationToken);

    public Task DeleteContactAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/contacts/{id}", null, cancellationToken);

    public Task<SwalekhaPersonLedgerList> GetContactLedgerAsync(Guid contactId, int page, int pageSize, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaPersonLedgerList>(HttpMethod.Get, $"api/swalekha/contacts/{contactId}/ledger?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<SwalekhaPersonLedgerEntryDto> AddLedgerEntryAsync(Guid contactId, SwalekhaPersonLedgerEntryPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaPersonLedgerEntryDto>(HttpMethod.Post, $"api/swalekha/contacts/{contactId}/ledger", payload, cancellationToken);

    public Task DeleteLedgerEntryAsync(Guid contactId, Guid entryId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/contacts/{contactId}/ledger/{entryId}", null, cancellationToken);

    public Task SettleContactAsync(Guid contactId, SwalekhaSettlePayload payload, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Post, $"api/swalekha/contacts/{contactId}/settle", payload, cancellationToken);

    public Task<List<SwalekhaExpenseSheetDto>> GetExpenseSheetsAsync(CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaExpenseSheetDto>>(HttpMethod.Get, "api/swalekha/expense-sheets", null, cancellationToken);

    public Task<SwalekhaExpenseSheetDto> GetExpenseSheetAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaExpenseSheetDto>(HttpMethod.Get, $"api/swalekha/expense-sheets/{id}", null, cancellationToken);

    public Task<SwalekhaExpenseSheetDto> CreateExpenseSheetAsync(SwalekhaExpenseSheetPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaExpenseSheetDto>(HttpMethod.Post, "api/swalekha/expense-sheets", payload, cancellationToken);

    public Task<SwalekhaExpenseSheetDto> UpdateExpenseSheetAsync(Guid id, SwalekhaExpenseSheetPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaExpenseSheetDto>(HttpMethod.Put, $"api/swalekha/expense-sheets/{id}", payload, cancellationToken);

    public Task DeleteExpenseSheetAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/expense-sheets/{id}", null, cancellationToken);

    public Task<SwalekhaExpenseEntryList> GetExpenseEntriesAsync(Guid sheetId, int page, int pageSize, bool includeHidden, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaExpenseEntryList>(HttpMethod.Get, $"api/swalekha/expense-sheets/{sheetId}/entries?page={page}&pageSize={pageSize}&includeHidden={includeHidden}", null, cancellationToken);

    public Task<SwalekhaExpenseEntryDto> AddExpenseEntryAsync(Guid sheetId, SwalekhaExpenseEntryPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaExpenseEntryDto>(HttpMethod.Post, $"api/swalekha/expense-sheets/{sheetId}/entries", payload, cancellationToken);

    public Task DeleteExpenseEntryAsync(Guid sheetId, Guid entryId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/expense-sheets/{sheetId}/entries/{entryId}", null, cancellationToken);

    public Task<SwalekhaIncomeList> GetIncomeAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaIncomeList>(HttpMethod.Get, $"api/swalekha/income?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<SwalekhaIncomeEntryDto> CreateIncomeAsync(SwalekhaIncomeEntryPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaIncomeEntryDto>(HttpMethod.Post, "api/swalekha/income", payload, cancellationToken);

    public Task<SwalekhaIncomeEntryDto> UpdateIncomeAsync(Guid id, SwalekhaIncomeEntryPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaIncomeEntryDto>(HttpMethod.Put, $"api/swalekha/income/{id}", payload, cancellationToken);

    public Task DeleteIncomeAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/income/{id}", null, cancellationToken);

    public Task<List<SwalekhaRecurringBillDto>> GetRecurringBillsAsync(CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaRecurringBillDto>>(HttpMethod.Get, "api/swalekha/recurring-bills", null, cancellationToken);

    public Task<SwalekhaRecurringBillDto> CreateRecurringBillAsync(SwalekhaRecurringBillPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaRecurringBillDto>(HttpMethod.Post, "api/swalekha/recurring-bills", payload, cancellationToken);

    public Task<SwalekhaRecurringBillDto> UpdateRecurringBillAsync(Guid id, SwalekhaRecurringBillPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaRecurringBillDto>(HttpMethod.Put, $"api/swalekha/recurring-bills/{id}", payload, cancellationToken);

    public Task DeleteRecurringBillAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/recurring-bills/{id}", null, cancellationToken);

    public Task<SwalekhaRecurringBillDto> MarkRecurringBillPaidAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaRecurringBillDto>(HttpMethod.Post, $"api/swalekha/recurring-bills/{id}/mark-paid", new SwalekhaMarkPaidPayload(null), cancellationToken);

    public Task<List<SwalekhaTripDto>> GetTripsAsync(bool includeClosed, CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaTripDto>>(HttpMethod.Get, $"api/swalekha/trips?includeClosed={includeClosed}", null, cancellationToken);

    public Task<SwalekhaTripDto> GetTripAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTripDto>(HttpMethod.Get, $"api/swalekha/trips/{id}", null, cancellationToken);

    public Task<SwalekhaTripDto> CreateTripAsync(SwalekhaTripPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTripDto>(HttpMethod.Post, "api/swalekha/trips", payload, cancellationToken);

    public Task<SwalekhaTripDto> UpdateTripAsync(Guid id, SwalekhaTripPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTripDto>(HttpMethod.Put, $"api/swalekha/trips/{id}", payload, cancellationToken);

    public Task DeleteTripAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/trips/{id}", null, cancellationToken);

    public Task<SwalekhaTripDto> CloseTripAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTripDto>(HttpMethod.Post, $"api/swalekha/trips/{id}/close", null, cancellationToken);

    public Task<SwalekhaTripDto> ReopenTripAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTripDto>(HttpMethod.Post, $"api/swalekha/trips/{id}/reopen", null, cancellationToken);

    public Task<List<SwalekhaFixedDepositDto>> GetFixedDepositsAsync(bool includeClosed, CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaFixedDepositDto>>(HttpMethod.Get, $"api/swalekha/fixed-deposits?includeClosed={includeClosed}", null, cancellationToken);

    public Task<SwalekhaFixedDepositDto> GetFixedDepositAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaFixedDepositDto>(HttpMethod.Get, $"api/swalekha/fixed-deposits/{id}", null, cancellationToken);

    public Task<SwalekhaFixedDepositDto> CreateFixedDepositAsync(SwalekhaFixedDepositPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaFixedDepositDto>(HttpMethod.Post, "api/swalekha/fixed-deposits", payload, cancellationToken);

    public Task<SwalekhaFixedDepositDto> UpdateFixedDepositAsync(Guid id, SwalekhaFixedDepositPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaFixedDepositDto>(HttpMethod.Put, $"api/swalekha/fixed-deposits/{id}", payload, cancellationToken);

    public Task DeleteFixedDepositAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/fixed-deposits/{id}", null, cancellationToken);

    public Task<SwalekhaFixedDepositDto> MarkFixedDepositMaturedAsync(Guid id, SwalekhaMarkMaturedPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaFixedDepositDto>(HttpMethod.Post, $"api/swalekha/fixed-deposits/{id}/mark-matured", payload, cancellationToken);

    public Task<List<SwalekhaRecurringDepositDto>> GetRecurringDepositsAsync(bool includeClosed, CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaRecurringDepositDto>>(HttpMethod.Get, $"api/swalekha/recurring-deposits?includeClosed={includeClosed}", null, cancellationToken);

    public Task<SwalekhaRecurringDepositDto> GetRecurringDepositAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaRecurringDepositDto>(HttpMethod.Get, $"api/swalekha/recurring-deposits/{id}", null, cancellationToken);

    public Task<SwalekhaRecurringDepositDto> CreateRecurringDepositAsync(SwalekhaRecurringDepositPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaRecurringDepositDto>(HttpMethod.Post, "api/swalekha/recurring-deposits", payload, cancellationToken);

    public Task<SwalekhaRecurringDepositDto> UpdateRecurringDepositAsync(Guid id, SwalekhaRecurringDepositPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaRecurringDepositDto>(HttpMethod.Put, $"api/swalekha/recurring-deposits/{id}", payload, cancellationToken);

    public Task DeleteRecurringDepositAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/recurring-deposits/{id}", null, cancellationToken);

    public Task<SwalekhaRecurringDepositDto> RecordRecurringDepositInstallmentAsync(Guid id, SwalekhaRecordInstallmentPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaRecurringDepositDto>(HttpMethod.Post, $"api/swalekha/recurring-deposits/{id}/record-installment", payload, cancellationToken);

    public Task<SwalekhaRecurringDepositDto> MarkRecurringDepositMaturedAsync(Guid id, SwalekhaMarkMaturedPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaRecurringDepositDto>(HttpMethod.Post, $"api/swalekha/recurring-deposits/{id}/mark-matured", payload, cancellationToken);

    public Task<List<SwalekhaMutualFundDto>> GetMutualFundsAsync(bool includeInactive, CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaMutualFundDto>>(HttpMethod.Get, $"api/swalekha/mutual-funds?includeInactive={includeInactive}", null, cancellationToken);

    public Task<SwalekhaMutualFundDto> GetMutualFundAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaMutualFundDto>(HttpMethod.Get, $"api/swalekha/mutual-funds/{id}", null, cancellationToken);

    public Task<SwalekhaMutualFundDto> CreateMutualFundAsync(SwalekhaMutualFundPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaMutualFundDto>(HttpMethod.Post, "api/swalekha/mutual-funds", payload, cancellationToken);

    public Task<SwalekhaMutualFundDto> UpdateMutualFundAsync(Guid id, SwalekhaMutualFundPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaMutualFundDto>(HttpMethod.Put, $"api/swalekha/mutual-funds/{id}", payload, cancellationToken);

    public Task DeleteMutualFundAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/mutual-funds/{id}", null, cancellationToken);

    public Task<SwalekhaMutualFundDto> UpdateMutualFundNavAsync(Guid id, decimal nav, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaMutualFundDto>(HttpMethod.Put, $"api/swalekha/mutual-funds/{id}/nav", new SwalekhaUpdateNavPayload(nav), cancellationToken);

    public Task<SwalekhaMutualFundReturnsDto> GetMutualFundReturnsAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaMutualFundReturnsDto>(HttpMethod.Get, $"api/swalekha/mutual-funds/{id}/returns", null, cancellationToken);

    public Task<SwalekhaMutualFundTransactionList> GetMutualFundTransactionsAsync(Guid fundId, int page, int pageSize, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaMutualFundTransactionList>(HttpMethod.Get, $"api/swalekha/mutual-funds/{fundId}/transactions?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<SwalekhaMutualFundTransactionDto> AddMutualFundTransactionAsync(Guid fundId, SwalekhaMutualFundTransactionPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaMutualFundTransactionDto>(HttpMethod.Post, $"api/swalekha/mutual-funds/{fundId}/transactions", payload, cancellationToken);

    public Task DeleteMutualFundTransactionAsync(Guid fundId, Guid transactionId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/mutual-funds/{fundId}/transactions/{transactionId}", null, cancellationToken);

    public Task<List<SwalekhaShareHoldingDto>> GetSharesAsync(bool includeInactive, CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaShareHoldingDto>>(HttpMethod.Get, $"api/swalekha/shares?includeInactive={includeInactive}", null, cancellationToken);

    public Task<SwalekhaShareHoldingDto> GetShareAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaShareHoldingDto>(HttpMethod.Get, $"api/swalekha/shares/{id}", null, cancellationToken);

    public Task<SwalekhaShareHoldingDto> CreateShareAsync(SwalekhaShareHoldingPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaShareHoldingDto>(HttpMethod.Post, "api/swalekha/shares", payload, cancellationToken);

    public Task<SwalekhaShareHoldingDto> UpdateShareAsync(Guid id, SwalekhaShareHoldingPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaShareHoldingDto>(HttpMethod.Put, $"api/swalekha/shares/{id}", payload, cancellationToken);

    public Task DeleteShareAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/shares/{id}", null, cancellationToken);

    public Task<SwalekhaShareHoldingDto> UpdateSharePriceAsync(Guid id, decimal price, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaShareHoldingDto>(HttpMethod.Put, $"api/swalekha/shares/{id}/price", new SwalekhaUpdateSharePricePayload(price), cancellationToken);

    public Task<SwalekhaShareTransactionList> GetShareTransactionsAsync(Guid holdingId, int page, int pageSize, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaShareTransactionList>(HttpMethod.Get, $"api/swalekha/shares/{holdingId}/transactions?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<SwalekhaShareTransactionDto> AddShareTransactionAsync(Guid holdingId, SwalekhaShareTransactionPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaShareTransactionDto>(HttpMethod.Post, $"api/swalekha/shares/{holdingId}/transactions", payload, cancellationToken);

    public Task DeleteShareTransactionAsync(Guid holdingId, Guid transactionId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/shares/{holdingId}/transactions/{transactionId}", null, cancellationToken);

    public Task<List<SwalekhaOtherAssetDto>> GetOtherAssetsAsync(bool includeInactive, CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaOtherAssetDto>>(HttpMethod.Get, $"api/swalekha/other-assets?includeInactive={includeInactive}", null, cancellationToken);

    public Task<SwalekhaOtherAssetDto> CreateOtherAssetAsync(SwalekhaOtherAssetPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaOtherAssetDto>(HttpMethod.Post, "api/swalekha/other-assets", payload, cancellationToken);

    public Task<SwalekhaOtherAssetDto> UpdateOtherAssetAsync(Guid id, SwalekhaOtherAssetPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaOtherAssetDto>(HttpMethod.Put, $"api/swalekha/other-assets/{id}", payload, cancellationToken);

    public Task DeleteOtherAssetAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/other-assets/{id}", null, cancellationToken);

    public Task<List<SwalekhaLoanDto>> GetLoansAsync(bool includeClosed, CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaLoanDto>>(HttpMethod.Get, $"api/swalekha/loans?includeClosed={includeClosed}", null, cancellationToken);

    public Task<SwalekhaLoanDto> GetLoanAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaLoanDto>(HttpMethod.Get, $"api/swalekha/loans/{id}", null, cancellationToken);

    public Task<SwalekhaLoanDto> CreateLoanAsync(SwalekhaLoanPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaLoanDto>(HttpMethod.Post, "api/swalekha/loans", payload, cancellationToken);

    public Task<SwalekhaLoanDto> UpdateLoanAsync(Guid id, SwalekhaLoanPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaLoanDto>(HttpMethod.Put, $"api/swalekha/loans/{id}", payload, cancellationToken);

    public Task DeleteLoanAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/loans/{id}", null, cancellationToken);

    public Task<SwalekhaCalculateEmiResult> CalculateEmiAsync(SwalekhaCalculateEmiPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaCalculateEmiResult>(HttpMethod.Post, "api/swalekha/loans/calculate-emi", payload, cancellationToken);

    public Task<List<SwalekhaAmortizationRow>> GetAmortizationScheduleAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaAmortizationRow>>(HttpMethod.Get, $"api/swalekha/loans/{id}/amortization-schedule", null, cancellationToken);

    public Task<SwalekhaLoanPaymentList> GetLoanPaymentsAsync(Guid loanId, int page, int pageSize, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaLoanPaymentList>(HttpMethod.Get, $"api/swalekha/loans/{loanId}/payments?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<SwalekhaLoanPaymentDto> AddLoanPaymentAsync(Guid loanId, SwalekhaLoanPaymentPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaLoanPaymentDto>(HttpMethod.Post, $"api/swalekha/loans/{loanId}/payments", payload, cancellationToken);

    public Task DeleteLoanPaymentAsync(Guid loanId, Guid paymentId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/loans/{loanId}/payments/{paymentId}", null, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var response = await SendCoreAsync(method, path, body, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new SwalekhaApiException("The server returned an empty response.");
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        => await SendCoreAsync(method, path, body, cancellationToken);

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }

            response = await _client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new SwalekhaApiException($"Couldn't reach the server at {ApiSettings.BaseUrl}. Check your connection.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new SwalekhaApiException("Your session has expired. Sign in again.", 401);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new SwalekhaApiException("This account isn't allowed to use Swalekha.", 403);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await TryReadMessageAsync(response, cancellationToken);
            throw new SwalekhaApiException(detail ?? $"Request failed ({(int)response.StatusCode}).", (int)response.StatusCode);
        }

        return response;
    }

    private static async Task<string?> TryReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
