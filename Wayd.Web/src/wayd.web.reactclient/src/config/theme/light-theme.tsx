import { theme } from 'antd'
import { themeBalham } from 'ag-grid-community'
import { useMemo } from 'react'
import { ThemeConstants } from './theme-constants'
import { AppThemeConfig, TimeLineStyles } from './theme-preset'
const { defaultAlgorithm } = theme
const lightAgGridTheme = themeBalham.withParams({
  borderRadius: 4,
})

export const lightTimeLineColors: TimeLineStyles = {
  item: {
    background: '#ecf0f1',
    foreground: '#c7edff',
    font: '#4d4d4d',
  },
  background: {
    background: '#d0d3d4',
  },
}

export const useLightThemePreset = (): AppThemeConfig =>
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
      timeline: lightTimeLineColors,
      appBar: {
        backgroundColor: 'var(--ant-color-primary)',
        color: '#ffffff',
        subtleColor: 'rgba(255, 255, 255, 0.88)',
      },
      integrations: {
        agGridTheme: lightAgGridTheme,
        antDesignChartsTheme: 'classic',
        antvisG6ChartsTheme: 'light',
      },
    }),
    [],
  )

const lightTheme = useLightThemePreset

export default lightTheme

