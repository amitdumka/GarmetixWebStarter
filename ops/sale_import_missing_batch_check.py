import argparse
import csv
import json
import re
from collections import defaultdict
from datetime import date, datetime
from decimal import Decimal, InvalidOperation, ROUND_HALF_UP
from pathlib import Path
from typing import Any

import openpyxl


def norm(value: Any) -> str:
    return re.sub(r"\s+", " ", str(value or "").strip()).upper()


def dec(value: Any) -> Decimal:
    if value is None:
        return Decimal("0")
    if isinstance(value, Decimal):
        return value
    if isinstance(value, (int, float)):
        return Decimal(str(value))
    text = re.sub(r"[^0-9.\-]", "", str(value).replace(",", "").strip())
    if not text or text == "-":
        return Decimal("0")
    try:
        return Decimal(text)
    except InvalidOperation:
        return Decimal("0")


def money(value: Any) -> str:
    return f"{dec(value).quantize(Decimal('0.01'), rounding=ROUND_HALF_UP):.2f}"


def qty(value: Any) -> str:
    text = f"{dec(value).quantize(Decimal('0.001'), rounding=ROUND_HALF_UP):.3f}"
    return text.rstrip("0").rstrip(".")


def parse_date(value: Any) -> date | None:
    if value is None:
        return None
    if isinstance(value, datetime):
        return value.date()
    if isinstance(value, date):
        return value
    text = str(value).strip()
    for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%Y-%m-%d", "%m/%d/%Y"):
        try:
            return datetime.strptime(text, fmt).date()
        except ValueError:
            continue
    return None


def header_map(sheet, header_row: int) -> dict[str, int]:
    result: dict[str, int] = {}
    for index, cell in enumerate(sheet[header_row], start=1):
        if cell.value is not None:
            result[norm(cell.value)] = index
    return result


def cell(row, headers: dict[str, int], name: str) -> Any:
    index = headers.get(norm(name))
    return row[index - 1] if index else None


def read_source_items(workbook_paths: list[Path]) -> dict[str, list[dict[str, str]]]:
    items_by_invoice: dict[str, list[dict[str, str]]] = defaultdict(list)
    for workbook_path in workbook_paths:
        wb = openpyxl.load_workbook(workbook_path, data_only=True, read_only=True)
        ws = wb["Item Details"]
        headers = header_map(ws, 3)
        for row in ws.iter_rows(min_row=4, values_only=True):
            invoice = str(cell(row, headers, "Invoice No./Txn No.") or "").strip()
            if not invoice:
                continue
            item_code = str(cell(row, headers, "Item Code") or "").strip()
            item_name = str(cell(row, headers, "Item Name") or "").strip()
            if not item_code and not item_name:
                continue
            item_date = parse_date(cell(row, headers, "Date"))
            items_by_invoice[invoice].append(
                {
                    "invoice": invoice,
                    "date": item_date.isoformat() if item_date else "",
                    "party": str(cell(row, headers, "Party Name") or "").strip(),
                    "itemCode": item_code,
                    "itemName": item_name,
                    "category": str(cell(row, headers, "Category") or "").strip(),
                    "hsn": str(cell(row, headers, "HSN/SAC") or "").strip(),
                    "size": str(cell(row, headers, "Size") or "").strip(),
                    "quantity": qty(cell(row, headers, "Quantity")),
                    "unit": str(cell(row, headers, "Unit") or "").strip(),
                    "unitPrice": money(cell(row, headers, "UnitPrice")),
                    "discountPercent": money(cell(row, headers, "Discount Percent")),
                    "discount": money(cell(row, headers, "Discount")),
                    "taxPercent": money(cell(row, headers, "Tax Percent")),
                    "tax": money(cell(row, headers, "Tax")),
                    "amount": money(cell(row, headers, "Amount")),
                    "sourceFile": workbook_path.name,
                }
            )
    return items_by_invoice


def load_stock_by_barcode(path: Path) -> dict[str, list[dict[str, Any]]]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if isinstance(payload, dict):
        rows = payload.get("items") or payload.get("data") or payload.get("stocks") or []
    else:
        rows = payload
    by_barcode: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        barcode = norm(row.get("barcode") or row.get("Barcode") or row.get("itemCode"))
        if barcode:
            by_barcode[barcode].append(row)
    return by_barcode


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workbooks", nargs="+", required=True)
    parser.add_argument("--missing-csv", required=True)
    parser.add_argument("--stock-json", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--start", type=int, default=0)
    parser.add_argument("--count", type=int, default=5)
    parser.add_argument("--label", default="batch")
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    with open(args.missing_csv, newline="", encoding="utf-8-sig") as handle:
        missing_invoices = list(csv.DictReader(handle))
    batch = missing_invoices[args.start : args.start + args.count]
    invoice_numbers = [row["invoice"] for row in batch]

    items_by_invoice = read_source_items([Path(item) for item in args.workbooks])
    stock_by_barcode = load_stock_by_barcode(Path(args.stock_json))

    detail_rows: list[dict[str, str]] = []
    missing_rows: list[dict[str, str]] = []
    invoice_stats: list[dict[str, Any]] = []
    for invoice_row in batch:
        invoice = invoice_row["invoice"]
        invoice_items = items_by_invoice.get(invoice, [])
        present = 0
        missing = 0
        for item in invoice_items:
            matches = stock_by_barcode.get(norm(item["itemCode"]), [])
            status = "Present" if matches else "Missing"
            if matches:
                present += 1
            else:
                missing += 1
                missing_rows.append(item)
            first = matches[0] if matches else {}
            detail_rows.append(
                {
                    **item,
                    "stockStatus": status,
                    "matchedBarcode": str(first.get("barcode") or ""),
                    "matchedProductName": str(first.get("productName") or ""),
                    "matchedMrp": money(first.get("mrp")),
                    "matchedCostPrice": money(first.get("costPrice")),
                    "currentStock": qty(first.get("currentStock")),
                    "stockId": str(first.get("stockId") or ""),
                    "productId": str(first.get("productId") or ""),
                }
            )
        invoice_stats.append(
            {
                "invoice": invoice,
                "date": invoice_row.get("date", ""),
                "customer": invoice_row.get("customer", ""),
                "expectedAmount": invoice_row.get("expectedImportBillAmount", ""),
                "sourceItemCount": len(invoice_items),
                "presentItemCount": present,
                "missingItemCount": missing,
                "status": "All items present" if missing == 0 else "Missing items",
            }
        )

    detail_csv = output_dir / f"{args.label}-item-stock-check.csv"
    todo_md = output_dir / f"{args.label}-missing-items-todo.md"
    summary_json = output_dir / f"{args.label}-summary.json"

    fieldnames = [
        "invoice",
        "date",
        "party",
        "itemCode",
        "itemName",
        "category",
        "hsn",
        "size",
        "quantity",
        "unit",
        "unitPrice",
        "discountPercent",
        "discount",
        "taxPercent",
        "tax",
        "amount",
        "stockStatus",
        "matchedBarcode",
        "matchedProductName",
        "matchedMrp",
        "matchedCostPrice",
        "currentStock",
        "stockId",
        "productId",
        "sourceFile",
    ]
    with detail_csv.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(detail_rows)

    summary = {
        "batchLabel": args.label,
        "start": args.start,
        "count": args.count,
        "invoiceNumbers": invoice_numbers,
        "invoiceCount": len(batch),
        "lineCount": len(detail_rows),
        "presentLineCount": sum(1 for row in detail_rows if row["stockStatus"] == "Present"),
        "missingLineCount": len(missing_rows),
        "invoiceStats": invoice_stats,
        "outputs": {
            "detailCsv": str(detail_csv),
            "todoMd": str(todo_md),
            "summaryJson": str(summary_json),
        },
    }
    summary_json.write_text(json.dumps(summary, indent=2), encoding="utf-8")

    lines = [
        f"# Sale Import Missing Product Batch - {args.label}",
        "",
        "Scope: first missing sale-import invoices from source Excel, checked against current stock barcode options.",
        "",
        "No database update was made.",
        "",
        "## Batch Invoices",
        "",
    ]
    for stat in invoice_stats:
        lines.append(
            f"- {stat['invoice']} ({stat['date']}, {stat['customer']}): "
            f"{stat['missingItemCount']} missing of {stat['sourceItemCount']} item lines, amount {stat['expectedAmount']}"
        )
    lines.extend(["", "## Missing Item Lines", ""])
    if not missing_rows:
        lines.append("All item lines in this batch are present in stock barcode options.")
    else:
        current_invoice = None
        for row in missing_rows:
            if row["invoice"] != current_invoice:
                current_invoice = row["invoice"]
                lines.extend(["", f"### {current_invoice}", ""])
            lines.append(
                f"- Item Code `{row['itemCode']}` | {row['itemName']} | "
                f"Qty {row['quantity']} {row['unit']} | Amount {row['amount']} | "
                f"Tax {row['taxPercent']}% | HSN {row['hsn']} | Category {row['category']} | Size {row['size']} | "
                "Action: provide matching Garmetix barcode OR approve create stock item."
            )
    lines.extend(["", "## Invoice Line Status", ""])
    for stat in invoice_stats:
        lines.append(f"- {stat['invoice']}: {stat['status']}")
    todo_md.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
