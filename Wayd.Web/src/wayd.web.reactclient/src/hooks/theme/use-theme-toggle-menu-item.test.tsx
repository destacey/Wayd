import useTheme, { ThemeContextType } from '../../components/contexts/theme'
import useThemeToggleMenuItem from './use-theme-toggle-menu-item'
import { Mock } from 'jest-mock'
import {
  BgColorsOutlined,
  HighlightFilled,
  HighlightOutlined,
} from '@ant-design/icons'

jest.mock('../../components/contexts/theme', () => ({
  __esModule: true,
  default: jest.fn(),
}))

const mockToken = {
  colorPrimary: '#1890ff',
  colorSuccess: '#52c41a',
  colorWarning: '#faad14',
  colorError: '#ff4d4f',
  colorInfo: '#1890ff',
  colorTextBase: '#000000',
  colorBgBase: '#ffffff',
  fontSize: 14,
  borderRadius: 6,
  wireframe: false,
  colorBgContainer: '#ffffff',
  colorText: '#000000',
  colorTextSecondary: '#666666',
}

const setCurrentMode = jest.fn()

const mockThemeContext: ThemeContextType = {
  currentTheme: 'wayd',
  currentMode: 'light',
  availableModes: ['light', 'dark', 'slate'],
  setCurrentTheme: jest.fn(),
  setCurrentMode,
  appBar: {
    backgroundColor: '#1890ff',
    color: '#ffffff',
    subtleColor: 'rgba(255,255,255,0.88)',
  },
  allowsPrimaryOverride: true,
  token: mockToken as any,
  badgeColor: '#1890ff',
  defaultPrimaryColor: '#1890ff',
  antDesignChartsTheme: 'classic',
  antvisG6ChartsTheme: 'light',
  userThemeConfig: null,
  setUserThemeConfig: jest.fn(),
}

describe('useThemeToggleMenuItem', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    ;(useTheme as Mock).mockReturnValue(mockThemeContext)
  })

  it('returns correct menu item structure', () => {
    // Arrange & Act
    const modeToggle = useThemeToggleMenuItem()

    // Assert
    expect(modeToggle).toMatchObject({
      key: 'theme-mode',
      label: 'Mode: Light',
      icon: expect.any(Object),
      onClick: expect.any(Function),
    })
  })

  it('cycles from light to dark mode when clicked', () => {
    // Arrange
    const modeToggle = useThemeToggleMenuItem()

    // Act
    modeToggle!.onClick()

    // Assert
    expect(setCurrentMode).toHaveBeenCalledWith('dark')
  })

  it('cycles from dark to slate mode when clicked', () => {
    // Arrange
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentMode: 'dark',
    })
    const modeToggle = useThemeToggleMenuItem()

    // Act
    modeToggle!.onClick()

    // Assert
    expect(setCurrentMode).toHaveBeenCalledWith('slate')
  })

  it('cycles from slate back to light mode when clicked', () => {
    // Arrange
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentMode: 'slate',
    })
    const modeToggle = useThemeToggleMenuItem()

    // Act
    modeToggle!.onClick()

    // Assert
    expect(setCurrentMode).toHaveBeenCalledWith('light')
  })

  it('returns null when the theme supports a single mode', () => {
    // Arrange
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentTheme: 'glass',
      currentMode: 'light',
      availableModes: ['light'],
    })

    // Act
    const modeToggle = useThemeToggleMenuItem()

    // Assert
    expect(modeToggle).toBeNull()
  })

  it('shows the light mode icon in light mode', () => {
    // Arrange & Act
    const modeToggle = useThemeToggleMenuItem()

    // Assert
    expect(modeToggle!.icon).toEqual(<HighlightOutlined />)
  })

  it('shows the dark mode icon in dark mode', () => {
    // Arrange
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentMode: 'dark',
    })

    // Act
    const modeToggle = useThemeToggleMenuItem()

    // Assert
    expect(modeToggle!.icon).toEqual(<HighlightFilled />)
  })

  it('shows the slate mode icon in slate mode', () => {
    // Arrange
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentMode: 'slate',
    })

    // Act
    const modeToggle = useThemeToggleMenuItem()

    // Assert
    expect(modeToggle!.icon).toEqual(<BgColorsOutlined />)
  })
})
