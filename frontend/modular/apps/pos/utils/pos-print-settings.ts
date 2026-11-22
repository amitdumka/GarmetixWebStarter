import { ref } from 'vue'

export type PosPrintFormat = 'a4' | 'a5' | 'thermal-2' | 'thermal-3'

export const POS_PRINT_FORMAT_STORAGE_KEY = 'garmetix.pos.printFormat'

// thermal-2 = 2 inch / 58 mm roll, thermal-3 = 3 inch / 80 mm roll (the API renders both natively).
export const posPrintFormatOptions: Array<{ label: string, value: PosPrintFormat }> = [
  { label: 'Normal printer (A4)', value: 'a4' },
  { label: 'Half page (A5)', value: 'a5' },
  { label: 'Thermal 58 mm (2 inch)', value: 'thermal-2' },
  { label: 'Thermal 80 mm (3 inch)', value: 'thermal-3' }
]

const validFormats = new Set<string>(posPrintFormatOptions.map(item => item.value))

function readStoredFormat(): PosPrintFormat {
  try {
    const stored = typeof window !== 'undefined' ? window.localStorage.getItem(POS_PRINT_FORMAT_STORAGE_KEY) : null
    if (stored && validFormats.has(stored)) return stored as PosPrintFormat
  } catch {
    // Storage can be blocked (private mode); fall back to the default layout.
  }
  return 'a4'
}

/** Last print layout chosen on this device (browser). Used whenever a page does not ask for a specific one. */
export function getSavedPrintFormat(): PosPrintFormat {
  return readStoredFormat()
}

export function savePrintFormat(format: PosPrintFormat) {
  if (!validFormats.has(format)) return
  try {
    window.localStorage.setItem(POS_PRINT_FORMAT_STORAGE_KEY, format)
  } catch {
    // Ignore: the choice still applies for this page session through the shared ref.
  }
}

// One shared ref so the picker on every page stays in sync within a session.
const sharedFormat = ref<PosPrintFormat>('a4')
let hydrated = false

export function usePosPrintFormat() {
  if (!hydrated && typeof window !== 'undefined') {
    sharedFormat.value = readStoredFormat()
    hydrated = true
  }
  return sharedFormat
}

export function setPosPrintFormat(format: PosPrintFormat) {
  sharedFormat.value = format
  savePrintFormat(format)
}
