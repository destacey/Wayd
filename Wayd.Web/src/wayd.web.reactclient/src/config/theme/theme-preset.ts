import { ConfigProviderProps, ThemeConfig } from 'antd'
import { Theme as AgGridTheme } from 'ag-grid-community'

export type ThemeProviderOverrides = Pick<
  ConfigProviderProps,
  'modal' | 'popover' | 'progress' | 'colorPicker'
>

export interface ThemePreset {
  theme: ThemeConfig
  providerOverrides?: ThemeProviderOverrides
}

export type TimeLineStyles = {
  item: {
    background: string
    foreground: string
    font: string
  }
  background: {
    background: string
  }
}

export interface AppThemeConfig {
  configProvider: ConfigProviderProps
  timeline: TimeLineStyles
  integrations: {
    agGridTheme: AgGridTheme
    antDesignChartsTheme: 'classic' | 'classicDark'
    antvisG6ChartsTheme: 'light' | 'dark'
  }
}

