'use client'

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
            // Soft-dark ramp with a whisper of slate's cool cast (~215°) so
            // dark and slate read as siblings. Explicit elevation steps
            // replace the algorithm's pure-black layout / #141414 containers,
            // which read as harsh "night mode".
            colorBgBase: '#1a1c20',
            colorBgLayout: '#17191d',
            colorBgContainer: '#1f2226',
            colorBgElevated: '#282b31',
            colorBorder: '#3d434d',
            colorBorderSecondary: '#30353d',
            borderRadius: 4,
            wireframe: false,
            // Default shadows vanish on dark surfaces; stronger blacks give
            // floating surfaces (dropdowns, popovers, modals) a visible lift.
            boxShadow:
              '0 6px 16px 0 rgba(0, 0, 0, 0.48), 0 3px 6px -4px rgba(0, 0, 0, 0.56), 0 9px 28px 8px rgba(0, 0, 0, 0.36)',
            boxShadowSecondary:
              '0 6px 16px 0 rgba(0, 0, 0, 0.52), 0 3px 6px -4px rgba(0, 0, 0, 0.60), 0 9px 28px 8px rgba(0, 0, 0, 0.40)',
          },
          components: {
            Layout: {
              // Chrome sits below the canvas (#141518) so the nav and header
              // frame the content, matching slate's pattern.
              headerBg: '#101216',
              triggerBg: '#101216',
              siderBg: '#101216',
            },
            Menu: {
              darkItemBg: '#101216',
              darkItemHoverBg: '#1d2126',
              darkPopupBg: '#1f2226',
              darkSubMenuItemBg: '#16191d',
            },
          },
        },
      },
      behavior: {
        allowsPrimaryOverride: true,
      },
      appBar: {
        backgroundColor: '#101216',
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

