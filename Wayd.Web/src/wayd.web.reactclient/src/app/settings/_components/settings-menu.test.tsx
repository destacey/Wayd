import { render, screen, fireEvent, within } from '@testing-library/react'
import SettingsMenu from './settings-menu'

const mockHasClaim = jest.fn()
/** Flag name → enabled. The rail reads more than one flag, so the mock keys on
 *  the name rather than answering the same for all of them. */
const mockFlags: Record<string, boolean> = {}

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasClaim: mockHasClaim }),
}))

jest.mock('@/src/hooks', () => {
  const actual = jest.requireActual('@/src/hooks')
  return {
    ...actual,
    useFeatureFlag: (name: string) => ({ isEnabled: mockFlags[name] ?? false }),
  }
})

jest.mock('next/navigation', () => ({
  usePathname: () => '/settings/user-management/users',
}))

const renderMenu = () => render(<SettingsMenu />)

/** The visible group headings, in rail order. */
const groups = () =>
  Array.from(document.querySelectorAll('.ant-menu-item-group-title')).map((el) =>
    el.textContent?.trim(),
  )

const allowAll = () => mockHasClaim.mockReturnValue(true)
const allowOnly = (...granted: string[]) =>
  mockHasClaim.mockImplementation((_type: string, value: string) =>
    granted.includes(value),
  )

describe('SettingsMenu', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    allowAll()
    mockFlags['planning-poker'] = true
    mockFlags['product-management'] = true
  })

  describe('grouping', () => {
    it('renders the seven groups in order', () => {
      // Arrange / Act — most-visited first, System last
      renderMenu()

      // Assert
      expect(groups()).toEqual([
        'Access',
        'Organization',
        'Planning',
        'Product Management',
        'PPM',
        'Work Management',
        'System',
      ])
    })

    it('puts scoring models under PPM', () => {
      // Arrange — a scoring model exists to rank projects, so it belongs with
      // the other PPM configuration rather than in a group of its own.
      // Act
      renderMenu()

      // Assert
      expect(screen.getByText('Scoring Models')).toBeInTheDocument()
    })

    it('puts connections under System', () => {
      // Act
      renderMenu()

      // Assert
      expect(screen.getByText('Connections')).toBeInTheDocument()
    })

    it('hides Planning when the planning poker flag is off', () => {
      // Arrange — the group holds only estimation scales, so the flag takes
      // the whole group with it.
      mockFlags['planning-poker'] = false

      // Act
      renderMenu()

      // Assert
      expect(groups()).not.toContain('Planning')
    })

    it('hides Product Management when its flag is off', () => {
      // Arrange — the group holds only product tags, so the flag takes the
      // whole group with it.
      mockFlags['product-management'] = false

      // Act
      renderMenu()

      // Assert
      expect(groups()).not.toContain('Product Management')
    })

    it('drops a group the viewer can see nothing in', () => {
      // Arrange — a heading over nothing is worse than no heading. Work
      // Management carries no View permission, so it always survives.
      allowOnly('Permissions.Users.View')

      // Act
      renderMenu()

      // Assert
      expect(groups()).toEqual(['Access', 'Work Management'])
    })

    it('renders groups as headings, not collapsible submenus', () => {
      // Arrange / Act — every item stays one click away. Fifteen items fit a
      // viewport, and collapsing would cost a click on each move between
      // groups, which is the common way people work in settings.
      renderMenu()

      // Assert
      expect(document.querySelector('.ant-menu-submenu')).toBeNull()
      expect(screen.getByText('Scoring Models')).toBeInTheDocument()
      expect(screen.getByText('Messaging')).toBeInTheDocument()
    })

    it('keeps a group when the viewer can see only one of its items', () => {
      // Arrange
      allowOnly('Permissions.ScoringModels.View')

      // Act
      renderMenu()

      // Assert
      expect(screen.getByText('Scoring Models')).toBeInTheDocument()
      expect(screen.queryByText('Expenditure Categories')).not.toBeInTheDocument()
    })
  })

  describe('filtering', () => {
    it('narrows to the matching item', () => {
      // Arrange
      renderMenu()

      // Act
      fireEvent.change(screen.getByLabelText('Find a setting'), {
        target: { value: 'scoring' },
      })

      // Assert — only PPM survives, and only its matching child
      expect(groups()).toEqual(['PPM'])
      expect(screen.getByText('Scoring Models')).toBeInTheDocument()
      expect(screen.queryByText('Project Lifecycles')).not.toBeInTheDocument()
    })

    it('matches a group by its own name and keeps its items', () => {
      // Arrange
      renderMenu()

      // Act
      fireEvent.change(screen.getByLabelText('Find a setting'), {
        target: { value: 'access' },
      })

      // Assert
      expect(groups()).toEqual(['Access'])
      expect(screen.getByText('Users')).toBeInTheDocument()
      expect(screen.getByText('Roles')).toBeInTheDocument()
    })

    it('is case insensitive', () => {
      // Arrange
      renderMenu()

      // Act
      fireEvent.change(screen.getByLabelText('Find a setting'), {
        target: { value: 'MESSAGING' },
      })

      // Assert
      expect(groups()).toEqual(['System'])
    })

    it('says so when nothing matches', () => {
      // Arrange — an empty rail with no explanation reads as a broken menu
      renderMenu()

      // Act
      fireEvent.change(screen.getByLabelText('Find a setting'), {
        target: { value: 'zzzz' },
      })

      // Assert
      expect(groups()).toEqual([])
      expect(screen.getByText(/No settings match/)).toBeInTheDocument()
    })

    it('restores the full menu when the query is cleared', () => {
      // Arrange
      renderMenu()
      const input = screen.getByLabelText('Find a setting')
      fireEvent.change(input, { target: { value: 'scoring' } })

      // Act
      fireEvent.change(input, { target: { value: '' } })

      // Assert
      expect(groups()).toHaveLength(7)
    })
  })


  describe('current page', () => {
    it('marks the item for the current route', () => {
      // Arrange / Act — the mocked pathname is the users list
      renderMenu()

      // Assert
      const selected = document.querySelector('.ant-menu-item-selected')
      expect(selected).not.toBeNull()
      expect(within(selected as HTMLElement).getByText('Users')).toBeInTheDocument()
    })
  })
})
