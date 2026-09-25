import {
  DEFAULT_THEME_SELECTION,
  isDarkMode,
  normalizeThemeSelection,
} from './theme-registry'

describe('isDarkMode', () => {
  it('treats light and mist as light modes', () => {
    // Arrange & Act & Assert
    expect(isDarkMode('light')).toBe(false)
    expect(isDarkMode('mist')).toBe(false)
  })

  it('treats dark and slate as dark modes', () => {
    // Arrange & Act & Assert
    expect(isDarkMode('dark')).toBe(true)
    expect(isDarkMode('slate')).toBe(true)
  })
})

describe('normalizeThemeSelection', () => {
  it('maps a legacy mode name to the Wayd theme in that mode', () => {
    // Arrange & Act
    const selection = normalizeThemeSelection('dark')

    // Assert
    expect(selection).toEqual({ theme: 'wayd', mode: 'dark' })
  })

  it('maps a legacy theme name to that theme in its default mode', () => {
    // Arrange & Act
    const selection = normalizeThemeSelection('geek')

    // Assert
    expect(selection).toEqual({ theme: 'geek', mode: 'dark' })
  })

  it('returns a valid stored selection unchanged', () => {
    // Arrange & Act
    const selection = normalizeThemeSelection({ theme: 'wayd', mode: 'slate' })

    // Assert
    expect(selection).toEqual({ theme: 'wayd', mode: 'slate' })
  })

  it('keeps a stored mist selection for the Wayd theme', () => {
    // Arrange & Act
    const selection = normalizeThemeSelection({ theme: 'wayd', mode: 'mist' })

    // Assert
    expect(selection).toEqual({ theme: 'wayd', mode: 'mist' })
  })

  it('clamps an unsupported mode to the theme default', () => {
    // Arrange & Act
    const selection = normalizeThemeSelection({ theme: 'glass', mode: 'dark' })

    // Assert
    expect(selection).toEqual({ theme: 'glass', mode: 'light' })
  })

  it('falls back to the default for an unknown name', () => {
    // Arrange & Act
    const selection = normalizeThemeSelection('neon')

    // Assert
    expect(selection).toEqual(DEFAULT_THEME_SELECTION)
  })

  it('does not treat inherited object properties as theme ids', () => {
    // Arrange & Act
    const selection = normalizeThemeSelection('toString')

    // Assert
    expect(selection).toEqual(DEFAULT_THEME_SELECTION)
  })

  it('falls back to the default for non-string, non-selection values', () => {
    // Arrange & Act & Assert
    expect(normalizeThemeSelection(null)).toEqual(DEFAULT_THEME_SELECTION)
    expect(normalizeThemeSelection(42)).toEqual(DEFAULT_THEME_SELECTION)
    expect(normalizeThemeSelection({ mode: 'dark' })).toEqual(
      DEFAULT_THEME_SELECTION,
    )
  })
})
