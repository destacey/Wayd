import { ConfigProviderProps, ThemeConfig } from 'antd'

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
  behavior: {
    allowsPrimaryOverride: boolean
  }
  timeline: TimeLineStyles
  appBar: {
    backgroundColor: string
    color: string
    subtleColor?: string
  }
  integrations: {
    antDesignChartsTheme: 'classic' | 'classicDark'
    antvisG6ChartsTheme: 'light' | 'dark'
  }
}

