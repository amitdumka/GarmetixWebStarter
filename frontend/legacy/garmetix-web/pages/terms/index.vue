<script setup lang="ts">
definePageMeta({ layout: false })

const lastUpdated = '30 June 2026'

useHead({
  title: 'Terms of Usage | Garmetix',
  meta: [
    { name: 'description', content: 'Terms of Usage for Garmetix and Aadwika Fashion digital billing, WhatsApp invoice links, customer feedback, and CRM services.' },
    { property: 'og:title', content: 'Terms of Usage | Garmetix' },
    { property: 'og:description', content: 'Usage terms for Garmetix digital billing, invoices, WhatsApp messages, feedback, and CRM services.' }
  ]
})

const terms = [
  {
    title: 'Acceptance of terms',
    body: 'By accessing Garmetix, a digital bill link, invoice PDF, feedback form, WhatsApp invoice message, or related customer service page, you agree to use the service only for lawful billing, support, customer-service, and business-operation purposes.'
  },
  {
    title: 'Service purpose',
    body: 'Garmetix is used for garment retail operations such as sales invoices, purchase records, stock, accounting, GST reports, customer feedback, digital bills, and operational CRM. Public digital bill links are provided to help customers view/download invoice copies and contact the store.'
  },
  {
    title: 'Customer responsibilities',
    body: 'Customers should verify invoice details, keep bill links private where needed, and contact the store if any bill, mobile number, amount, or item detail appears incorrect.'
  },
  {
    title: 'Business-user responsibilities',
    body: 'Store users must keep login credentials confidential, use role permissions responsibly, avoid unauthorized data exports, and ensure customer messaging follows applicable law and platform policies.'
  },
  {
    title: 'WhatsApp and messaging',
    body: 'Invoice and service messages may be sent through WhatsApp Cloud API or another configured provider. Marketing or offer messages should be sent only where permitted and with proper template/category approval where required by the provider.'
  },
  {
    title: 'Reviews and feedback',
    body: 'Garmetix may show review, feedback, social-media, or support links on digital bill pages. Customers should provide honest feedback. The store should not misuse review flows or offer improper incentives for reviews.'
  },
  {
    title: 'Availability and accuracy',
    body: 'The service is provided for business operations. While care is taken to keep records accurate, temporary downtime, network issues, provider failures, or delayed WhatsApp/webhook statuses may occur.'
  },
  {
    title: 'Limitation of misuse',
    body: 'Users must not attempt to access unauthorized invoices, guess public tokens, misuse customer data, interfere with the service, or upload/send unlawful, abusive, fraudulent, or misleading content.'
  },
  {
    title: 'Changes to terms',
    body: 'These terms may be updated from time to time as Garmetix features, business needs, provider requirements, or legal obligations change. The updated page will show the latest effective date.'
  }
]
</script>

<template>
  <div class="min-h-screen bg-slate-50 px-4 py-8 text-slate-900 dark:bg-slate-950 dark:text-white sm:px-6 lg:px-8">
    <main class="mx-auto max-w-5xl space-y-6">
      <UCard>
        <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div class="space-y-2">
            <UBadge color="primary" variant="subtle">Garmetix • Aadwika Fashion</UBadge>
            <h1 class="text-3xl font-bold tracking-tight sm:text-4xl">Terms of Usage</h1>
            <p class="max-w-3xl text-sm leading-6 text-slate-600 dark:text-slate-300">
              These terms describe acceptable use of Garmetix, Aadwika Fashion digital invoices, WhatsApp invoice links,
              customer feedback, CRM tools, and related business-service pages.
            </p>
            <p class="text-xs font-medium text-slate-500 dark:text-slate-400">Effective date: {{ lastUpdated }}</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-shield-check" color="neutral" variant="outline" to="/privacy" label="Privacy Policy" />
            <UButton icon="i-lucide-home" color="primary" variant="solid" to="/" label="Open app" />
          </div>
        </div>
      </UCard>

      <div class="space-y-4">
        <UCard v-for="(item, index) in terms" :key="item.title">
          <div class="flex gap-4">
            <div class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
              {{ index + 1 }}
            </div>
            <div class="space-y-2">
              <h2 class="text-lg font-semibold">{{ item.title }}</h2>
              <p class="text-sm leading-6 text-slate-600 dark:text-slate-300">{{ item.body }}</p>
            </div>
          </div>
        </UCard>
      </div>

      <UCard>
        <div class="space-y-3 text-sm leading-6 text-slate-600 dark:text-slate-300">
          <h2 class="text-lg font-semibold text-slate-950 dark:text-white">Contact</h2>
          <p>
            For questions about these terms, digital bills, WhatsApp invoice messages, or account support, contact
            <strong>ameetkrsah@gmail.com</strong> with your invoice number and registered mobile number if applicable.
          </p>
        </div>
      </UCard>

      <div class="flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500 dark:text-slate-400">
        <div>© Aadwika Fashion / Garmetix. All rights reserved.</div>
        <div class="flex gap-3">
          <NuxtLink class="font-medium text-primary hover:underline" to="/privacy">Privacy Policy</NuxtLink>
          <NuxtLink class="font-medium text-primary hover:underline" to="/goods-return-policy">Return Policy</NuxtLink>
          <NuxtLink class="font-medium text-primary hover:underline" to="/contact-us">Contact</NuxtLink>
        </div>
      </div>
    </main>
  </div>
</template>
