import { type Theme as AgGridTheme } from 'ag-grid-community'
import { GlobalToken } from 'antd'
import { UserThemeConfigDto } from '@/src/services/wayd-api'

export type ThemeName = 'light' | 'dark' | 'slate' | 'cartoon' | 'shadcn' | 'glass' | 'geek'

export type { UserThemeConfigDto }

export interface ThemeContextType {
  currentThemeName: ThemeName
  setCurrentThemeName: (themeName: ThemeName) => void
  agGridTheme: AgGridTheme
  token: GlobalToken
  badgeColor: string
  antDesignChartsTheme: string
  antvisG6ChartsTheme: string
  userThemeConfig: UserThemeConfigDto | null
  setUserThemeConfig: (config: UserThemeConfigDto | null) => void
}
