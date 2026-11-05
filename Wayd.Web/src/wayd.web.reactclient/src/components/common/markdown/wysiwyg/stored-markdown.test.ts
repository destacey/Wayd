import { toStoredMarkdown } from './stored-markdown'

describe('toStoredMarkdown', () => {
  it.each([
    ['a table at the start', '\n| A |\n| - |\n', '| A |\n| - |'],
    ['an empty paragraph after a list', '- one\n\n', '- one'],
    ['blank lines inside', 'a\n\n\nb', 'a\n\n\nb'],
    ['trailing spaces of a hard break', 'a  \nb', 'a  \nb'],
  ])('trims only the blank lines around %s', (_, serialized, expected) => {
    // Arrange / Act / Assert
    expect(toStoredMarkdown(serialized)).toBe(expected)
  })
})
