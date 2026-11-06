import { mergeDeliverableOrder } from './deliverable-order'

describe('mergeDeliverableOrder', () => {
  it('keeps hidden deliverables at their positions and fills the rest in the new order', () => {
    // Arrange
    const current = ['a', 'hidden-1', 'b', 'c', 'hidden-2']
    const hidden = new Set(['hidden-1', 'hidden-2'])

    // Act
    const result = mergeDeliverableOrder(current, hidden, ['c', 'a', 'b'])

    // Assert
    expect(result).toEqual(['c', 'hidden-1', 'a', 'b', 'hidden-2'])
  })

  it('returns the new order unchanged when nothing is hidden', () => {
    // Arrange
    const current = ['a', 'b', 'c']

    // Act
    const result = mergeDeliverableOrder(current, new Set(), ['b', 'c', 'a'])

    // Assert
    expect(result).toEqual(['b', 'c', 'a'])
  })
})
