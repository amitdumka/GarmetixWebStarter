namespace Garmetix.Api.Hr;

public sealed record GenerateMonthlyAttendanceRequest(
    int Year,
    int Month,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId);

public sealed record GenerateMonthlyAttendanceResponse(
    int Year,
    int Month,
    int EmployeesProcessed,
    int RecordsCreated,
    int RecordsUpdated);
