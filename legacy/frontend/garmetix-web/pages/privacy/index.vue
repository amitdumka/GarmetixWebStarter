<script setup lang="ts">
definePageMeta({ layout: false })

const lastUpdated = '30 June 2026'
const publicBaseUrl = 'https://garmetix.aadwikafashion.in'

useHead({
  title: 'Privacy Policy | Garmetix',
  meta: [
    { name: 'description', content: 'Privacy Policy for Garmetix and Aadwika Fashion digital billing, WhatsApp invoice links, customer feedback, and CRM services.' },
    { property: 'og:title', content: 'Privacy Policy | Garmetix' },
    { property: 'og:description', content: 'How Garmetix handles customer billing, digital invoice, WhatsApp, feedback, and CRM data.' }
  ]
})

const sections = [
  {
    title: 'Information we collect',
    items: [
      'Customer name, mobile number, invoice number, purchase amount, invoice date, purchased item summary, payment mode, and store details needed for billing and customer support.',
      'Digital bill activity such as invoice link opened, PDF downloaded, review link clicked, feedback submitted, and WhatsApp delivery/read status where available from Meta/WhatsApp.',
      'Basic device and request information such as IP address, browser user agent, and timestamps for security, audit, abuse prevention, and service diagnostics.'
    ]
  },
  {
    title: 'How we use information',
    items: [
      'To generate invoices, digital bill links, downloadable bill PDFs, GST/compliance records, customer support records, and store-level operational reports.',
      'To send transactional WhatsApp/SMS/email messages such as invoice links, purchase confirmations, payment information, and service updates.',
      'To collect customer feedback, improve store service quality, manage loyalty/customer relationship workflows, and help the store owner understand customer engagement.'
    ]
  },
  {
    title: 'WhatsApp and third-party services',
    items: [
      'Garmetix may use Meta WhatsApp Cloud API or another configured provider to send invoice and service messages to customers.',
      'Delivery/read status is handled according to the provider\'s policies and availability. Garmetix stores status only for billing, audit, and customer service purposes.',
      'Review buttons may open Google, Instagram, Facebook, or other store-owned public pages. Those services are governed by their own privacy policies.'
    ]
  },
  {
    title: 'Data sharing',
    items: [
      'Customer data is not sold. It is used for store billing, support, accounting, compliance, and customer relationship operations.',
      'Data may be shared with service providers needed to operate the app, such as hosting, database, WhatsApp/message providers, email providers, backup services, or accounting/GST support when configured by the business owner.',
      'Data may be disclosed if legally required or needed to protect business, customers, or system security.'
    ]
  },
  {
    title: 'Data retention and security',
    items: [
      'Invoices and transaction records are retained as required for accounting, GST, audit, warranty, exchange, and business support requirements.',
      'Public digital bill links use non-guessable tokens and may be disabled or expired by the store administrator.',
      'Access to administrative pages is protected by role-based login permissions. Public invoice pages expose only bill-view information needed by the customer.'
    ]
  },
  {
    title: 'Customer choices',
    items: [
      'Customers may request correction of their name/mobile number in store records by contacting the store.',
      'Customers may request that marketing messages be stopped. Transactional billing messages may still be sent where required for purchase/service support.',
      'Customers can contact the business for questions about digital bill links, feedback records, or invoice copies.'
    ]
  }
]
</script>

<template>
  <div class="min-h-screen bg-slate-50 px-4 py-8 text-slate-900 dark:bg-slate-950 dark:text-white sm:px-6 lg:px-8">
    <main class="mx-auto max-w-5xl space-y-6">
      <UCard class="overflow-hidden">
        <div class="space-y-5">
          <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div class="space-y-2">
              <UBadge color="primary" variant="subtle">Garmetix • Aadwika Fashion</UBadge>
              <h1 class="text-3xl font-bold tracking-tight sm:text-4xl">Privacy Policy</h1>
              <p class="max-w-3xl text-sm leading-6 text-slate-600 dark:text-slate-300">
                This Privacy Policy explains how Garmetix and Aadwika Fashion handle customer billing, digital invoice,
                WhatsApp invoice-link, feedback, review, CRM, and related operational data.
              </p>
            </div>
            <UButton icon="i-lucide-home" color="neutral" variant="outline" to="/" label="Open app" />
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
              <p class="font-semibold text-slate-500 dark:text-slate-400">Contact</p>
              <p class="font-bold">ameetkrsah@gmail.com</p>
            </div>
          </div>
        </div>
      </UCard>

      <div class="grid gap-4 md:grid-cols-2">
        <UCard v-for="section in sections" :key="section.title">
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-shield-check" class="h-5 w-5 text-primary" />
              <h2 class="text-lg font-semibold">{{ section.title }}</h2>
            </div>
          </template>
          <ul class="space-y-3 text-sm leading-6 text-slate-600 dark:text-slate-300">
            <li v-for="item in section.items" :key="item" class="flex gap-2">
              <UIcon name="i-lucide-check-circle-2" class="mt-0.5 h-4 w-4 shrink-0 text-primary" />
              <span>{{ item }}</span>
            </li>
          </ul>
        </UCard>
      </div>

      <UCard>
        <div class="space-y-3 text-sm leading-6 text-slate-600 dark:text-slate-300">
          <h2 class="text-lg font-semibold text-slate-950 dark:text-white">Business contact</h2>
          <p>
            For privacy, invoice, feedback, or WhatsApp billing message questions, contact Aadwika Fashion / Garmetix support
            at <strong>ameetkrsah@gmail.com</strong>. Include your invoice number and registered mobile number when asking about a bill.
          </p>
          <p class="text-xs text-slate-500 dark:text-slate-400">
            This page is provided for customer transparency and platform verification. It is not a substitute for legal advice.
          </p>
        </div>
      </UCard>

      <div class="flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500 dark:text-slate-400">
        <div>© Aadwika Fashion / Garmetix. All rights reserved.</div>
        <div class="flex gap-3">
          <NuxtLink class="font-medium text-primary hover:underline" to="/terms">Terms of Usage</NuxtLink>
          <NuxtLink class="font-medium text-primary hover:underline" to="/goods-return-policy">Return Policy</NuxtLink>
          <NuxtLink class="font-medium text-primary hover:underline" to="/contact-us">Contact</NuxtLink>
        </div>
      </div>
    </main>
  </div>
</template>
