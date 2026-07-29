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
            // Cool-grey neutrals (~213°, matching dark/slate) instead of the
            // stock pure-neutral greys. The canvas tint separates white cards
            // from the page; containers stay pure white on purpose — tinting
            // white surfaces reads as dirty. Borders are a step stronger than
            // stock so grid lines and dividers survive projectors.
            colorBgLayout: '#f2f4f8',
            colorBorder: '#ccd3db',
            colorBorderSecondary: '#e2e6eb',
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

