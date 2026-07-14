export type GstReviewShareTarget = {
  toEmail: string
  toName?: string
  whatsAppNumber?: string
  note?: string
}

export type GstReviewShareLog = {
  id: string
  kind: string
  returnPeriod: string
  toEmail: string
  toName?: string
  attachmentNames: string[]
  sentAt: string
  message?: string
}

const CONTACT_KEY = 'garmetix:gst-review-contact:v1'
const LOG_KEY = 'garmetix:gst-review-share-log:v1'

const contactState = reactive({
  toEmail: '',
  toName: '',
  whatsAppNumber: '',
  note: 'Please review the attached GST return and book reports.'
})

const shareLogs = ref<GstReviewShareLog[]>([])
const loaded = ref(false)

function canUseStorage() {
  return typeof window !== 'undefined' && Boolean(window.localStorage)
}

function normalizeMobile(value: string | undefined | null) {
  return String(value || '').replace(/[^0-9]/g, '')
}

export function useGstReviewContact() {
  function load() {
    if (!canUseStorage() || loaded.value) return
    loaded.value = true

    try {
      const raw = window.localStorage.getItem(CONTACT_KEY)
      if (raw) {
        const saved = JSON.parse(raw)
        contactState.toEmail = saved.toEmail || ''
        contactState.toName = saved.toName || ''
        contactState.whatsAppNumber = normalizeMobile(saved.whatsAppNumber)
        contactState.note = saved.note || contactState.note
      }
    } catch {
      // Ignore bad browser cache and let user save again.
    }

    try {
      const rawLogs = window.localStorage.getItem(LOG_KEY)
      shareLogs.value = rawLogs ? JSON.parse(rawLogs).slice(0, 25) : []
    } catch {
      shareLogs.value = []
    }
  }

  function save(target: GstReviewShareTarget) {
    contactState.toEmail = String(target.toEmail || '').trim()
    contactState.toName = String(target.toName || '').trim()
    contactState.whatsAppNumber = normalizeMobile(target.whatsAppNumber)
    contactState.note = String(target.note || contactState.note || '').trim()

    if (!canUseStorage()) return

    window.localStorage.setItem(CONTACT_KEY, JSON.stringify({
      toEmail: contactState.toEmail,
      toName: contactState.toName,
      whatsAppNumber: contactState.whatsAppNumber,
      note: contactState.note
    }))
  }

  function applyTo(target: GstReviewShareTarget) {
    load()
    if (!target.toEmail && contactState.toEmail) target.toEmail = contactState.toEmail
    if (!target.toName && contactState.toName) target.toName = contactState.toName
    if (!target.whatsAppNumber && contactState.whatsAppNumber) target.whatsAppNumber = contactState.whatsAppNumber
    if ((!target.note || target.note === 'Please review the attached GST return and book reports.' || target.note === 'Please review the attached GST book reports.') && contactState.note) {
      target.note = contactState.note
    }
  }

  function addLog(input: Omit<GstReviewShareLog, 'id' | 'sentAt'>) {
    load()
    const row: GstReviewShareLog = {
      ...input,
      id: `${Date.now()}-${Math.random().toString(16).slice(2)}`,
      sentAt: new Date().toISOString(),
      attachmentNames: input.attachmentNames || []
    }
    shareLogs.value = [row, ...shareLogs.value].slice(0, 25)

    if (canUseStorage()) {
      window.localStorage.setItem(LOG_KEY, JSON.stringify(shareLogs.value))
    }
  }

  return {
    contact: contactState,
    shareLogs,
    isConfigured: computed(() => Boolean(contactState.toEmail)),
    load,
    save,
    applyTo,
    addLog
  }
}
