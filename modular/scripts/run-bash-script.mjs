import { existsSync } from 'node:fs'
import { isAbsolute, resolve } from 'node:path'
import { spawnSync } from 'node:child_process'

const [scriptArg, ...args] = process.argv.slice(2)

if (!scriptArg) {
  console.error('Usage: node scripts/run-bash-script.mjs <script> [args...]')
  process.exit(1)
}

const scriptPath = isAbsolute(scriptArg) ? scriptArg : resolve(process.cwd(), scriptArg)
if (!existsSync(scriptPath)) {
  console.error(`Script not found: ${scriptPath}`)
  process.exit(1)
}

const bashCandidates = [
  process.env.GARMETIX_BASH_PATH,
  'C:\\Program Files\\Git\\bin\\bash.exe',
  'C:\\Program Files\\Git\\usr\\bin\\bash.exe',
  '/bin/bash',
  '/usr/bin/bash',
  'bash'
].filter(Boolean)

let lastError = ''
for (const bashPath of bashCandidates) {
  if (bashPath.includes('\\') && !existsSync(bashPath)) continue

  const result = spawnSync(bashPath, [scriptPath, ...args], {
    stdio: 'inherit',
    shell: false,
    windowsHide: true
  })

  if (result.error) {
    lastError = result.error.message
    continue
  }

  process.exit(result.status ?? 0)
}

console.error(`Could not find a working bash runtime. Last error: ${lastError || 'none'}`)
console.error('On Windows, install Git Bash or set GARMETIX_BASH_PATH to bash.exe.')
process.exit(1)
