<script setup lang="ts">
definePageMeta({ layout: false })
useHead({
  title: 'Digital Invoice',
  meta: [
    { name: 'robots', content: 'noindex,nofollow' },
    { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' }
  ]
})

const route = useRoute()
const token = computed(() => String(route.params.token || ''))
const loading = ref(true)
const loadError = ref('')
const invoice = ref<any | null>(null)
const feedbackOpen = ref(false)
const feedbackSaving = ref(false)
const feedbackDone = ref(false)
const feedbackForm = reactive({ rating: 5, message: '' })

const headerBanners = computed(() => (invoice.value?.banners || []).filter((item: any) => item.position === 'Header'))
const footerBanners = computed(() => (invoice.value?.banners || []).filter((item: any) => item.position !== 'Header'))
const hasBalance = computed(() => Number(invoice.value?.balanceAmount || 0) > 0)
const goodsReturnPolicyUrl = computed(() => invoice.value?.goodsReturnPolicyUrl || '/goods-return-policy')
const initials = computed(() => String(invoice.value?.storeName || invoice.value?.companyName || 'Garmetix')
  .split(/\s+/)
  .filter(Boolean)
  .slice(0, 2)
  .map((part) => part.charAt(0).toUpperCase())
  .join('') || 'GB')

async function loadInvoice() {
  loading.value = true
  loadError.value = ''
  try {
    invoice.value = await $fetch<any>(`/api/public/digital-bills/${encodeURIComponent(token.value)}`)
  } catch (error: any) {
    loadError.value = error?.data?.message || error?.message || 'Digital invoice link is invalid, disabled, or expired.'
  } finally {
    loading.value = false
  }
}

async function track(eventType: string, targetUrl?: string | null, source?: string | null) {
  try {
    await $fetch(`/api/public/digital-bills/${encodeURIComponent(token.value)}/events`, {
      method: 'POST',
      body: { eventType, targetUrl, source }
    })
  } catch {}
}

function openExternal(url: string | null | undefined, eventType: string, source?: string | null) {
  if (!url) return
  track(eventType, url, source)
  window.open(url, '_blank', 'noopener,noreferrer')
}

async function submitFeedback() {
  feedbackSaving.value = true
  try {
    await $fetch(`/api/public/digital-bills/${encodeURIComponent(token.value)}/feedback`, {
      method: 'POST',
      body: { rating: feedbackForm.rating, message: feedbackForm.message }
    })
    feedbackDone.value = true
  } catch (error: any) {
    alert(error?.data?.message || error?.message || 'Feedback could not be submitted.')
  } finally {
    feedbackSaving.value = false
  }
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}
function date(value: string) { return value ? new Date(value).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }) : '-' }

onMounted(loadInvoice)
</script>

<template>
  <main class="min-h-screen bg-gradient-to-b from-primary/10 via-slate-50 to-slate-100 px-3 py-4 text-slate-900 dark:from-primary/15 dark:via-slate-950 dark:to-slate-950 dark:text-white sm:px-4">
    <div class="mx-auto max-w-2xl space-y-4 pb-24">
      <div v-if="loading" class="rounded-3xl border border-white/70 bg-white/90 p-8 text-center shadow-sm backdrop-blur dark:border-slate-800 dark:bg-slate-900/90">
        <div class="mx-auto mb-4 h-12 w-12 animate-pulse rounded-full bg-primary/20" />
        <p class="font-semibold">Loading your digital invoice...</p>
        <p class="mt-1 text-sm text-slate-500">Please wait while we fetch the bill securely.</p>
      </div>

      <div v-else-if="loadError" class="rounded-3xl border border-white/70 bg-white/95 p-8 text-center shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div class="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-warning/10 text-warning">
          <UIcon name="i-lucide-file-warning" class="h-7 w-7" />
        </div>
        <h1 class="mt-4 text-xl font-bold">Invoice not available</h1>
        <p class="mt-2 text-sm text-slate-500">{{ loadError }}</p>
        <p class="mt-4 rounded-2xl bg-slate-50 p-3 text-xs text-slate-500 dark:bg-slate-800">The link may be expired, disabled, or typed incorrectly. Please contact the store for a fresh invoice link.</p>
      </div>

      <template v-else-if="invoice">
        <a
          v-for="banner in headerBanners"
          :key="banner.id"
          :href="banner.targetUrl || undefined"
          class="group block overflow-hidden rounded-3xl border border-white/70 bg-white shadow-sm transition hover:-translate-y-0.5 hover:shadow-lg dark:border-slate-800 dark:bg-slate-900"
          @click.prevent="openExternal(banner.targetUrl, 'BannerClicked', banner.id)"
        >
          <img :src="banner.imageUrl" :alt="banner.title" class="h-32 w-full object-cover sm:h-40" />
          <div class="flex items-center justify-between px-4 py-2 text-xs font-medium text-slate-500">
            <span>{{ banner.title }}</span>
            <span v-if="banner.targetUrl" class="text-primary">View offer</span>
          </div>
        </a>

        <section class="overflow-hidden rounded-3xl border border-white/70 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div class="bg-gradient-to-r from-primary/15 to-slate-50 p-5 dark:from-primary/20 dark:to-slate-900">
            <div class="flex items-start justify-between gap-4">
              <div class="flex min-w-0 gap-3">
                <div class="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-lg font-bold text-white shadow-sm">{{ initials }}</div>
                <div class="min-w-0">
                  <p class="text-[11px] font-semibold uppercase tracking-[0.25em] text-primary">Digital Invoice</p>
                  <h1 class="mt-1 truncate text-2xl font-black">{{ invoice.storeName || invoice.companyName }}</h1>
                  <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">{{ invoice.companyName }}</p>
                </div>
              </div>
              <UBadge :color="hasBalance ? 'warning' : 'success'" variant="subtle" class="shrink-0">{{ hasBalance ? 'Balance Due' : 'Paid' }}</UBadge>
            </div>
            <p class="mt-4 text-sm leading-6 text-slate-600 dark:text-slate-300">{{ invoice.companyAddress }}</p>
            <div class="mt-3 flex flex-wrap gap-2 text-xs text-slate-500">
              <span class="rounded-full bg-white/80 px-3 py-1 dark:bg-slate-800">GSTIN: {{ invoice.companyGstin || '-' }}</span>
              <span class="rounded-full bg-white/80 px-3 py-1 dark:bg-slate-800">Phone: {{ invoice.companyPhone || '-' }}</span>
            </div>
          </div>

          <div class="grid gap-3 p-5 text-sm sm:grid-cols-2">
            <div class="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800"><span class="text-slate-500">Invoice No.</span><strong class="block break-all text-base">{{ invoice.invoiceNumber }}</strong></div>
            <div class="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800"><span class="text-slate-500">Date</span><strong class="block text-base">{{ date(invoice.invoiceDate) }}</strong></div>
            <div class="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800"><span class="text-slate-500">Customer</span><strong class="block text-base">{{ invoice.customerName || 'Walk-in customer' }}</strong></div>
            <div class="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800"><span class="text-slate-500">Mobile</span><strong class="block text-base">{{ invoice.customerMobileMasked || '-' }}</strong></div>
          </div>
        </section>

        <section class="rounded-3xl border border-white/70 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div class="flex items-center justify-between">
            <h2 class="text-lg font-bold">Items purchased</h2>
            <span class="text-xs text-slate-500">{{ invoice.items?.length || 0 }} item(s)</span>
          </div>
          <div class="mt-3 divide-y divide-slate-200 dark:divide-slate-800">
            <div v-for="item in invoice.items" :key="`${item.barcode}-${item.productName}`" class="py-3 text-sm">
              <div class="flex justify-between gap-3"><strong class="leading-5">{{ item.productName }}</strong><span class="shrink-0 font-semibold">{{ money(item.amount) }}</span></div>
              <div class="mt-2 flex flex-wrap gap-2 text-xs text-slate-500">
                <span class="rounded-full bg-slate-100 px-2 py-1 dark:bg-slate-800">Qty {{ item.quantity }}</span>
                <span class="rounded-full bg-slate-100 px-2 py-1 dark:bg-slate-800">MRP {{ money(item.mrp) }}</span>
                <span class="rounded-full bg-slate-100 px-2 py-1 dark:bg-slate-800">Disc {{ money(item.discountAmount) }}</span>
                <span class="rounded-full bg-slate-100 px-2 py-1 dark:bg-slate-800">GST {{ item.taxPercentage }}%</span>
                <span v-if="item.hsnCode" class="rounded-full bg-slate-100 px-2 py-1 dark:bg-slate-800">HSN {{ item.hsnCode }}</span>
              </div>
            </div>
          </div>
        </section>

        <section class="rounded-3xl border border-white/70 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 class="text-lg font-bold">Bill summary</h2>
          <div class="mt-4 space-y-2 text-sm">
            <div class="flex justify-between"><span>MRP</span><strong>{{ money(invoice.mrp) }}</strong></div>
            <div class="flex justify-between"><span>Discount</span><strong class="text-success">-{{ money(invoice.discountAmount) }}</strong></div>
            <div class="flex justify-between"><span>Taxable</span><strong>{{ money(invoice.netAmount) }}</strong></div>
            <div class="flex justify-between"><span>Tax</span><strong>{{ money(invoice.taxAmount) }}</strong></div>
            <div class="flex justify-between"><span>Round off</span><strong>{{ money(invoice.roundOff) }}</strong></div>
            <div class="mt-3 flex justify-between border-t border-slate-200 pt-4 text-xl dark:border-slate-800"><span class="font-bold">Total</span><strong>{{ money(invoice.billAmount) }}</strong></div>
            <div class="flex justify-between text-success"><span>Paid</span><strong>{{ money(invoice.paidAmount) }}</strong></div>
            <div class="flex justify-between" :class="hasBalance ? 'text-warning' : 'text-slate-500'"><span>Balance</span><strong>{{ money(invoice.balanceAmount) }}</strong></div>
          </div>
        </section>

        <section class="rounded-3xl border border-white/70 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 class="text-lg font-bold">Need anything else?</h2>
          <p class="mt-1 text-sm text-slate-500">Download your bill, contact the store, or share your honest feedback.</p>
          <div class="mt-4 grid gap-3">
            <UButton block size="lg" icon="i-lucide-file-down" label="Download PDF Bill" :to="invoice.pdfUrl" target="_blank" />
            <UButton block color="neutral" variant="outline" icon="i-lucide-repeat-2" label="Goods return / exchange policy" :to="goodsReturnPolicyUrl" target="_blank" />
            <UButton v-if="invoice.reviewSettings?.googleReviewUrl && invoice.reviewSettings?.enableGoogleReview" block color="warning" variant="subtle" icon="i-lucide-star" :label="invoice.reviewSettings.reviewButtonText" @click="openExternal(invoice.reviewSettings.googleReviewUrl, 'ReviewClicked')" />
            <UButton v-if="invoice.reviewSettings?.instagramUrl && invoice.reviewSettings?.enableInstagram" block color="error" variant="subtle" icon="i-lucide-instagram" label="Follow us on Instagram" @click="openExternal(invoice.reviewSettings.instagramUrl, 'InstagramClicked')" />
            <UButton v-if="invoice.reviewSettings?.facebookUrl && invoice.reviewSettings?.enableFacebook" block color="info" variant="subtle" icon="i-lucide-thumbs-up" label="Follow us on Facebook" @click="openExternal(invoice.reviewSettings.facebookUrl, 'FacebookClicked')" />
            <UButton v-if="invoice.reviewSettings?.whatsAppSupportNumber && invoice.reviewSettings?.enableWhatsappSupport" block color="success" variant="subtle" icon="i-lucide-message-circle" label="WhatsApp store support" @click="openExternal(`https://wa.me/${invoice.reviewSettings.whatsAppSupportNumber}`, 'WhatsappSupportClicked')" />
            <UButton v-if="invoice.reviewSettings?.enablePrivateFeedback" block color="neutral" variant="outline" icon="i-lucide-message-square-text" :label="invoice.reviewSettings.feedbackButtonText" @click="feedbackOpen = !feedbackOpen" />
          </div>

          <form v-if="feedbackOpen && !feedbackDone" class="mt-4 space-y-3 rounded-2xl bg-slate-50 p-4 dark:bg-slate-800" @submit.prevent="submitFeedback">
            <UFormField label="Rating"><UInput v-model.number="feedbackForm.rating" type="number" min="1" max="5" /></UFormField>
            <UFormField label="Message"><UTextarea v-model="feedbackForm.message" placeholder="Share your feedback" /></UFormField>
            <UButton type="submit" :loading="feedbackSaving" label="Submit feedback" />
          </form>
          <div v-else-if="feedbackDone" class="mt-4 rounded-2xl bg-success/10 p-4 text-sm text-success">Thank you. Your feedback has been submitted.</div>
        </section>

        <section class="rounded-3xl border border-primary/20 bg-primary/5 p-5 shadow-sm dark:border-primary/30 dark:bg-primary/10">
          <div class="flex items-start gap-3">
            <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-primary/15 text-primary">
              <UIcon name="i-lucide-repeat-2" class="h-5 w-5" />
            </div>
            <div class="min-w-0 space-y-2">
              <h2 class="text-lg font-bold">Goods return & exchange policy</h2>
              <p class="text-sm leading-6 text-slate-600 dark:text-slate-300">
                Goods once sold are not refundable. Eligible goods may be exchanged within 7 days only with invoice copy,
                intact price tag, original packing and unused sale condition. Approved credit notes are adjustable against
                future purchase as per policy validity.
              </p>
              <UButton size="sm" color="primary" variant="subtle" icon="i-lucide-external-link" label="Read full policy" :to="goodsReturnPolicyUrl" target="_blank" />
            </div>
          </div>
        </section>

        <a
          v-for="banner in footerBanners"
          :key="banner.id"
          :href="banner.targetUrl || undefined"
          class="group block overflow-hidden rounded-3xl border border-white/70 bg-white shadow-sm transition hover:-translate-y-0.5 hover:shadow-lg dark:border-slate-800 dark:bg-slate-900"
          @click.prevent="openExternal(banner.targetUrl, 'BannerClicked', banner.id)"
        >
          <img :src="banner.imageUrl" :alt="banner.title" class="h-28 w-full object-cover sm:h-32" />
          <div class="flex items-center justify-between px-4 py-2 text-xs font-medium text-slate-500">
            <span>{{ banner.title }}</span>
            <span v-if="banner.targetUrl" class="text-primary">View offer</span>
          </div>
        </a>

        <div class="fixed inset-x-0 bottom-0 z-20 border-t border-slate-200 bg-white/95 px-3 py-3 shadow-2xl backdrop-blur dark:border-slate-800 dark:bg-slate-950/95 sm:hidden">
          <div class="mx-auto flex max-w-2xl items-center gap-2">
            <UButton class="flex-1" icon="i-lucide-file-down" label="PDF" :to="invoice.pdfUrl" target="_blank" />
            <UButton class="flex-1" color="neutral" variant="outline" icon="i-lucide-repeat-2" label="Policy" :to="goodsReturnPolicyUrl" target="_blank" />
            <UButton v-if="invoice.reviewSettings?.googleReviewUrl && invoice.reviewSettings?.enableGoogleReview" class="flex-1" color="warning" variant="subtle" icon="i-lucide-star" label="Review" @click="openExternal(invoice.reviewSettings.googleReviewUrl, 'ReviewClicked')" />
            <UButton v-if="invoice.reviewSettings?.whatsAppSupportNumber && invoice.reviewSettings?.enableWhatsappSupport" class="flex-1" color="success" variant="subtle" icon="i-lucide-message-circle" label="Support" @click="openExternal(`https://wa.me/${invoice.reviewSettings.whatsAppSupportNumber}`, 'WhatsappSupportClicked')" />
          </div>
        </div>

        <p class="pb-4 text-center text-xs text-slate-500">Powered by Garmetix Digital Bill CRM · Policy linked on invoice and PDF</p>
      </template>
    </div>
  </main>
</template>
