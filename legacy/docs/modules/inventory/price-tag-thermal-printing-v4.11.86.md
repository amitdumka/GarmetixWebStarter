# v4.11.86 Price Tag Thermal Printing

## Scope

Adds a browser-based price tag printing page for thermal label sizes:

- 50 × 30 mm
- 50 × 25 mm

The page supports stock search, purchase-inward item loading, editable copies/MRP/barcode/product text, browser print preview, Code128 barcode rendering, and TSPL command export.

## Page

`Inventory → Price Tags`

Route:

```text
/price-tags
```

## Printer workflow

### Desktop USB thermal printer

1. Install the printer driver in Windows/Linux/macOS.
2. Open `Inventory → Price Tags`.
3. Select `Browser / driver print`.
4. Print from browser dialog to the USB printer.
5. In printer/page setup, select the correct label size: 50×30 mm or 50×25 mm.

### Mobile Bluetooth thermal printers

Normal web apps cannot reliably print directly to Bluetooth printers across all phones/printer brands. This stage therefore exports TSPL commands.

Recommended workflow:

1. Open `Inventory → Price Tags`.
2. Prepare tags.
3. Click `Download TSPL`.
4. Send the `.tspl.txt` command file through the printer vendor mobile app or a local bridge app.

A future local print bridge can consume the same TSPL command output and send it directly to paired Bluetooth/USB printers.

## Backend endpoints

```text
GET  /api/price-tags/search
GET  /api/price-tags/purchase-inwards
GET  /api/price-tags/purchase-inwards/{id}/items
POST /api/price-tags/prepare
```

## Notes

- Browser print uses SVG Code128 barcodes, so scanner readability depends on printer DPI, browser scaling, and label media quality.
- Use 100% scaling and no margins where possible.
- TSPL commands use `BARCODE ... "128"`, which works on many TSC/TSPL-compatible thermal label printers. Some Bluetooth printers may use ESC/POS, CPCL, ZPL, or vendor-specific command formats; those need bridge/vendor app support.
