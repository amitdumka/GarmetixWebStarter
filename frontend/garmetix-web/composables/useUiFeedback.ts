type ToastColor = 'success' | 'error' | 'warning' | 'info' | 'neutral'

type UiLogEntry = {
  id: string
  at: string
  title: string
  message: string
  details?: string
  color: ToastColor
}

export function useUiFeedback() {
  const toast = useToast()
  const logs = useState<UiLogEntry[]>('garmetix.ui.logs', () => [])

  function notify(title: string, description?: string, color: ToastColor = 'success') {
    const cleanTitle = sanitizeMessage(title)
    const cleanDescription = description ? sanitizeMessage(description) : undefined

    toast.add({
      title: cleanTitle,
      description: cleanDescription,
      color
    })

    addLog(cleanTitle, cleanDescription || '', undefined, color)
  }

  function saved(entity = 'Record') {
    notify(`${entity} saved`)
  }

  function updated(entity = 'Record') {
    notify(`${entity} updated`)
  }

  function deleted(entity = 'Record') {
    notify(`${entity} deleted`, undefined, 'warning')
  }

  function failed(title = 'Action failed', error?: unknown) {
    const parsed = parseError(error)
    const message = parsed.message || 'Please check the details in the message log.'

    toast.add({
      title: sanitizeMessage(title),
      description: sanitizeMessage(message),
      color: 'error'
    })

    addLog(title, message, parsed.details, 'error')
  }

  function errorMessage(error: unknown, fallback = 'Action failed.') {
    return sanitizeMessage(parseError(error).message || fallback)
  }

  function cleanMessage(value: string) {
    return sanitizeMessage(value)
  }

  function addLog(title: string, message: string, details?: string, color: ToastColor = 'info') {
    logs.value = [
      {
        id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
        at: new Date().toISOString(),
        title: sanitizeMessage(title),
        message: sanitizeMessage(message),
        details: details ? sanitizeDetails(details) : undefined,
        color
      },
      ...logs.value
    ].slice(0, 100)
  }

  function clearLogs() {
    logs.value = []
  }

  function parseError(error?: unknown) {
    const raw = error as any
    const statusCode = raw?.statusCode || raw?.status || raw?.response?.status
    const data = raw?.data || raw?.response?._data
    const message =
      data?.message ||
      data?.title ||
      raw?.statusMessage ||
      raw?.message ||
      (statusCode ? `Request failed with status ${statusCode}.` : '')

    const details = safeStringify({
      statusCode,
      statusMessage: raw?.statusMessage,
      message: raw?.message,
      data
    })

    return {
      message: statusCode && message && !String(message).includes(String(statusCode))
        ? `${message} (${statusCode})`
        : String(message || ''),
      details
    }
  }

  function sanitizeMessage(value: string) {
    return String(value || '')
      .replace(/https?:\/\/[^\s"'<>),]+/gi, 'server')
      .replace(/\blocalhost:\d+\b/gi, 'server')
      .replace(/\b127\.0\.0\.1:\d+\b/gi, 'server')
      .replace(/\[([A-Z]+)\]\s+server\/api\/?/gi, '$1 request failed')
      .trim()
  }

  function sanitizeDetails(value: string) {
    return sanitizeMessage(value)
  }

  function safeStringify(value: unknown) {
    try {
      return JSON.stringify(value, null, 2)
    } catch {
      return String(value || '')
    }
  }

  return {
    logs,
    notify,
    saved,
    updated,
    deleted,
    failed,
    errorMessage,
    cleanMessage,
    addLog,
    clearLogs
  }
}
