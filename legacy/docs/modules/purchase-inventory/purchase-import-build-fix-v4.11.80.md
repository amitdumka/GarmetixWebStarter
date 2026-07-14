# Purchase Import Build Fix v4.11.80

Version: 4.11.80  
Stage: Stage 11D-65 Purchase Import Build Fix

## Problem

Docker API build failed during `dotnet publish` with:

```text
PurchaseInvoiceImportService.cs(1141,27): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'DateTime' and '<null>'
```

## Cause

`ParseInvoiceText` inferred the type of `invoiceDate` from a conditional expression that returned `DateTime` on the true branch and `null` on the false branch.

## Fix

Changed the local variable to an explicit nullable DateTime:

```csharp
DateTime? invoiceDate = DateRegex.Match(normalized) is { Success: true } dateMatch && TryParseDate(dateMatch.Groups["date"].Value, out var parsedDate) ? parsedDate : null;
```

The purchase transaction retry hotfix from v4.11.79 remains intact.
