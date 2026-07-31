import { useMemo } from 'react'
import {
  ThemeId,
  ThemeMode,
  ThemeSelection,
} from '@/src/components/contexts/theme/types'
import { AppThemeConfig } from './theme-preset'
import { useWaydLightTheme } from './wayd-light-theme'
import { useWaydDarkTheme } from './wayd-dark-theme'
import { useWaydSlateTheme } from './wayd-slate-theme'
import useCartoonTheme from './cartoon-theme'
import useShadcnTheme from './shadcn-theme'
import useGlassTheme from './glass-theme'
import useGeekTheme from './geek-theme'
import useIllustrationTheme from './illustration-theme'

export interface ThemeMetadata {
  label: string
  defaultMode: ThemeMode
  modes: ThemeMode[]
}

/**
 * Static theme catalog: which themes exist and which modes each supports.
 * Kept separate from the preset hooks so selection validation and UI option
 * lists don't need the (style-generating) presets instantiated.
 */
export const THEME_METADATA: Record<ThemeId, ThemeMetadata> = {
  wayd: { label: 'Wayd', defaultMode: 'light', modes: ['light', 'dark', 'slate'] },
  cartoon: { label: 'Cartoon', defaultMode: 'light', modes: ['light'] },
  shadcn: { label: 'Shadcn', defaultMode: 'light', modes: ['light'] },
  glass: { label: 'Glass', defaultMode: 'light', modes: ['light'] },
  geek: { label: 'Geek', defaultMode: 'dark', modes: ['dark'] },
  illustration: { label: 'Illustration', defaultMode: 'light', modes: ['light'] },
}

export const DEFAULT_THEME_SELECTION: ThemeSelection = {
  theme: 'wayd',
  mode: 'light',
}

const isThemeId = (value: unknown): value is ThemeId =>
  typeof value === 'string' && Object.hasOwn(THEME_METADATA, value)

const isThemeMode = (value: unknown): value is ThemeMode =>
  value === 'light' || value === 'dark' || value === 'slate'

/**
 * Coerce a persisted value into a valid selection. Handles the legacy flat
 * format where the `appTheme` key held a single name: `light`/`dark`/`slate`
 * were modes of the Wayd theme; every other name was a theme id.
 */
export function normalizeThemeSelection(raw: unknown): ThemeSelection {
  if (typeof raw === 'string') {
    if (isThemeMode(raw)) return { theme: 'wayd', mode: raw }
    if (isThemeId(raw)) {
      return { theme: raw, mode: THEME_METADATA[raw].defaultMode }
    }
    return DEFAULT_THEME_SELECTION
  }

  if (raw && typeof raw === 'object' && Object.hasOwn(raw, 'theme')) {
    const { theme, mode } = raw as { theme?: unknown; mode?: unknown }
    if (isThemeId(theme)) {
      const metadata = THEME_METADATA[theme]
      return {
        theme,
        mode:
          isThemeMode(mode) && metadata.modes.includes(mode)
            ? mode
            : metadata.defaultMode,
      }
    }
  }

  return DEFAULT_THEME_SELECTION
}

/** Instantiated presets for every theme, keyed by theme id then mode. */
export const useThemeRegistry = (): Record<
  ThemeId,
  Partial<Record<ThemeMode, AppThemeConfig>>
> => {
  const waydLight = useWaydLightTheme()
  const waydDark = useWaydDarkTheme()
  const waydSlate = useWaydSlateTheme()
  const cartoon = useCartoonTheme()
  const shadcn = useShadcnTheme()
  const glass = useGlassTheme()
  const geek = useGeekTheme()
  const illustration = useIllustrationTheme()

  return useMemo(
    () => ({
      wayd: { light: waydLight, dark: waydDark, slate: waydSlate },
      cartoon: { light: cartoon },
      shadcn: { light: shadcn },
      glass: { light: glass },
      geek: { dark: geek },
      illustration: { light: illustration },
    }),
    [
      waydLight,
      waydDark,
      waydSlate,
      cartoon,
      shadcn,
      glass,
      geek,
      illustration,
    ],
  )
}
