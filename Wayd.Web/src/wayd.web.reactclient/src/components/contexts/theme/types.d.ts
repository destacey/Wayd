import { GlobalToken } from 'antd'
import { UserThemeConfigDto } from '@/src/services/wayd-api'

export type ThemeName =
  | 'light'
  | 'dark'
  | 'slate'
  | 'cartoon'
  | 'shadcn'
  | 'glass'
  | 'geek'
  | 'illustration'

export type { UserThemeConfigDto }

export interface ThemeContextType {
  currentThemeName: ThemeName
  setCurrentThemeName: (themeName: ThemeName) => void
  appBar: {
    backgroundColor: string
    color: string
    subtleColor?: string
  }
  allowsPrimaryOverride: boolean
  token: GlobalToken
  badgeColor: string
  defaultPrimaryColor: string
  antDesignChartsTheme: string
  antvisG6ChartsTheme: string
  userThemeConfig: UserThemeConfigDto | null
  setUserThemeConfig: (config: UserThemeConfigDto | null) => void
}
