<script setup lang="ts">
const auth = useAuth()
const api = useGarmetixApi()
const feedback = useUiFeedback()

const loading = ref(false)
const savingProfile = ref(false)
const changingPassword = ref(false)
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const profileMessage = ref('')
const profileError = ref('')
const passwordMessage = ref('')
const passwordError = ref('')
const loadError = ref('')

useHead({ title: 'My Profile | Garmetix' })

const profileForm = reactive({
  name: '',
  userName: '',
  email: ''
})

const passwordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const companyName = computed(() => companies.value.find((item) => item.id === auth.user.value?.companyId)?.name || 'All / not assigned')
const storeName = computed(() => stores.value.find((item) => item.id === auth.user.value?.storeId)?.name || 'All / not assigned')

function loadFormFromUser() {
  profileForm.name = auth.user.value?.name || ''
  profileForm.userName = auth.user.value?.userName || ''
  profileForm.email = auth.user.value?.email || ''
}

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    auth.restore()
    await auth.me()
    loadFormFromUser()
    const [companyRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Your account details could not be loaded. Try again.', 'Profile load failed')
  } finally {
    loading.value = false
  }
}

async function saveProfile() {
  profileMessage.value = ''
  profileError.value = ''
  savingProfile.value = true
  try {
    await auth.updateProfile(profileForm.name, profileForm.userName, profileForm.email)
    profileMessage.value = 'Profile updated successfully.'
    feedback.saved('Profile')
  } catch (error: any) {
    profileError.value = feedback.errorMessage(error, 'Could not update profile.', 'Profile update failed')
  } finally {
    savingProfile.value = false
  }
}

async function changeCurrentPassword() {
  passwordMessage.value = ''
  passwordError.value = ''

  if (passwordForm.newPassword !== passwordForm.confirmPassword) {
    passwordError.value = 'New password and confirm password do not match.'
    return
  }

  changingPassword.value = true
  try {
    const response = await auth.changePassword(passwordForm.currentPassword, passwordForm.newPassword)
    passwordMessage.value = response.message
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
    feedback.saved('Password')
  } catch (error: any) {
    passwordError.value = feedback.errorMessage(error, 'Could not change password.', 'Password change failed')
  } finally {
    changingPassword.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="My Profile" :companies="companies" :stores="stores" @refresh="refresh">
    <div class="space-y-6">
      <UiModulePageHeader
        title="My Profile"
        description="View your account details and update your own profile/password. Role, permissions and workspace assignment are read-only here and can only be changed by an admin."
        icon="i-lucide-user-cog"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ready' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Your profile is unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
      />

      <div v-if="loading && !auth.user.value" class="profile-detail-grid">
        <USkeleton v-for="row in 6" :key="row" class="h-24 w-full" />
      </div>

      <div v-else class="profile-detail-grid">
        <div class="profile-detail-card">
          <span>Name</span>
          <strong>{{ auth.user.value?.name || '-' }}</strong>
        </div>
        <div class="profile-detail-card">
          <span>Username</span>
          <strong>{{ auth.user.value?.userName || '-' }}</strong>
        </div>
        <div class="profile-detail-card">
          <span>Email</span>
          <strong>{{ auth.user.value?.email || '-' }}</strong>
        </div>
        <div class="profile-detail-card">
          <span>Role</span>
          <strong>{{ auth.user.value?.role || '-' }}</strong>
        </div>
        <div class="profile-detail-card">
          <span>User type</span>
          <strong>{{ auth.user.value?.userType || '-' }}</strong>
        </div>
        <div class="profile-detail-card">
          <span>Workspace</span>
          <strong>{{ companyName }} / {{ storeName }}</strong>
        </div>
      </div>

      <div class="grid gap-6 xl:grid-cols-2">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-id-card" class="h-5 w-5" />
              <h2 class="font-semibold">Editable profile</h2>
            </div>
          </template>

          <form class="form-grid" @submit.prevent="saveProfile">
            <UFormField label="Display name">
              <UInput v-model="profileForm.name" icon="i-lucide-user" required />
            </UFormField>
            <UFormField label="Username">
              <UInput v-model="profileForm.userName" icon="i-lucide-user-round" autocomplete="username" required />
            </UFormField>
            <UFormField label="Email">
              <UInput v-model="profileForm.email" icon="i-lucide-mail" autocomplete="email" type="email" required />
            </UFormField>

            <UAlert v-if="profileMessage" color="success" variant="subtle" icon="i-lucide-check-circle" :description="profileMessage" />
            <UAlert v-if="profileError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="profileError" />

            <div class="modal-actions">
              <UButton type="submit" icon="i-lucide-save" label="Save Profile" :loading="savingProfile" />
            </div>
          </form>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-key-round" class="h-5 w-5" />
              <h2 class="font-semibold">Change password</h2>
            </div>
          </template>

          <form class="form-grid" @submit.prevent="changeCurrentPassword">
            <UFormField label="Current password">
              <UInput v-model="passwordForm.currentPassword" icon="i-lucide-lock-keyhole" type="password" autocomplete="current-password" required />
            </UFormField>
            <UFormField label="New password">
              <UInput v-model="passwordForm.newPassword" icon="i-lucide-lock" type="password" autocomplete="new-password" required />
            </UFormField>
            <UFormField label="Confirm new password">
              <UInput v-model="passwordForm.confirmPassword" icon="i-lucide-lock" type="password" autocomplete="new-password" required />
            </UFormField>

            <UAlert v-if="passwordMessage" color="success" variant="subtle" icon="i-lucide-check-circle" :description="passwordMessage" />
            <UAlert v-if="passwordError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="passwordError" />

            <div class="modal-actions">
              <UButton type="submit" icon="i-lucide-key-round" label="Change Password" :loading="changingPassword" />
            </div>
          </form>
        </UCard>
      </div>

      <UAlert
        color="neutral"
        variant="subtle"
        icon="i-lucide-shield-check"
        title="Security policy"
        description="Role, permission, admin access, company, store group and store assignment are read-only here. Admin users can manage them from Admin > Roles & Users."
      />
    </div>
  </AppShell>
</template>
