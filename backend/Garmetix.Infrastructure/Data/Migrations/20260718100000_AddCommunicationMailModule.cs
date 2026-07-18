using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunicationMailModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunicationConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    ConversationType = table.Column<string>(type: "text", nullable: false),
                    SourceModule = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table => table.PrimaryKey("PK_CommunicationConversations", x => x.Id));

            migrationBuilder.CreateTable(
                name: "CommunicationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderNameSnapshot = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    BodyFormat = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    IsDraft = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReplyToMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table => table.PrimaryKey("PK_CommunicationMessages", x => x.Id));

            migrationBuilder.CreateTable(
                name: "CommunicationRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FolderState = table.Column<string>(type: "text", nullable: false),
                    TrashedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_CommunicationRecipients", x => x.Id));

            migrationBuilder.CreateTable(
                name: "CommunicationAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    StoredFileName = table.Column<string>(type: "text", nullable: false),
                    StoredRelativePath = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Sha256Checksum = table.Column<string>(type: "text", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_CommunicationAttachments", x => x.Id));

            migrationBuilder.CreateTable(
                name: "CommunicationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyOnDirectMessage = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyOnBroadcast = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DigestFrequency = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_CommunicationPreferences", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailProviderConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<string>(type: "text", nullable: false),
                    SmtpPresetKey = table.Column<string>(type: "text", nullable: true),
                    Host = table.Column<string>(type: "text", nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: true),
                    EnableSsl = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UseStartTls = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    FromEmail = table.Column<string>(type: "text", nullable: false),
                    FromName = table.Column<string>(type: "text", nullable: false),
                    ReplyToEmail = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    DailyRateLimit = table.Column<int>(type: "integer", nullable: true),
                    PerMinuteRateLimit = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table => table.PrimaryKey("PK_EmailProviderConfigurations", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailProviderCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialKey = table.Column<string>(type: "text", nullable: false),
                    EncryptedValue = table.Column<string>(type: "text", nullable: false),
                    MaskedDisplayValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_EmailProviderCredentials", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateKey = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    IsSystemTemplate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table => table.PrimaryKey("PK_EmailTemplates", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: true),
                    SampleDataJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_EmailTemplateVersions", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceModule = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    ProviderIdUsed = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    TextBody = table.Column<string>(type: "text", nullable: true),
                    ScheduledForUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProcessingLeaseUntilUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProcessingLeaseOwner = table.Column<string>(type: "text", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 8),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "text", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "text", nullable: true),
                    ResentFromQueueItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table => table.PrimaryKey("PK_EmailQueueItems", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    QueueItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    EmailAddress = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_EmailRecipients", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    QueueItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    StoredFileName = table.Column<string>(type: "text", nullable: false),
                    StoredRelativePath = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Sha256Checksum = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_EmailAttachments", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    QueueItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WasSuccess = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_EmailDeliveryAttempts", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailDeliveryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    QueueItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "text", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    ProviderEventId = table.Column<string>(type: "text", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RawPayloadSanitizedJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_EmailDeliveryEvents", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailSuppressionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailAddress = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RemovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RemovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RemovalReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_EmailSuppressionEntries", x => x.Id));

            migrationBuilder.CreateTable(
                name: "EmailUsageCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Synced = table.Column<bool>(type: "boolean", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodKey = table.Column<string>(type: "text", nullable: false),
                    SentCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FailedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table => table.PrimaryKey("PK_EmailUsageCounters", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_CommunicationConversations_CompanyId_LastMessageAtUtc", table: "CommunicationConversations", columns: new[] { "CompanyId", "LastMessageAtUtc" });
            migrationBuilder.CreateIndex(name: "IX_CommunicationMessages_ConversationId_CreatedAt", table: "CommunicationMessages", columns: new[] { "ConversationId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_CommunicationRecipients_MessageId_RecipientUserId", table: "CommunicationRecipients", columns: new[] { "MessageId", "RecipientUserId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_CommunicationRecipients_RecipientUserId_FolderState_IsRead", table: "CommunicationRecipients", columns: new[] { "RecipientUserId", "FolderState", "IsRead" });
            migrationBuilder.CreateIndex(name: "IX_CommunicationRecipients_RecipientUserId_ConversationId", table: "CommunicationRecipients", columns: new[] { "RecipientUserId", "ConversationId" });
            migrationBuilder.CreateIndex(name: "IX_CommunicationAttachments_MessageId", table: "CommunicationAttachments", column: "MessageId");
            migrationBuilder.CreateIndex(name: "IX_CommunicationPreferences_UserId", table: "CommunicationPreferences", column: "UserId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_EmailProviderConfigurations_Scope_Enabled_Priority", table: "EmailProviderConfigurations", columns: new[] { "CompanyId", "StoreGroupId", "StoreId", "IsEnabled", "Priority" });
            migrationBuilder.CreateIndex(name: "IX_EmailProviderCredentials_ProviderId_CredentialKey", table: "EmailProviderCredentials", columns: new[] { "ProviderId", "CredentialKey" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_EmailTemplates_CompanyId_TemplateKey", table: "EmailTemplates", columns: new[] { "CompanyId", "TemplateKey" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_EmailTemplateVersions_TemplateId_VersionNumber", table: "EmailTemplateVersions", columns: new[] { "TemplateId", "VersionNumber" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_EmailQueueItems_IdempotencyKey", table: "EmailQueueItems", column: "IdempotencyKey", unique: true);
            migrationBuilder.CreateIndex(name: "IX_EmailQueueItems_Status_NextAttemptAtUtc", table: "EmailQueueItems", columns: new[] { "Status", "NextAttemptAtUtc" });
            migrationBuilder.CreateIndex(name: "IX_EmailQueueItems_CorrelationId", table: "EmailQueueItems", column: "CorrelationId");
            migrationBuilder.CreateIndex(name: "IX_EmailQueueItems_Source", table: "EmailQueueItems", columns: new[] { "SourceModule", "SourceType", "SourceId" });
            migrationBuilder.CreateIndex(name: "IX_EmailQueueItems_ProviderMessageId", table: "EmailQueueItems", column: "ProviderMessageId");
            migrationBuilder.CreateIndex(name: "IX_EmailRecipients_QueueItemId", table: "EmailRecipients", column: "QueueItemId");
            migrationBuilder.CreateIndex(name: "IX_EmailAttachments_QueueItemId", table: "EmailAttachments", column: "QueueItemId");
            migrationBuilder.CreateIndex(name: "IX_EmailDeliveryAttempts_QueueItemId_AttemptNumber", table: "EmailDeliveryAttempts", columns: new[] { "QueueItemId", "AttemptNumber" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_EmailDeliveryEvents_ProviderEventId", table: "EmailDeliveryEvents", column: "ProviderEventId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_EmailDeliveryEvents_QueueItemId", table: "EmailDeliveryEvents", column: "QueueItemId");
            migrationBuilder.CreateIndex(name: "IX_EmailDeliveryEvents_ProviderMessageId", table: "EmailDeliveryEvents", column: "ProviderMessageId");
            migrationBuilder.CreateIndex(name: "IX_EmailSuppressionEntries_CompanyId_EmailAddress", table: "EmailSuppressionEntries", columns: new[] { "CompanyId", "EmailAddress" });
            migrationBuilder.CreateIndex(name: "IX_EmailUsageCounters_ProviderId_CompanyId_PeriodKey", table: "EmailUsageCounters", columns: new[] { "ProviderId", "CompanyId", "PeriodKey" }, unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CommunicationAttachments");
            migrationBuilder.DropTable(name: "CommunicationRecipients");
            migrationBuilder.DropTable(name: "CommunicationPreferences");
            migrationBuilder.DropTable(name: "CommunicationMessages");
            migrationBuilder.DropTable(name: "CommunicationConversations");
            migrationBuilder.DropTable(name: "EmailProviderCredentials");
            migrationBuilder.DropTable(name: "EmailProviderConfigurations");
            migrationBuilder.DropTable(name: "EmailTemplateVersions");
            migrationBuilder.DropTable(name: "EmailTemplates");
            migrationBuilder.DropTable(name: "EmailRecipients");
            migrationBuilder.DropTable(name: "EmailAttachments");
            migrationBuilder.DropTable(name: "EmailDeliveryAttempts");
            migrationBuilder.DropTable(name: "EmailDeliveryEvents");
            migrationBuilder.DropTable(name: "EmailSuppressionEntries");
            migrationBuilder.DropTable(name: "EmailUsageCounters");
            migrationBuilder.DropTable(name: "EmailQueueItems");
        }
    }
}
