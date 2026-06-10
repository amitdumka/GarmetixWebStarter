using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmetix.Infrastructure.Data.Migrations;

public partial class EnhanceNonGstGoodsMemoReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" ADD COLUMN IF NOT EXISTS ""GrossAmount"" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" ADD COLUMN IF NOT EXISTS ""TaxableAmount"" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" ADD COLUMN IF NOT EXISTS ""TaxRate"" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" ADD COLUMN IF NOT EXISTS ""TaxAmount"" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" ADD COLUMN IF NOT EXISTS ""CostRate"" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" ADD COLUMN IF NOT EXISTS ""CostAmount"" numeric(18,2) NOT NULL DEFAULT 0;

            UPDATE ""NonGstGoodsItems""
            SET ""GrossAmount"" = CASE WHEN ""GrossAmount"" = 0 THEN ROUND(""Quantity"" * ""Rate"", 2) ELSE ""GrossAmount"" END,
                ""TaxableAmount"" = CASE WHEN ""TaxableAmount"" = 0 THEN ""Amount"" ELSE ""TaxableAmount"" END,
                ""TaxRate"" = 0,
                ""TaxAmount"" = 0
            WHERE TRUE;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" DROP COLUMN IF EXISTS ""GrossAmount"";
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" DROP COLUMN IF EXISTS ""TaxableAmount"";
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" DROP COLUMN IF EXISTS ""TaxRate"";
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" DROP COLUMN IF EXISTS ""TaxAmount"";
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" DROP COLUMN IF EXISTS ""CostRate"";
            ALTER TABLE IF EXISTS ""NonGstGoodsItems"" DROP COLUMN IF EXISTS ""CostAmount"";
        ");
    }
}
