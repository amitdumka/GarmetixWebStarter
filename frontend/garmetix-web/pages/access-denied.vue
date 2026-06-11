<script setup lang="ts">
definePageMeta({ layout: false })

const route = useRoute()
const auth = useAuth()
const access = useAccessControl()

const attemptedPath = computed(() => String(route.query.path || '/dashboard'))
const reason = computed(() => String(route.query.reason || 'You do not have access to this page.'))
const rule = computed(() => access.getPathRule(attemptedPath.value))
const homePath = computed(() => {
  const roles = access.userRoles.value
  return roles.includes('admin') || roles.includes('owner') || roles.includes('accountant') || roles.includes('remoteAccountant')
    ? '/dashboard/business'
    : '/dashboard/store-manager'
})
</script>

<template>
  <div class="min-h-screen bg-default text-default flex items-center justify-center p-6">
    <UCard class="max-w-xl w-full">
      <template #header>
        <div class="flex items-center gap-3">
          <UAvatar icon="i-lucide-shield-alert" color="warning" />
          <div>
            <h1 class="text-xl font-semibold">Access denied</h1>
            <p class="text-sm text-muted">Your account is signed in but this page is not enabled for your role.</p>
          </div>
        </div>
      </template>

      <div class="space-y-4">
        <UAlert color="warning" variant="subtle" icon="i-lucide-lock-keyhole" :title="reason" />
        <div class="rounded-lg border border-default p-4 text-sm space-y-2">
          <p><strong>Requested page:</strong> {{ attemptedPath }}</p>
          <p><strong>Your role:</strong> {{ auth.user.value?.role || auth.user.value?.userType || 'User' }}</p>
          <p v-if="rule"><strong>Allowed roles:</strong> {{ rule.roles.join(', ') }}</p>
        </div>
      </div>

      <template #footer>
        <div class="flex flex-wrap gap-2 justify-end">
          <UButton color="neutral" variant="outline" icon="i-lucide-user-cog" label="My Profile" to="/profile" />
          <UButton icon="i-lucide-gauge" label="Go to dashboard" :to="homePath" />
        </div>
      </template>
    </UCard>
  </div>
</template>
