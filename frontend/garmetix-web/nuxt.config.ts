export default defineNuxtConfig({
  compatibilityDate: '2026-06-04',
  css: ['~/assets/css/main.css'],
  app: {
    head: {
      title: 'Garmetix',
      titleTemplate: (titleChunk) => titleChunk ? `${titleChunk} · Garmetix` : 'Garmetix',
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
  runtimeConfig: {
    public: {
      apiBase: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5080/api'
    }
  },
  modules: ['@nuxt/ui'],
  colorMode: {
    preference: 'dark',
    fallback: 'dark',
    classSuffix: ''
  },
  ui: {
    theme: {
      colors: ['primary', 'success', 'warning', 'error', 'neutral']
    }
  }
})
