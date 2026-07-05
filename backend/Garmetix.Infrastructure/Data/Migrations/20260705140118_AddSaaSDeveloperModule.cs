using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaaSDeveloperModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SaaSClientId",
                table: "Companies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttendanceMode",
                table: "AttendanceShifts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BreakEndMinutes",
                table: "AttendanceShifts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BreakStartMinutes",
                table: "AttendanceShifts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CountBreakAsWork",
                table: "AttendanceShifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasBreak",
                table: "AttendanceShifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiredSessionsForFullDay",
                table: "AttendanceShifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredSessionsForHalfDay",
                table: "AttendanceShifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresBreakPunch",
                table: "AttendanceShifts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShiftCategory",
                table: "AttendanceShifts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "BreakInTime",
                table: "Attendance",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "BreakOutTime",
                table: "Attendance",
                type: "interval",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFeedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigitalBillCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudienceKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PublicPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LastInvoiceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MessageBody = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalBillCampaignRecipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigitalBillCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Segment = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SearchText = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Channel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    MessageTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MessageBody = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OfferUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    PreparedCount = table.Column<int>(type: "integer", nullable: false),
                    SentCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    OpenCountAtCreate = table.Column<int>(type: "integer", nullable: false),
                    ReviewClickCountAtCreate = table.Column<int>(type: "integer", nullable: false),
                    FeedbackCountAtCreate = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalBillCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigitalInvoiceEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalInvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    EventAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalInvoiceEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigitalInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PublicToken = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PdfPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    HtmlSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisabledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DisabledBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DisableReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LastOpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OpenCount = table.Column<int>(type: "integer", nullable: false),
                    PdfDownloadCount = table.Column<int>(type: "integer", nullable: false),
                    ReviewClickCount = table.Column<int>(type: "integer", nullable: false),
                    FeedbackCount = table.Column<int>(type: "integer", nullable: false),
                    WhatsAppStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LastWhatsAppSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DotMatrixPrintQueueEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OperationTimeUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PartyName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PaymentMode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SequenceNo = table.Column<long>(type: "bigint", nullable: false),
                    LineWidth = table.Column<int>(type: "integer", nullable: false),
                    PrinterName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    PrintedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PrintableText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DotMatrixPrintQueueEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DotMatrixPrintSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrinterName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OutputMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SpoolDirectory = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PrintTransactions = table.Column<bool>(type: "boolean", nullable: false),
                    PrintDayOpeningClosing = table.Column<bool>(type: "boolean", nullable: false),
                    PrintEditsAndDeletes = table.Column<bool>(type: "boolean", nullable: false),
                    PrintAttendanceInDaySummary = table.Column<bool>(type: "boolean", nullable: false),
                    PrintBankUpiSummary = table.Column<bool>(type: "boolean", nullable: false),
                    LineWidth = table.Column<int>(type: "integer", nullable: false),
                    RetryLimit = table.Column<int>(type: "integer", nullable: false),
                    PollSeconds = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DotMatrixPrintSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAttendanceShiftRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MatchValue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendanceShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAttendanceShiftRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAttendanceShiftRules_AttendanceShifts_AttendanceShi~",
                        column: x => x.AttendanceShiftId,
                        principalTable: "AttendanceShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeAttendanceShiftRules_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceAdBanners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Position = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceAdBanners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PosHeldBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientHeldBillId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HeldAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerMobileNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayableTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DraftJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HeldByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    HeldByUserName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ResumedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosHeldBills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SourceFileName = table.Column<string>(type: "text", nullable: false),
                    StoredFilePath = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "text", nullable: false),
                    OcrProvider = table.Column<string>(type: "text", nullable: false),
                    OcrStatus = table.Column<string>(type: "text", nullable: false),
                    RawTextPath = table.Column<string>(type: "text", nullable: true),
                    RawJsonPath = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ParserTemplate = table.Column<string>(type: "text", nullable: true),
                    ParserTemplateReason = table.Column<string>(type: "text", nullable: true),
                    ImportQaNotes = table.Column<string>(type: "text", nullable: true),
                    AcceptanceStatus = table.Column<string>(type: "text", nullable: true),
                    AcceptanceNotes = table.Column<string>(type: "text", nullable: true),
                    AcceptanceTestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcceptanceTestedBy = table.Column<string>(type: "text", nullable: true),
                    CorrectionStatus = table.Column<string>(type: "text", nullable: true),
                    CorrectionNotes = table.Column<string>(type: "text", nullable: true),
                    CorrectionRequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CorrectionRequestedBy = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    VendorNameRaw = table.Column<string>(type: "text", nullable: true),
                    VendorNameFinal = table.Column<string>(type: "text", nullable: true),
                    VendorGstinRaw = table.Column<string>(type: "text", nullable: true),
                    VendorGstinFinal = table.Column<string>(type: "text", nullable: true),
                    VendorMobileNumber = table.Column<string>(type: "text", nullable: true),
                    VendorAddress = table.Column<string>(type: "text", nullable: true),
                    SupplierInvoiceNumber = table.Column<string>(type: "text", nullable: true),
                    SupplierInvoiceDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TaxableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FreightAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoundOff = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BillAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMode = table.Column<int>(type: "integer", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DuplicatePurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedPurchaseInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    PostedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VerifiedBy = table.Column<string>(type: "text", nullable: true),
                    DuplicateOverrideReason = table.Column<string>(type: "text", nullable: true),
                    DuplicateOverrideBy = table.Column<string>(type: "text", nullable: true),
                    DuplicateOverrideAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceImportVendorProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    VendorGstin = table.Column<string>(type: "text", nullable: true),
                    VendorName = table.Column<string>(type: "text", nullable: true),
                    IgnoredLinePatternsJson = table.Column<string>(type: "text", nullable: true),
                    ProductAliasesJson = table.Column<string>(type: "text", nullable: true),
                    LastLearnedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SuccessfulDraftCount = table.Column<int>(type: "integer", nullable: false),
                    LearningNotes = table.Column<string>(type: "text", nullable: true),
                    PreferredParserTemplate = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceImportVendorProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaaSClients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GSTIN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaaSClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaaSPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxCompanies = table.Column<int>(type: "integer", nullable: false),
                    MaxStoreGroups = table.Column<int>(type: "integer", nullable: false),
                    MaxStores = table.Column<int>(type: "integer", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    IncludedModulesCsv = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaaSPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreReviewSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleReviewUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InstagramUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FacebookUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WhatsAppSupportNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EnableGoogleReview = table.Column<bool>(type: "boolean", nullable: false),
                    EnableInstagram = table.Column<bool>(type: "boolean", nullable: false),
                    EnableFacebook = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWhatsappSupport = table.Column<bool>(type: "boolean", nullable: false),
                    EnablePrivateFeedback = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewButtonText = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FeedbackButtonText = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreReviewSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaxStores = table.Column<int>(type: "integer", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    IncludedModulesCsv = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMessageLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DigitalInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerMobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MessageBody = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMessageLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppProviderSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoSendDigitalBills = table.Column<bool>(type: "boolean", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApiToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PhoneNumberId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SenderId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LanguageCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MessageTemplateText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SendPdfLink = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackToManualLog = table.Column<bool>(type: "boolean", nullable: false),
                    RetryLimit = table.Column<int>(type: "integer", nullable: false),
                    LastTestAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppProviderSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceImportFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileKind = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    StoredFilePath = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceImportFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceImportFiles_PurchaseInvoiceImportBatches_Bat~",
                        column: x => x.BatchId,
                        principalTable: "PurchaseInvoiceImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceImportLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductNameRaw = table.Column<string>(type: "text", nullable: true),
                    ProductNameFinal = table.Column<string>(type: "text", nullable: true),
                    BarcodeRaw = table.Column<string>(type: "text", nullable: true),
                    BarcodeFinal = table.Column<string>(type: "text", nullable: true),
                    HsnCode = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Mrp = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GstPriceMode = table.Column<string>(type: "text", nullable: false),
                    TaxId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IgstAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MatchStatus = table.Column<string>(type: "text", nullable: false),
                    ReviewRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewMessage = table.Column<string>(type: "text", nullable: true),
                    ProductCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductSubCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductType = table.Column<int>(type: "integer", nullable: false),
                    ProductGroup = table.Column<int>(type: "integer", nullable: false),
                    Ignored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceImportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceImportLines_PurchaseInvoiceImportBatches_Bat~",
                        column: x => x.BatchId,
                        principalTable: "PurchaseInvoiceImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaaSTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaaSClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaaSPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenString = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsActivated = table.Column<bool>(type: "boolean", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaaSTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaaSTokens_SaaSClients_SaaSClientId",
                        column: x => x.SaaSClientId,
                        principalTable: "SaaSClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaaSTokens_SaaSPlans_SaaSPlanId",
                        column: x => x.SaaSPlanId,
                        principalTable: "SaaSPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CompanyId_StoreId_Barcode",
                table: "Stocks",
                columns: new[] { "CompanyId", "StoreId", "Barcode" });

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CompanyId_StoreId_ProductId_IsOFB",
                table: "Stocks",
                columns: new[] { "CompanyId", "StoreId", "ProductId", "IsOFB" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_CompanyId_StoreId_InwardDate",
                table: "PurchaseInvoices",
                columns: new[] { "CompanyId", "StoreId", "InwardDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_CompanyId_StoreId_OnDate",
                table: "PurchaseInvoices",
                columns: new[] { "CompanyId", "StoreId", "OnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_CompanyId_StoreId_VendorId_OnDate",
                table: "PurchaseInvoices",
                columns: new[] { "CompanyId", "StoreId", "VendorId", "OnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_CompanyId_InvoiceId",
                table: "InvoiceItems",
                columns: new[] { "CompanyId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_SaaSClientId",
                table: "Companies",
                column: "SaaSClientId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedback_CompanyId_DigitalInvoiceId",
                table: "CustomerFeedback",
                columns: new[] { "CompanyId", "DigitalInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFeedback_CompanyId_StoreId_SubmittedAt",
                table: "CustomerFeedback",
                columns: new[] { "CompanyId", "StoreId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalBillCampaignRecipients_CompanyId_CampaignId_Status",
                table: "DigitalBillCampaignRecipients",
                columns: new[] { "CompanyId", "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalBillCampaignRecipients_CompanyId_CustomerMobile_Crea~",
                table: "DigitalBillCampaignRecipients",
                columns: new[] { "CompanyId", "CustomerMobile", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalBillCampaigns_CompanyId_Segment_FromDate_ToDate",
                table: "DigitalBillCampaigns",
                columns: new[] { "CompanyId", "Segment", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalBillCampaigns_CompanyId_StoreId_Status_CreatedAt",
                table: "DigitalBillCampaigns",
                columns: new[] { "CompanyId", "StoreId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalInvoiceEvents_CompanyId_DigitalInvoiceId_EventAt",
                table: "DigitalInvoiceEvents",
                columns: new[] { "CompanyId", "DigitalInvoiceId", "EventAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalInvoiceEvents_CompanyId_StoreId_EventType_EventAt",
                table: "DigitalInvoiceEvents",
                columns: new[] { "CompanyId", "StoreId", "EventType", "EventAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalInvoices_CompanyId_InvoiceId_InvoiceType",
                table: "DigitalInvoices",
                columns: new[] { "CompanyId", "InvoiceId", "InvoiceType" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalInvoices_CompanyId_StoreId_InvoiceDate",
                table: "DigitalInvoices",
                columns: new[] { "CompanyId", "StoreId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalInvoices_PublicToken",
                table: "DigitalInvoices",
                column: "PublicToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DotMatrixPrintQueueEntries_CompanyId_StoreId_BusinessDate_S~",
                table: "DotMatrixPrintQueueEntries",
                columns: new[] { "CompanyId", "StoreId", "BusinessDate", "SequenceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_DotMatrixPrintQueueEntries_DeduplicationKey",
                table: "DotMatrixPrintQueueEntries",
                column: "DeduplicationKey");

            migrationBuilder.CreateIndex(
                name: "IX_DotMatrixPrintQueueEntries_SourceType_SourceId_ActionType",
                table: "DotMatrixPrintQueueEntries",
                columns: new[] { "SourceType", "SourceId", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_DotMatrixPrintQueueEntries_Status_CreatedAt",
                table: "DotMatrixPrintQueueEntries",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DotMatrixPrintSettings_CompanyId_StoreId",
                table: "DotMatrixPrintSettings",
                columns: new[] { "CompanyId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendanceShiftRules_AttendanceShiftId",
                table: "EmployeeAttendanceShiftRules",
                column: "AttendanceShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendanceShiftRules_CompanyId_StoreId_Active_Prior~",
                table: "EmployeeAttendanceShiftRules",
                columns: new[] { "CompanyId", "StoreId", "Active", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendanceShiftRules_CompanyId_StoreId_EmployeeId_A~",
                table: "EmployeeAttendanceShiftRules",
                columns: new[] { "CompanyId", "StoreId", "EmployeeId", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendanceShiftRules_EmployeeId",
                table: "EmployeeAttendanceShiftRules",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAdBanners_CompanyId_StoreId_Position_IsActive",
                table: "InvoiceAdBanners",
                columns: new[] { "CompanyId", "StoreId", "Position", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PosHeldBills_CompanyId_ClientHeldBillId",
                table: "PosHeldBills",
                columns: new[] { "CompanyId", "ClientHeldBillId" });

            migrationBuilder.CreateIndex(
                name: "IX_PosHeldBills_CompanyId_StoreGroupId_StoreId_Status_HeldAt",
                table: "PosHeldBills",
                columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "Status", "HeldAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportBatches_CompanyId_StoreId_Status_Creat~",
                table: "PurchaseInvoiceImportBatches",
                columns: new[] { "CompanyId", "StoreId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportBatches_CompanyId_VendorGstinFinal_Sup~",
                table: "PurchaseInvoiceImportBatches",
                columns: new[] { "CompanyId", "VendorGstinFinal", "SupplierInvoiceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportBatches_PostedPurchaseInvoiceId",
                table: "PurchaseInvoiceImportBatches",
                column: "PostedPurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportBatches_Sha256Hash",
                table: "PurchaseInvoiceImportBatches",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportFiles_BatchId",
                table: "PurchaseInvoiceImportFiles",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportFiles_CompanyId_BatchId_FileKind",
                table: "PurchaseInvoiceImportFiles",
                columns: new[] { "CompanyId", "BatchId", "FileKind" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportFiles_Sha256Hash",
                table: "PurchaseInvoiceImportFiles",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportLines_BatchId",
                table: "PurchaseInvoiceImportLines",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportLines_CompanyId_BatchId_LineNumber",
                table: "PurchaseInvoiceImportLines",
                columns: new[] { "CompanyId", "BatchId", "LineNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportLines_CompanyId_ProductId",
                table: "PurchaseInvoiceImportLines",
                columns: new[] { "CompanyId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportVendorProfiles_CompanyId_VendorGstin",
                table: "PurchaseInvoiceImportVendorProfiles",
                columns: new[] { "CompanyId", "VendorGstin" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceImportVendorProfiles_CompanyId_VendorId",
                table: "PurchaseInvoiceImportVendorProfiles",
                columns: new[] { "CompanyId", "VendorId" });

            migrationBuilder.CreateIndex(
                name: "IX_SaaSTokens_SaaSClientId",
                table: "SaaSTokens",
                column: "SaaSClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SaaSTokens_SaaSPlanId",
                table: "SaaSTokens",
                column: "SaaSPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreReviewSettings_CompanyId_StoreId",
                table: "StoreReviewSettings",
                columns: new[] { "CompanyId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_CompanyId",
                table: "TenantSubscriptions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessageLogs_CompanyId_DigitalInvoiceId",
                table: "WhatsAppMessageLogs",
                columns: new[] { "CompanyId", "DigitalInvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessageLogs_CompanyId_StoreId_Status_CreatedAt",
                table: "WhatsAppMessageLogs",
                columns: new[] { "CompanyId", "StoreId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppProviderSettings_CompanyId_StoreId",
                table: "WhatsAppProviderSettings",
                columns: new[] { "CompanyId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppProviderSettings_CompanyId_StoreId_IsEnabled_AutoSe~",
                table: "WhatsAppProviderSettings",
                columns: new[] { "CompanyId", "StoreId", "IsEnabled", "AutoSendDigitalBills" });

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_SaaSClients_SaaSClientId",
                table: "Companies",
                column: "SaaSClientId",
                principalTable: "SaaSClients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_SaaSClients_SaaSClientId",
                table: "Companies");

            migrationBuilder.DropTable(
                name: "CustomerFeedback");

            migrationBuilder.DropTable(
                name: "DigitalBillCampaignRecipients");

            migrationBuilder.DropTable(
                name: "DigitalBillCampaigns");

            migrationBuilder.DropTable(
                name: "DigitalInvoiceEvents");

            migrationBuilder.DropTable(
                name: "DigitalInvoices");

            migrationBuilder.DropTable(
                name: "DotMatrixPrintQueueEntries");

            migrationBuilder.DropTable(
                name: "DotMatrixPrintSettings");

            migrationBuilder.DropTable(
                name: "EmployeeAttendanceShiftRules");

            migrationBuilder.DropTable(
                name: "InvoiceAdBanners");

            migrationBuilder.DropTable(
                name: "PosHeldBills");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceImportFiles");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceImportLines");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceImportVendorProfiles");

            migrationBuilder.DropTable(
                name: "SaaSTokens");

            migrationBuilder.DropTable(
                name: "StoreReviewSettings");

            migrationBuilder.DropTable(
                name: "TenantSubscriptions");

            migrationBuilder.DropTable(
                name: "WhatsAppMessageLogs");

            migrationBuilder.DropTable(
                name: "WhatsAppProviderSettings");

            migrationBuilder.DropTable(
                name: "PurchaseInvoiceImportBatches");

            migrationBuilder.DropTable(
                name: "SaaSClients");

            migrationBuilder.DropTable(
                name: "SaaSPlans");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_CompanyId_StoreId_Barcode",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_CompanyId_StoreId_ProductId_IsOFB",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_CompanyId_StoreId_InwardDate",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_CompanyId_StoreId_OnDate",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_CompanyId_StoreId_VendorId_OnDate",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_CompanyId_InvoiceId",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_Companies_SaaSClientId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SaaSClientId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AttendanceMode",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "BreakEndMinutes",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "BreakStartMinutes",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "CountBreakAsWork",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "HasBreak",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "RequiredSessionsForFullDay",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "RequiredSessionsForHalfDay",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "RequiresBreakPunch",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "ShiftCategory",
                table: "AttendanceShifts");

            migrationBuilder.DropColumn(
                name: "BreakInTime",
                table: "Attendance");

            migrationBuilder.DropColumn(
                name: "BreakOutTime",
                table: "Attendance");
        }
    }
}
