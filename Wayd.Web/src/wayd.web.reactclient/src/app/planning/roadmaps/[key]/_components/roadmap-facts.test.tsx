import { render, screen } from '@testing-library/react'
import RoadmapFacts from './roadmap-facts'

jest.unmock('dayjs')

const roadmap = {
  id: 'roadmap-1',
  key: 8,
  name: 'Platform 2026',
  start: new Date('2026-01-05'),
  end: new Date('2026-12-18'),
  visibility: { id: '1', name: 'Public' },
  state: { id: '1', name: 'Active' },
  roadmapManagers: [],
  colors: [],
} as any

describe('RoadmapFacts', () => {
  it('renders the span as the calendar dates it is', () => {
    // Arrange / Act — stored as UTC, so formatting locally would shift them a
    // day earlier for anyone behind UTC.
    render(<RoadmapFacts roadmap={roadmap} />)

    // Assert
    expect(screen.getByText('Jan 5, 2026')).toBeInTheDocument()
    expect(screen.getByText('Dec 18, 2026')).toBeInTheDocument()
  })

  it('lists the managers, which the tooltip previously hid', () => {
    // Arrange — the legacy page carried these names only inside a title
    // attribute, so they were unreachable without hovering and unlinkable.
    render(
      <RoadmapFacts
        roadmap={{
          ...roadmap,
          roadmapManagers: [
            { id: 'm2', key: 77, name: 'Wei Chen' },
            { id: 'm1', key: 42, name: 'Ada Lovelace' },
          ],
        }}
      />,
    )

    // Assert — sorted, and each links to the person
    const links = screen.getAllByRole('link')
    const names = links.map((l) => l.textContent)
    expect(names.indexOf('Ada Lovelace')).toBeLessThan(names.indexOf('Wei Chen'))
    expect(screen.getByRole('link', { name: /Ada Lovelace/ })).toHaveAttribute(
      'href',
      '/organizations/employees/42',
    )
  })

  it('says so when a roadmap has no managers', () => {
    // Arrange / Act — an unmanaged roadmap is worth noticing, not hiding.
    render(<RoadmapFacts roadmap={roadmap} />)

    // Assert
    expect(screen.getByText('None assigned')).toBeInTheDocument()
  })

  it('omits the description row when there is none', () => {
    // Arrange / Act
    render(<RoadmapFacts roadmap={roadmap} />)

    // Assert
    expect(screen.queryByText('Description')).toBeNull()
  })
})
