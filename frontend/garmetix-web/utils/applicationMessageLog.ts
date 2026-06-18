type ClientMessageLogEntry = {
  level: string
  eventName: string
  message: string
  detailsJson?: string
  resource?: string
  success: boolean
}

export async function persistClientMessageLog(entry: ClientMessageLogEntry) {
  if (!import.meta.client) {
    return
  }

  const token = localStorage.getItem('garmetix.token')
  if (!token) {
    return
  }

  const config = useRuntimeConfig()
  try {
    await $fetch(`${config.public.apiBase}/message-logs/client`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
      body: entry
    })
  } catch {
    // Logging must never create another user-facing error or recursive log request.
  }
}

export function browserErrorDetails(error: unknown, extra?: Record<string, unknown>) {
  const value = error as any
  return redactSensitiveDetails(JSON.stringify({
    name: value?.name,
    message: value?.message || String(error || ''),
    stack: value?.stack,
    ...extra
  }, null, 2))
}

export function redactSensitiveDetails(value: string) {
  return String(value || '')
    .replace(/(Authorization:\s*Bearer\s+)[^\s"\\]+/gi, '$1[hidden]')
    .replace(/(Bearer\s+)[A-Za-z0-9._~+/=-]+/gi, '$1[hidden]')
    .replace(/("(?:password|token|secret|authorization|apiKey|api_key)"\s*:\s*)"[^"]*"/gi, '$1"[hidden]"')
}
