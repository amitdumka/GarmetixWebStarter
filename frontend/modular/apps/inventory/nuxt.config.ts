export default defineNuxtConfig({
  ssr: false,
  modules: ['@nuxt/ui'],
  css: ['~/assets/css/main.css', '../../packages/shared-ui/assets/modular-shell.css'],
  app: {
    baseURL: process.env.GARMETIX_NUXT_BASE_URL || process.env.NUXT_PUBLIC_GARMETIX_ADMIN_BASE_PATH || '/',
    head: {
      title: 'Garmetix Inventory',
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
      apiBaseUrl: process.env.NUXT_PUBLIC_GARMETIX_API_BASE_URL || '/api',
      appId: 'inventory',
      title: 'Garmetix Inventory',
    }
  },

  routeRules: {
    '/api/**': {
      proxy: 'http://localhost:5000/api/**'
    }
  },

  devServer: {
    port: 3105
  },

  compatibilityDate: '2024-04-03',
  devtools: { enabled: true },
  ssr: false
})
