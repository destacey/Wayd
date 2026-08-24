import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import RecordFactsRail from './record-facts-rail'
import { RecordLayoutConstants } from '@/src/config/theme/theme-constants'

const renderRail = (props?: Partial<React.ComponentProps<typeof RecordFactsRail>>) => {
  const onWidthChange = jest.fn()
  const onOpenChange = jest.fn()
  render(
    <RecordFactsRail
      open
      onOpenChange={onOpenChange}
      width={300}
      onWidthChange={onWidthChange}
      {...props}
    >
      <div>a fact</div>
    </RecordFactsRail>,
  )
  return { onWidthChange, onOpenChange }
}

describe('RecordFactsRail resizing', () => {
  it('widens as the pointer moves toward the content', () => {
    // Arrange — the panel is right-anchored, so width is measured from the
    // right edge of the window inward.
    const { onWidthChange } = renderRail()
    const resizer = screen.getByRole('separator')

    // Act
    fireEvent.mouseDown(resizer)
    fireEvent.mouseMove(document, { clientX: window.innerWidth - 400 })

    // Assert
    expect(onWidthChange).toHaveBeenLastCalledWith(400)
  })

  it('stops widening at the maximum', () => {
    // Arrange
    const { onWidthChange } = renderRail()

    // Act — drag far past the left edge of the screen
    fireEvent.mouseDown(screen.getByRole('separator'))
    fireEvent.mouseMove(document, { clientX: -5000 })

    // Assert
    expect(onWidthChange).toHaveBeenLastCalledWith(
      RecordLayoutConstants.FACTS_RAIL_MAX_WIDTH,
    )
  })

  it('stops narrowing at the minimum', () => {
    // Arrange
    const { onWidthChange } = renderRail()

    // Act
    fireEvent.mouseDown(screen.getByRole('separator'))
    fireEvent.mouseMove(document, { clientX: window.innerWidth + 5000 })

    // Assert
    expect(onWidthChange).toHaveBeenLastCalledWith(
      RecordLayoutConstants.FACTS_RAIL_MIN_WIDTH,
    )
  })

  it('stops resizing once the button is released', () => {
    // Arrange — the listeners live on the document, so a missed teardown would
    // leave the panel following the pointer around the page.
    const { onWidthChange } = renderRail()
    fireEvent.mouseDown(screen.getByRole('separator'))
    fireEvent.mouseUp(document)
    onWidthChange.mockClear()

    // Act
    fireEvent.mouseMove(document, { clientX: 100 })

    // Assert
    expect(onWidthChange).not.toHaveBeenCalled()
  })

  it('resizes from the keyboard', async () => {
    // Arrange
    const user = userEvent.setup()
    const { onWidthChange } = renderRail()
    const resizer = screen.getByRole('separator')
    resizer.focus()

    // Act — left widens, matching the drag direction
    await user.keyboard('{ArrowLeft}')

    // Assert
    expect(onWidthChange).toHaveBeenCalledWith(316)
  })

  it('exposes its bounds to assistive technology', () => {
    // Arrange / Act
    renderRail()

    // Assert
    const resizer = screen.getByRole('separator')
    expect(resizer).toHaveAttribute('aria-valuenow', '300')
    expect(resizer).toHaveAttribute(
      'aria-valuemin',
      String(RecordLayoutConstants.FACTS_RAIL_MIN_WIDTH),
    )
  })

  it('has nothing to resize when closed', () => {
    // Arrange / Act
    renderRail({ open: false })

    // Assert
    expect(screen.queryByRole('separator')).toBeNull()
    expect(screen.queryByText('a fact')).toBeNull()
  })
})
