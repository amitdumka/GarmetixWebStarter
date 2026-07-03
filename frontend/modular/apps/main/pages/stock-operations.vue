<template>
  <MainReadOnlyTable
    title="Stock Operations"
    description="Stock operations route for transfers, adjustments and stock-count follow-up."
    endpoint="inventory/stock-reports/summary"
    table-title="Stock Operation Watch"
    :row-keys="['stockId', 'productId', 'barcode']"
    :title-keys="['productName']"
    :subtitle-keys="['reconciliationStatus', 'ageBucket', 'risk']"
    :summary="[
      { label: 'Mismatches', keys: ['reconciliationMismatchRows'], type: 'number', caption: 'Rows needing review' },
      { label: 'Pending Accounting', keys: ['pendingAccountingDocuments'], type: 'number', caption: 'Documents not posted' },
      { label: 'Aged 90+', keys: ['agedOver90DaysRows'], type: 'number', caption: 'Slow stock rows' },
      { label: 'Low Stock', keys: ['lowStockRows'], type: 'number', caption: 'Rows at risk' }
    ]"
    :columns="[
      { label: 'Risk', keys: ['risk'] },
      { label: 'Age', keys: ['ageDays'], type: 'number' },
      { label: 'Movements', keys: ['movementCount'], type: 'number' }
    ]"
  />
</template>

<script setup lang="ts">
useHead({ title: 'Stock Operations - Garmetix Back Office' })
</script>
