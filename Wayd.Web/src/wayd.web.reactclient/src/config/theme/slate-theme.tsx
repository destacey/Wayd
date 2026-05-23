import { theme } from 'antd'
import { useMemo } from 'react'
import { ThemePreset } from './theme-preset'
const { darkAlgorithm } = theme

export const useSlateThemePreset = (): ThemePreset =>
  useMemo(
    () => ({
      theme: {
        algorithm: darkAlgorithm,
        token: {
          colorPrimary: '#2196f3',
          colorBgBase: '#2d2d2d',
          colorTextBase: '#f0f0f0',
          borderRadius: 4,
          wireframe: false,
        },
        components: {
          Layout: {
            headerBg: '#1e1e1e',
            triggerBg: '#1e1e1e',
            siderBg: '#252525',
          },
          Menu: {
            darkItemBg: '#252525',
            darkItemHoverBg: '#333333',
            darkPopupBg: '#252525',
            darkSubMenuItemBg: '#2a2a2a',
            darkItemColor: 'rgba(255, 255, 255, 0.88)',
            darkItemSelectedColor: '#ffffff',
          },
        },
      },
    }),
    [],
  )

const slateTheme = useSlateThemePreset

export default slateTheme
