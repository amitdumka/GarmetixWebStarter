import { getStoredToken } from '@garmetix/shared-auth'

export default defineNuxtRouteMiddleware((to) => {
  if (!import.meta.client || to.path === '/login') return
  const token = getStoredToken(window.localStorage)
  if (!token) return navigateTo('/login')
})
