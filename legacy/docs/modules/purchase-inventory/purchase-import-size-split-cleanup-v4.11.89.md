# v4.11.89 Purchase Import Size Split Cleanup

Adds shop-floor review tools for supplier invoice import lines where one scanned row represents multiple pieces in different sizes or colors.

## Features

- Split one imported invoice line into quantity-wise product draft lines.
- Enter one size/color label per piece, for example `38`, `40`, `Blue-M`, `Blue-L`.
- Split lines become quantity `1` rows with product names suffixed by the size/color label.
- Split lines can be matched with existing products or posted as new products with generated barcodes.
- Product match search now scores similar names using tokens instead of only exact full-text contains.
- Unposted/failed scanned invoice drafts can be deleted from history with stored proof/audit files.
- Posted imports remain protected because they are purchase proof.

## Why

Some supplier invoices, especially apparel invoices, show one row with quantity greater than one, while the actual physical stock must be tracked size-wise or color-wise. This review tool lets the operator split that row before posting stock inward.
