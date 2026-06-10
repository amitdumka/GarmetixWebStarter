using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Commercial;

public static class LoyaltyService
{
    public static async Task AwardSalePointsAsync(Invoice invoice, Customer? customer, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (customer is null || invoice.BillAmount <= 0 || invoice.ReturnInvoice || invoice.InvoiceStatus == Garmetix.Core.Enums.InvoiceStatus.Cancelled)
        {
            return;
        }

        var program = await db.LoyaltyPrograms
            .Where(item => item.CompanyId == invoice.CompanyId && item.StoreId == invoice.StoreId && item.Enabled)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null || invoice.BillAmount < program.MinimumBillAmount || program.EarnPointsPerRupee <= 0)
        {
            return;
        }

        var points = Math.Round(invoice.BillAmount * program.EarnPointsPerRupee, 2);
        if (points <= 0)
        {
            return;
        }

        customer.LoyaltyPoints += points;
        var storeGroupId = await db.Stores.AsNoTracking().Where(item => item.Id == invoice.StoreId).Select(item => item.StoreGroupId).FirstOrDefaultAsync(cancellationToken);
        db.LoyaltyPointLedgers.Add(new LoyaltyPointLedger
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            OnDate = DateTime.Now,
            SourceType = "SaleInvoice",
            SourceId = invoice.Id,
            SourceNumber = invoice.InvoiceNumber,
            PointsIn = points,
            PointsOut = 0,
            BalanceAfter = customer.LoyaltyPoints,
            Remarks = "Sale invoice loyalty points",
            CompanyId = invoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = invoice.StoreId
        });
    }

    public static async Task ReverseSalePointsAsync(Invoice invoice, Customer? customer, decimal proportion, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (customer is null || proportion <= 0)
        {
            return;
        }

        var earned = await db.LoyaltyPointLedgers
            .Where(item => item.SourceType == "SaleInvoice" && item.SourceId == invoice.Id && item.PointsIn > 0)
            .SumAsync(item => item.PointsIn, cancellationToken);
        var points = Math.Round(earned * Math.Min(proportion, 1m), 2);
        if (points <= 0)
        {
            return;
        }

        points = Math.Min(points, customer.LoyaltyPoints);
        customer.LoyaltyPoints -= points;
        var storeGroupId = await db.Stores.AsNoTracking().Where(item => item.Id == invoice.StoreId).Select(item => item.StoreGroupId).FirstOrDefaultAsync(cancellationToken);
        db.LoyaltyPointLedgers.Add(new LoyaltyPointLedger
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            OnDate = DateTime.Now,
            SourceType = "SalesReturn",
            SourceId = invoice.Id,
            SourceNumber = invoice.InvoiceNumber,
            PointsIn = 0,
            PointsOut = points,
            BalanceAfter = customer.LoyaltyPoints,
            Remarks = "Sales return loyalty reversal",
            CompanyId = invoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = invoice.StoreId
        });
    }
}
