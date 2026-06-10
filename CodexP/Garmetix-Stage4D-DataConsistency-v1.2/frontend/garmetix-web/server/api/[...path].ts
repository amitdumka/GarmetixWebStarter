import { getRequestURL, getRouterParam, proxyRequest } from 'h3'

export default defineEventHandler((event) => {
  const config = useRuntimeConfig()
  const backendBase = String(config.apiInternalBase || 'http://localhost:5080/api').replace(/\/+$/, '')
  const path = getRouterParam(event, 'path') || ''
  const query = getRequestURL(event).search

  return proxyRequest(event, `${backendBase}/${path}${query}`)
})
