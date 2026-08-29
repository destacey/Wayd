'use client'

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
import {
  DEFAULT_THEME_SELECTION,
  normalizeThemeSelection,
  THEME_METADATA,
  useThemeRegistry,
} from '@/src/config/theme/theme-registry'
import {
  ThemeContextType,
  ThemeId,
  ThemeMode,
  ThemeSelection,
  UserThemeConfigDto,
} from './types'
import { getProfileClient } from '@/src/services/clients'

export const ThemeContext = createContext<ThemeContextType | null>(null)

function mergeThemeConfig(
  base: ThemeConfig,
  overrides: UserThemeConfigDto | null,
  allowsPrimaryOverride: boolean,
): ThemeConfig {
  if (!overrides) return base

  const algorithms = [
    base.algorithm ?? theme.defaultAlgorithm,
    ...(overrides.useCompactAlgorithm ? [theme.compactAlgorithm] : []),
  ].flat()

  return {
    ...base,
    algorithm: algorithms,
    token: {
      ...base.token,
      ...(allowsPrimaryOverride && overrides.colorPrimary
        ? { colorPrimary: overrides.colorPrimary }
        : {}),
    },
    components: {
      ...base.components,
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
  // Same `appTheme` key as the legacy flat format; stored values may still be
  // a bare name string, so every read goes through normalizeThemeSelection.
  const [storedSelection, setStoredSelection] = useLocalStorageState<
    ThemeSelection | string
  >('appTheme', DEFAULT_THEME_SELECTION)
  const selection = useMemo(
    () => normalizeThemeSelection(storedSelection),
    [storedSelection],
  )
  const { theme: currentTheme, mode: currentMode } = selection
  const availableModes = THEME_METADATA[currentTheme].modes

  const setCurrentTheme = useCallback(
    (themeId: ThemeId) => {
      setStoredSelection((prev) => {
        const current = normalizeThemeSelection(prev)
        const metadata = THEME_METADATA[themeId]
        return {
          theme: themeId,
          mode: metadata.modes.includes(current.mode)
            ? current.mode
            : metadata.defaultMode,
        }
      })
    },
    [setStoredSelection],
  )

  const setCurrentMode = useCallback(
    (mode: ThemeMode) => {
      setStoredSelection((prev) => {
        const current = normalizeThemeSelection(prev)
        if (!THEME_METADATA[current.theme].modes.includes(mode)) return current
        return { theme: current.theme, mode }
      })
    },
    [setStoredSelection],
  )

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

  const themeRegistry = useThemeRegistry()
  const activeTheme =
    themeRegistry[currentTheme][currentMode] ??
    themeRegistry[currentTheme][THEME_METADATA[currentTheme].defaultMode]!
  const currentThemeConfig = useMemo(
    () =>
      mergeThemeConfig(
        activeTheme.configProvider.theme ?? ({} as ThemeConfig),
        userThemeConfig,
        activeTheme.behavior.allowsPrimaryOverride,
      ),
    [activeTheme, userThemeConfig],
  )
  const providerOverrides = {
    modal: activeTheme.configProvider.modal,
    popover: activeTheme.configProvider.popover,
    progress: activeTheme.configProvider.progress,
    colorPicker: activeTheme.configProvider.colorPicker,
  }
  const {
    theme: _unusedTheme,
    modal: _unusedModal,
    ...providerPassthrough
  } = activeTheme.configProvider
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
    root.setAttribute('data-theme', currentTheme)
    root.setAttribute('data-mode', currentMode)

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
  }, [currentTheme, currentMode])

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
      {...providerPassthrough}
      theme={currentThemeConfig}
      modal={modalConfig}
      popover={providerOverrides.popover}
      progress={providerOverrides.progress}
      colorPicker={providerOverrides.colorPicker}
    >
      <ThemeTokenProvider
        currentTheme={currentTheme}
        currentMode={currentMode}
        availableModes={availableModes}
        setCurrentTheme={setCurrentTheme}
        setCurrentMode={setCurrentMode}
        appBar={activeTheme.appBar}
        allowsPrimaryOverride={activeTheme.behavior.allowsPrimaryOverride}
        defaultPrimaryColor={String(activeTheme.configProvider.theme?.token?.colorPrimary ?? '')}
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
  currentTheme: ThemeId
  currentMode: ThemeMode
  availableModes: ThemeMode[]
  setCurrentTheme: (theme: ThemeId) => void
  setCurrentMode: (mode: ThemeMode) => void
  appBar: ThemeContextType['appBar']
  allowsPrimaryOverride: boolean
  defaultPrimaryColor: string
  antDesignChartsTheme: string
  antvisG6ChartsTheme: string
  userThemeConfig: UserThemeConfigDto | null
  setUserThemeConfig: (config: UserThemeConfigDto | null) => void
}

const ThemeTokenProvider = ({
  children,
  currentTheme,
  currentMode,
  availableModes,
  setCurrentTheme,
  setCurrentMode,
  appBar,
  allowsPrimaryOverride,
  defaultPrimaryColor,
  antDesignChartsTheme,
  antvisG6ChartsTheme,
  userThemeConfig,
  setUserThemeConfig,
}: ThemeTokenProviderProps) => {
  const { token } = theme.useToken()
  const badgeColor = token.colorPrimary

  const themeContextValue = useMemo(
    () => ({
      currentTheme,
      currentMode,
      availableModes,
      setCurrentTheme,
      setCurrentMode,
      appBar,
      allowsPrimaryOverride,
      defaultPrimaryColor,
      token,
      badgeColor,
      antDesignChartsTheme,
      antvisG6ChartsTheme,
      userThemeConfig,
      setUserThemeConfig,
    }),
    [
      currentTheme,
      currentMode,
      availableModes,
      setCurrentTheme,
      setCurrentMode,
      appBar,
      allowsPrimaryOverride,
      defaultPrimaryColor,
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
