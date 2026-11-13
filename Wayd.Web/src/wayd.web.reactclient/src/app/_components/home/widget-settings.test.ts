import {
  hasCustomSettings,
  resolveSettings,
  rowsSetting,
  rowsToShow,
  settingsToStore,
  WidgetSetting,
} from './widget-settings'

const settings: WidgetSetting[] = [
  rowsSetting(5),
  { kind: 'toggle', key: 'unread-only', label: 'Unread only', default: false },
]

describe('resolveSettings', () => {
  it('gives every declared setting its default when nothing is stored', () => {
    // Act
    const values = resolveSettings(settings, undefined)

    // Assert
    expect(values).toEqual({ rows: '5', 'unread-only': 'false' })
  })

  it('takes a stored value the setting accepts', () => {
    // Act
    const values = resolveSettings(settings, {
      rows: '10',
      'unread-only': 'true',
    })

    // Assert
    expect(values).toEqual({ rows: '10', 'unread-only': 'true' })
  })

  it('reads a stored value the setting does not offer as its default', () => {
    // Act
    const values = resolveSettings(settings, {
      rows: '7',
      'unread-only': 'yes',
    })

    // Assert
    expect(values).toEqual({ rows: '5', 'unread-only': 'false' })
  })

  it('leaves out a stored key the widget does not declare', () => {
    // Act
    const values = resolveSettings(settings, { retired: 'x' })

    // Assert
    expect(values).not.toHaveProperty('retired')
  })

  it('is empty for a widget with no settings', () => {
    // Act
    const values = resolveSettings(undefined, { rows: '10' })

    // Assert
    expect(values).toEqual({})
  })
})

describe('settingsToStore', () => {
  it('keeps only the values that differ from their default', () => {
    // Act
    const stored = settingsToStore(settings, {
      rows: '10',
      'unread-only': 'false',
    })

    // Assert
    expect(stored).toEqual({ rows: '10' })
  })

  it('drops unknown keys and values the setting does not offer', () => {
    // Act
    const stored = settingsToStore(settings, { rows: '7', retired: 'x' })

    // Assert
    expect(stored).toEqual({})
  })
})

describe('hasCustomSettings', () => {
  it('is true only when a stored value changes what the widget shows', () => {
    // Act & Assert
    expect(hasCustomSettings(settings, { rows: '10' })).toBe(true)
    expect(hasCustomSettings(settings, { rows: '5' })).toBe(false)
    expect(hasCustomSettings(settings, { retired: 'x' })).toBe(false)
    expect(hasCustomSettings(settings, undefined)).toBe(false)
  })
})

describe('rowsSetting', () => {
  it('defaults to the rows the widget showed before', () => {
    // Act
    const setting = rowsSetting(8)

    // Assert
    expect(setting.default).toBe('8')
    expect(setting.options.map((o) => o.value)).toContain('8')
  })
})

describe('rowsToShow', () => {
  it('reads the setting, falling back when it is missing', () => {
    // Act & Assert
    expect(rowsToShow({ rows: '10' }, 5)).toBe(10)
    expect(rowsToShow({}, 5)).toBe(5)
    expect(rowsToShow(undefined, 5)).toBe(5)
  })
})
