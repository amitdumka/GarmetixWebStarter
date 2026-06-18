<script setup lang="ts">
const auth = useAuth()
const api = useGarmetixApi()
const feedback = useUiFeedback()

const loading = ref(true)
const target = ref('/dashboard/store-manager')
const reason = ref('Opening your permitted dashboard…')

async function resolveDashboard() {
  loading.value = true
  try {
    const home = await api.get<any>('dashboard/home')
    target.value = home?.route || chooseLocalDashboard()
    reason.value = home?.reason || 'Dashboard selected from your role and workspace.'
    await navigateTo(target.value, { replace: true })
  } catch (error) {
    target.value = chooseLocalDashboard()
    reason.value = 'Dashboard selected locally because server route selection was unavailable.'
    feedback.failed('Dashboard route selection failed', error)
    await navigateTo(target.value, { replace: true })
  } finally {
    loading.value = false
  }
}

function chooseLocalDashboard() {
  auth.restore()
  const role = `${auth.user.value?.role || ''} ${auth.user.value?.userType || ''}`.toLowerCase()
  if (role.includes('payroll')) return '/payroll'
  if (role.includes('hr')) return '/hr'
  return auth.canSeeAdmin.value || role.includes('accountant') ? '/dashboard/business' : '/dashboard/store-manager'
}

onMounted(resolveDashboard)
</script>

<template>
  <div class="dashboard-v3-page">
    <UCard>
      <div class="dashboard-v3-redirect">
        <UIcon name="i-lucide-gauge" class="h-8 w-8" />
        <div>
          <h1>Opening dashboard</h1>
          <p>{{ reason }}</p>
          <UButton :to="target" :loading="loading" label="Open now" icon="i-lucide-arrow-right" />
        </div>
      </div>
    </UCard>
  </div>
</template>
