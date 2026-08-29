import { render, screen } from '@testing-library/react'
import { UserDetailsDto } from '@/src/services/wayd-api'
import UserOverview from './user-overview'

jest.mock('next/link', () => {
  const MockLink = ({ href, children }: any) => <a href={href}>{children}</a>
  MockLink.displayName = 'MockLink'
  return MockLink
})

// The identity history grid has its own tests; here it only needs to not reach
// for the API.
jest.mock('../../_components/user-identity-history', () => ({
  __esModule: true,
  default: () => <div>identity history</div>,
}))

const user = (overrides: Partial<UserDetailsDto> = {}): UserDetailsDto =>
  ({
    id: 'e1a7b300-0000-0000-0000-000000000001',
    userName: 'rmorgan',
    firstName: 'Rowan',
    lastName: 'Morgan',
    email: 'rowan.morgan@acme.example',
    isActive: true,
    phoneNumber: '+1 555 0100',
    loginProvider: 'Wayd',
    roles: [],
    ...overrides,
  }) as UserDetailsDto

/** The value rendered under a given label. */
const valueFor = (label: string) =>
  screen.getByText(label).parentElement?.textContent?.replace(label, '')

describe('UserOverview', () => {
  describe('account', () => {
    it('renders the account fields', () => {
      // Arrange / Act
      render(<UserOverview user={user()} />)

      // Assert
      expect(valueFor('User Name')).toBe('rmorgan')
      expect(valueFor('Email')).toBe('rowan.morgan@acme.example')
      expect(valueFor('First Name')).toBe('Rowan')
      expect(valueFor('Last Name')).toBe('Morgan')
      expect(valueFor('Phone Number')).toBe('+1 555 0100')
    })

    it('names Entra rather than showing its provider id', () => {
      // Arrange / Act — "MicrosoftEntraId" is not something to show a person
      render(<UserOverview user={user({ loginProvider: 'MicrosoftEntraId' })} />)

      // Assert
      expect(valueFor('Login Provider')).toBe('Microsoft Entra ID')
    })

    it('says Never rather than leaving last activity blank', () => {
      // Arrange / Act — an empty value reads as a missing field rather than a
      // user who has not signed in
      render(<UserOverview user={user({ lastActivityAt: undefined })} />)

      // Assert
      expect(valueFor('Last Activity')).toBe('Never')
    })

    it('says Not set for an absent phone number', () => {
      // Arrange / Act — the field keeps its place; a section has the width for
      // it, and a vanishing row shifts everything beside it.
      render(<UserOverview user={user({ phoneNumber: undefined })} />)

      // Assert
      expect(valueFor('Phone Number')).toBe('Not set')
    })

    it('leads with a lockout that is still in effect', () => {
      // Arrange — a locked account is the first thing an admin needs to see,
      // so it is an alert at the top of the card rather than a tag among the
      // fields.
      const future = new Date(Date.now() + 60 * 60 * 1000)

      // Act
      render(<UserOverview user={user({ lockoutEnd: future })} />)

      // Assert
      expect(screen.getByText('Account locked')).toBeInTheDocument()
    })

    it('ignores a lockout that has expired', () => {
      // Arrange — a past lockoutEnd means the account is no longer locked
      const past = new Date(Date.now() - 60 * 60 * 1000)

      // Act
      render(<UserOverview user={user({ lockoutEnd: past })} />)

      // Assert
      expect(screen.queryByText('Account locked')).not.toBeInTheDocument()
    })

    it('links the employee the account belongs to', () => {
      // Arrange / Act
      render(
        <UserOverview
          user={user({ employee: { id: 'x', key: 42, name: 'Rowan Morgan' } })}
        />,
      )

      // Assert
      expect(screen.getByRole('link', { name: 'Rowan Morgan' })).toHaveAttribute(
        'href',
        '/organizations/employees/42',
      )
    })

    it('says so when no employee is linked', () => {
      // Arrange / Act — an unlinked account is a real state, not a blank
      render(<UserOverview user={user({ employee: undefined })} />)

      // Assert
      expect(valueFor('Employee')).toBe('Not linked')
    })
  })

  describe('roles', () => {
    it('links each role and counts them in the heading', () => {
      // Arrange / Act
      render(
        <UserOverview
          user={user({
            roles: [
              { id: 'r1', name: 'Admin' },
              { id: 'r2', name: 'Delivery Lead' },
            ] as any,
          })}
        />,
      )

      // Assert
      expect(screen.getByText('Roles (2)')).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'Admin' })).toHaveAttribute(
        'href',
        '/settings/user-management/roles/r1',
      )
      expect(
        screen.getByRole('link', { name: 'Delivery Lead' }),
      ).toBeInTheDocument()
    })

    it('sorts roles case insensitively', () => {
      // Arrange / Act — a plain sort would put every capitalised name first
      render(
        <UserOverview
          user={user({
            roles: [
              { id: 'r1', name: 'delivery lead' },
              { id: 'r2', name: 'Admin' },
            ] as any,
          })}
        />,
      )

      // Assert
      const links = screen.getAllByRole('link').map((l) => l.textContent)
      expect(links).toEqual(['Admin', 'delivery lead'])
    })

    it('explains what no roles means rather than showing an empty card', () => {
      // Arrange / Act
      render(<UserOverview user={user({ roles: [] })} />)

      // Assert
      expect(screen.getByText(/no roles/)).toBeInTheDocument()
      expect(screen.getByText('Roles')).toBeInTheDocument()
    })
  })
})
