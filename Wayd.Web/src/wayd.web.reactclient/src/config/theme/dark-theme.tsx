import { theme } from 'antd'
import { useMemo } from 'react'
import { ThemePreset } from './theme-preset'
const { darkAlgorithm } = theme

export const useDarkThemePreset = (): ThemePreset =>
  useMemo(
    () => ({
      theme: {
        algorithm: darkAlgorithm,
        token: {
          colorPrimary: '#1f83d2',
          borderRadius: 4,
          wireframe: false,
        },
        components: {
          Layout: {
            headerBg: '#313131',
            triggerBg: '#313131',
            siderBg: '#1f1f1f',
          },
          Menu: {
            darkItemBg: '#1f1f1f',
            darkItemHoverBg: '#2e2e2e',
            darkPopupBg: '#1f1f1f',
            darkSubMenuItemBg: '#262626',
          },
        },
      },
    }),
    [],
  )

const darkTheme = useDarkThemePreset

export default darkTheme
