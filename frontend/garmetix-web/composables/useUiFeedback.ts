type ToastColor = 'success' | 'error' | 'warning' | 'info' | 'neutral'

export function useUiFeedback() {
  const toast = useToast()

  function notify(title: string, description?: string, color: ToastColor = 'success') {
    toast.add({
      title,
      description,
      color
    })
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
    const detail = (error as any)?.data?.message || (error as any)?.data?.title || (error as any)?.message
    notify(title, detail || 'Please check the API and try again.', 'error')
  }

  return {
    notify,
    saved,
    updated,
    deleted,
    failed
  }
}
