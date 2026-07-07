import fs from 'fs'

const path = 'frontend/modular/config/routes.ts'
let content = fs.readFileSync(path, 'utf8')

// Remove routeRoles
content = content.replace(/  pos: \['SuperAdmin', 'Owner', 'Admin', 'StoreManager', 'Cashier', 'Salesman'\],\n/, '')
content = content.replace(/  hr: \['SuperAdmin', 'Owner', 'Admin', 'PowerUser', 'HrManager'\],\n/, '')
content = content.replace(/  books: \['SuperAdmin', 'Owner', 'Admin', 'Accountant', 'CA'\],\n/, '')

// Remove routeModules
content = content.replace(/  pos: \{ label: 'POS', icon: 'i-lucide-scan-barcode', app: 'pos' \},\n/, '')
content = content.replace(/  accounting: \{ label: 'Accounting', icon: 'i-lucide-book-open-check', app: 'books' \},\n/, '')
content = content.replace(/  gst: \{ label: 'GST', icon: 'i-lucide-file-check-2', app: 'books' \},\n/, '')
content = content.replace(/  offBook: \{ label: 'Off Book', icon: 'i-lucide-wallet-cards', app: 'pos' \},\n/, '')
content = content.replace(/  hr: \{ label: 'HR And Payroll', icon: 'i-lucide-user-round-check', app: 'hr' \},\n/, '')

// Remove routes where targetApp is pos, hr, or books
const lines = content.split('\n')
const filteredLines = lines.filter(line => {
  if (line.includes("targetApp: 'pos'")) return false
  if (line.includes("targetApp: 'hr'")) return false
  if (line.includes("targetApp: 'books'")) return false
  return true
})

content = filteredLines.join('\n')

// Remove appLabels
content = content.replace(/    pos: 'POS',\n/, '')
content = content.replace(/    hr: 'HR',\n/, '')
content = content.replace(/    books: 'Books',\n/, '')

fs.writeFileSync(path, content, 'utf8')
console.log('Cleaned routes.ts')
