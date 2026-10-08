// Serves the production web build with the exact headers render.yaml sets, so the CSP can be
// checked before it ships. The API host in the policy is swapped for the local API.
// Usage: node serve-with-render-headers.mjs <path-to-web/dist> [port]
import { createServer } from 'node:http'
import { readFileSync, existsSync, statSync } from 'node:fs'
import { extname, join } from 'node:path'
import { gzipSync } from 'node:zlib'

const dist = process.argv[2]
const port = Number(process.argv[3] ?? 4173)
const yaml = readFileSync(new URL('../../../render.yaml', import.meta.url), 'utf8')
const headers = [...yaml.matchAll(/- path: \/\*\n\s+name: (.+)\n\s+value: (.+)/g)].map(([, name, value]) => [
  name.trim(),
  value.trim().replace(/^"|"$/g, '').replaceAll('https://foundu-api.onrender.com', 'http://localhost:5292'),
])
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.woff2': 'font/woff2', '.ico': 'image/x-icon', '.webmanifest': 'application/manifest+json' }

createServer((req, res) => {
  let file = join(dist, decodeURIComponent(req.url.split('?')[0]))
  if (!existsSync(file) || statSync(file).isDirectory()) file = join(dist, 'index.html') // SPA rewrite
  for (const [name, value] of headers) res.setHeader(name, value)
  res.setHeader('Content-Type', types[extname(file)] ?? 'application/octet-stream')
  // Compressed like Render's CDN, so performance measured here is comparable.
  const body = readFileSync(file)
  if (/gzip/.test(req.headers['accept-encoding'] ?? '') && /\.(js|css|html|svg)$|^[^.]*$/.test(file)) {
    res.setHeader('Content-Encoding', 'gzip')
    return res.end(gzipSync(body))
  }
  res.end(body)
}).listen(port, '127.0.0.1', () => console.log(`serving ${dist} on http://127.0.0.1:${port} with ${headers.length} headers`))
