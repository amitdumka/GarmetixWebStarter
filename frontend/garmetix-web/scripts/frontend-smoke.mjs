#!/usr/bin/env node

const baseUrl = (process.env.GARMETIX_WEB_BASE_URL || process.env.PUBLIC_FRONTEND_BASE_URL || 'http://127.0.0.1:3000').replace(/\/$/, '')
const expectedVersion = process.env.GARMETIX_EXPECTED_VERSION || '4.9.15'
const expectedBuildCode = process.env.GARMETIX_EXPECTED_BUILD_CODE || 'GARMETIX-8I-20260619-49150'

async function fetchText(path) {
  const response = await fetch(`${baseUrl}${path}`, { redirect: 'manual' })
  const text = await response.text()
  return { response, text }
}

async function fetchJson(path) {
  const response = await fetch(`${baseUrl}${path}`, { redirect: 'manual' })
  const text = await response.text()
  let data
  try {
    data = JSON.parse(text)
  } catch (error) {
    throw new Error(`${path} did not return JSON. HTTP ${response.status}. Body: ${text.slice(0, 240)}`)
  }
  return { response, data }
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message)
  }
}

console.log(`Garmetix frontend smoke test: ${baseUrl}`)

const root = await fetchText('/')
assert(root.response.status >= 200 && root.response.status < 400, `Root page returned HTTP ${root.response.status}`)
assert(/Garmetix/i.test(root.text), 'Root page does not contain Garmetix branding')
assert(/Login|Welcome back|auth/i.test(root.text), 'Root page does not look like the login/auth page')
console.log('✓ Root/login page responds')

const appInfo = await fetchJson('/api/app-info/version')
assert(appInfo.response.status >= 200 && appInfo.response.status < 400, `/api/app-info/version returned HTTP ${appInfo.response.status}`)
assert(appInfo.data.version === expectedVersion, `Expected version ${expectedVersion}, got ${appInfo.data.version}`)
assert(appInfo.data.buildCode === expectedBuildCode, `Expected build code ${expectedBuildCode}, got ${appInfo.data.buildCode}`)
console.log(`✓ App info version ${appInfo.data.version} / ${appInfo.data.buildCode}`)

const testManifest = await fetchJson('/api/test-automation/manifest')
assert(testManifest.response.status >= 200 && testManifest.response.status < 400, `/api/test-automation/manifest returned HTTP ${testManifest.response.status}`)
assert(Array.isArray(testManifest.data.checks), 'Test automation manifest is missing checks array')
assert(testManifest.data.checks.some((item) => item.code === 'FRONTEND_SMOKE' || item.Code === 'FRONTEND_SMOKE'), 'Manifest does not include FRONTEND_SMOKE')
console.log(`✓ Test automation manifest exposes ${testManifest.data.checks.length} checks`)

console.log('Frontend smoke test completed.')
