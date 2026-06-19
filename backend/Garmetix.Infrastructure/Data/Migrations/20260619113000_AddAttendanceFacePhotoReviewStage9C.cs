using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class AddAttendanceFacePhotoReviewStage9C : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewStatus" character varying(40) NOT NULL DEFAULT 'PendingReview';
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewedAtUtc" timestamp without time zone NULL;
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewedBy" character varying(120) NULL;
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewRemarks" character varying(300) NULL;
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "ReviewReason" character varying(80) NULL;
        ALTER TABLE "AttendancePhotoProofs" ADD COLUMN IF NOT EXISTS "RegularizationRequestId" uuid NULL;
        UPDATE "AttendancePhotoProofs"
        SET "ReviewStatus" = CASE
            WHEN "VerificationStatus" IN ('ManualApproved', 'Approved') THEN 'Approved'
            WHEN "VerificationStatus" IN ('Rejected') THEN 'Rejected'
            WHEN "VerificationStatus" IN ('Flagged') THEN 'Flagged'
            WHEN "VerificationStatus" IN ('NeedsRegularization') THEN 'NeedsRegularization'
            ELSE COALESCE(NULLIF("ReviewStatus", ''), 'PendingReview')
        END;
        CREATE INDEX IF NOT EXISTS "IX_AttendancePhotoProofs_CompanyId_StoreId_ReviewStatus_CapturedAtUtc" ON "AttendancePhotoProofs" ("CompanyId", "StoreId", "ReviewStatus", "CapturedAtUtc");
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
        DROP INDEX IF EXISTS "IX_AttendancePhotoProofs_CompanyId_StoreId_ReviewStatus_CapturedAtUtc";
        ALTER TABLE "AttendancePhotoProofs" DROP COLUMN IF EXISTS "RegularizationRequestId";
        ALTER TABLE "AttendancePhotoProofs" DROP COLUMN IF EXISTS "ReviewReason";
        ALTER TABLE "AttendancePhotoProofs" DROP COLUMN IF EXISTS "ReviewRemarks";
        ALTER TABLE "AttendancePhotoProofs" DROP COLUMN IF EXISTS "ReviewedBy";
        ALTER TABLE "AttendancePhotoProofs" DROP COLUMN IF EXISTS "ReviewedAtUtc";
        ALTER TABLE "AttendancePhotoProofs" DROP COLUMN IF EXISTS "ReviewStatus";
        """);
    }
}
