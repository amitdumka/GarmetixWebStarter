namespace Garmetix.Api.FinalAccounts;

public interface IFinalAccountsPostingAdapter
{
    string AdapterKey { get; }
    string SourceType { get; }
    string RuleCode { get; }
    string RuleVersion { get; }
    string DisplayName { get; }
    string SourceTable { get; }
    string Description { get; }

    Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(
        Guid sourceId,
        FinalAccountsScopeDto scope,
        HttpContext context,
        CancellationToken cancellationToken);
}
