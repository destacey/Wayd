// jest.setup stubs dayjs down to formatting, which leaves the grid's date columns without
// dayjs.isDayjs. The grid needs the real module.
jest.unmock('dayjs')

import { render, screen } from '@testing-library/react'
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
  it('tints every cell from the highest active incident and links each one', () => {
    // Arrange
    const hotRelease = release(1, '2026.07', { name: 'SEV-2', color: '#D4A017' })
    ;(hotRelease as { activeIncidents: unknown }).activeIncidents = [
      { key: 4, title: 'Pricing stall', severityName: 'SEV-2' },
    ]
    const releases = [hotRelease, release(2, '2026.08')]

    // Act
    render(<ReleasesGrid releases={releases} isLoading={false} />)

    // Assert
    expect(
      screen.getByRole('columnheader', { name: /Incident/ }),
    ).toBeInTheDocument()
    const hot = screen.getByText('2026.07').closest('tr')!
    expect(screen.getByRole('link', { name: 'SEV-2' })).toHaveAttribute(
      'href',
      '/product-management/incidents/4',
    )
    const hotCells = hot.querySelectorAll('td')
    expect(hotCells.length).toBeGreaterThan(1)
    for (const cell of hotCells) {
      const background = cellBackground(cell as HTMLElement)
      expect(background).toContain('color-mix')
      expect(background).toContain('rgb(212, 160, 23)')
    }

    const cool = screen.getByText('2026.08').closest('tr')!
    for (const cell of cool.querySelectorAll('td')) {
      expect(cellBackground(cell as HTMLElement)).toBe('')
    }
  })
})
