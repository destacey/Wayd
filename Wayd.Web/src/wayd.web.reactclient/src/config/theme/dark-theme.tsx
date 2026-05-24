import { theme } from 'antd'
import { colorSchemeDark, themeBalham } from 'ag-grid-community'
import { useMemo } from 'react'
import { AppThemeConfig, TimeLineStyles } from './theme-preset'
const { darkAlgorithm } = theme

export const darkTimeLineColors: TimeLineStyles = {
  item: {
    background: '#303030',
    foreground: '#17354d',
    font: '#FFFFFF',
  },
  background: {
    background: '#61646e',
  },
}

const agGridDarkTheme = themeBalham.withPart(colorSchemeDark)

export const useDarkThemePreset = (): AppThemeConfig =>
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
      timeline: darkTimeLineColors,
      integrations: {
        agGridTheme: agGridDarkTheme,
        antDesignChartsTheme: 'classicDark',
        antvisG6ChartsTheme: 'dark',
      },
    }),
    [],
  )

const darkTheme = useDarkThemePreset

export default darkTheme

