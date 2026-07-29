import { theme } from 'antd'
import { useMemo } from 'react'
import { AppThemeConfig } from './theme-preset'
const { darkAlgorithm } = theme

/** Wayd theme — dark mode. */
export const useWaydDarkTheme = (): AppThemeConfig =>
  useMemo(
    () => ({
      configProvider: {
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
      },
      behavior: {
        allowsPrimaryOverride: true,
      },
      appBar: {
        backgroundColor: '#313131',
        color: '#ffffff',
        subtleColor: 'rgba(255, 255, 255, 0.88)',
      },
      integrations: {
        antDesignChartsTheme: 'classicDark',
        antvisG6ChartsTheme: 'dark',
      },
    }),
    [],
  )

export default useWaydDarkTheme

