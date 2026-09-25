import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import PpmViewSelector from './ppm-view-selector'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

describe('PpmViewSelector', () => {
  it('offers List and Timeline by default and reports the chosen view', async () => {
    // Arrange
    const onChange = jest.fn()
    render(<PpmViewSelector value="List" onChange={onChange} />)

    // Act
    await userEvent.click(screen.getByTitle('Timeline'))

    // Assert
    expect(screen.queryByTitle('Card view')).not.toBeInTheDocument()
    expect(onChange).toHaveBeenCalledWith('Timeline')
  })

  it('shows the views it is given, in that order', () => {
    // Arrange / Act
    render(
      <PpmViewSelector
        views={['Card', 'List', 'Timeline']}
        value="Card"
        onChange={jest.fn()}
      />,
    )

    // Assert
    const titles = screen
      .getAllByRole('img', { hidden: true })
      .map(
        (icon) =>
          icon.getAttribute('title') ??
          icon.closest('[title]')?.getAttribute('title'),
      )
      .filter(Boolean)
    expect(titles).toEqual(['Card view', 'List', 'Timeline'])
  })
})
