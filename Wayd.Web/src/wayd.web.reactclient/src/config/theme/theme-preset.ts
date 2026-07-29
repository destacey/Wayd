import { ConfigProviderProps } from 'antd'

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

