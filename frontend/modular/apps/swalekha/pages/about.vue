<template>
  <section class="space-y-5">
    <div>
      <p class="garmetix-kicker"><UIcon name="i-lucide-info" class="size-4" /> Swalekha</p>
      <h1 class="garmetix-dashboard-title">About Swalekha</h1>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-book-heart" class="size-5 text-primary" />
          <p class="font-semibold text-highlighted">Swalekha - Personal &amp; Personal Finance</p>
        </div>
      </template>

      <div class="space-y-3 text-sm text-muted">
        <p>
          <span class="font-medium text-highlighted">Swalekha</span> (स्व-लेखा - "self" + "ledger/writing") is a
          completely private, Owner-only workspace for personal finance and life organization - bank accounts, loans,
          investments, insurance, contacts and person-to-person ledgers, travel and household expense sheets, and a
          personal organizer. It runs on its own isolated database, separate from every business app on this
          platform, and is reachable only by an Owner-type login.
        </p>
        <p>
          Every Owner's data is private to them - two Owner logins on the same company never see each other's
          records unless they explicitly link up as family and confirm it from both sides.
        </p>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <p class="font-semibold text-highlighted">Developed By</p>
      </template>

      <div class="flex items-start gap-4">
        <div class="flex size-14 shrink-0 items-center justify-center rounded-lg bg-primary/10">
          <UIcon name="i-lucide-code-2" class="size-7 text-primary" />
        </div>
        <div class="space-y-1">
          <p class="text-base font-semibold text-highlighted">AKS Labs (India)</p>
          <p class="text-sm text-muted">Amit Kumar - Founder &amp; Developer</p>
          <p class="text-sm text-muted">
            AKS Labs (India) builds the Garmetix retail platform and Swalekha, its personal finance module,
            designed and developed independently as a companion tool for personal record-keeping alongside the
            main business software.
          </p>
        </div>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <p class="font-semibold text-highlighted">Version</p>
      </template>
      <p class="text-sm text-muted">
        Swalekha is built and versioned alongside the rest of the Garmetix modular platform. This is an actively
        evolving personal project - new stages are added over time.
      </p>
    </UCard>

    <UCard>
      <template #header>
        <div class="flex flex-wrap items-center justify-between gap-2">
          <p class="font-semibold text-highlighted">Data Isolation Self-Check</p>
          <UButton icon="i-lucide-shield-check" size="sm" :loading="checking" @click="runSelfCheck">Run Self-Check</UButton>
        </div>
      </template>

      <div class="space-y-3">
        <p class="text-sm text-muted">
          A live, runtime check confirming this Owner's data is genuinely isolated from every other Owner - not a
          static claim, an assertion run against the database right now.
        </p>

        <UAlert
          v-if="selfCheckError"
          icon="i-lucide-circle-alert"
          color="error"
          variant="subtle"
          :description="selfCheckError"
        />

        <div v-if="selfCheck" class="space-y-2">
          <UAlert
            :icon="selfCheck.allPassed ? 'i-lucide-circle-check' : 'i-lucide-circle-alert'"
            :color="selfCheck.allPassed ? 'success' : 'error'"
            variant="subtle"
            :title="selfCheck.allPassed ? 'All checks passed' : 'One or more checks failed'"
          />
          <div v-for="check in selfCheck.checks" :key="check.name" class="flex items-start gap-3 rounded-lg border border-default p-3">
            <UIcon
              :name="check.passed ? 'i-lucide-check-circle-2' : 'i-lucide-x-circle'"
              :class="check.passed ? 'text-success' : 'text-error'"
              class="mt-0.5 size-5 shrink-0"
            />
            <div class="min-w-0">
              <p class="text-sm font-medium text-highlighted">{{ check.name }}</p>
              <p class="text-xs text-muted">{{ check.detail }}</p>
            </div>
          </div>
        </div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaSelfCheck } from '../utils/swalekha-api'

useHead({ title: 'About - Swalekha' })

const api = useSwalekhaApiClient()
const checking = ref(false)
const selfCheck = ref<SwalekhaSelfCheck | null>(null)
const selfCheckError = ref('')

async function runSelfCheck() {
  checking.value = true
  selfCheckError.value = ''
  try {
    selfCheck.value = await api.get<SwalekhaSelfCheck>('security/self-check')
  } catch (err) {
    selfCheckError.value = err instanceof Error ? err.message : 'Could not run the self-check.'
  } finally {
    checking.value = false
  }
}
</script>
