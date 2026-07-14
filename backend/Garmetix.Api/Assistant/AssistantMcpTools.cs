using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Garmetix.Api.Assistant;

/// <summary>
/// MCP-facing wrapper around AssistantToolCatalog (Stage 14F.7). Purely additive:
/// every method here just forwards to the same AssistantToolCatalog.ExecuteAsync
/// the in-app chat assistant already uses, so an MCP caller is bound by the exact
/// same WorkspaceScope.ApplyTo(...) tenant scoping as everything else in the API -
/// no business logic is duplicated. See AssistantToolCatalog.cs for the actual
/// query implementations and AssistantOptions.McpEnabled for the feature flag.
/// </summary>
[McpServerToolType]
public sealed class AssistantMcpTools(AssistantToolCatalog catalog, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(Name = "get_store_sales_summary")]
    [Description("Get total sales amount, invoice count and average bill value for a date range, optionally scoped to a company, store group or store. Use this for questions like 'how were sales last week' or 'what did store X sell yesterday'.")]
    public Task<string> GetStoreSalesSummaryAsync(
        [Description("Optional company GUID to filter by.")] string? companyId = null,
        [Description("Optional store group GUID to filter by.")] string? storeGroupId = null,
        [Description("Optional store GUID to filter by.")] string? storeId = null,
        [Description("Start date, format yyyy-MM-dd. Defaults to the start of the current month.")] string? fromDate = null,
        [Description("End date (inclusive), format yyyy-MM-dd. Defaults to today.")] string? toDate = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync("get_store_sales_summary", new { companyId, storeGroupId, storeId, fromDate, toDate }, cancellationToken);

    [McpServerTool(Name = "compare_store_performance")]
    [Description("Compare sales, purchases and stock value across stores for a date range, ranked highest sales first. Use this for 'which store is doing best' or 'compare store A and store B'.")]
    public Task<string> CompareStorePerformanceAsync(
        [Description("Optional company GUID to filter by.")] string? companyId = null,
        [Description("Optional store group GUID to filter by.")] string? storeGroupId = null,
        [Description("Start date, format yyyy-MM-dd. Defaults to the start of the current month.")] string? fromDate = null,
        [Description("End date (inclusive), format yyyy-MM-dd. Defaults to today.")] string? toDate = null,
        [Description("Maximum number of stores to return, default 10.")] int? top = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync("compare_store_performance", new { companyId, storeGroupId, fromDate, toDate, top }, cancellationToken);

    [McpServerTool(Name = "get_low_stock_items")]
    [Description("List on-book products at or below a quantity threshold, ordered lowest stock first. Use this for 'what's running low' or 'what needs reordering'.")]
    public Task<string> GetLowStockItemsAsync(
        [Description("Optional company GUID to filter by.")] string? companyId = null,
        [Description("Optional store group GUID to filter by.")] string? storeGroupId = null,
        [Description("Optional store GUID to filter by.")] string? storeId = null,
        [Description("Quantity at or below which an item is considered low stock. Default 3.")] double? thresholdQty = null,
        [Description("Maximum number of rows to return, default 20.")] int? take = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync("get_low_stock_items", new { companyId, storeGroupId, storeId, thresholdQty, take }, cancellationToken);

    [McpServerTool(Name = "get_outstanding_dues")]
    [Description("List outstanding customer receivables or vendor payables (bills with an amount still unpaid), largest due first. Use this for 'who owes us money' or 'which vendors are we due to pay'.")]
    public Task<string> GetOutstandingDuesAsync(
        [Description("Whether to list customer receivables or vendor payables. One of: customer, vendor.")] string partyType,
        [Description("Optional company GUID to filter by.")] string? companyId = null,
        [Description("Optional store GUID to filter by (customer dues only).")] string? storeId = null,
        [Description("Maximum number of rows to return, default 10.")] int? take = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync("get_outstanding_dues", new { partyType, companyId, storeId, take }, cancellationToken);

    [McpServerTool(Name = "get_business_snapshot")]
    [Description("Get a broad business snapshot for a date range: key metrics, top stores and store groups, customer/vendor dues grouped by age, cash payment summary and health warnings. Use this for open-ended questions like 'how is the business doing' or 'give me an overview'.")]
    public Task<string> GetBusinessSnapshotAsync(
        [Description("Optional company GUID to filter by.")] string? companyId = null,
        [Description("Optional store group GUID to filter by.")] string? storeGroupId = null,
        [Description("Optional store GUID to filter by.")] string? storeId = null,
        [Description("Start date, format yyyy-MM-dd. Defaults to the start of the current month.")] string? fromDate = null,
        [Description("End date inclusive, format yyyy-MM-dd. Defaults to today.")] string? toDate = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync("get_business_snapshot", new { companyId, storeGroupId, storeId, fromDate, toDate }, cancellationToken);

    [McpServerTool(Name = "get_today_snapshot")]
    [Description("Get today's snapshot: cash flow, staff attendance, absent employees and quick action items. Use this for 'how is today going', 'who is absent today' or 'what is our cash position today'.")]
    public Task<string> GetTodaySnapshotAsync(
        [Description("Optional company GUID to filter by.")] string? companyId = null,
        [Description("Optional store group GUID to filter by.")] string? storeGroupId = null,
        [Description("Optional store GUID to filter by.")] string? storeId = null,
        [Description("Date, format yyyy-MM-dd. Defaults to today.")] string? date = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync("get_today_snapshot", new { companyId, storeGroupId, storeId, date }, cancellationToken);

    private Task<string> ExecuteAsync(string toolName, object input, CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("MCP tool call has no HTTP request context to resolve workspace scope from.");
        return catalog.ExecuteAsync(toolName, JsonSerializer.Serialize(input), context, cancellationToken);
    }
}
