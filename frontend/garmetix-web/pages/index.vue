<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const saving = ref(false)
const program = reactive({ enabled: true, name: 'Garmetix Loyalty', earnPointsPerRupee: 0.01, redeemValuePerPoint: 1, minimumBillAmount: 0, expiryDays: null as number | null })

async function refresh() {
  if (!auth.isAuthenticated.value || !workspace.storeId.value) return
  try {
    const data = await api.get<any>(`loyalty/program?storeId=${workspace.storeId.value}`)
    if (data) Object.assign(program, data)
  } catch (error) {
    feedback.failed('Could not load loyalty program', error)
  }
}

async function save() {
  saving.value = true
  try {
    await api.create<any>('loyalty/program', {
      companyId: workspace.companyId.value,
      storeGroupId: workspace.storeGroupId.value,
      storeId: workspace.storeId.value,
      enabled: program.enabled,
      name: program.name,
      earnPointsPerRupee: Number(program.earnPointsPerRupee || 0),
      redeemValuePerPoint: Number(program.redeemValuePerPoint || 0),
      minimumBillAmount: Number(program.minimumBillAmount || 0),
      expiryDays: program.expiryDays ? Number(program.expiryDays) : null
    })
    feedback.saved('Loyalty program saved')
    await refresh()
  } catch (error) {
    feedback.failed('Could not save loyalty program', error)
  } finally {
    saving.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Loyalty Program" @refresh="refresh" @workspace-change="refresh">
    <UCard>
      <template #header><strong>Store Loyalty Rule</strong></template>
      <UAlert color="primary" variant="subtle" title="Automatic earn" description="When enabled, paid sales invoices earn points for the customer. Sales returns reverse proportional earned points." />
      <div class="form-two-column mt-4">
        <UFormField label="Enabled"><USwitch v-model="program.enabled" /></UFormField>
        <UFormField label="Program name"><UInput v-model="program.name" /></UFormField>
      </div>
      <div class="form-three-column">
        <UFormField label="Points per rupee"><UInput v-model="program.earnPointsPerRupee" type="number" step="0.01" /></UFormField>
        <UFormField label="Redeem value per point"><UInput v-model="program.redeemValuePerPoint" type="number" step="0.01" /></UFormField>
        <UFormField label="Minimum bill"><UInput v-model="program.minimumBillAmount" type="number" step="0.01" /></UFormField>
      </div>
      <UFormField label="Expiry days"><UInput v-model="program.expiryDays" type="number" placeholder="Optional" /></UFormField>
      <UButton label="Save Loyalty Program" icon="i-lucide-gift" :loading="saving" @click="save" />
    </UCard>
  </AppShell>
</template>
