'use client'

import { ColorPicker, type ColorPickerProps, Space } from 'antd'
import {
  generate,
  green,
  presetPalettes,
  red,
  yellow,
  grey,
  orange,
  cyan,
  purple,
  magenta,
} from '@ant-design/colors'
import useTheme from '../contexts/theme'
import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from 'react'
import type { Color } from 'antd/es/color-picker'

export interface ModaColorPickerProps {
  id?: string // required by antd for custom form controls
  value?: string // required by antd for custom form controls
  onChange?: (value: string | undefined) => void // required by antd for custom form controls
  /** Whether the picker is open, when the caller controls it (a grid cell). */
  open?: boolean
  /** Called whenever the picker opens or closes. */
  onOpenChange?: (open: boolean) => void
  /** Shows the hex value beside the swatch. On by default. */
  showText?: boolean
}

export interface ModaColorPickerRef {
  focus: () => void
  blur: () => void
}

type Presets = Required<ColorPickerProps>['presets'][number]

const redPalette = red.slice(2, 8).filter((_, index) => index % 2 === 0)
const orangePalette = orange.slice(2, 8).filter((_, index) => index % 2 === 0)
const yellowPalette = yellow.slice(2, 8).filter((_, index) => index % 2 === 0)
const greenPalette = green.slice(2, 8).filter((_, index) => index % 2 === 0)
const cyanPalette = cyan.slice(2, 8).filter((_, index) => index % 2 === 0)
const purplePalette = purple.slice(2, 8).filter((_, index) => index % 2 === 0)
const magentaPalette = magenta.slice(2, 8).filter((_, index) => index % 2 === 0)
const greyPalette = grey.slice(1, 6).filter((_, index) => index % 2 === 0)

const genPresets = (presets = presetPalettes) =>
  Object.entries(presets).map<Presets>(([label, colors]) => ({ label, colors }))

const customPanelRender: ColorPickerProps['panelRender'] = (
  _,
  { components: { Presets } },
) => (
  <Space style={{ width: '85px' }}>
    <Presets />
  </Space>
)

const WaydColorPicker = forwardRef<ModaColorPickerRef, ModaColorPickerProps>(
  (props, ref) => {
    const { value, onChange } = props
    const [ownOpen, setOwnOpen] = useState(false)
    const isOpen = props.open ?? ownOpen
    const setIsOpen = (open: boolean) => {
      if (props.open === undefined) setOwnOpen(open)
      props.onOpenChange?.(open)
    }
    const [isTriggerFocused, setIsTriggerFocused] = useState(false)
    const rootRef = useRef<HTMLSpanElement | null>(null)
    const shouldRefocusOnCloseRef = useRef(false)
    const refocusTimeoutRef = useRef<number | null>(null)
    const { token } = useTheme()

    const primaryPalette = generate(token.colorPrimary)
      .slice(2, 8)
      .filter((_, index) => index % 2 === 0)

    const colorPresets = genPresets({
      colors: [
        ...primaryPalette,
        ...redPalette,
        ...orangePalette,
        ...yellowPalette,
        ...greenPalette,
        ...cyanPalette,
        ...purplePalette,
        ...magentaPalette,
        ...greyPalette,
      ],
    })

    const clearPendingRefocus = useCallback(() => {
      if (refocusTimeoutRef.current !== null) {
        window.clearTimeout(refocusTimeoutRef.current)
        refocusTimeoutRef.current = null
      }
    }, [])

    const getTrigger = useCallback(() => {
      return rootRef.current?.querySelector(
        '.ant-color-picker-trigger',
      ) as HTMLElement | null
    }, [])

    const isFocusOnTrigger = useCallback(() => {
      const activeElement = document.activeElement as HTMLElement | null
      if (!activeElement) return false

      return Boolean(activeElement.closest('.ant-color-picker-trigger'))
    }, [])

    const focusTrigger = useCallback(() => {
      const trigger = getTrigger()

      if (trigger) {
        if (trigger.tabIndex < 0) {
          trigger.tabIndex = 0
        }
        trigger.focus({ preventScroll: true })
      }
    }, [getTrigger])

    const focusTriggerWithRetry = useCallback((attempt = 0) => {
      const run = (currentAttempt: number) => {
        focusTrigger()

        refocusTimeoutRef.current = window.setTimeout(() => {
          refocusTimeoutRef.current = null

          const hasFocus = isFocusOnTrigger()
          // Even if focused now, keep checking for a few frames because
          // modal/popup teardown can steal focus right after close.
          if (hasFocus && currentAttempt < 4) {
            run(currentAttempt + 1)
            return
          }

          if (!hasFocus && currentAttempt < 8) {
            run(currentAttempt + 1)
          }
        }, 16)
      }

      run(attempt)
    }, [focusTrigger, isFocusOnTrigger])

    useImperativeHandle(
      ref,
      () => ({
        focus: () => focusTriggerWithRetry(),
        blur: () => {
          getTrigger()?.blur()
        },
      }),
      [focusTriggerWithRetry, getTrigger],
    )

    useEffect(() => {
      return () => {
        clearPendingRefocus()
      }
    }, [clearPendingRefocus])

    useEffect(() => {
      const root = rootRef.current
      if (!root) return

      const updateFocusState = () => {
        setIsTriggerFocused(isFocusOnTrigger())
      }

      const onFocusOut = () => {
        // Focus transitions settle after blur; check on next tick.
        setTimeout(updateFocusState, 0)
      }

      root.addEventListener('focusin', updateFocusState)
      root.addEventListener('focusout', onFocusOut)

      return () => {
        root.removeEventListener('focusin', updateFocusState)
        root.removeEventListener('focusout', onFocusOut)
      }
    }, [isFocusOnTrigger])

    const onPickerChange = (color: Color | undefined) => {
      const colorValue = color?.toHexString()
      if (colorValue !== value) {
        onChange?.(colorValue)
      } else {
        // Clear the color if the same color is selected
        onChange?.(undefined)
      }
      shouldRefocusOnCloseRef.current = true
      clearPendingRefocus()
      setIsOpen(false)
      // Controlled close doesn't always emit onOpenChange(false), so refocus here too.
      requestAnimationFrame(() => {
        requestAnimationFrame(() => focusTriggerWithRetry())
      })
    }

    return (
      <span ref={rootRef}>
        <ColorPicker
          className={
            isTriggerFocused ? 'ant-color-picker-trigger-active' : undefined
          }
          open={isOpen}
          defaultFormat="hex"
          value={value}
          presets={colorPresets}
          allowClear
          showText={props.showText ?? true}
          format="hex"
          size="small"
          onOpenChange={(open) => {
            setIsOpen(open)
            if (open) {
              shouldRefocusOnCloseRef.current = false
              clearPendingRefocus()
            }
            if (!open && shouldRefocusOnCloseRef.current) {
              shouldRefocusOnCloseRef.current = false
              clearPendingRefocus()
              // Wait until popup fully closes before restoring focus.
              requestAnimationFrame(() => {
                requestAnimationFrame(() => focusTriggerWithRetry())
              })
            }
          }}
          onChange={onPickerChange}
          panelRender={customPanelRender}
        />
      </span>
    )
  },
)

WaydColorPicker.displayName = 'WaydColorPicker'

export default WaydColorPicker
