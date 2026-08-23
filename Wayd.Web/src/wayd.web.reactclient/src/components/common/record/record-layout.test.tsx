import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Grid } from 'antd'
import RecordLayout from './record-layout'

const mockReplace = jest.fn()
let mockParams = new URLSearchParams()

jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockReplace }),
  usePathname: () => '/organizations/teams/14',
  useSearchParams: () => mockParams,
}))

jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  return {
    ...actual,
    Grid: { ...actual.Grid, useBreakpoint: jest.fn() },
  }
})

const mockBreakpoint = Grid.useBreakpoint as unknown as jest.Mock

const SECTIONS = [
  { id: 'overview', label: 'Overview' },
  { id: 'backlog', label: 'Backlog', count: 42 },
]
const REPORTS = [{ id: 'cycle-time', label: 'Cycle Time' }]

const renderLayout = () =>
  render(
    <RecordLayout
      sections={SECTIONS}
      reports={REPORTS}
      defaultSection="overview"
    >
      {(section) => <div>section: {section}</div>}
    </RecordLayout>,
  )

describe('RecordLayout', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockParams = new URLSearchParams()
    mockBreakpoint.mockReturnValue({ md: true, lg: true })
  })

  it('renders the default section when the URL carries no param', () => {
    // Arrange / Act
    renderLayout()

    // Assert
    expect(screen.getByText('section: overview')).toBeInTheDocument()
  })

  it('renders the section named in the URL', () => {
    // Arrange
    mockParams = new URLSearchParams('section=backlog')

    // Act
    renderLayout()

    // Assert
    expect(screen.getByText('section: backlog')).toBeInTheDocument()
  })

  it('falls back to the default when the URL names an unknown section', () => {
    // Arrange — a shared link can reach someone without access to that section
    mockParams = new URLSearchParams('section=nope')

    // Act
    renderLayout()

    // Assert
    expect(screen.getByText('section: overview')).toBeInTheDocument()
  })

  it('writes a bare URL for the default section', async () => {
    // Arrange
    mockParams = new URLSearchParams('section=backlog')
    renderLayout()

    // Act
    await userEvent.click(screen.getByRole('tab', { name: /Overview/ }))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith('/organizations/teams/14', {
      scroll: false,
    })
  })

  it('writes a section param for any other section, without scrolling', async () => {
    // Arrange
    renderLayout()

    // Act
    await userEvent.click(screen.getByRole('tab', { name: /Backlog/ }))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/organizations/teams/14?section=backlog',
      { scroll: false },
    )
  })

  it('addresses reports the same way as sections', async () => {
    // Arrange
    renderLayout()

    // Act
    await userEvent.click(screen.getByRole('tab', { name: /Cycle Time/ }))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/organizations/teams/14?section=cycle-time',
      { scroll: false },
    )
  })

  it('heads the content with the active section label', () => {
    // Arrange
    mockParams = new URLSearchParams('section=backlog')

    // Act
    renderLayout()

    // Assert — the rail marks position; the content needs its own heading, or
    // a section opens as an unlabelled grid under the identity bar.
    expect(screen.getByRole('heading', { name: 'Backlog' })).toBeInTheDocument()
  })

  it('places section actions beside that heading', () => {
    // Arrange / Act
    render(
      <RecordLayout
        sections={SECTIONS}
        defaultSection="overview"
        sectionActions={<button>Add item</button>}
      >
        {() => <div>content</div>}
      </RecordLayout>,
    )

    // Assert
    expect(
      screen.getByRole('button', { name: 'Add item' }),
    ).toBeInTheDocument()
  })

  it('omits the heading for a section that renders its own', () => {
    // Arrange — the cycle time report titles itself alongside its controls, so
    // the layout heading would stack a duplicate above it.
    mockParams = new URLSearchParams('section=cycle-time')

    // Act
    render(
      <RecordLayout
        sections={SECTIONS}
        reports={[
          { id: 'cycle-time', label: 'Cycle Time', hideHeading: true },
        ]}
        defaultSection="overview"
      >
        {() => <div>report content</div>}
      </RecordLayout>,
    )

    // Assert — still reachable in the rail, just not repeated in the content
    expect(
      screen.queryByRole('heading', { name: 'Cycle Time' }),
    ).toBeNull()
    expect(screen.getByRole('tab', { name: /Cycle Time/ })).toBeInTheDocument()
  })

  it('shows counts in the rail', () => {
    // Arrange / Act
    renderLayout()

    // Assert
    expect(screen.getByText('42')).toBeInTheDocument()
  })

  it('renders a Select instead of the rail below the md breakpoint', () => {
    // Arrange
    mockBreakpoint.mockReturnValue({ md: false })

    // Act
    renderLayout()

    // Assert
    expect(screen.queryByRole('tablist')).toBeNull()
    expect(screen.getByRole('combobox')).toBeInTheDocument()
  })

  it('keeps the rail usable when a section throws', () => {
    // Arrange
    const Boom = () => {
      throw new Error('section blew up')
    }
    const spy = jest.spyOn(console, 'error').mockImplementation(() => {})

    // Act
    render(
      <RecordLayout sections={SECTIONS} defaultSection="overview">
        {() => <Boom />}
      </RecordLayout>,
    )

    // Assert — the failure is contained; navigation survives
    expect(
      screen.getByText('This section could not be loaded'),
    ).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: /Backlog/ })).toBeInTheDocument()

    spy.mockRestore()
  })
})
