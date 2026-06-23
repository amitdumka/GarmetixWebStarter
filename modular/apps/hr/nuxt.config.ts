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
      appId: 'hr'
    }
  },
  devServer: {
    port: 3102
  }
})

