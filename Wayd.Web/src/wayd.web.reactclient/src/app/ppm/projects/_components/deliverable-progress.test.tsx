import React from 'react'
import { render, screen } from '@testing-library/react'
import DeliverableProgress from './deliverable-progress'

describe('DeliverableProgress', () => {
  it('says there are no linked tasks rather than drawing an empty bar', () => {
    // Arrange & Act
    render(<DeliverableProgress progress={null} />)

    // Assert
    expect(screen.getByText('No linked tasks')).toBeInTheDocument()
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
  })

  it('shows zero progress as a bar, since linked work not started is still progress to report', () => {
    // Arrange & Act
    render(<DeliverableProgress progress={0} />)

    // Assert
    expect(
      screen.getByLabelText('0% of the linked plan work done'),
    ).toBeInTheDocument()
  })

  it('rounds to a whole percent', () => {
    // Arrange & Act
    render(<DeliverableProgress progress={33.33} />)

    // Assert
    expect(
      screen.getByLabelText('33% of the linked plan work done'),
    ).toBeInTheDocument()
  })
})
