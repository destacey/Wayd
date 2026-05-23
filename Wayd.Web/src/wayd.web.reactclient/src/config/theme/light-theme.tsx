import { theme } from 'antd'
import { useMemo } from 'react'
import { ThemeConstants } from './theme-constants'
import { ThemePreset } from './theme-preset'
const { defaultAlgorithm } = theme

export const useLightThemePreset = (): ThemePreset =>
  useMemo(
    () => ({
      theme: {
        algorithm: defaultAlgorithm,
        token: {
          colorPrimary: ThemeConstants.COLOR_PRIMARY,
          borderRadius: 4,
          wireframe: false,
        },
        components: {
          Layout: {
            headerBg: ThemeConstants.COLOR_PRIMARY,
          },
          Tabs: {
            colorBorderSecondary: '#d9d9d9',
          },
        },
      },
    }),
    [],
  )

const lightTheme = useLightThemePreset

export default lightTheme
