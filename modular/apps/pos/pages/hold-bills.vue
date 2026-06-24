<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-4">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Paused counter work</p>
          <h2 class="mt-1 text-2xl font-semibold">Hold Bills</h2>
          <p class="mt-2 max-w-2xl text-sm text-muted">
            Resume parked sale drafts from this POS browser without sending them to accounting until Save & Print.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" @click="loadHeldBills">Refresh</UButton>
          <UButton color="neutral" variant="ghost" icon="i-lucide-trash-2" :disabled="!heldBills.length" @click="clearAll">Clear all</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 border border-default bg-muted/10 p-4 lg:grid-cols-[1fr_auto]">
      <UFormField label="Search held bill" name="search">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Customer, mobile, item, or note" autofocus />
      </UFormField>
      <div class="flex items-end">
        <UButton to="/sale" icon="i-lucide-scan-barcode">New Sale</UButton>
      </div>
    </div>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      <article
        v-for="bill in filteredHeldBills"
        :key="bill.id"
        class="border border-default bg-muted/10 p-4"
      >
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0">
            <h3 class="truncate font-semibold">{{ bill.customerName || 'Walk-in Customer' }}</h3>
            <p class="truncate text-sm text-muted">{{ bill.customerMobileNumber || 'No mobile' }}</p>
          </div>
          <UBadge color="warning" variant="soft">{{ bill.itemCount }} items</UBadge>
        </div>
        <dl class="mt-4 space-y-2 text-sm">
          <div class="flex justify-between gap-3"><dt class="text-muted">Held at</dt><dd class="text-right">{{ formatDateTime(bill.heldAt) }}</dd></div>
          <div class="flex justify-between gap-3"><dt class="text-muted">Quantity</dt><dd>{{ bill.quantity }}</dd></div>
          <div class="flex justify-between gap-3 text-base font-semibold"><dt>Total</dt><dd>{{ money(bill.payableTotal) }}</dd></div>
          <div v-if="bill.note" class="border-t border-default pt-2"><dt class="text-muted">Note</dt><dd>{{ bill.note }}</dd></div>
        </dl>
        <div class="mt-4 flex flex-wrap gap-2">
          <UButton size="sm" icon="i-lucide-play" @click="resumeBill(bill)">Resume</UButton>
          <UButton size="sm" color="error" variant="ghost" icon="i-lucide-trash-2" @click="removeBill(bill.id)">Remove</UButton>
        </div>
      </article>
    </section>

    <div v-if="!filteredHeldBills.length" class="border border-dashed border-default p-8 text-center text-muted">
      No held bills found.
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  clearHeldBills,
  readHeldBills,
  removeHeldBill,
  writeHeldBills,
  writeSaleDraft,
  type PosHeldBill
} from '../utils/local-pos-storage'

useHead({ title: 'Hold Bills - Garmetix POS' })

const search = ref('')
const heldBills = ref<PosHeldBill[]>([])
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')

const filteredHeldBills = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return heldBills.value
  return heldBills.value.filter((bill) => [
    bill.customerName,
    bill.customerMobileNumber,
    bill.note,
    JSON.stringify(bill.draft)
  ].some(value => String(value || '').toLowerCase().includes(term)))
})

function money(value: number | string | null | undefined) {
  return formatIndianMoney(value)
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(date)
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
}

function loadHeldBills() {
  if (!import.meta.client) return
  heldBills.value = readHeldBills()
}

function saveHeldBills() {
  if (!import.meta.client) return
  writeHeldBills(heldBills.value)
}

async function resumeBill(bill: PosHeldBill) {
  if (!import.meta.client) return
  writeSaleDraft(bill.draft || {})
  heldBills.value = heldBills.value.filter(item => item.id !== bill.id)
  saveHeldBills()
  showMessage('success', 'Held bill restored to sale screen.')
  await navigateTo('/sale')
}

function removeBill(id: string) {
  heldBills.value = heldBills.value.filter(item => item.id !== id)
  removeHeldBill(id)
  showMessage('neutral', 'Held bill removed.')
}

function clearAll() {
  heldBills.value = []
  clearHeldBills()
  showMessage('neutral', 'All held bills cleared from this POS browser.')
}

onMounted(loadHeldBills)
</script>
