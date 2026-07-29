import { GlobalToken } from 'antd'
import { UserThemeConfigDto } from '@/src/services/wayd-api'

/** A visual identity: shape language, typography, surface treatments. */
export type ThemeId =
  | 'wayd'
  | 'cartoon'
  | 'shadcn'
  | 'glass'
  | 'geek'
  | 'illustration'

/**
 * A color-scheme variant of a theme. Every theme supports at least one mode;
 * `slate` is the Wayd theme's alternate dark mode.
 */
export type ThemeMode = 'light' | 'dark' | 'slate'

/** The persisted theme selection (localStorage `appTheme` key). */
export interface ThemeSelection {
  theme: ThemeId
  mode: ThemeMode
}

export type { UserThemeConfigDto }

export interface ThemeContextType {
  currentTheme: ThemeId
  currentMode: ThemeMode
  /** Modes supported by the current theme. */
  availableModes: ThemeMode[]
  /** Switch theme; keeps the current mode when the new theme supports it. */
  setCurrentTheme: (theme: ThemeId) => void
  /** Switch mode within the current theme; ignored if unsupported. */
  setCurrentMode: (mode: ThemeMode) => void
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
