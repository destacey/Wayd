import { themeBalham } from 'ag-grid-community'
import useTheme, { ThemeContextType } from '../../components/contexts/theme'
import useThemeToggleMenuItem from './use-theme-toggle-menu-item'
import { Mock } from 'jest-mock'
import { BgColorsOutlined, HighlightFilled, HighlightOutlined } from '@ant-design/icons'

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

const mockThemeContext: ThemeContextType = {
  currentThemeName: 'light',
  setCurrentThemeName: jest.fn(),
  appBar: {
    backgroundColor: '#1890ff',
    color: '#ffffff',
    subtleColor: 'rgba(255,255,255,0.88)',
  },
  agGridTheme: themeBalham,
  token: mockToken as any,
  badgeColor: '#1890ff',
  antDesignChartsTheme: 'classic',
  antvisG6ChartsTheme: 'light',
  userThemeConfig: null,
  setUserThemeConfig: jest.fn(),
}

describe('useThemeToggleMenuItem', () => {
  beforeEach(() => {
    jest.resetAllMocks()
    ;(useTheme as Mock).mockReturnValue(mockThemeContext)
  })

  it('returns correct menu item structure', () => {
    const themeToggle = useThemeToggleMenuItem()
    expect(themeToggle).toMatchObject({
      key: 'theme',
      label: 'Theme: Light',
      icon: expect.any(Object),
      onClick: expect.any(Function),
    })
  })

  it('cycles from light to dark theme when clicked', () => {
    const themeToggle = useThemeToggleMenuItem()
    themeToggle.onClick()

    expect(mockThemeContext.setCurrentThemeName).toHaveBeenCalledWith('dark')
  })

  it('cycles from dark to slate theme when clicked', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'dark',
    })

    const themeToggle = useThemeToggleMenuItem()
    themeToggle.onClick()

    expect(mockThemeContext.setCurrentThemeName).toHaveBeenCalledWith('slate')
  })

  it('cycles from slate to cartoon theme when clicked', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'slate',
    })

    const themeToggle = useThemeToggleMenuItem()
    themeToggle.onClick()

    expect(mockThemeContext.setCurrentThemeName).toHaveBeenCalledWith('cartoon')
  })

  it('cycles from cartoon to shadcn theme when clicked', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'cartoon',
    })

    const themeToggle = useThemeToggleMenuItem()
    themeToggle.onClick()

    expect(mockThemeContext.setCurrentThemeName).toHaveBeenCalledWith('shadcn')
  })

  it('cycles from shadcn to glass theme when clicked', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'shadcn',
    })

    const themeToggle = useThemeToggleMenuItem()
    themeToggle.onClick()

    expect(mockThemeContext.setCurrentThemeName).toHaveBeenCalledWith('glass')
  })

  it('cycles from glass to geek theme when clicked', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'glass',
    })

    const themeToggle = useThemeToggleMenuItem()
    themeToggle.onClick()

    expect(mockThemeContext.setCurrentThemeName).toHaveBeenCalledWith('geek')
  })

  it('cycles from geek to light theme when clicked', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'geek',
    })

    const themeToggle = useThemeToggleMenuItem()
    themeToggle.onClick()

    expect(mockThemeContext.setCurrentThemeName).toHaveBeenCalledWith('light')
  })

  it('uses the correct icon for the light theme', () => {
    const themeToggle = useThemeToggleMenuItem()
    const icon = themeToggle.icon as React.ReactElement
    expect(icon.type).toBe(HighlightOutlined)
  })

  it('uses the correct icon for the dark theme', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'dark',
    })

    const themeToggle = useThemeToggleMenuItem()
    const icon = themeToggle.icon as React.ReactElement
    expect(icon.type).toBe(HighlightFilled)
  })

  it('uses the correct icon for the cartoon theme', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'cartoon',
    })

    const themeToggle = useThemeToggleMenuItem()
    const icon = themeToggle.icon as React.ReactElement
    expect(icon.type).toBe(BgColorsOutlined)
  })

  it('uses the correct icon for the shadcn theme', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'shadcn',
    })

    const themeToggle = useThemeToggleMenuItem()
    const icon = themeToggle.icon as React.ReactElement
    expect(icon.type).toBe(BgColorsOutlined)
  })

  it('uses the correct icon for the glass theme', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'glass',
    })

    const themeToggle = useThemeToggleMenuItem()
    const icon = themeToggle.icon as React.ReactElement
    expect(icon.type).toBe(BgColorsOutlined)
  })

  it('uses the correct icon for the geek theme', () => {
    ;(useTheme as Mock).mockReturnValue({
      ...mockThemeContext,
      currentThemeName: 'geek',
    })

    const themeToggle = useThemeToggleMenuItem()
    const icon = themeToggle.icon as React.ReactElement
    expect(icon.type).toBe(BgColorsOutlined)
  })
})
