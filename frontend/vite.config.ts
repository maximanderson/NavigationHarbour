import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'

// Build-time product identity (../product.json). Nothing is read at runtime: the values are inlined into the
// bundle (__PRODUCT_NAME__, __PRODUCT_TECHNICAL_NAME__) and into index.html. A missing or invalid file stops the build.
function readProduct(): { displayName: string; technicalName: string; description: string } {
  const file = fileURLToPath(new URL('../product.json', import.meta.url))
  const product = JSON.parse(readFileSync(file, 'utf8')) as Record<string, unknown>
  for (const key of ['displayName', 'technicalName', 'tagline', 'description']) {
    if (typeof product[key] !== 'string' || (product[key] as string).trim() === '') {
      throw new Error(`product.json: '${key}' is missing or empty.`)
    }
  }
  if (!/^[A-Za-z][A-Za-z0-9]{0,39}$/.test(product.technicalName as string)) {
    throw new Error("product.json: 'technicalName' must start with a letter and contain only letters and digits (maximum 40 characters).")
  }
  return product as { displayName: string; technicalName: string; description: string }
}

const product = readProduct()

const escapeHtml = (value: string) =>
  value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')

function productHtml(): Plugin {
  return {
    name: 'product-html',
    transformIndexHtml: (html) =>
      html
        .replaceAll('%PRODUCT_NAME%', escapeHtml(product.displayName))
        .replaceAll('%PRODUCT_DESCRIPTION%', escapeHtml(product.description)),
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), productHtml()],
  define: {
    __PRODUCT_NAME__: JSON.stringify(product.displayName),
    __PRODUCT_TECHNICAL_NAME__: JSON.stringify(product.technicalName),
  },
})
