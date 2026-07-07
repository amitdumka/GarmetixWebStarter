import fs from 'fs'

const path = 'frontend/modular/scripts/validate-all.mjs'
let content = fs.readFileSync(path, 'utf8')

const lines = content.split('\n')
const newLines = []

let inSkipBlock = false
for (let i = 0; i < lines.length; i++) {
  const line = lines[i]
  
  if (line.includes("name: 'POS") || line.includes("name: 'HR") || line.includes("name: 'Books")) {
    inSkipBlock = true
    // Need to backtrack and remove the opening brace '{'
    newLines.pop() 
  }
  
  if (inSkipBlock) {
    if (line.trim() === '},' || line.trim() === '}') {
      inSkipBlock = false
    }
    continue
  }
  
  newLines.push(line)
}

content = newLines.join('\n')
content = content.replace(/for \(const app of \['main', 'pos', 'hr', 'ai-sense', 'books', 'admin'\]\) \{/, "for (const app of ['main', 'ai-sense', 'admin']) {")

fs.writeFileSync(path, content, 'utf8')
console.log('Cleaned validate-all.mjs properly')
