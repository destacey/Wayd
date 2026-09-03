import {
  createApproximateMeasurer,
  createTextMeasurer,
  truncateToWidth,
  wrapToWidth,
  type TextMeasurer,
} from './measure-text'

/**
 * A measurer with deliberately non-uniform glyph widths, so the tests exercise
 * the behaviour a fixed characters-x-ratio estimate got wrong: real fonts make
 * "W" roughly four times the width of "i".
 */
const variableWidth: TextMeasurer = {
  width: (text, fontSize) => {
    let total = 0
    for (const ch of text) {
      if (ch === 'W') total += fontSize * 0.95
      else if (ch === 'i' || ch === 'l') total += fontSize * 0.25
      else if (ch === ' ') total += fontSize * 0.3
      else total += fontSize * 0.55
    }
    return total
  },
}

describe('createTextMeasurer', () => {
  test('falls back to an estimate when no canvas context exists', () => {
    // Arrange — jsdom has no 2D context, which is the SSR/test path.
    const measurer = createTextMeasurer('Inter, sans-serif')

    // Act
    const width = measurer.width('abcd', 10)

    // Assert — a usable number rather than a throw or NaN.
    expect(width).toBeGreaterThan(0)
    expect(Number.isFinite(width)).toBe(true)
  })

  test('scales linearly with font size in the fallback path', () => {
    // Arrange
    const measurer = createTextMeasurer('Inter, sans-serif')

    // Act / Assert
    expect(measurer.width('abcd', 20)).toBeCloseTo(
      measurer.width('abcd', 10) * 2,
    )
  })
})

describe('truncateToWidth', () => {
  test('returns the text unchanged when it already fits', () => {
    // Arrange / Act / Assert
    expect(truncateToWidth('Alpha', 1000, 13, variableWidth)).toBe('Alpha')
  })

  test('cuts to an ellipsis when the text overruns', () => {
    // Arrange
    const label = 'Consolidated Create Test 1.2'

    // Act
    const cut = truncateToWidth(label, 60, 13, variableWidth)

    // Assert
    expect(cut.endsWith('…')).toBe(true)
    expect(variableWidth.width(cut, 13)).toBeLessThanOrEqual(60)
  })

  test('keeps more characters for narrow glyphs than for wide ones', () => {
    // Arrange — the exact case a fixed ratio cannot express.
    const narrow = truncateToWidth(
      'iiiiiiiiiiiiiiiiiiii',
      100,
      13,
      variableWidth,
    )
    const wide = truncateToWidth('WWWWWWWWWWWWWWWWWWWW', 100, 13, variableWidth)

    // Act / Assert
    expect(narrow.length).toBeGreaterThan(wide.length)
  })

  test('never returns text wider than the budget', () => {
    // Arrange
    const label = 'Strategic Initiative Management'

    // Act / Assert — walk a range of budgets; none may overflow.
    for (let w = 5; w <= 200; w += 5) {
      const cut = truncateToWidth(label, w, 13, variableWidth)
      if (cut) expect(variableWidth.width(cut, 13)).toBeLessThanOrEqual(w)
    }
  })

  test('returns nothing when even an ellipsis will not fit', () => {
    // Arrange / Act / Assert
    expect(truncateToWidth('Alpha', 1, 13, variableWidth)).toBe('')
  })
})

describe('wrapToWidth', () => {
  test('keeps a short label on one line', () => {
    // Arrange / Act / Assert
    expect(wrapToWidth('Team One', 1000, 13, 3, variableWidth)).toEqual([
      'Team One',
    ])
  })

  test('wraps at word boundaries and preserves the words', () => {
    // Arrange
    const label = 'Project Portfolio Management 1'

    // Act
    const lines = wrapToWidth(label, 120, 13, 4, variableWidth)

    // Assert
    expect(lines.length).toBeGreaterThan(1)
    expect(lines.join(' ')).toBe(label)
  })

  test('keeps every line inside the width budget', () => {
    // Arrange
    const label = 'Consolidated Create Test 1.2 for the Platform Team'

    // Act
    const lines = wrapToWidth(label, 90, 13, 5, variableWidth)

    // Assert
    lines.forEach((line) =>
      expect(variableWidth.width(line, 13)).toBeLessThanOrEqual(90),
    )
  })

  test('ellipsises the last line when the text needs more lines than it has', () => {
    // Arrange
    const label = 'A very long group name that will not fit in two short lines'

    // Act
    const lines = wrapToWidth(label, 70, 13, 2, variableWidth)

    // Assert
    expect(lines).toHaveLength(2)
    expect(lines[1].endsWith('…')).toBe(true)
  })

  test('breaks a single over-long word mid-word', () => {
    // Arrange — matches the live column's `overflow-wrap: anywhere`.
    const label = 'Supercalifragilisticexpialidocious'

    // Act
    const lines = wrapToWidth(label, 60, 13, 4, variableWidth)

    // Assert
    expect(lines.length).toBeGreaterThan(1)
    lines.forEach((line) =>
      expect(variableWidth.width(line, 13)).toBeLessThanOrEqual(60),
    )
  })

  test('returns nothing for empty or whitespace-only text', () => {
    // Arrange / Act / Assert
    expect(wrapToWidth('   ', 100, 13, 3, variableWidth)).toEqual([])
  })
})

describe('createApproximateMeasurer', () => {
  test('applies the given ratio uniformly', () => {
    // Arrange
    const measurer = createApproximateMeasurer(0.5)

    // Act / Assert
    expect(measurer.width('abcd', 10)).toBe(20)
  })
})
