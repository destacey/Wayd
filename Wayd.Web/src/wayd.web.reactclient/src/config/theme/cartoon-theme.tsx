import { useMemo } from 'react'
import { theme } from 'antd'
import { createPart, themeBalham } from 'ag-grid-community'
import { createStyles } from 'antd-style'
import { AppThemeConfig, TimeLineStyles } from './theme-preset'

export const cartoonThemeTokens = {
  brand: {
    text: '#51463B',
    primary: '#225555',
    link: '#3f79a8',
    linkHover: '#4b89bb',
    linkActive: '#2f5f87',
    error: '#DA8787',
    info: '#9CD3D3',
    bgBase: '#FAFAEE',
    border: '#225555',
    cardBg: '#BBAA99',
    selectOptionSelectedBg: '#CBC4AF',
    siderBg: '#D8D0BD',
  },
  shape: {
    lineWidth: 2,
    lineWidthBold: 2,
    borderRadius: 18,
    borderRadiusLG: 18,
    borderRadiusSM: 18,
    tooltipRadius: 6,
    controlHeightSM: 28,
    controlHeight: 36,
  },
  timeline: {
    itemBackground: '#f3ebd8',
    itemForeground: '#225555',
    itemFont: '#51463B',
    background: '#cbc4af',
  },
} as const

export const cartoonThemeConfig: { timeLineColors: TimeLineStyles } = {
  timeLineColors: {
    item: {
      background: cartoonThemeTokens.timeline.itemBackground,
      foreground: cartoonThemeTokens.timeline.itemForeground,
      font: cartoonThemeTokens.timeline.itemFont,
    },
    background: {
      background: cartoonThemeTokens.timeline.background,
    },
  },
}

const agGridCartoonTheme = themeBalham
  .withPart(
    createPart({
      feature: 'colorScheme',
      params: {
        backgroundColor: '#f7f1e4',
        foregroundColor: '#51463B',
        browserColorScheme: 'light',
      },
    }),
  )
  .withParams({
    borderRadius: cartoonThemeTokens.shape.borderRadius,
  })

const useStyles = createStyles(({ css, cssVar }) => {
  const sharedBorder = {
    border: `${cssVar.lineWidth} ${cssVar.lineType} ${cssVar.colorBorder}`,
  }

  return {
    sharedBorder,
    progressTrack: css({
      ...sharedBorder,
      marginInlineStart: `calc(-1 * ${cssVar.lineWidth})`,
      marginBlockStart: `calc(-1 * ${cssVar.lineWidth})`,
    }),
  }
})

const useCartoonTheme = () => {
  const { styles } = useStyles()

  return useMemo<AppThemeConfig>(
    () => ({
      configProvider: {
        theme: {
          algorithm: theme.defaultAlgorithm,
          token: {
            colorText: cartoonThemeTokens.brand.text,
            colorPrimary: cartoonThemeTokens.brand.primary,
            colorLink: cartoonThemeTokens.brand.link,
            colorLinkHover: cartoonThemeTokens.brand.linkHover,
            colorLinkActive: cartoonThemeTokens.brand.linkActive,
            colorError: cartoonThemeTokens.brand.error,
            colorInfo: cartoonThemeTokens.brand.info,
            colorInfoBorder: cartoonThemeTokens.brand.border,
            colorBorder: cartoonThemeTokens.brand.border,
            colorBorderSecondary: cartoonThemeTokens.brand.border,
            lineWidth: cartoonThemeTokens.shape.lineWidth,
            lineWidthBold: cartoonThemeTokens.shape.lineWidthBold,
            borderRadius: cartoonThemeTokens.shape.borderRadius,
            borderRadiusLG: cartoonThemeTokens.shape.borderRadiusLG,
            borderRadiusSM: cartoonThemeTokens.shape.borderRadiusSM,
            controlHeightSM: cartoonThemeTokens.shape.controlHeightSM,
            controlHeight: cartoonThemeTokens.shape.controlHeight,
            colorBgBase: cartoonThemeTokens.brand.bgBase,
          },
          components: {
            Button: {
              primaryShadow: 'none',
              dangerShadow: 'none',
              defaultShadow: 'none',
            },
            Modal: {
              boxShadow: 'none',
            },
            Card: {
              colorBgContainer: cartoonThemeTokens.brand.cardBg,
            },
            Tooltip: {
              borderRadius: cartoonThemeTokens.shape.tooltipRadius,
              colorBorder: cartoonThemeTokens.brand.border,
              algorithm: true,
            },
            Select: {
              optionSelectedBg: cartoonThemeTokens.brand.selectOptionSelectedBg,
            },
            Layout: {
              headerBg: cartoonThemeTokens.brand.primary,
              triggerBg: cartoonThemeTokens.brand.primary,
              siderBg: cartoonThemeTokens.brand.siderBg,
            },
          },
        },
        modal: {
          classNames: {
            container: styles.sharedBorder,
          },
        },
        colorPicker: {
          arrow: false,
        },
        popover: {
          classNames: {
            container: styles.sharedBorder,
          },
        },
        progress: {
          classNames: {
            rail: styles.sharedBorder,
            track: styles.progressTrack,
          },
          styles: {
            rail: {
              height: 16,
            },
            track: {
              height: 16,
            },
          },
        },
      },
      timeline: cartoonThemeConfig.timeLineColors,
      integrations: {
        agGridTheme: agGridCartoonTheme,
        antDesignChartsTheme: 'classic',
        antvisG6ChartsTheme: 'light',
      },
    }),
    [styles.progressTrack, styles.sharedBorder],
  )
}

export default useCartoonTheme

