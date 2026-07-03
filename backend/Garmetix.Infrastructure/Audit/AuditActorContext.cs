namespace Garmetix.Infrastructure.Audit;

public sealed class AuditActorContext
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? StoreGroupId { get; set; }
    public Guid? StoreId { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public string? IpAddress { get; set; }
    public string? TraceIdentifier { get; set; }
}
