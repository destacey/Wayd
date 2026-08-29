'use client'

import { theme } from 'antd'
import { useMemo } from 'react'
import { AppThemeConfig } from './theme-preset'
const { darkAlgorithm } = theme

/** Wayd theme — slate mode (alternate dark). */
export const useWaydSlateTheme = (): AppThemeConfig =>
  useMemo(
    () => ({
      configProvider: {
        theme: {
          algorithm: darkAlgorithm,
          token: {
            colorPrimary: '#2196f3',
            // Slate is the screen-sharing mode: a true mid-tone, noticeably
            // lighter than dark mode so it projects well. Cool blue-grey ramp
            // (~213° hue) with explicit elevation steps — the dark algorithm's
            // derived surfaces compress on a mid-tone base, so layout/
            // container/elevated are pinned for visible depth, and borders are
            // strong enough that grid lines survive projector/video compression.
            colorBgBase: '#333a43',
            colorBgLayout: '#282e35',
            colorBgContainer: '#3a424c',
            colorBgElevated: '#444e5a',
            colorBorder: '#5c6979',
            colorBorderSecondary: '#4f5a67',
            // Mid-tone surfaces eat text-contrast headroom, so the alpha
            // ladder is pinned brighter than the dark algorithm's defaults —
            // this is what keeps labels/captions crisp instead of hazy.
            colorTextBase: '#ffffff',
            colorText: 'rgba(255, 255, 255, 0.92)',
            colorTextSecondary: 'rgba(255, 255, 255, 0.72)',
            colorTextTertiary: 'rgba(255, 255, 255, 0.55)',
            colorTextQuaternary: 'rgba(255, 255, 255, 0.38)',
            // Default shadows are tuned for light backgrounds and vanish on
            // mid-tone surfaces; stronger alphas give floating surfaces
            // (dropdowns, popovers, modals) a visible lift beyond lightness.
            boxShadow:
              '0 6px 16px 0 rgba(0, 0, 0, 0.32), 0 3px 6px -4px rgba(0, 0, 0, 0.40), 0 9px 28px 8px rgba(0, 0, 0, 0.24)',
            boxShadowSecondary:
              '0 6px 16px 0 rgba(0, 0, 0, 0.36), 0 3px 6px -4px rgba(0, 0, 0, 0.44), 0 9px 28px 8px rgba(0, 0, 0, 0.28)',
            borderRadius: 4,
            wireframe: false,
          },
          components: {
            Layout: {
              // Chrome sits two steps below the canvas (#282e35) so the nav
              // and header frame the content without needing border CSS.
              headerBg: '#1f252c',
              triggerBg: '#1f252c',
              siderBg: '#1f252c',
            },
            Menu: {
              darkItemBg: '#1f252c',
              darkItemHoverBg: '#2b323b',
              darkPopupBg: '#272d35',
              darkSubMenuItemBg: '#252c34',
              darkItemColor: 'rgba(255, 255, 255, 0.88)',
              darkItemSelectedColor: '#ffffff',
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
        antDesignChartsTheme: 'classicDark',
        antvisG6ChartsTheme: 'dark',
      },
    }),
    [],
  )

export default useWaydSlateTheme

