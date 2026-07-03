export default defineNuxtConfig({
  ssr: false,
  modules: ['@nuxt/ui'],
  css: ['~/assets/css/main.css', '../../packages/shared-ui/assets/modular-shell.css'],
  app: {
    baseURL: process.env.GARMETIX_NUXT_BASE_URL || process.env.NUXT_PUBLIC_GARMETIX_AI_SENSE_BASE_PATH || '/',
    head: {
      title: 'Garmetix AI Sense',
      titleTemplate: '%s | Garmetix',
      meta: [
        { name: 'application-name', content: 'Garmetix' },
        { name: 'apple-mobile-web-app-title', content: 'Garmetix' },
        { name: 'theme-color', content: '#020617' }
      ],
      link: [
        { rel: 'icon', type: 'image/png', sizes: '32x32', href: '/favicon-32x32.png' },
        { rel: 'icon', type: 'image/png', sizes: '192x192', href: '/garmetix-icon-192.png' },
        { rel: 'apple-touch-icon', sizes: '180x180', href: '/apple-touch-icon.png' },
        { rel: 'manifest', href: '/site.webmanifest' }
      ]
    }
  },
  colorMode: {
    preference: 'dark',
    fallback: 'dark',
    classSuffix: ''
  },
  ui: {
    theme: {
      colors: ['primary', 'success', 'warning', 'error', 'neutral']
    }
  },
  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_GARMETIX_API_BASE_URL || 'http://localhost:5080/api',
      appId: 'ai-sense',
      appUrls: {
        NUXT_PUBLIC_GARMETIX_MAIN_URL: process.env.NUXT_PUBLIC_GARMETIX_MAIN_URL || process.env.NUXT_PUBLIC_MAIN_WEB_URL || '',
        NUXT_PUBLIC_GARMETIX_POS_URL: process.env.NUXT_PUBLIC_GARMETIX_POS_URL || process.env.NUXT_PUBLIC_POS_WEB_URL || '',
        NUXT_PUBLIC_GARMETIX_HR_URL: process.env.NUXT_PUBLIC_GARMETIX_HR_URL || process.env.NUXT_PUBLIC_HR_WEB_URL || '',
        NUXT_PUBLIC_GARMETIX_AI_SENSE_URL: process.env.NUXT_PUBLIC_GARMETIX_AI_SENSE_URL || process.env.NUXT_PUBLIC_AI_SENSE_WEB_URL || 'http://localhost:3103',
        NUXT_PUBLIC_GARMETIX_BOOKS_URL: process.env.NUXT_PUBLIC_GARMETIX_BOOKS_URL || process.env.NUXT_PUBLIC_ACCOUNTING_WEB_URL || '',
        NUXT_PUBLIC_GARMETIX_ADMIN_URL: process.env.NUXT_PUBLIC_GARMETIX_ADMIN_URL || process.env.NUXT_PUBLIC_SAAS_WEB_URL || ''
      }
    }
  },
  devServer: {
    port: 3103
  }
})
