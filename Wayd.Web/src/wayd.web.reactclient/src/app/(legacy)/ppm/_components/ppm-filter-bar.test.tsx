import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import PpmFilterBar from './ppm-filter-bar'

const STATUS_OPTIONS = [
  { value: 1, label: 'Proposed' },
  { value: 5, label: 'Approved' },
  { value: 2, label: 'Active' },
  { value: 3, label: 'Completed' },
]

const onStatusChange = jest.fn()

const renderBar = (selectedStatuses: number[]) =>
  render(
    <PpmFilterBar
      statusOptions={STATUS_OPTIONS}
      selectedStatuses={selectedStatuses}
      onStatusChange={onStatusChange}
    />,
  )

/**
 * antd marks the active state with a class rather than aria, so selection is
 * read off the button's own styling.
 */
const isLit = (label: string) => {
  const button = screen.getByRole('button', { name: label })
  return button.className.includes('color-primary')
}

describe('PpmFilterBar status buttons', () => {
  beforeEach(() => jest.clearAllMocks())

  it('lights only the selected statuses', () => {
    // Arrange / Act
    renderBar([2])

    // Assert
    expect(isLit('Active')).toBe(true)
    expect(isLit('Proposed')).toBe(false)
    expect(isLit('Completed')).toBe(false)
  })

  it('lights every status when the selection is empty', () => {
    // Arrange / Act — an empty selection queries all statuses, so showing
    // none lit would contradict the data on screen.
    renderBar([])

    // Assert
    expect(isLit('Proposed')).toBe(true)
    expect(isLit('Approved')).toBe(true)
    expect(isLit('Active')).toBe(true)
    expect(isLit('Completed')).toBe(true)
  })

  it('adds a status to an existing selection', async () => {
    // Arrange
    renderBar([2])

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Proposed' }))

    // Assert
    expect(onStatusChange).toHaveBeenCalledWith([2, 1])
  })

  it('removes a status from an existing selection', async () => {
    // Arrange
    renderBar([2, 1])

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Proposed' }))

    // Assert
    expect(onStatusChange).toHaveBeenCalledWith([2])
  })

  it('narrows to the rest when turning one off while showing all', async () => {
    // Arrange — every button is lit, so a click reads as turning that one off
    // rather than selecting it alone.
    renderBar([])

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Completed' }))

    // Assert
    expect(onStatusChange).toHaveBeenCalledWith([1, 5, 2])
  })

  it('clears to all when the last selected status is turned off', async () => {
    // Arrange
    renderBar([2])

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Active' }))

    // Assert — an empty selection is the query for every status, which the
    // buttons then render as all lit.
    expect(onStatusChange).toHaveBeenCalledWith([])
  })
})
