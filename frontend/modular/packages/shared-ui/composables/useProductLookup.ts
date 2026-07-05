export type ProductLookupItem = {
  productId: string
  stockId?: string
  name: string
  barcode: string
  hsnCode?: string
  availableQty: number
  mrp: number
  taxRate: number
  taxType: string
  unit: string
  category: string
  subCategory: string
  taxId?: string
  productCategoryId?: string
  productSubCategoryId?: string
}

export function useProductLookup() {
  const api = useGarmetixApi()
  const cacheKey = 'garmetix.productLookup.cache.v1'
  const cachedProducts = ref<ProductLookupItem[]>([])
  const loadingLookup = ref(false)

  function loadCache() {
    if (!import.meta.client) return
    try {
      cachedProducts.value = JSON.parse(localStorage.getItem(cacheKey) || '[]')
    } catch {
      cachedProducts.value = []
    }
  }

  function saveCache(items: ProductLookupItem[]) {
    cachedProducts.value = mergeProducts(items, cachedProducts.value).slice(0, 500)
    if (import.meta.client) {
      localStorage.setItem(cacheKey, JSON.stringify(cachedProducts.value))
    }
  }

  async function searchProducts(query: string, storeId?: string) {
    const term = query.trim()
    if (!term) {
      return cachedProducts.value.slice(0, 25)
    }

    loadingLookup.value = true
    try {
      const serverRows = await api.get<ProductLookupItem[]>(`product-lookup?query=${encodeURIComponent(term)}${storeId ? `&storeId=${storeId}` : ''}&take=25`)
      saveCache(serverRows)
      return serverRows
    } catch {
      return cachedProducts.value.filter((item) => [item.name, item.barcode, item.hsnCode, item.category].join(' ').toLowerCase().includes(term.toLowerCase())).slice(0, 25)
    } finally {
      loadingLookup.value = false
    }
  }

  async function byBarcode(barcode: string, storeId?: string) {
    const code = barcode.trim()
    if (!code) return null
    try {
      const item = await api.get<ProductLookupItem>(`product-lookup/barcode/${encodeURIComponent(code)}${storeId ? `?storeId=${storeId}` : ''}`)
      saveCache([item])
      return item
    } catch {
      return cachedProducts.value.find((item) => item.barcode.toLowerCase() === code.toLowerCase()) || null
    }
  }

  function mergeProducts(primary: ProductLookupItem[], secondary: ProductLookupItem[]) {
    const map = new Map<string, ProductLookupItem>()
    for (const item of [...primary, ...secondary]) {
      map.set(`${item.productId}:${item.barcode}`, item)
    }
    return Array.from(map.values())
  }

  if (import.meta.client) {
    loadCache()
  }

  return { cachedProducts, loadingLookup, loadCache, saveCache, searchProducts, byBarcode }
}
