'use client'

import { HighlightFilled, HighlightOutlined, BgColorsOutlined } from '@ant-design/icons'
import useTheme from '../../components/contexts/theme'
import { ThemeMode } from '../../components/contexts/theme/types'

const ICONS: Record<ThemeMode, React.ReactNode> = {
  light: <HighlightOutlined />,
  dark: <HighlightFilled />,
  slate: <BgColorsOutlined />,
}

const LABELS: Record<ThemeMode, string> = {
  light: 'Mode: Light',
  dark: 'Mode: Dark',
  slate: 'Mode: Slate',
}

/**
 * Menu item that cycles through the current theme's available modes.
 * Returns null when the theme only supports a single mode.
 */
const useThemeToggleMenuItem = () => {
  const { currentMode, availableModes, setCurrentMode } = useTheme()

  if (availableModes.length < 2) return null

  const toggleMode = () => {
    const idx = availableModes.indexOf(currentMode)
    setCurrentMode(availableModes[(idx + 1) % availableModes.length])
  }

  return {
    key: 'theme-mode',
    label: LABELS[currentMode],
    icon: ICONS[currentMode],
    onClick: toggleMode,
  }
}

export default useThemeToggleMenuItem
