export default defineNuxtConfig({
  ssr: false,
  modules: ['@nuxt/ui'],
  app: {
    head: {
      title: 'Garmetix HR'
    }
  },
  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_GARMETIX_API_BASE_URL || 'http://localhost:5080/api',
      appId: 'hr',
      appUrls: {
        NUXT_PUBLIC_GARMETIX_MAIN_URL: process.env.NUXT_PUBLIC_GARMETIX_MAIN_URL || process.env.NUXT_PUBLIC_MAIN_WEB_URL || '',
        NUXT_PUBLIC_GARMETIX_POS_URL: process.env.NUXT_PUBLIC_GARMETIX_POS_URL || process.env.NUXT_PUBLIC_POS_WEB_URL || '',
        NUXT_PUBLIC_GARMETIX_HR_URL: process.env.NUXT_PUBLIC_GARMETIX_HR_URL || process.env.NUXT_PUBLIC_HR_WEB_URL || 'http://localhost:3102',
        NUXT_PUBLIC_GARMETIX_AI_SENSE_URL: process.env.NUXT_PUBLIC_GARMETIX_AI_SENSE_URL || process.env.NUXT_PUBLIC_AI_SENSE_WEB_URL || '',
        NUXT_PUBLIC_GARMETIX_BOOKS_URL: process.env.NUXT_PUBLIC_GARMETIX_BOOKS_URL || process.env.NUXT_PUBLIC_ACCOUNTING_WEB_URL || '',
        NUXT_PUBLIC_GARMETIX_ADMIN_URL: process.env.NUXT_PUBLIC_GARMETIX_ADMIN_URL || process.env.NUXT_PUBLIC_SAAS_WEB_URL || ''
      }
    }
  },
  devServer: {
    port: 3102
  }
})
