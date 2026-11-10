import type { Mock } from 'vitest'
import React from 'react'
import { render, screen } from '@testing-library/react'
import { Grid } from 'antd'
import RecordLayout from './record-layout'

let mockParams = new URLSearchParams()

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn() }),
  usePathname: () => '/organizations/teams/4',
  useSearchParams: () => mockParams,
}))

vi.mock('antd', async () => {
  const actual = await vi.importActual<any>('antd')
  return {
    ...actual,
    Grid: { ...actual.Grid, useBreakpoint: vi.fn() },
  }
})

const mockBreakpoint = Grid.useBreakpoint as unknown as Mock

describe('RecordLayout — rendering before data arrives', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockBreakpoint.mockReturnValue({ md: true })
  })

  it('renders the requested section immediately, not only the default', () => {
    // Arrange — a deep link straight into a non-default section. RecordLayout
    // renders the active section on first paint, so a page whose sections
    // dereference its record must guard on load before rendering this.
    mockParams = new URLSearchParams('section=backlog')

    const seen: string[] = []

    // Act
    render(
      <RecordLayout
        sections={[
          { id: 'details', label: 'Details' },
          { id: 'backlog', label: 'Backlog' },
        ]}
        defaultSection="details"
      >
        {(section) => {
          seen.push(section)
          return <div>rendered: {section}</div>
        }}
      </RecordLayout>,
    )

    // Assert
    expect(seen).toContain('backlog')
    expect(screen.getByText('rendered: backlog')).toBeInTheDocument()
  })
})
