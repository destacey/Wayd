import { roadmapColorDisplayText } from './roadmap-items-grid.columns'
import type { RoadmapColorDto } from '@/src/services/wayd-api'

const palette: RoadmapColorDto[] = [
  { color: '#FF0000', name: 'At Risk', order: 1, isDefault: false },
  { color: '#00FF00', name: 'On Track', order: 2, isDefault: true },
]

describe('roadmapColorDisplayText', () => {
  it('returns an empty string when the item has no color', () => {
    expect(roadmapColorDisplayText(undefined, palette)).toBe('')
    expect(roadmapColorDisplayText(null, palette)).toBe('')
    expect(roadmapColorDisplayText('', palette)).toBe('')
  })

  it('does not fall back to the default color for an uncolored item', () => {
    // '#00FF00' is the default, but an item with no color still shows nothing.
    expect(roadmapColorDisplayText(undefined, palette)).toBe('')
  })

  it('returns the palette caption when the color matches a configured entry', () => {
    expect(roadmapColorDisplayText('#FF0000', palette)).toBe('At Risk')
    expect(roadmapColorDisplayText('#00FF00', palette)).toBe('On Track')
  })

  it('matches the palette case-insensitively', () => {
    expect(roadmapColorDisplayText('#ff0000', palette)).toBe('At Risk')
  })

  it('returns the uppercased hex when the color is not in the palette', () => {
    expect(roadmapColorDisplayText('#123abc', palette)).toBe('#123ABC')
  })

  it('returns the uppercased hex when no palette is configured', () => {
    expect(roadmapColorDisplayText('#abc123', [])).toBe('#ABC123')
  })
})
