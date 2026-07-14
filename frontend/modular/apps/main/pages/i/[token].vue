<template>
  <main class="min-h-screen bg-slate-950 text-slate-100">
    <div class="mx-auto grid max-w-5xl gap-4 px-4 py-5 sm:px-6 lg:px-8">
      <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

      <section v-if="loading" class="rounded-lg border border-slate-800 bg-slate-900/70 p-8 text-center">
        <UIcon name="i-lucide-loader-circle" class="mx-auto mb-3 size-8 animate-spin text-primary" />
        <p class="text-sm text-slate-300">Loading digital invoice...</p>
      </section>

      <template v-else-if="invoice">
        <PublicBanners :items="bannersBy('Header')" @open="openBanner" />

        <section class="rounded-lg border border-slate-800 bg-slate-900/80 p-5 shadow-xl shadow-black/20">
          <div class="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
            <div>
              <p class="text-xs uppercase tracking-wide text-primary">{{ readText(invoice, ['storeName'], 'Store') }}</p>
              <h1 class="mt-1 text-2xl font-semibold text-white">{{ readText(invoice, ['companyName'], 'Garmetix') }}</h1>
              <p class="mt-1 max-w-xl text-sm text-slate-400">{{ readText(invoice, ['companyAddress'], '') }}</p>
              <p class="mt-1 text-sm text-slate-400">
                {{ readText(invoice, ['companyPhone'], '') }}
                <span v-if="readText(invoice, ['companyGstin'], '')"> | GSTIN {{ readText(invoice, ['companyGstin'], '') }}</span>
              </p>
            </div>
            <div class="rounded-md border border-slate-700 bg-slate-950/70 p-4 text-left md:min-w-72 md:text-right">
              <p class="text-xs uppercase tracking-wide text-slate-500">Invoice</p>
              <p class="text-xl font-semibold text-white">{{ readText(invoice, ['invoiceNumber']) }}</p>
              <p class="text-sm text-slate-400">{{ formatDate(readText(invoice, ['invoiceDate'], '')) }}</p>
              <UBadge class="mt-2" color="success" variant="subtle">{{ readText(invoice, ['invoiceStatus'], 'Saved') }}</UBadge>
            </div>
          </div>

          <div class="mt-5 grid gap-3 md:grid-cols-3">
            <div class="rounded-md border border-slate-800 bg-slate-950/50 p-3">
              <p class="text-xs uppercase text-slate-500">Customer</p>
              <p class="font-semibold text-white">{{ readText(invoice, ['customerName'], 'Customer') }}</p>
              <p class="text-sm text-slate-400">{{ readText(invoice, ['customerMobileMasked'], '') }}</p>
            </div>
            <div class="rounded-md border border-slate-800 bg-slate-950/50 p-3">
              <p class="text-xs uppercase text-slate-500">Bill Amount</p>
              <p class="text-2xl font-semibold text-white">{{ money(readNumber(invoice, ['billAmount'])) }}</p>
            </div>
            <div class="rounded-md border border-slate-800 bg-slate-950/50 p-3">
              <p class="text-xs uppercase text-slate-500">Balance</p>
              <p class="text-2xl font-semibold" :class="readNumber(invoice, ['balanceAmount']) > 0 ? 'text-warning' : 'text-success'">
                {{ money(readNumber(invoice, ['balanceAmount'])) }}
              </p>
            </div>
          </div>

          <div class="mt-5 flex flex-wrap gap-2">
            <UButton icon="i-lucide-download" @click="openPdf">Download PDF</UButton>
            <UButton v-if="reviewEnabled" color="success" variant="soft" icon="i-lucide-star" @click="openReview">
              {{ readText(reviewSettings, ['reviewButtonText'], 'Share review') }}
            </UButton>
            <UButton v-if="whatsappEnabled" color="neutral" variant="soft" icon="i-lucide-message-circle" @click="openWhatsApp">WhatsApp Support</UButton>
            <UButton v-if="readText(invoice, ['goodsReturnPolicyUrl'], '')" color="neutral" variant="ghost" icon="i-lucide-rotate-ccw" @click="openUrl(readText(invoice, ['goodsReturnPolicyUrl'], ''))">Return Policy</UButton>
          </div>
        </section>

        <section class="rounded-lg border border-slate-800 bg-slate-900/80 p-5">
          <div class="mb-4 flex items-center justify-between gap-3">
            <div>
              <p class="text-xs uppercase tracking-wide text-slate-500">Items</p>
              <h2 class="text-lg font-semibold text-white">Purchased items</h2>
            </div>
            <UBadge color="neutral" variant="soft">{{ items.length }} line(s)</UBadge>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[760px] border-collapse text-sm">
              <thead class="bg-slate-950/70 text-left text-xs uppercase text-slate-500">
                <tr>
                  <th class="border-b border-slate-800 p-3">Item</th>
                  <th class="border-b border-slate-800 p-3">Barcode</th>
                  <th class="border-b border-slate-800 p-3 text-right">Qty</th>
                  <th class="border-b border-slate-800 p-3 text-right">MRP</th>
                  <th class="border-b border-slate-800 p-3 text-right">Discount</th>
                  <th class="border-b border-slate-800 p-3 text-right">Tax</th>
                  <th class="border-b border-slate-800 p-3 text-right">Amount</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="!items.length"><td colspan="7" class="p-6 text-center text-slate-400">No invoice items found.</td></tr>
                <tr v-for="item in items" :key="`${readText(item, ['barcode'], '')}-${readText(item, ['productName'], '')}`">
                  <td class="border-b border-slate-800 p-3">
                    <p class="font-medium text-white">{{ readText(item, ['productName'], 'Item') }}</p>
                    <p class="text-xs text-slate-500">{{ readText(item, ['hsnCode'], '') }} {{ readText(item, ['unit'], '') }}</p>
                  </td>
                  <td class="border-b border-slate-800 p-3 text-slate-300">{{ readText(item, ['barcode'], '') }}</td>
                  <td class="border-b border-slate-800 p-3 text-right">{{ number(readNumber(item, ['quantity'])) }}</td>
                  <td class="border-b border-slate-800 p-3 text-right">{{ money(readNumber(item, ['mrp'])) }}</td>
                  <td class="border-b border-slate-800 p-3 text-right">{{ money(readNumber(item, ['discountAmount'])) }}</td>
                  <td class="border-b border-slate-800 p-3 text-right">{{ money(readNumber(item, ['taxAmount'])) }}</td>
                  <td class="border-b border-slate-800 p-3 text-right font-semibold text-white">{{ money(readNumber(item, ['amount'])) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
          <PublicBanners :items="bannersBy('Bottom')" @open="openBanner" />

          <div class="rounded-lg border border-slate-800 bg-slate-900/80 p-5">
            <p class="mb-3 text-xs uppercase tracking-wide text-slate-500">Bill Summary</p>
            <div class="grid gap-2 text-sm">
              <SummaryLine label="MRP" :value="money(readNumber(invoice, ['mrp']))" />
              <SummaryLine label="Discount" :value="money(readNumber(invoice, ['discountAmount']))" />
              <SummaryLine label="Net" :value="money(readNumber(invoice, ['netAmount']))" />
              <SummaryLine label="Tax" :value="money(readNumber(invoice, ['taxAmount']))" />
              <SummaryLine label="Round off" :value="money(readNumber(invoice, ['roundOff']))" />
              <SummaryLine label="Paid" :value="money(readNumber(invoice, ['paidAmount']))" />
              <div class="mt-2 flex items-center justify-between border-t border-slate-800 pt-3 text-lg font-semibold">
                <span>Total</span>
                <span>{{ money(readNumber(invoice, ['billAmount'])) }}</span>
              </div>
            </div>
          </div>
        </section>

        <section v-if="privateFeedbackEnabled" class="rounded-lg border border-slate-800 bg-slate-900/80 p-5">
          <div class="mb-4">
            <p class="text-xs uppercase tracking-wide text-slate-500">Feedback</p>
            <h2 class="text-lg font-semibold text-white">{{ readText(reviewSettings, ['feedbackButtonText'], 'Share private feedback') }}</h2>
          </div>
          <div v-if="feedbackSubmitted" class="rounded-md border border-success/30 bg-success/10 p-4 text-sm text-success">
            Thank you. Your feedback has been submitted.
          </div>
          <div v-else class="grid gap-3">
            <div class="flex flex-wrap gap-2">
              <UButton v-for="score in [1, 2, 3, 4, 5]" :key="score" :color="feedback.rating === score ? 'primary' : 'neutral'" :variant="feedback.rating === score ? 'solid' : 'soft'" @click="feedback.rating = score">
                {{ score }} Star
              </UButton>
            </div>
            <UTextarea v-model="feedback.message" :rows="3" placeholder="Tell us what went well or what we should improve." />
            <div>
              <UButton icon="i-lucide-send" :loading="feedbackSaving" @click="submitFeedback">Submit Feedback</UButton>
            </div>
          </div>
        </section>

        <PublicBanners :items="bannersBy('Footer')" @open="openBanner" />
      </template>
    </div>
  </main>
</template>

<script setup lang="ts">
import { formatIndianMoney, stripServerUrl } from '@garmetix/shared-utils'
import { defineComponent, h, type PropType } from 'vue'
import { formatDate, readArray, readNumber, readText, type ApiRecord } from '../../utils/main-api'

const route = useRoute()
const runtimeConfig = useRuntimeConfig()
const toast = useToast()

const loading = ref(true)
const error = ref('')
const invoice = ref<ApiRecord | null>(null)
const feedbackSaving = ref(false)
const feedbackSubmitted = ref(false)
const feedback = reactive({ rating: 5, message: '' })

const SummaryLine = defineComponent({
  props: {
    label: { type: String, required: true },
    value: { type: String, required: true }
  },
  setup(props) {
    return () => h('div', { class: 'flex items-center justify-between gap-3 text-slate-300' }, [
      h('span', props.label),
      h('span', { class: 'font-medium text-white' }, props.value)
    ])
  }
})

const PublicBanners = defineComponent({
  props: {
    items: { type: Array as PropType<ApiRecord[]>, default: () => [] }
  },
  emits: ['open'],
  setup(props, { emit }) {
    return () => props.items.length
      ? h('section', { class: 'grid gap-3 md:grid-cols-2' }, props.items.map(row => h('button', {
        type: 'button',
        class: 'overflow-hidden rounded-lg border border-slate-800 bg-slate-900/80 text-left shadow-lg shadow-black/10 transition hover:border-primary/50',
        onClick: () => emit('open', row)
      }, [
        readText(row, ['imageUrl'], '')
          ? h('img', {
            src: readText(row, ['imageUrl'], ''),
            alt: readText(row, ['title'], 'Offer banner'),
            class: 'h-40 w-full object-cover',
            loading: 'lazy'
          })
          : h('div', { class: 'flex h-40 items-center justify-center bg-slate-950/70 text-sm text-slate-500' }, 'Offer banner'),
        h('div', { class: 'p-4' }, [
          h('p', { class: 'font-semibold text-white' }, readText(row, ['title'], 'Offer')),
          h('p', { class: 'mt-1 line-clamp-1 text-xs text-slate-500' }, readText(row, ['targetUrl'], 'Tap to view offer'))
        ])
      ])))
      : null
  }
})

const token = computed(() => {
  const value = route.params.token
  return Array.isArray(value) ? value[0] : String(value || '')
})
const items = computed(() => readArray(invoice.value, ['items']))
const banners = computed(() => readArray(invoice.value, ['banners']))
const reviewSettings = computed(() => {
  const value = invoice.value?.reviewSettings
  return value && typeof value === 'object' && !Array.isArray(value) ? value as ApiRecord : {}
})
const reviewEnabled = computed(() => reviewSettings.value.enableGoogleReview === true && Boolean(readText(reviewSettings.value, ['googleReviewUrl'], '')))
const whatsappEnabled = computed(() => reviewSettings.value.enableWhatsappSupport === true && Boolean(readText(reviewSettings.value, ['whatsAppSupportNumber', 'whatsappSupportNumber'], '')))
const privateFeedbackEnabled = computed(() => reviewSettings.value.enablePrivateFeedback !== false)

function apiBase() {
  return String(runtimeConfig.public.apiBaseUrl || '/api').replace(/\/+$/, '')
}

function apiUrl(path: string) {
  const normalized = String(path || '').replace(/^\/+/, '').replace(/^api\/+/i, '')
  return `${apiBase()}/${normalized}`
}

function browserUrl(value: string) {
  if (!value) return ''
  if (/^https?:\/\//i.test(value)) return value
  if (value.startsWith('/')) return value
  return apiUrl(value)
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function money(value: number) {
  return formatIndianMoney(value)
}

function bannersBy(position: string) {
  return banners.value.filter(row => readText(row, ['position'], '').toLowerCase() === position.toLowerCase())
}

async function publicRequest<T>(path: string, options: RequestInit = {}) {
  const response = await fetch(apiUrl(path), {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers || {})
    }
  })
  if (!response.ok) {
    const message = await response.text()
    throw new Error(stripServerUrl(message || `Request failed with ${response.status}`))
  }
  return await response.json() as T
}

async function track(eventType: string, source?: string, targetUrl?: string) {
  try {
    await publicRequest(`public/digital-bills/${encodeURIComponent(token.value)}/events`, {
      method: 'POST',
      body: JSON.stringify({ eventType, source, targetUrl })
    })
  } catch {
    // Tracking should never block the customer from opening links.
  }
}

async function loadInvoice() {
  loading.value = true
  error.value = ''
  try {
    invoice.value = await publicRequest<ApiRecord>(`public/digital-bills/${encodeURIComponent(token.value)}`)
  } catch (caught) {
    invoice.value = null
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Digital invoice could not be opened.')
  } finally {
    loading.value = false
  }
}

async function openPdf() {
  const pdf = readText(invoice.value, ['pdfUrl'], '') || `public/digital-bills/${encodeURIComponent(token.value)}/pdf`
  await track('PdfDownloaded', 'PublicInvoice', pdf)
  window.open(browserUrl(pdf), '_blank', 'noopener,noreferrer')
}

async function openReview() {
  const url = readText(reviewSettings.value, ['googleReviewUrl'], '')
  if (!url) return
  await track('ReviewClicked', 'GoogleReview', url)
  openUrl(url)
}

async function openWhatsApp() {
  const numberValue = readText(reviewSettings.value, ['whatsAppSupportNumber', 'whatsappSupportNumber'], '')
  const digits = numberValue.replace(/\D/g, '')
  if (!digits) return
  const text = encodeURIComponent(`Hello, I need support for invoice ${readText(invoice.value, ['invoiceNumber'], '')}.`)
  const url = `https://wa.me/${digits}?text=${text}`
  await track('WhatsappSupportClicked', 'WhatsAppSupport', url)
  openUrl(url)
}

async function openBanner(row: ApiRecord) {
  const target = readText(row, ['targetUrl'], '')
  if (!target) return
  await track('BannerClicked', readText(row, ['id'], ''), target)
  openUrl(target)
}

function openUrl(url: string) {
  if (url) window.open(browserUrl(url), '_blank', 'noopener,noreferrer')
}

async function submitFeedback() {
  feedbackSaving.value = true
  try {
    await publicRequest(`public/digital-bills/${encodeURIComponent(token.value)}/feedback`, {
      method: 'POST',
      body: JSON.stringify({
        rating: feedback.rating,
        message: feedback.message.trim() || null
      })
    })
    feedbackSubmitted.value = true
    toast.add({ title: 'Feedback submitted', color: 'success' })
  } catch (caught) {
    toast.add({ title: 'Feedback failed', description: stripServerUrl(caught instanceof Error ? caught.message : 'Could not submit feedback.'), color: 'error' })
  } finally {
    feedbackSaving.value = false
  }
}

onMounted(loadInvoice)
useHead(() => ({
  title: invoice.value ? `${readText(invoice.value, ['invoiceNumber'], 'Digital Invoice')} - Garmetix` : 'Digital Invoice - Garmetix'
}))
</script>
