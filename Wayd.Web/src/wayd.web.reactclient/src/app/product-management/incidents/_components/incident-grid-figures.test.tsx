import { render, screen } from '@testing-library/react'
import IncidentGridFigures from './incident-grid-figures'

describe('IncidentGridFigures', () => {
  it('counts zero active incidents as zero and leaves the other figures absent', () => {
    // Arrange
    const figures = {
      activeCount: 0,
      longestActive: null,
      meanTimeToRecoverMilliseconds: null,
    }

    // Act
    render(<IncidentGridFigures {...figures} />)

    // Assert
    expect(screen.getByText('Active incidents')).toBeInTheDocument()
    expect(screen.getByText('0')).toBeInTheDocument()
    expect(screen.getAllByText('—')).toHaveLength(2)
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('names the longest active incident and shows a real mean', () => {
    // Arrange
    const figures = {
      activeCount: 2,
      longestActive: {
        key: 3,
        title: 'Checkout down',
        activeMilliseconds: 4 * 60 * 60 * 1000,
      },
      meanTimeToRecoverMilliseconds: 2 * 60 * 60 * 1000,
    }

    // Act
    render(<IncidentGridFigures {...figures} />)

    // Assert
    expect(screen.getByText('2')).toBeInTheDocument()
    expect(screen.getByText('4h')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'INC-3 Checkout down' })).toHaveAttribute(
      'href',
      '/product-management/incidents/3',
    )
    expect(screen.getByText('2h')).toBeInTheDocument()
  })
})
