import { render, screen, within } from '@testing-library/react'
import { ProgramDetailsDto } from '@/src/services/wayd-api'
import ProgramFacts from './program-facts'

jest.mock('next/link', () => {
  const MockLink = ({ href, children }: any) => <a href={href}>{children}</a>
  MockLink.displayName = 'MockLink'
  return MockLink
})

jest.mock('@/src/components/common/links/links-card', () => ({
  __esModule: true,
  default: () => <div data-testid="links-card" />,
}))

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeByIdQuery: () => ({
    data: undefined,
    isLoading: false,
    isError: false,
  }),
}))

const theme = (id: string, key: number, name: string) => ({ id, key, name })

const baseProgram = {
  id: 'aaaaaaaa-0000-4000-a000-000000000001',
  key: 12,
  name: 'Atlas Program',
  description: '',
  status: { id: '1', name: 'Active' },
  portfolio: { id: 'p1', key: 7, name: 'Core Platform' },
  programSponsors: [],
  programOwners: [],
  programManagers: [],
  strategicThemes: [],
  canManageProgram: false,
} as unknown as ProgramDetailsDto

const renderFacts = (overrides: Partial<ProgramDetailsDto> = {}) =>
  render(<ProgramFacts program={{ ...baseProgram, ...overrides }} />)

describe('ProgramFacts', () => {
  it('renders strategic themes as a list rather than one joined string', () => {
    // Arrange / Act
    renderFacts({
      strategicThemes: [
        theme('t1', 1, 'Resilience'),
        theme('t2', 2, 'Cost Control'),
      ],
    } as unknown as Partial<ProgramDetailsDto>)

    // Assert — each theme is its own item, so none of them run together.
    const items = screen.getAllByRole('listitem').map((li) => li.textContent)
    expect(items).toContain('Resilience')
    expect(items).toContain('Cost Control')
  })

  it('sorts themes case-insensitively rather than by codepoint', () => {
    // Arrange — chosen so a plain .sort() disagrees: capitals sort ahead of
    // lowercase by codepoint, putting "Banana" first.
    // Act
    renderFacts({
      strategicThemes: [
        theme('t1', 1, 'apple Pie'),
        theme('t2', 2, 'Banana Split'),
      ],
    } as unknown as Partial<ProgramDetailsDto>)

    // Assert
    const items = screen.getAllByRole('listitem').map((li) => li.textContent)
    expect(items).toEqual(['apple Pie', 'Banana Split'])
  })

  it('omits the themes row entirely when the program has none', () => {
    // Arrange / Act
    renderFacts()

    // Assert
    expect(screen.queryByText('Strategic Themes')).not.toBeInTheDocument()
  })

  it('lists the roles sponsors first, matching the other PPM records', () => {
    // Arrange / Act
    renderFacts()

    // Assert
    const roles = screen.getByText('Roles').parentElement!
    const labels = within(roles)
      .getAllByText(/^(Sponsors|Owners|PMs)$/)
      .map((el) => el.textContent)
    expect(labels).toEqual(['Sponsors', 'Owners', 'PMs'])
  })
})
