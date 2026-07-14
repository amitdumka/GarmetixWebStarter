<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const rows = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    rows.value = await api.get<any[]>('digital-bills/feedback')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check API service and database migration.', 'Feedback load failed')
  } finally {
    loading.value = false
  }
}
function date(value: string) { return value ? new Date(value).toLocaleString('en-IN') : '-' }
onMounted(refresh)
</script>

<template>
  <AppShell title="Customer Feedback" @refresh="refresh">
    <UiModulePageHeader title="Customer Feedback" description="Feedback submitted from public digital invoice pages." icon="i-lucide-message-square-text" />
    <UiRegisterPanel class="mt-4" title="Feedback register" :description="`${rows.length} feedback rows`" :loading="loading" :error="loadError" :empty="rows.length === 0" empty-title="No feedback yet" empty-description="Customer feedback submitted from invoice links will appear here." empty-icon="i-lucide-message-square-text" @retry="refresh">
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Date</th><th>Invoice</th><th>Customer</th><th>Mobile</th><th class="text-right">Rating</th><th>Message</th></tr></thead>
          <tbody>
            <tr v-for="row in rows" :key="row.id"><td>{{ date(row.submittedAt) }}</td><td>{{ row.invoiceNumber }}</td><td>{{ row.customerName }}</td><td>{{ row.customerMobile }}</td><td class="text-right">{{ row.rating }}/5</td><td>{{ row.message || '-' }}</td></tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>
