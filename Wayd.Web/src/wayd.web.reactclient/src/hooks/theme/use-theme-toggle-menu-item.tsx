'use client'

import { HighlightFilled, HighlightOutlined, BgColorsOutlined } from '@ant-design/icons'
import useTheme from '../../components/contexts/theme'
import { ThemeName } from '../../components/contexts/theme/types'

const CYCLE: ThemeName[] = [
  'cartoon',
  'dark',
  'geek',
  'glass',
  'illustration',
  'light',
  'shadcn',
  'slate',
]

const ICONS: Record<ThemeName, React.ReactNode> = {
  light: <HighlightOutlined />,
  dark: <HighlightFilled />,
  slate: <BgColorsOutlined />,
  cartoon: <BgColorsOutlined />,
  shadcn: <BgColorsOutlined />,
  glass: <BgColorsOutlined />,
  geek: <BgColorsOutlined />,
  illustration: <BgColorsOutlined />,
}

const LABELS: Record<ThemeName, string> = {
  light: 'Theme: Light',
  dark: 'Theme: Dark',
  slate: 'Theme: Slate',
  cartoon: 'Theme: Cartoon',
  shadcn: 'Theme: Shadcn',
  glass: 'Theme: Glass',
  geek: 'Theme: Geek',
  illustration: 'Theme: Illustration',
}

const useThemeToggleMenuItem = () => {
  const { setCurrentThemeName, currentThemeName } = useTheme()

  const toggleTheme = () => {
    const idx = CYCLE.indexOf(currentThemeName)
    setCurrentThemeName(CYCLE[(idx + 1) % CYCLE.length])
  }

  return {
    key: 'theme',
    label: LABELS[currentThemeName],
    icon: ICONS[currentThemeName],
    onClick: toggleTheme,
  }
}

export default useThemeToggleMenuItem
