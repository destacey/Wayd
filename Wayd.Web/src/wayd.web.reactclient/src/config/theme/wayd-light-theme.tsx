import { theme } from 'antd'
import { useMemo } from 'react'
import { ThemeConstants } from './theme-constants'
import { AppThemeConfig } from './theme-preset'
const { defaultAlgorithm } = theme

/** Wayd theme — light mode. */
export const useWaydLightTheme = (): AppThemeConfig =>
  useMemo(
    () => ({
      configProvider: {
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
      },
      behavior: {
        allowsPrimaryOverride: true,
      },
      appBar: {
        backgroundColor: 'var(--ant-color-primary)',
        color: '#ffffff',
        subtleColor: 'rgba(255, 255, 255, 0.88)',
      },
      integrations: {
        antDesignChartsTheme: 'classic',
        antvisG6ChartsTheme: 'light',
      },
    }),
    [],
  )

export default useWaydLightTheme

