export default defineNuxtConfig({
  compatibilityDate: '2026-06-04',
  css: ['~/assets/css/main.css'],
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
