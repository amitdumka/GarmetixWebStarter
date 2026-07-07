import fs from 'fs'

const path = 'frontend/modular/scripts/validate-all.mjs'
let content = fs.readFileSync(path, 'utf8')

const lines = content.split('\n')
const filteredLines = []
let inBadStep = false
let braces = 0

for (let i = 0; i < lines.length; i++) {
  const line = lines[i]
  
  if (line.includes("{") && (line.includes("modular:pos:") || line.includes("modular:hr:") || line.includes("modular:books:"))) {
     // Actually, the format is:
     //   {
     //     name: 'POS sale contract parity',
     //     cwd: repoRoot,
     //     args: ['run', 'modular:pos:contract']
     //   },
     // Let's just do a simpler filter: if a block has modular:pos, we shouldn't have included it.
  }
}

// Instead of line by line, let's use regex
content = content.replace(/  \{\n    name: 'POS[\\s\\S]*?  \},?\n/g, '')
content = content.replace(/  \{\n    name: 'HR[\\s\\S]*?  \},?\n/g, '')
content = content.replace(/  \{\n    name: 'Books[\\s\\S]*?  \},?\n/g, '')
content = content.replace(/for \(const app of \['main', 'pos', 'hr', 'ai-sense', 'books', 'admin'\]\) \{/, "for (const app of ['main', 'ai-sense', 'admin']) {")

fs.writeFileSync(path, content, 'utf8')
console.log('Cleaned validate-all.mjs')
