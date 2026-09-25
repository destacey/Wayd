import { chunk } from './chunk'

describe('chunk', () => {
  it('splits into consecutive slices of the given size, the last one shorter', () => {
    // Arrange / Act / Assert
    expect(chunk([1, 2, 3, 4, 5], 2)).toEqual([[1, 2], [3, 4], [5]])
    expect(chunk(['a', 'b'], 5)).toEqual([['a', 'b']])
  })

  it('gives nothing for an empty list and everything for a non-positive size', () => {
    expect(chunk([], 3)).toEqual([])
    expect(chunk([1, 2, 3], 0)).toEqual([[1, 2, 3]])
  })
})
