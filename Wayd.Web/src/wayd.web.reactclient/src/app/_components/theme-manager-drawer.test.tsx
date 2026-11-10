import type { Mock } from 'vitest'
import React from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import ThemeManagerDrawer from './theme-manager-drawer'
import useTheme, { ThemeContextType } from '@/src/components/contexts/theme'

vi.mock('@/src/components/contexts/theme', () => ({
  default: vi.fn(),
}))

vi.mock('antd', async () => {
  const actual = await vi.importActual<any>('antd')
  return {
    ...actual,
    Drawer: ({ children, title }: any) => (
      <section>
        <h2>{title}</h2>
        {children}
      </section>
    ),
    Flex: ({ children }: any) => <div>{children}</div>,
    Tooltip: ({ children }: any) => <>{children}</>,
    Typography: {
      Text: ({ children }: any) => <span>{children}</span>,
    },
    Select: ({ value, options, onChange }: any) => (
      <select
        aria-label="Theme"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      >
        {options.map((o: any) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    ),
    Segmented: ({ value, options, onChange }: any) => (
      <div>
        {options.map((o: any) => (
          <button
            key={o.value}
            aria-label={o.label}
            aria-pressed={value === o.value}
            onClick={() => onChange(o.value)}
          >
            {o.label}
          </button>
        ))}
      </div>
    ),
  }
})

const setCurrentTheme = vi.fn()
const setCurrentMode = vi.fn()
const setUserThemeConfig = vi.fn()

const baseThemeContext: ThemeContextType = {
  currentTheme: 'wayd',
  currentMode: 'light',
  availableModes: ['light', 'dark', 'slate'],
  setCurrentTheme,
  setCurrentMode,
  appBar: {
    backgroundColor: '#1890ff',
    color: '#ffffff',
  },
  allowsPrimaryOverride: true,
  token: {
    colorPrimary: '#1890ff',
  } as any,
  badgeColor: '#1890ff',
  defaultPrimaryColor: '#1890ff',
  antDesignChartsTheme: 'classic',
  antvisG6ChartsTheme: 'light',
  userThemeConfig: null,
  setUserThemeConfig,
}

describe('ThemeManagerDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    ;(useTheme as Mock).mockReturnValue(baseThemeContext)
  })

  it('resets primary override on theme change and preserves compact density', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...baseThemeContext,
      userThemeConfig: {
        colorPrimary: '#f5222d',
        useCompactAlgorithm: true,
      },
    })

    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    fireEvent.change(screen.getByLabelText('Theme'), {
      target: { value: 'shadcn' },
    })

    expect(setCurrentTheme).toHaveBeenCalledWith('shadcn')
    expect(setUserThemeConfig).toHaveBeenCalledWith({
      useCompactAlgorithm: true,
    })
  })

  it('does not offer a deprecated theme to users who are not on it', () => {
    // Arrange & Act
    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    // Assert
    expect(
      screen.queryByRole('option', { name: 'Glass' }),
    ).not.toBeInTheDocument()
  })

  it('keeps a deprecated theme listed and selected for users still on it', () => {
    // Arrange
    ;(useTheme as Mock).mockReturnValue({
      ...baseThemeContext,
      currentTheme: 'glass',
      availableModes: ['light'],
    })

    // Act
    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    // Assert
    expect(screen.getByRole('option', { name: 'Glass' })).toBeInTheDocument()
    expect(screen.getByLabelText('Theme')).toHaveValue('glass')
  })

  it('changes mode without resetting user overrides', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...baseThemeContext,
      userThemeConfig: {
        colorPrimary: '#f5222d',
        useCompactAlgorithm: false,
      },
    })

    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    fireEvent.click(screen.getByLabelText('Dark'))

    expect(setCurrentMode).toHaveBeenCalledWith('dark')
    expect(setUserThemeConfig).not.toHaveBeenCalled()
  })

  it('hides the mode control when the theme supports a single mode', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...baseThemeContext,
      currentTheme: 'glass',
      availableModes: ['light'],
    })

    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    expect(screen.queryByText('Mode')).not.toBeInTheDocument()
  })

  it('hides primary color controls when theme does not allow primary override', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...baseThemeContext,
      allowsPrimaryOverride: false,
    })

    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    expect(screen.queryByText('Primary Color')).not.toBeInTheDocument()
  })

  it('shows primary color controls when theme allows primary override', () => {
    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    expect(screen.getByText('Primary Color')).toBeInTheDocument()
  })

  it('shows and selects the Theme Default swatch when default primary is not in presets', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...baseThemeContext,
      token: { colorPrimary: '#1f83d2' } as any,
      defaultPrimaryColor: '#1f83d2',
      userThemeConfig: null,
    })

    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    const selected = screen.getByLabelText('Theme Default (selected)')
    expect(selected).toBeInTheDocument()
    expect(selected).toHaveAttribute('aria-pressed', 'true')
  })

  it('resets all theme overrides when reset is clicked', () => {
    render(<ThemeManagerDrawer open={true} onClose={vi.fn()} />)

    fireEvent.click(screen.getByRole('button', { name: 'Reset to Defaults' }))
    expect(setUserThemeConfig).toHaveBeenCalledWith(null)
  })
})
