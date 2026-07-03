<script setup lang="ts">
definePageMeta({ layout: false })

const lastUpdated = '02 July 2026'
const publicBaseUrl = 'https://garmetix.aadwikafashion.in'

useHead({
  title: 'Goods Return & Exchange Policy | Aadwika Fashion',
  meta: [
    { name: 'description', content: 'Goods return and exchange policy for Aadwika Fashion / Garmetix digital invoices.' },
    { property: 'og:title', content: 'Goods Return & Exchange Policy | Aadwika Fashion' },
    { property: 'og:description', content: 'Exchange-only policy, eligibility conditions, credit note validity and invoice requirements.' }
  ]
})

const eligibility = [
  'Exchange request must be made within 7 days from the date of purchase.',
  'Original invoice copy or valid digital invoice link must be shown at the store.',
  'Goods must be unused, unwashed and in the same condition as sold.',
  'Original price tag, labels, accessories and packaging must be intact.',
  'Goods must not have any tear, alteration, stain mark, smell, dirt, damage or signs of use.',
  'Exchange is subject to store inspection and approval at the time of request.'
]

const notEligible = [
  'Used, washed, damaged, altered, dirty, stained or torn goods.',
  'Goods without original invoice proof.',
  'Goods without price tag, original packing or required labels.',
  'Requests made after the 7-day exchange period.',
  'Items specifically marked as non-exchangeable, final sale, clearance or special-order goods, where applicable.'
]
</script>

<template>
  <main class="min-h-screen bg-slate-50 px-4 py-8 text-slate-900 dark:bg-slate-950 dark:text-white sm:px-6 lg:px-8">
    <div class="mx-auto max-w-5xl space-y-6">
      <UCard class="overflow-hidden">
        <div class="space-y-5">
          <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div class="space-y-2">
              <UBadge color="primary" variant="subtle">Aadwika Fashion • Garmetix</UBadge>
              <h1 class="text-3xl font-bold tracking-tight sm:text-4xl">Goods Return & Exchange Policy</h1>
              <p class="max-w-3xl text-sm leading-6 text-slate-600 dark:text-slate-300">
                We request customers to check size, colour, design, quantity and bill details carefully before purchase.
                Once goods are sold, they cannot be returned for money refund. Eligible goods may be exchanged within
                the policy period subject to store inspection and approval.
              </p>
            </div>
            <div class="flex flex-wrap gap-2">
              <UButton icon="i-lucide-receipt-text" color="neutral" variant="outline" to="/" label="Open app" />
              <UButton icon="i-lucide-file-text" color="primary" variant="subtle" to="/terms" label="Terms" />
            </div>
          </div>

          <div class="grid gap-3 rounded-2xl bg-primary/5 p-4 text-sm sm:grid-cols-3">
            <div>
              <p class="font-semibold text-slate-500 dark:text-slate-400">Effective date</p>
              <p class="font-bold">{{ lastUpdated }}</p>
            </div>
            <div>
              <p class="font-semibold text-slate-500 dark:text-slate-400">Website</p>
              <p class="break-all font-bold">{{ publicBaseUrl }}</p>
            </div>
            <div>
              <p class="font-semibold text-slate-500 dark:text-slate-400">Policy type</p>
              <p class="font-bold">Exchange only • No cash refund</p>
            </div>
          </div>
        </div>
      </UCard>

      <div class="grid gap-4 lg:grid-cols-3">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-ban" class="h-5 w-5 text-error" />
              <h2 class="text-lg font-semibold">No refund after sale</h2>
            </div>
          </template>
          <p class="text-sm leading-6 text-slate-600 dark:text-slate-300">
            Goods once sold will not be taken back for cash, bank, UPI, card or any other money refund. Please review
            the product carefully before billing.
          </p>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-repeat-2" class="h-5 w-5 text-primary" />
              <h2 class="text-lg font-semibold">Exchange within 7 days</h2>
            </div>
          </template>
          <p class="text-sm leading-6 text-slate-600 dark:text-slate-300">
            Eligible goods may be exchanged within 7 days from purchase date, only with invoice proof and only if the
            item is in original sale condition.
          </p>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-file-check-2" class="h-5 w-5 text-success" />
              <h2 class="text-lg font-semibold">Credit note only</h2>
            </div>
          </template>
          <p class="text-sm leading-6 text-slate-600 dark:text-slate-300">
            If an approved product return is not immediately exchanged, no refund will be issued. A store credit note
            may be issued and adjusted against future purchase as per validity rules.
          </p>
        </UCard>
      </div>

      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-check-circle-2" class="h-5 w-5 text-primary" />
            <h2 class="text-lg font-semibold">Exchange eligibility conditions</h2>
          </div>
        </template>
        <ul class="grid gap-3 text-sm leading-6 text-slate-600 dark:text-slate-300 md:grid-cols-2">
          <li v-for="item in eligibility" :key="item" class="flex gap-2 rounded-2xl bg-slate-50 p-3 dark:bg-slate-800">
            <UIcon name="i-lucide-check" class="mt-0.5 h-4 w-4 shrink-0 text-success" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-alert-triangle" class="h-5 w-5 text-warning" />
            <h2 class="text-lg font-semibold">Goods not eligible for exchange</h2>
          </div>
        </template>
        <ul class="grid gap-3 text-sm leading-6 text-slate-600 dark:text-slate-300 md:grid-cols-2">
          <li v-for="item in notEligible" :key="item" class="flex gap-2 rounded-2xl bg-slate-50 p-3 dark:bg-slate-800">
            <UIcon name="i-lucide-x" class="mt-0.5 h-4 w-4 shrink-0 text-error" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-calendar-clock" class="h-5 w-5 text-primary" />
            <h2 class="text-lg font-semibold">Credit note validity</h2>
          </div>
        </template>
        <div class="space-y-3 text-sm leading-6 text-slate-600 dark:text-slate-300">
          <p>
            Where management approves a product return to credit instead of immediate exchange, a store credit note
            will be issued. This credit note can be used only for purchase of goods from the same store/business.
          </p>
          <div class="grid gap-3 md:grid-cols-2">
            <div class="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800">
              <p class="font-semibold text-slate-950 dark:text-white">Regular validity</p>
              <p class="mt-1">Valid up to 31 March of the same financial year.</p>
            </div>
            <div class="rounded-2xl bg-slate-50 p-4 dark:bg-slate-800">
              <p class="font-semibold text-slate-950 dark:text-white">Jan / Feb / Mar credit notes</p>
              <p class="mt-1">Valid for 6 months from the date of issue.</p>
            </div>
          </div>
          <p>
            Credit note cannot be encashed as money refund and cannot be transferred unless specifically approved by
            the business.
          </p>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-info" class="h-5 w-5 text-primary" />
            <h2 class="text-lg font-semibold">Important note</h2>
          </div>
        </template>
        <p class="text-sm leading-6 text-slate-600 dark:text-slate-300">
          Exchange approval is subject to physical verification of the product and invoice by store staff. The store
          reserves the right to refuse exchange where the product condition, invoice proof, time limit or policy
          requirements are not satisfied.
        </p>
      </UCard>

      <div class="flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500 dark:text-slate-400">
        <div>© Aadwika Fashion / Garmetix. All rights reserved.</div>
        <div class="flex gap-3">
          <NuxtLink class="font-medium text-primary hover:underline" to="/privacy">Privacy Policy</NuxtLink>
          <NuxtLink class="font-medium text-primary hover:underline" to="/terms">Terms of Usage</NuxtLink>
          <NuxtLink class="font-medium text-primary hover:underline" to="/contact-us">Contact</NuxtLink>
        </div>
      </div>
    </div>
  </main>
</template>
