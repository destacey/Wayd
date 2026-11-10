import { virtualSpacers } from './virtual-geometry'

describe('virtualSpacers', () => {
  test('are zero when nothing is rendered', () => {
    // Arrange / Act
    const spacers = virtualSpacers({
      firstRowStart: 0,
      lastRowEnd: 0,
      totalSize: 0,
      hasRows: false,
    })

    // Assert
    expect(spacers).toEqual({ top: 0, bottom: 0 })
  })

  test('fill the space above and below the rendered rows', () => {
    // Arrange / Act
    const spacers = virtualSpacers({
      firstRowStart: 280,
      lastRowEnd: 840,
      totalSize: 1260,
      hasRows: true,
    })

    // Assert
    expect(spacers).toEqual({ top: 280, bottom: 420 })
  })
})
