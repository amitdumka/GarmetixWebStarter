<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker"><UIcon name="i-lucide-clipboard-check" class="size-4" /> For your accountant</p>
          <h2 class="garmetix-dashboard-title">Accountant Review</h2>
          <p class="garmetix-dashboard-subtitle">
            Items in the books and customer master that look wrong or need a decision. Run the checks, tick what has been reviewed, then print, export or send the list to your accountant.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UBadge :color="openCount ? 'warning' : 'success'" variant="subtle">{{ openCount }} open of {{ findings.length }}</UBadge>
          <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="runChecks">Run Checks</UButton>
          <UButton icon="i-lucide-file-down" color="neutral" variant="soft" :disabled="!findings.length" @click="exportCsv">CSV</UButton>
          <UButton icon="i-lucide-printer" color="neutral" variant="soft" :disabled="!findings.length" @click="printList">Print</UButton>
          <UButton icon="i-lucide-send" color="neutral" variant="soft" :disabled="!findings.length" @click="shareOpen = true">Send To Accountant</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[900px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Reviewed</th>
            <th class="border-b border-default p-3">Severity</th>
            <th class="border-b border-default p-3">Area</th>
            <th class="border-b border-default p-3">Finding</th>
            <th class="border-b border-default p-3">Suggested action</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="5" class="p-8 text-center text-muted">Running checks...</td></tr>
          <tr v-else-if="!findings.length"><td colspan="5" class="p-8 text-center text-muted">No findings. Press Run Checks.</td></tr>
          <template v-else>
            <tr v-for="item in findings" :key="item.id" :class="reviewed[item.id] ? 'opacity-60' : ''">
              <td class="border-b border-default p-3"><UCheckbox :model-value="Boolean(reviewed[item.id])" @update:model-value="setReviewed(item.id, Boolean($event))" /></td>
              <td class="border-b border-default p-3"><UBadge :color="item.severity === 'High' ? 'error' : item.severity === 'Medium' ? 'warning' : 'neutral'" variant="subtle">{{ item.severity }}</UBadge></td>
              <td class="border-b border-default p-3">{{ item.area }}</td>
              <td class="border-b border-default p-3">
                <p class="font-semibold text-highlighted">{{ item.title }}</p>
                <p class="text-xs text-muted">{{ item.detail }}</p>
              </td>
              <td class="border-b border-default p-3 text-muted">{{ item.action }}</td>
            </tr>
          </template>
        </tbody>
      </table>
    </section>
    <p class="text-xs text-muted">Reviewed ticks are saved in this browser only. They are a working aid, not an audit record.</p>

    <UModal v-model:open="shareOpen" title="Send To Accountant">
      <template #body>
        <div class="grid gap-3">
          <UFormField label="Accountant email"><UInput v-model="share.toEmail" type="email" class="w-full" /></UFormField>
          <UFormField label="Accountant name"><UInput v-model="share.toName" class="w-full" /></UFormField>
          <UFormField label="WhatsApp number (with country code)"><UInput v-model="share.whatsAppNumber" class="w-full" /></UFormField>
          <UFormField label="Message"><UTextarea v-model="share.note" :rows="3" class="w-full" /></UFormField>
          <p class="text-xs text-muted">This opens your email app or WhatsApp with the open findings filled in. Nothing is sent from the server.</p>
          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-mail" :disabled="!share.toEmail" @click="sendEmail">Open Email</UButton>
            <UButton icon="i-lucide-message-circle" color="neutral" variant="soft" :disabled="!share.whatsAppNumber" @click="sendWhatsApp">Open WhatsApp</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'
import { useGstReviewContact } from '../composables/useGstReviewContact'

useHead({ title: 'Accountant Review - Garmetix Books' })

type Finding = { id: string, severity: 'High' | 'Medium' | 'Low', area: string, title: string, detail: string, action: string }

const REVIEWED_KEY = 'garmetix:accountant-review-ticks:v1'
const { get } = useBooksApiClient()
const toast = useToast()
const contact = useGstReviewContact()
const findings = ref<Finding[]>([])
const reviewed = ref<Record<string, boolean>>({})
const loading = ref(false)
const error = ref('')
const shareOpen = ref(false)
const share = reactive({ toEmail: '', toName: '', whatsAppNumber: '', note: 'Please review these items found in the books and advise whether any correction is needed.' })

const openCount = computed(() => findings.value.filter(item => !reviewed.value[item.id]).length)

function money(value: number) {
  return formatIndianMoney(value)
}

function setReviewed(id: string, value: boolean) {
  reviewed.value = { ...reviewed.value, [id]: value }
  try { window.localStorage.setItem(REVIEWED_KEY, JSON.stringify(reviewed.value)) } catch { /* ignore */ }
}

async function runChecks() {
  loading.value = true
  error.value = ''
  const out: Finding[] = []
  try {
    const [ledgers, customers, validation] = await Promise.all([
      get<unknown>('ledgers').then(toRows),
      get<unknown>('customers').then(toRows),
      get<ApiRecord>('accounting/journal/validation').catch(() => null)
    ])

    // 1. Customer Store Credit ledger vs what customers actually hold.
    const storeCredit = ledgers.find(item => readText(item, ['name'], '').toLowerCase() === 'customer store credit')
    const held = customers.reduce((sum, item) => sum + readNumber(item, ['creditBalance']), 0)
    if (storeCredit) {
      const statement = toRows(await get<unknown>(`accounting/ledger-statement/${readText(storeCredit, ['id'])}`))
      const last = statement[statement.length - 1]
      const balance = last ? readNumber(last, ['balance']) : 0
      const side = last ? readText(last, ['balanceType'], 'Dr') : 'Dr'
      const signed = side === 'Cr' ? balance : -balance
      if (Math.abs(signed - held) > 0.5) {
        out.push({
          id: 'store-credit-balance',
          severity: side === 'Dr' ? 'High' : 'Medium',
          area: 'Customer Store Credit',
          title: `Ledger shows ${money(balance)} ${side}, customers hold ${money(held)}`,
          detail: 'A liability ledger should be a credit that equals the credit customers can still use. The old Vyapar credit-note import posted redemptions without matching credit entries, so the ledger sits on the debit side.',
          action: 'Decide whether the imported credit notes were real liabilities or already settled, then post a correcting entry (Admin: Billing store-credit-correction) with the reason.'
        })
      }
    }

    // 2. Journal validation issues.
    for (const issue of validation ? readArray(validation, ['issues']) : []) {
      const rec = issue as ApiRecord
      out.push({
        id: `journal-${readText(rec, ['journalEntryId'], readText(rec, ['entryNumber']))}`,
        severity: readText(rec, ['severity'], 'Medium') === 'Error' ? 'High' : 'Medium',
        area: 'Journal',
        title: `${readText(rec, ['entryNumber'])}: ${readText(rec, ['message'])}`,
        detail: `${readText(rec, ['sourceType'])} ${readText(rec, ['referenceNumber'], '')} dated ${readText(rec, ['onDate'], '').slice(0, 10)}. Debit ${money(readNumber(rec, ['debit']))}, credit ${money(readNumber(rec, ['credit']))}.`,
        action: 'Re-post the source document from its screen, or confirm it is a cancelled document that should have no lines.'
      })
    }

    // 3. Customers with a mobile that is not 10 digits.
    for (const row of customers) {
      const mobile = readText(row, ['mobileNumber'], '')
      if (mobile && mobile !== '0000000000' && !/^\d{10}$/.test(mobile)) {
        out.push({ id: `mobile-${readText(row, ['id'])}`, severity: 'Low', area: 'Customers', title: `Invalid mobile for ${readText(row, ['name'])}: ${mobile}`, detail: 'Mobile numbers should be exactly 10 digits.', action: 'Correct the number on the customer record.' })
      }
    }

    // 4. Walk-in style names that carry a real mobile.
    for (const row of customers) {
      const name = readText(row, ['name'], '').toLowerCase().replace(/[^a-z]/g, '')
      const mobile = readText(row, ['mobileNumber'], '')
      if (name.startsWith('walkin') && mobile !== '0000000000') {
        out.push({ id: `walkin-${readText(row, ['id'])}`, severity: 'Low', area: 'Customers', title: `"${readText(row, ['name'])}" has a real mobile (${mobile})`, detail: 'Named walk-in but linked to a real number, so it is not the shared Walk-in Customer.', action: 'Rename to the customer\'s real name (keeps the number).' })
      }
    }

    // 5. Duplicate ledger names.
    const byName = new Map<string, ApiRecord[]>()
    for (const row of ledgers) {
      const key = readText(row, ['name'], '').trim().toLowerCase()
      if (key) byName.set(key, [...(byName.get(key) || []), row])
    }
    for (const [key, rows] of byName) {
      if (rows.length > 1) {
        out.push({ id: `dupledger-${key}`, severity: 'Low', area: 'Ledgers', title: `${rows.length} ledgers named "${readText(rows[0], ['name'])}"`, detail: 'Same name on more than one ledger; may be different parties or a duplicate.', action: 'Merge if they are the same party, otherwise make the names distinguishable.' })
      }
    }

    // 6. Test records left in production.
    for (const row of customers) {
      if (/^test\b/i.test(readText(row, ['name'], ''))) {
        out.push({ id: `test-${readText(row, ['id'])}`, severity: 'Low', area: 'Test data', title: `Test customer "${readText(row, ['name'])}" (${readText(row, ['mobileNumber'])})`, detail: 'Created while testing returns and exchanges. Its documents net to zero money and stock.', action: 'Keep as audit trail or merge/remove with the accountant\'s approval.' })
      }
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not run the checks.'
    toast.add({ title: 'Could not run checks', description: error.value, color: 'error' })
  } finally {
    const order = { High: 0, Medium: 1, Low: 2 }
    findings.value = out.sort((a, b) => order[a.severity] - order[b.severity])
    loading.value = false
  }
}

function plainText(openOnly: boolean) {
  const rows = findings.value.filter(item => !openOnly || !reviewed.value[item.id])
  return rows.map((item, index) => `${index + 1}. [${item.severity}] ${item.area} - ${item.title}\n   ${item.detail}\n   Action: ${item.action}`).join('\n\n')
}

function exportCsv() {
  const esc = (value: string) => `"${value.replace(/"/g, '""')}"`
  const lines = ['Reviewed,Severity,Area,Finding,Detail,Suggested action']
  for (const item of findings.value) lines.push([reviewed.value[item.id] ? 'Yes' : 'No', item.severity, item.area, item.title, item.detail, item.action].map(esc).join(','))
  const url = URL.createObjectURL(new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = `accountant-review-${new Date().toISOString().slice(0, 10)}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

function printList() {
  const win = window.open('', '_blank')
  if (!win) return
  const escape = (value: string) => value.replace(/&/g, '&amp;').replace(/</g, '&lt;')
  win.document.write(`<html><head><title>Accountant Review</title></head><body style="font-family:sans-serif"><h2>Accountant Review - ${new Date().toLocaleDateString('en-IN')}</h2><pre style="white-space:pre-wrap;font-size:13px">${escape(plainText(false))}</pre></body></html>`)
  win.document.close()
  win.print()
}

function sendEmail() {
  contact.save({ toEmail: share.toEmail, toName: share.toName, whatsAppNumber: share.whatsAppNumber, note: share.note })
  const body = `${share.note}\n\n${plainText(true)}`
  window.location.href = `mailto:${encodeURIComponent(share.toEmail)}?subject=${encodeURIComponent('Garmetix books - items for your review')}&body=${encodeURIComponent(body.slice(0, 1800))}`
  contact.addLog({ kind: 'Accountant Review', returnPeriod: new Date().toISOString().slice(0, 10), toEmail: share.toEmail, toName: share.toName, attachmentNames: [], message: 'Email opened' })
}

function sendWhatsApp() {
  contact.save({ toEmail: share.toEmail, toName: share.toName, whatsAppNumber: share.whatsAppNumber, note: share.note })
  const text = `${share.note}\n\n${plainText(true)}`
  window.open(`https://wa.me/${share.whatsAppNumber.replace(/\D/g, '')}?text=${encodeURIComponent(text.slice(0, 1500))}`, '_blank')
}

onMounted(() => {
  contact.load()
  share.toEmail = contact.contact.toEmail
  share.toName = contact.contact.toName
  share.whatsAppNumber = contact.contact.whatsAppNumber
  try { reviewed.value = JSON.parse(window.localStorage.getItem(REVIEWED_KEY) || '{}') } catch { reviewed.value = {} }
  runChecks()
})
</script>
