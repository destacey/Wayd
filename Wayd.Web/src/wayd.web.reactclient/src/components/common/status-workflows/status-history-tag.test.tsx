import { fireEvent, render, screen } from '@testing-library/react'
import { StatusCategory, StatusNavigationDto } from '@/src/services/wayd-api'
import StatusHistoryTag from './status-history-tag'

const status: StatusNavigationDto = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Released',
  category: StatusCategory.Done,
  alias: 11,
}

describe('StatusHistoryTag', () => {
  it('renders the status name', () => {
    // Arrange / Act
    render(<StatusHistoryTag status={status} />)

    // Assert
    expect(screen.getByText('Released')).toBeInTheDocument()
  })

  it('opens the history when clicked', () => {
    // Arrange
    const onOpenHistory = jest.fn()
    render(<StatusHistoryTag status={status} onOpenHistory={onOpenHistory} />)

    // Act
    fireEvent.click(screen.getByRole('button'))

    // Assert
    expect(onOpenHistory).toHaveBeenCalledTimes(1)
  })

  it('opens the history from the keyboard', () => {
    // Arrange — the tag is a span rather than a button, so it carries the key handling a button
    // would have given it for free.
    const onOpenHistory = jest.fn()
    render(<StatusHistoryTag status={status} onOpenHistory={onOpenHistory} />)

    // Act
    fireEvent.keyDown(screen.getByRole('button'), { key: 'Enter' })
    fireEvent.keyDown(screen.getByRole('button'), { key: ' ' })

    // Assert
    expect(onOpenHistory).toHaveBeenCalledTimes(2)
  })

  it('renders without interaction when no handler is given', () => {
    // Arrange / Act — a record with no history section still shows its status.
    render(<StatusHistoryTag status={status} />)

    // Assert
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(screen.getByText('Released')).toBeInTheDocument()
  })
})
