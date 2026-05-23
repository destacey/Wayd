import { ConfigProviderProps, ThemeConfig } from 'antd'

export type ThemeProviderOverrides = Pick<
  ConfigProviderProps,
  'modal' | 'popover' | 'progress' | 'colorPicker'
>

export interface ThemePreset {
  theme: ThemeConfig
  providerOverrides?: ThemeProviderOverrides
}
