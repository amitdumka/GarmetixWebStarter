export default defineNuxtConfig({
  ssr: false,
  modules: ['@nuxt/ui'],
  css: ['~/assets/css/main.css', '../../packages/shared-ui/assets/modular-shell.css'],
  app: {
    baseURL: process.env.GARMETIX_NUXT_BASE_URL || process.env.NUXT_PUBLIC_SWALEKHA_BASE_PATH || '/',
    head: {
      title: 'Swalekha',
      titleTemplate: '%s | Swalekha',
      meta: [
        { name: 'application-name', content: 'Swalekha' },
        { name: 'apple-mobile-web-app-title', content: 'Swalekha' },
        { name: 'theme-color', content: '#1e1b2e' }
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
      appId: 'swalekha'
      // Deliberately no `appUrls` here - Swalekha is a fully isolated module and never
      // renders the shared cross-app switcher (see app.vue), so it has no need to know
      // about the other apps' URLs, and no other app's build is required to know about
      // Swalekha's URL either (the one exception is the single profile-menu link, wired
      // via NUXT_PUBLIC_SWALEKHA_URL on the *other* apps, not here).
    }
  },
  devServer: {
    port: 3109
  }
})
