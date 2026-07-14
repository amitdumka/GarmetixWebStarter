<template>
  <MainReadOnlyTable
    title="Inventory"
    description="Inventory route for product, stock and store availability views."
    endpoint="inventory/stock-reports/summary"
    table-title="Stock Risk Rows"
    :row-keys="['stockId', 'productId', 'barcode']"
    :title-keys="['productName']"
    :subtitle-keys="['barcode', 'storeName', 'risk']"
    :summary="[
      { label: 'Stock Rows', keys: ['stockRows'], type: 'number', caption: 'On-book stock rows' },
      { label: 'Quantity', keys: ['totalQuantity'], type: 'number', caption: 'Ledger quantity' },
      { label: 'Value', keys: ['totalInventoryValue'], type: 'money', caption: 'Inventory valuation' },
      { label: 'Low Stock', keys: ['lowStockRows'], type: 'number', caption: 'Rows at risk' }
    ]"
    :columns="[
      { label: 'Store', keys: ['storeName'] },
      { label: 'Qty', keys: ['ledgerQuantity'], type: 'number' },
      { label: 'Value', keys: ['inventoryValue'], type: 'money' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Inventory - Garmetix Back Office' })
</script>
