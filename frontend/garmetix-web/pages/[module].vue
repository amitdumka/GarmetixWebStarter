<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const route = useRoute()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])

const titleMap: Record<string, string> = {
  inventory: 'Inventory',
  vouchers: 'Vouchers',
  accounting: 'Accounting',
  'petty-cash': 'Petty Cash',
  hr: 'HR',
  payroll: 'Payroll',
  reports: 'Reports',
  audit: 'Audit',
  access: 'Access',
  'import-export': 'Import Export',
  setup: 'Company Setup'
}

const moduleKey = computed(() => String(route.params.module || ''))
const title = computed(() => titleMap[moduleKey.value] || 'Module')

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  const [companyRows, storeRows] = await Promise.all([
    api.list<any>('companies'),
    api.list<any>('stores')
  ])

  companies.value = companyRows
  stores.value = storeRows
}

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    :title="title"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">{{ title }}</h2>
          <span class="status warn">Next</span>
        </div>
        <div class="empty-state">
          <p>This module is ready for its own list and form page.</p>
        </div>
      </section>
    </section>
  </AppShell>
</template>
