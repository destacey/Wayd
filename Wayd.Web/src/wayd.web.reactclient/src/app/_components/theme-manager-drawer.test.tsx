import React from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import ThemeManagerDrawer from './theme-manager-drawer'
import useTheme, { ThemeContextType } from '@/src/components/contexts/theme'

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: jest.fn(),
}))

jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
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
        aria-label="Mode"
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
            aria-label={`Density ${o.label}`}
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

const setCurrentThemeName = jest.fn()
const setUserThemeConfig = jest.fn()

const baseThemeContext: ThemeContextType = {
  currentThemeName: 'light',
  setCurrentThemeName,
  appBar: {
    backgroundColor: '#1890ff',
    color: '#ffffff',
  },
  allowsPrimaryOverride: true,
  agGridTheme: {} as any,
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
    jest.clearAllMocks()
    ;(useTheme as jest.Mock).mockReturnValue(baseThemeContext)
  })

  it('resets primary override on mode change and preserves compact density', () => {
    ;(useTheme as jest.Mock).mockReturnValue({
      ...baseThemeContext,
      userThemeConfig: {
        colorPrimary: '#f5222d',
        useCompactAlgorithm: true,
      },
    })

    render(<ThemeManagerDrawer open={true} onClose={jest.fn()} />)

    fireEvent.change(screen.getByLabelText('Mode'), {
      target: { value: 'dark' },
    })

    expect(setCurrentThemeName).toHaveBeenCalledWith('dark')
    expect(setUserThemeConfig).toHaveBeenCalledWith({ useCompactAlgorithm: true })
  })

  it('hides primary color controls when theme does not allow primary override', () => {
    ;(useTheme as jest.Mock).mockReturnValue({
      ...baseThemeContext,
      allowsPrimaryOverride: false,
    })

    render(<ThemeManagerDrawer open={true} onClose={jest.fn()} />)

    expect(screen.queryByText('Primary Color')).not.toBeInTheDocument()
  })

  it('shows primary color controls when theme allows primary override', () => {
    render(<ThemeManagerDrawer open={true} onClose={jest.fn()} />)

    expect(screen.getByText('Primary Color')).toBeInTheDocument()
  })

  it('shows and selects the Theme Default swatch when default primary is not in presets', () => {
    ;(useTheme as jest.Mock).mockReturnValue({
      ...baseThemeContext,
      token: { colorPrimary: '#1f83d2' } as any,
      defaultPrimaryColor: '#1f83d2',
      userThemeConfig: null,
    })

    render(<ThemeManagerDrawer open={true} onClose={jest.fn()} />)

    const selected = screen.getByLabelText('Theme Default (selected)')
    expect(selected).toBeInTheDocument()
    expect(selected).toHaveAttribute('aria-pressed', 'true')
  })

  it('resets all theme overrides when reset is clicked', () => {
    render(<ThemeManagerDrawer open={true} onClose={jest.fn()} />)

    fireEvent.click(screen.getByRole('button', { name: 'Reset to Defaults' }))
    expect(setUserThemeConfig).toHaveBeenCalledWith(null)
  })
})

