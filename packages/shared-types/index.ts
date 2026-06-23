export type FrontendAppId = 'main-web' | 'pos-web' | 'hr-web' | 'ai-sense-web' | 'books-web' | 'saas'

export type RouteOwner = {
  appId: FrontendAppId
  path: string
  label: string
  module: string
  roles: string[]
}

export type SelectOption<TValue extends string = string> = {
  label: string
  value: TValue
}

