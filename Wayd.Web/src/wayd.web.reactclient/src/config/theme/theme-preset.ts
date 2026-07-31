import { ConfigProviderProps } from 'antd'

export interface AppThemeConfig {
  configProvider: ConfigProviderProps
  behavior: {
    allowsPrimaryOverride: boolean
  }
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

