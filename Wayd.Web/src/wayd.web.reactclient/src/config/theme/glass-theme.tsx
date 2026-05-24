import { useMemo } from 'react'
import { theme } from 'antd'
import { themeBalham } from 'ag-grid-community'
import { createStyles } from 'antd-style'
import { AppThemeConfig, TimeLineStyles } from './theme-preset'

export const glassTimeLineStyles: TimeLineStyles = {
  item: {
    background: '#e8eef5',
    foreground: '#3b4a5a',
    font: '#2f3a45',
  },
  background: {
    background: '#d4dde8',
  },
}

const cx = (...classes: Array<string | false | null | undefined>) =>
  classes.filter(Boolean).join(' ')

const useStyles = createStyles(({ css, cssVar }) => {
  const glassBorder = {
    boxShadow: [
      `${cssVar.boxShadowSecondary}`,
      'inset 0 0 5px 2px rgba(255, 255, 255, 0.3)',
      'inset 0 5px 2px rgba(255, 255, 255, 0.2)',
    ].join(','),
  }

  const glassBox = {
    ...glassBorder,
    background: `color-mix(in srgb, ${cssVar.colorBgContainer} 15%, transparent)`,
    backdropFilter: 'blur(12px)',
  }

  return {
    glassBorder,
    glassBox,
    notBackdropFilter: css({
      backdropFilter: 'none',
    }),
    app: css({
      textShadow: '0 1px rgba(0,0,0,0.1)',
    }),
    cardRoot: css({
      ...glassBox,
      backgroundColor: `color-mix(in srgb, ${cssVar.colorBgContainer} 40%, transparent)`,
    }),
    modalContainer: css({
      ...glassBox,
      backdropFilter: 'none',
    }),
    buttonRoot: css({
      ...glassBorder,
    }),
    buttonRootDefaultColor: css({
      background: 'transparent',
      color: cssVar.colorText,
      '&:hover': {
        background: 'rgba(255,255,255,0.2)',
        color: `color-mix(in srgb, ${cssVar.colorText} 90%, transparent)`,
      },
      '&:active': {
        background: 'rgba(255,255,255,0.1)',
        color: `color-mix(in srgb, ${cssVar.colorText} 80%, transparent)`,
      },
    }),
    dropdownRoot: css({
      ...glassBox,
      borderRadius: cssVar.borderRadiusLG,
      ul: {
        background: 'transparent',
      },
    }),
    switchRoot: css({ ...glassBorder, border: 'none' }),
    segmentedRoot: css({
      ...glassBorder,
      background: 'transparent',
      backdropFilter: 'none',
      '& .ant-segmented-thumb': {
        ...glassBox,
      },
      '& .ant-segmented-item-selected': {
        ...glassBox,
      },
    }),
    radioButtonRoot: css({
      '&.ant-radio-button-wrapper': {
        ...glassBorder,
        background: 'transparent',
        borderColor: 'rgba(255, 255, 255, 0.2)',
        color: cssVar.colorText,
        '&:hover': {
          borderColor: 'rgba(255, 255, 255, 0.24)',
          color: cssVar.colorText,
        },
        '&.ant-radio-button-wrapper-checked:not(.ant-radio-button-wrapper-disabled)': {
          ...glassBox,
          borderColor: 'rgba(255, 255, 255, 0.28)',
          color: cssVar.colorText,
          '&::before': {
            backgroundColor: 'rgba(255, 255, 255, 0.18)',
          },
          '&:hover': {
            color: cssVar.colorText,
          },
        },
      },
    }),
  }
})

const useGlassTheme = (): AppThemeConfig => {
  const { styles } = useStyles()

  return useMemo(
    () => ({
      configProvider: {
        theme: {
          algorithm: theme.defaultAlgorithm,
          token: {
            colorPrimary: '#476b91',
            colorBgBase: '#eaf1f8',
            colorBgLayout: '#eaf1f8',
            colorBgContainer: '#f7fbff',
            colorBorder: '#c9d6e5',
            colorBorderSecondary: '#dce6f0',
            borderRadius: 12,
            borderRadiusLG: 12,
            borderRadiusSM: 12,
            borderRadiusXS: 12,
            motionDurationSlow: '0.2s',
            motionDurationMid: '0.1s',
            motionDurationFast: '0.05s',
          },
        },
        app: {
          className: styles.app,
        },
        card: {
          classNames: {
            root: styles.cardRoot,
          },
        },
        modal: {
          classNames: {
            container: styles.modalContainer,
          },
        },
        button: {
          classNames: ({ props }) => ({
            root: cx(
              styles.buttonRoot,
              (props.variant !== 'solid' ||
                props.color === 'default' ||
                props.type === 'default') &&
                styles.buttonRootDefaultColor,
            ),
          }),
        },
        alert: {
          className: cx(styles.glassBox, styles.notBackdropFilter),
        },
        colorPicker: {
          classNames: {
            root: cx(styles.glassBox, styles.notBackdropFilter),
          },
          arrow: false,
        },
        dropdown: {
          classNames: {
            root: styles.dropdownRoot,
          },
        },
        select: {
          classNames: {
            root: cx(styles.glassBox, styles.notBackdropFilter),
            popup: {
              root: styles.glassBox,
            },
          },
        },
        datePicker: {
          classNames: {
            root: cx(styles.glassBox, styles.notBackdropFilter),
            popup: {
              container: styles.glassBox,
            },
          },
        },
        input: {
          classNames: {
            root: cx(styles.glassBox, styles.notBackdropFilter),
          },
        },
        inputNumber: {
          classNames: {
            root: cx(styles.glassBox, styles.notBackdropFilter),
          },
        },
        popover: {
          classNames: {
            container: styles.glassBox,
          },
        },
        switch: {
          classNames: {
            root: styles.switchRoot,
          },
        },
        radio: {
          classNames: {
            root: styles.radioButtonRoot,
          },
        },
            segmented: {
              className: styles.segmentedRoot,
            },
            Layout: {
              headerBg: 'rgba(71, 107, 145, 0.78)',
              triggerBg: 'rgba(71, 107, 145, 0.78)',
              siderBg: 'rgba(234, 241, 248, 0.72)',
            },
            progress: {
              classNames: {
                track: styles.glassBorder,
              },
          styles: {
            track: {
              height: 12,
            },
            rail: {
              height: 12,
            },
          },
        },
      },
      timeline: glassTimeLineStyles,
      appBar: {
        backgroundColor: 'var(--ant-color-primary)',
        color: '#ffffff',
        subtleColor: 'rgba(255, 255, 255, 0.9)',
      },
      integrations: {
        agGridTheme: themeBalham,
        antDesignChartsTheme: 'classic',
        antvisG6ChartsTheme: 'light',
      },
    }),
    [styles],
  )
}

export default useGlassTheme
