import {
  createContext,
  ReactNode,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import { useLocalStorageState } from '@/src/hooks'
import { ConfigProvider, theme, ThemeConfig } from 'antd'
import { useLightThemePreset } from '@/src/config/theme/light-theme'
import { useDarkThemePreset } from '@/src/config/theme/dark-theme'
import { useSlateThemePreset } from '@/src/config/theme/slate-theme'
import useCartoonTheme from '@/src/config/theme/cartoon-theme'
import { AppThemeConfig } from '@/src/config/theme/theme-preset'
import { ThemeContextType, ThemeName, UserThemeConfigDto } from './types'
import { getProfileClient } from '@/src/services/clients'

export const ThemeContext = createContext<ThemeContextType | null>(null)

function mergeThemeConfig(base: ThemeConfig, overrides: UserThemeConfigDto | null, themeName: ThemeName): ThemeConfig {
  if (!overrides) return base

  const algorithms = [
    base.algorithm ?? theme.defaultAlgorithm,
    ...(overrides.useCompactAlgorithm ? [theme.compactAlgorithm] : []),
  ].flat()

  const applyHeaderColor =
    overrides.colorPrimary &&
    (themeName === 'light' || themeName === 'slate' || themeName === 'cartoon')

  return {
    ...base,
    algorithm: algorithms,
    token: {
      ...base.token,
      ...(overrides.colorPrimary ? { colorPrimary: overrides.colorPrimary } : {}),
    },
    components: {
      ...base.components,
      Layout: {
        ...base.components?.Layout,
        ...(applyHeaderColor ? { headerBg: overrides.colorPrimary } : {}),
      },
    },
  }
}

// Debounce helper — returns a stable function that delays calling fn by ms.
function useDebouncedCallback<T extends unknown[]>(
  fn: (...args: T) => void,
  ms: number,
) {
  const ref = useRef(fn)
  useEffect(() => {
    ref.current = fn
  })
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)

  return useCallback((...args: T) => {
    if (timer.current) clearTimeout(timer.current)
    timer.current = setTimeout(() => ref.current(...args), ms)
  }, [ms])
}

export const ThemeProvider = ({ children }: { children: ReactNode }) => {
  const [currentThemeName, setCurrentThemeName] =
    useLocalStorageState<ThemeName>('appTheme', 'light')
  const [userThemeConfig, setUserThemeConfigState] = useState<UserThemeConfigDto | null>(null)
  const hasMountedRef = useRef(false)
  const transitionTimeoutRef = useRef<number | null>(null)

  // Load theme config from server once on mount.
  useEffect(() => {
    getProfileClient()
      .getThemeConfig()
      .then((config) => {
        if (config) setUserThemeConfigState(config)
      })
      .catch(() => {
        // Non-fatal — default theme is used.
      })
  }, [])

  const saveThemeConfig = useDebouncedCallback(
    (config: UserThemeConfigDto | null) => {
      getProfileClient()
        .updateThemeConfig(config ?? undefined)
        .catch(() => {
          // Silent — user can retry via settings.
        })
    },
    500,
  )

  const setUserThemeConfig = useCallback(
    (config: UserThemeConfigDto | null) => {
      setUserThemeConfigState(config)
      saveThemeConfig(config)
    },
    [saveThemeConfig],
  )

  const lightPreset = useLightThemePreset()
  const darkPreset = useDarkThemePreset()
  const slatePreset = useSlateThemePreset()
  const cartoonThemeConfig = useCartoonTheme()
  const themesByName: Record<ThemeName, AppThemeConfig> = {
    light: lightPreset,
    dark: darkPreset,
    slate: slatePreset,
    cartoon: cartoonThemeConfig,
  }
  const activeTheme = themesByName[currentThemeName]
  const currentTheme = useMemo(
    () =>
      mergeThemeConfig(
        activeTheme.configProvider.theme ?? ({} as ThemeConfig),
        userThemeConfig,
        currentThemeName,
      ),
    [activeTheme, userThemeConfig, currentThemeName],
  )
  const providerOverrides = {
    modal: activeTheme.configProvider.modal,
    popover: activeTheme.configProvider.popover,
    progress: activeTheme.configProvider.progress,
    colorPicker: activeTheme.configProvider.colorPicker,
  }
  const modalConfig = useMemo(
    () => ({
      closable: true,
      mask: { closable: false },
      ...(providerOverrides.modal ?? {}),
    }),
    [providerOverrides.modal],
  )

  useLayoutEffect(() => {
    const root = document.documentElement
    root.setAttribute('data-theme', currentThemeName)

    // Skip animation for first paint; only animate explicit theme changes.
    if (!hasMountedRef.current) {
      hasMountedRef.current = true
      return
    }

    root.classList.add('theme-transitioning')
    if (transitionTimeoutRef.current) {
      window.clearTimeout(transitionTimeoutRef.current)
    }
    transitionTimeoutRef.current = window.setTimeout(() => {
      root.classList.remove('theme-transitioning')
      transitionTimeoutRef.current = null
    }, 350)
  }, [currentThemeName])

  useEffect(
    () => () => {
      if (transitionTimeoutRef.current) {
        window.clearTimeout(transitionTimeoutRef.current)
      }
      document.documentElement.classList.remove('theme-transitioning')
    },
    [],
  )

  return (
    <ConfigProvider
      theme={currentTheme}
      modal={modalConfig}
      popover={providerOverrides.popover}
      progress={providerOverrides.progress}
      colorPicker={providerOverrides.colorPicker}
    >
      <ThemeTokenProvider
        currentThemeName={currentThemeName}
        setCurrentThemeName={setCurrentThemeName}
        agGridTheme={activeTheme.integrations.agGridTheme}
        antDesignChartsTheme={activeTheme.integrations.antDesignChartsTheme}
        antvisG6ChartsTheme={activeTheme.integrations.antvisG6ChartsTheme}
        userThemeConfig={userThemeConfig}
        setUserThemeConfig={setUserThemeConfig}
      >
        {children}
      </ThemeTokenProvider>
    </ConfigProvider>
  )
}

interface ThemeTokenProviderProps {
  children: ReactNode
  currentThemeName: ThemeName
  setCurrentThemeName: (value: ThemeName) => void
  agGridTheme: ThemeContextType['agGridTheme']
  antDesignChartsTheme: string
  antvisG6ChartsTheme: string
  userThemeConfig: UserThemeConfigDto | null
  setUserThemeConfig: (config: UserThemeConfigDto | null) => void
}

const ThemeTokenProvider = ({
  children,
  currentThemeName,
  setCurrentThemeName,
  agGridTheme,
  antDesignChartsTheme,
  antvisG6ChartsTheme,
  userThemeConfig,
  setUserThemeConfig,
}: ThemeTokenProviderProps) => {
  const { token } = theme.useToken()
  const badgeColor = token.colorPrimary

  const themeContextValue = useMemo(
    () => ({
      currentThemeName,
      setCurrentThemeName,
      agGridTheme,
      token,
      badgeColor,
      antDesignChartsTheme,
      antvisG6ChartsTheme,
      userThemeConfig,
      setUserThemeConfig,
    }),
    [
      currentThemeName,
      setCurrentThemeName,
      agGridTheme,
      token,
      badgeColor,
      antDesignChartsTheme,
      antvisG6ChartsTheme,
      userThemeConfig,
      setUserThemeConfig,
    ],
  )

  return (
    <ThemeContext.Provider value={themeContextValue}>
      {children}
    </ThemeContext.Provider>
  )
}
