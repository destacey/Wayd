import { theme } from 'antd'
import { themeBalham } from 'ag-grid-community'
import { useMemo } from 'react'
import { ThemeConstants } from './theme-constants'
import { AppThemeConfig, TimeLineStyles } from './theme-preset'
const { defaultAlgorithm } = theme

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
      timeline: lightTimeLineColors,
      integrations: {
        agGridTheme: themeBalham,
        antDesignChartsTheme: 'classic',
        antvisG6ChartsTheme: 'light',
      },
    }),
    [],
  )

const lightTheme = useLightThemePreset

export default lightTheme

