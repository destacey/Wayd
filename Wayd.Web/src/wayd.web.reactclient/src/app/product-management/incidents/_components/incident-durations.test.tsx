import { render, screen } from '@testing-library/react'
import IncidentDurations from './incident-durations'

describe('IncidentDurations', () => {
  it('omits a stretch that has not started and keeps a real zero', () => {
    // Arrange
    const durations = {
      noticedToResponseStartedMilliseconds: 0,
      responseToRecoveredMilliseconds: null,
      activeMilliseconds: 3_600_000,
    }

    // Act
    render(<IncidentDurations durations={durations} />)

    // Assert
    expect(screen.getByText('Noticed to response')).toBeInTheDocument()
    expect(screen.getByText('0s')).toBeInTheDocument()
    expect(screen.queryByText('Response to recovery')).not.toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.getByText('1h')).toBeInTheDocument()
  })

  it('renders nothing when every stretch is absent', () => {
    // Arrange
    const durations = {
      noticedToResponseStartedMilliseconds: null,
      responseToRecoveredMilliseconds: null,
      activeMilliseconds: null,
    }

    // Act
    const { container } = render(<IncidentDurations durations={durations} />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
