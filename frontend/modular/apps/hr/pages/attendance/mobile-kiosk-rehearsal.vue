<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-smartphone-nfc" class="size-4" /> Kiosk rehearsal</p>
          <h2 class="garmetix-dashboard-title">Mobile Kiosk Rehearsal</h2>
          <p class="garmetix-dashboard-subtitle">{{ readText(rehearsal, ['goal'], 'Physical tablet acceptance checklist.') }}</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Prerequisites</h3>
            <p class="garmetix-panel-subtitle">Required before physical tablet testing.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(rehearsal, ['prerequisites'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-circle-check" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>

      <div class="grid gap-4">
        <div v-for="phase in phases" :key="phase.name" class="garmetix-table-panel">
          <div class="garmetix-panel-header">
            <div>
              <h3 class="garmetix-panel-title">{{ phase.name }}</h3>
              <p class="garmetix-panel-subtitle">Checks and evidence to capture.</p>
            </div>
          </div>
          <div class="grid gap-4 md:grid-cols-2">
            <div>
              <h4 class="mb-2 text-sm font-semibold">Checks</h4>
              <ul class="space-y-2 text-sm text-muted">
                <li v-for="item in phase.checks" :key="item" class="flex gap-2">
                  <UIcon name="i-lucide-list-checks" class="mt-0.5 size-4 shrink-0 text-primary" />
                  <span>{{ item }}</span>
                </li>
              </ul>
            </div>
            <div>
              <h4 class="mb-2 text-sm font-semibold">Evidence</h4>
              <ul class="space-y-2 text-sm text-muted">
                <li v-for="item in phase.evidence" :key="item" class="flex gap-2">
                  <UIcon name="i-lucide-camera" class="mt-0.5 size-4 shrink-0 text-success" />
                  <span>{{ item }}</span>
                </li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="grid gap-4 lg:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Pass Criteria</h3>
            <p class="garmetix-panel-subtitle">All items must pass before production use.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(rehearsal, ['passCriteria'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-badge-check" class="mt-0.5 size-4 shrink-0 text-success" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Blockers</h3>
            <p class="garmetix-panel-subtitle">Resolve before go-live.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(rehearsal, ['blockers'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-ban" class="mt-0.5 size-4 shrink-0 text-error" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Mobile Kiosk Rehearsal - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const rehearsal = ref<ApiRecord | null>(null)
const phases = computed(() => readArray(rehearsal.value, ['phases']).map(phase => ({
  name: readText(phase, ['name']),
  checks: textList(phase, ['checks']),
  evidence: textList(phase, ['evidence'])
})))

function textList(source: ApiRecord | null | undefined, keys: string[]) {
  return readArray(source, keys).map(item => typeof item === 'string' ? item : JSON.stringify(item))
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    rehearsal.value = await get<ApiRecord>('api/attendance/mobile-kiosk/rehearsal')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load kiosk rehearsal checklist.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
