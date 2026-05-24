import { theme } from 'antd'
import { createPart, themeBalham } from 'ag-grid-community'
import { useMemo } from 'react'
import { AppThemeConfig, TimeLineStyles } from './theme-preset'
const { darkAlgorithm } = theme

export const slateTimeLineColors: TimeLineStyles = {
  item: {
    background: '#c0c0c0',
    foreground: '#17354d',
    font: '#f0f0f0',
  },
  background: {
    background: '#787878',
  },
}

const agGridGreyTheme = themeBalham.withPart(
  createPart({
    feature: 'colorScheme',
    params: {
      backgroundColor: '#2d2d2d',
      foregroundColor: '#e0e0e0',
      browserColorScheme: 'dark',
    },
  }),
).withParams({
  borderRadius: 4,
})

export const useSlateThemePreset = (): AppThemeConfig =>
  useMemo(
    () => ({
      configProvider: {
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
      },
      behavior: {
        allowsPrimaryOverride: true,
      },
      timeline: slateTimeLineColors,
      appBar: {
        backgroundColor: 'var(--ant-color-primary)',
        color: '#ffffff',
        subtleColor: 'rgba(255, 255, 255, 0.88)',
      },
      integrations: {
        agGridTheme: agGridGreyTheme,
        antDesignChartsTheme: 'classicDark',
        antvisG6ChartsTheme: 'dark',
      },
    }),
    [],
  )

const slateTheme = useSlateThemePreset

export default slateTheme

