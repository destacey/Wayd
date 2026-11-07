import { fireEvent, render, screen, within } from '@testing-library/react'
import SettingsNavBar from './settings-nav-bar'

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasClaim: () => true }),
}))

jest.mock('@/src/hooks', () => {
  const actual = jest.requireActual('@/src/hooks')
  return { ...actual, useFeatureFlag: () => ({ isEnabled: false }) }
})

jest.mock('@/src/store/features/admin/imports-api', () => ({
  useGetImportDefinitionsQuery: () => ({ data: [] }),
}))

jest.mock('@/src/store/features/common/status-workflows-api', () => ({
  useGetWorkflowOwnerTypesQuery: () => ({ data: [] }),
}))

let mockPathname = '/settings/user-management/users'

jest.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
}))

const trigger = () => screen.getByRole('button', { name: /^Settings menu:/ })

describe('SettingsNavBar', () => {
  beforeEach(() => {
    mockPathname = '/settings/user-management/users'
  })

  it('names the current group and page', () => {
    // Arrange
    // Act
    render(<SettingsNavBar />)

    // Assert
    expect(trigger()).toHaveTextContent('Access › Users')
  })

  it('says where the viewer is in its accessible name', () => {
    // Arrange
    // Act
    render(<SettingsNavBar />)

    // Assert
    expect(trigger()).toHaveAccessibleName('Settings menu: Access › Users')
    expect(trigger()).toHaveAttribute('aria-haspopup', 'dialog')
  })

  it('falls back to Settings on a settings page the menu omits', () => {
    // Arrange — Estimation Scales is listed only with planning-poker on, and
    // every flag is off here; the segment fallback would name Users instead.
    mockPathname = '/settings/planning/estimation-scales'

    // Act
    render(<SettingsNavBar />)

    // Assert
    expect(trigger()).toHaveAccessibleName('Settings menu: Settings')
  })

  it('opens the settings menu in a drawer', async () => {
    // Arrange
    render(<SettingsNavBar />)

    // Act
    fireEvent.click(trigger())

    // Assert
    const drawer = await screen.findByRole('dialog')
    expect(within(drawer).getByLabelText('Find a setting')).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Roles' })).toBeVisible()
    expect(trigger()).toHaveAttribute('aria-expanded', 'true')
  })

  it('closes the drawer when an item is picked', async () => {
    // Arrange
    render(<SettingsNavBar />)
    fireEvent.click(trigger())
    const drawer = await screen.findByRole('dialog')

    // Act
    fireEvent.click(within(drawer).getByRole('link', { name: 'Roles' }))

    // Assert
    expect(trigger()).toHaveAttribute('aria-expanded', 'false')
  })

  it('closes the drawer when the page changes by another route', async () => {
    // Arrange
    const { rerender } = render(<SettingsNavBar />)
    fireEvent.click(trigger())
    await screen.findByRole('dialog')

    // Act
    mockPathname = '/settings/user-management/roles'
    rerender(<SettingsNavBar />)

    // Assert
    expect(trigger()).toHaveAttribute('aria-expanded', 'false')
    expect(trigger()).toHaveTextContent('Access › Roles')
  })
})
