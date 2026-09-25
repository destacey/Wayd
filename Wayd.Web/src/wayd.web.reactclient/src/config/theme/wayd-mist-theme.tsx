'use client'

import { theme } from 'antd'
import { useMemo } from 'react'
import { ThemeConstants } from './theme-constants'
import { AppThemeConfig } from './theme-preset'
const { defaultAlgorithm } = theme

/** Wayd theme — mist mode (alternate light). */
export const useWaydMistTheme = (): AppThemeConfig =>
  useMemo(
    () => ({
      configProvider: {
        theme: {
          algorithm: defaultAlgorithm,
          token: {
            colorPrimary: ThemeConstants.COLOR_PRIMARY,
            // Mist is light mode with the glare taken off: the same cool
            // ~213° ramp as light/slate, dimmed to a pale grey-blue. Unlike
            // light, containers are tinted too — pure white is what glares —
            // and elevated surfaces step *up* toward white so popovers lift
            // off the page. Borders are stronger than light's because the
            // gap between surface steps is smaller.
            colorBgBase: '#e3e7ec',
            colorBgLayout: '#d9dee5',
            colorBgContainer: '#e8ebf0',
            colorBgElevated: '#f3f5f8',
            colorBorder: '#a8b2be',
            colorBorderSecondary: '#c4cbd4',
            // Tinted surfaces cost text contrast, so the alpha ladder is
            // pinned darker than stock; tertiary holds ≥4.5:1 on containers.
            colorText: 'rgba(0, 0, 0, 0.9)',
            colorTextSecondary: 'rgba(0, 0, 0, 0.7)',
            colorTextTertiary: 'rgba(0, 0, 0, 0.58)',
            colorTextQuaternary: 'rgba(0, 0, 0, 0.36)',
            // The derived link (#1677ff) is ≈3.4:1 on these surfaces. Deeper
            // blues hold ≥4.5:1, and hover deepens further for contrast.
            colorLink: '#1565c0',
            colorLinkHover: '#0d47a1',
            colorLinkActive: '#0b3c8a',
            boxShadow:
              '0 6px 16px 0 rgba(0, 0, 0, 0.12), 0 3px 6px -4px rgba(0, 0, 0, 0.16), 0 9px 28px 8px rgba(0, 0, 0, 0.08)',
            boxShadowSecondary:
              '0 6px 16px 0 rgba(0, 0, 0, 0.14), 0 3px 6px -4px rgba(0, 0, 0, 0.18), 0 9px 28px 8px rgba(0, 0, 0, 0.10)',
            borderRadius: 4,
            wireframe: false,
          },
          components: {
            Layout: {
              headerBg: ThemeConstants.COLOR_PRIMARY,
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
      },
      integrations: {
        antDesignChartsTheme: 'classic',
        antvisG6ChartsTheme: 'light',
      },
    }),
    [],
  )

export default useWaydMistTheme
