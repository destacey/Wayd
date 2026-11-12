import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ReleaseDto } from '@/src/services/wayd-api'
import ReleasesGrid from './releases-grid'

const release = (
  key: number,
  version: string,
  severity?: { name: string; color: string },
): ReleaseDto =>
  ({
    id: `r${key}`,
    key,
    version,
    status: { id: 's1', name: 'Planned', category: 0, alias: 0 },
    versions: [],
    packages: [],
    activeIncidentSeverityName: severity?.name,
    activeIncidentSeverityColor: severity?.color,
  }) as unknown as ReleaseDto

const cellBackground = (cell: HTMLElement) =>
  cell.style.backgroundColor || cell.style.background

describe('ReleasesGrid', () => {
  it('counts active incidents in a pill and does not tint the row', async () => {
    // Arrange
    const hotRelease = release(1, '2026.07', {
      name: 'SEV-2',
      color: '#D4A017',
    })
    ;(hotRelease as { activeIncidents: unknown }).activeIncidents = [
      {
        key: 4,
        title: 'Pricing stall',
        severityName: 'SEV-2',
        severityColor: '#D4A017',
        statusName: 'Open',
      },
    ]
    const releases = [hotRelease, release(2, '2026.08')]

    // Act
    render(<ReleasesGrid releases={releases} isLoading={false} />)

    // Assert
    expect(
      screen.getByRole('columnheader', { name: /Incident/ }),
    ).toBeInTheDocument()
    const hot = screen.getByText('2026.07').closest('tr')!
    const pill = screen.getByLabelText('1 active incident')
    expect(pill).toHaveTextContent('1')
    pill.focus()
    expect(pill).toHaveFocus()
    const link = await screen.findByRole('link', { name: /Pricing stall/ })
    expect(link).toHaveAttribute('href', '/product-management/incidents/4')
    expect(link).toHaveTextContent('Open')
    const hotCells = hot.querySelectorAll('td')
    expect(hotCells.length).toBeGreaterThan(1)
    for (const cell of hotCells) {
      expect(cellBackground(cell as HTMLElement)).toBe('')
    }

    const cool = screen.getByText('2026.08').closest('tr')!
    for (const cell of cool.querySelectorAll('td')) {
      expect(cellBackground(cell as HTMLElement)).toBe('')
    }
  })
})
