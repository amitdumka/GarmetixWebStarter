export function useServerDocumentPrint() {
  const api = useGarmetixApi()
  const config = useRuntimeConfig()

  async function fetchPdf(path: string) {
    const response = await fetch(`${config.public.apiBase}/${path.replace(/^\/+/, '')}`, {
      headers: api.authHeaders()
    })
    if (!response.ok) {
      const detail = await response.text()
      throw new Error(detail || `PDF generation failed (${response.status}).`)
    }

    const blob = await response.blob()
    if (blob.type !== 'application/pdf' || blob.size < 100) {
      throw new Error('The server did not return a valid PDF document.')
    }
    return blob
  }

  async function printPdf(path: string) {
    const blob = await fetchPdf(path)
    const url = URL.createObjectURL(blob)
    const frame = document.createElement('iframe')
    frame.title = 'Garmetix PDF print document'
    frame.style.position = 'fixed'
    frame.style.right = '0'
    frame.style.bottom = '0'
    frame.style.width = '1px'
    frame.style.height = '1px'
    frame.style.border = '0'
    frame.style.opacity = '0'
    document.body.appendChild(frame)

    const cleanup = () => {
      frame.remove()
      URL.revokeObjectURL(url)
    }
    frame.onload = () => {
      window.setTimeout(() => {
        try {
          frame.contentWindow?.focus()
          frame.contentWindow?.print()
        } finally {
          window.setTimeout(cleanup, 30_000)
        }
      }, 500)
    }
    frame.src = url
  }

  async function downloadPdf(path: string, fileName: string) {
    const blob = await fetchPdf(path)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
  }

  return { fetchPdf, printPdf, downloadPdf }
}
