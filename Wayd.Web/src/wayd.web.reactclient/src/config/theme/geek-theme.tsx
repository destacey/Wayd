import { useMemo } from 'react'
import { themeBalham, colorSchemeDark } from 'ag-grid-community'
import { theme } from 'antd'
import { createStyles } from 'antd-style'
import { AppThemeConfig, TimeLineStyles } from './theme-preset'

export const geekTimeLineStyles: TimeLineStyles = {
  item: {
    background: '#163114',
    foreground: '#39ff14',
    font: '#9aff86',
  },
  background: {
    background: '#0d1e0c',
  },
}

const cx = (...classes: Array<string | false | null | undefined>) =>
  classes.filter(Boolean).join(' ')

const useStyles = createStyles(({ css, cssVar }) => {
  const lightBorder = {
    border: `${cssVar.lineWidth} solid ${cssVar.colorPrimary}`,
    boxShadow: `0 0 3px ${cssVar.colorPrimary}, inset 0 0 10px ${cssVar.colorPrimary}`,
  }

  return {
    lightBorder,
    app: css({
      textShadow: '0 0 5px color-mix(in srgb, currentColor 50%, transparent)',
    }),
    modalContainer: css({
      ...lightBorder,
      padding: 0,
    }),
    modalHeader: css({
      padding: `${cssVar.padding} ${cssVar.paddingLG}`,
      margin: 0,
      position: 'relative',
      '&:after': {
        ...lightBorder,
        content: '""',
        position: 'absolute',
        insetInline: 0,
        bottom: 0,
        border: 0,
        height: cssVar.lineWidth,
        background: cssVar.colorPrimary,
      },
    }),
    modalBody: css({
      padding: `${cssVar.padding} ${cssVar.paddingLG}`,
    }),
    modalFooter: css({
      padding: `${cssVar.padding} ${cssVar.paddingLG}`,
    }),
    buttonRoot: css({
      ...lightBorder,
      border: undefined,
      borderWidth: cssVar.lineWidth,
      borderColor: cssVar.colorPrimary,
    }),
    buttonRootSolid: css({
      color: cssVar.colorBgContainer,
      border: 'none',
      fontWeight: 'bolder',
    }),
    buttonRootSolidDanger: css({
      boxShadow: `0 0 5px ${cssVar.colorError}`,
    }),
    colorPickerBody: css({
      pointerEvents: 'none',
    }),
    tooltipRoot: css({
      padding: cssVar.padding,
    }),
    tooltipContainer: css({
      ...lightBorder,
      color: cssVar.colorPrimary,
    }),
    progressTrack: css({
      backgroundColor: cssVar.colorPrimary,
    }),
  }
})

const agGridGeekTheme = themeBalham
  .withPart(colorSchemeDark)
  .withParams({
    foregroundColor: '#9aff86',
    backgroundColor: '#0e1310',
    borderColor: '#39ff14',
    accentColor: '#39ff14',
    borderRadius: 0,
  })

const useGeekTheme = (): AppThemeConfig => {
  const { styles } = useStyles()

  return useMemo(
    () => ({
      configProvider: {
        theme: {
          algorithm: theme.darkAlgorithm,
          token: {
            borderRadius: 0,
            lineWidth: 2,
            colorPrimary: '#39ff14',
            colorText: '#39ff14',
            colorInfo: '#39ff14',
            controlHeightSM: 26,
            controlHeight: 34,
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
        app: {
          className: styles.app,
        },
        modal: {
          classNames: {
            container: styles.modalContainer,
            header: styles.modalHeader,
            body: styles.modalBody,
            footer: styles.modalFooter,
          },
        },
        button: {
          classNames: ({ props }) => ({
            root: cx(
              styles.buttonRoot,
              props.variant === 'solid' && styles.buttonRootSolid,
              props.variant === 'solid' &&
                props.danger &&
                styles.buttonRootSolidDanger,
            ),
          }),
        },
        alert: {
          className: styles.lightBorder,
        },
        colorPicker: {
          classNames: {
            root: styles.lightBorder,
            body: styles.colorPickerBody,
          },
          arrow: false,
        },
        select: {
          classNames: {
            root: styles.lightBorder,
          },
        },
        datePicker: {
          classNames: {
            root: styles.lightBorder,
          },
        },
        input: {
          classNames: {
            root: styles.lightBorder,
          },
        },
        inputNumber: {
          classNames: {
            root: styles.lightBorder,
          },
        },
        tooltip: {
          arrow: false,
          classNames: {
            root: styles.tooltipRoot,
            container: styles.tooltipContainer,
          },
        },
        progress: {
          classNames: {
            track: styles.progressTrack,
          },
        },
      },
      timeline: geekTimeLineStyles,
      integrations: {
        agGridTheme: agGridGeekTheme,
        antDesignChartsTheme: 'classicDark',
        antvisG6ChartsTheme: 'dark',
      },
    }),
    [styles],
  )
}

export default useGeekTheme
