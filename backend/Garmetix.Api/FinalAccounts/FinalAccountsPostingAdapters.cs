namespace Garmetix.Api.FinalAccounts;

public interface IFinalAccountsPostingAdapter
{
    string SourceType { get; }
    string RuleCode { get; }
    string RuleVersion { get; }

    Task<FinalAccountsPostingPreviewRequest> BuildPreviewAsync(
        Guid sourceId,
        FinalAccountsScopeDto scope,
        HttpContext context,
        CancellationToken cancellationToken);
}
