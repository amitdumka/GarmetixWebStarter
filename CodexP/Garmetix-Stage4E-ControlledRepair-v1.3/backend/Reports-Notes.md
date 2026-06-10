# Reports Export and Cache Notes

The Reports page now has frontend export/cache support for the existing report views:

- Sales
- Purchase
- Stock
- Petty Cash
- Attendance
- Payroll

## Export options

- CSV export remains available from the primary toolbar action.
- Excel export downloads the current report table as an `.xls` spreadsheet-compatible file.
- PDF export uses the browser print dialog; choose **Save as PDF**.
- Print uses the same print layout and stores a local cache snapshot first.

## Cache behavior

Report cache is stored in browser `localStorage` using this key shape:

```text
garmetix.report.{reportKind}.{fromDate}.{toDate}.{search}
```

Use the Reports page buttons:

- **Cache**: stores current live report rows.
- **Load Cache**: loads cached rows for the selected report/filter/search.
- **Live**: returns to live data.

This is an operator convenience cache, not a server audit cache. Server-side report cache can be added later if report volume becomes high.
