import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import PpmFilterBar from './ppm-filter-bar'
import { StatusOptionModel } from '@/src/components/types'

const STATUS_OPTIONS: StatusOptionModel[] = [
  { value: 1, label: 'Proposed', lifecycleCategory: 'NotStarted' },
  { value: 5, label: 'Approved', lifecycleCategory: 'NotStarted' },
  { value: 2, label: 'Active', lifecycleCategory: 'Active' },
  { value: 3, label: 'Completed', lifecycleCategory: 'Completed' },
]

/** Roadmaps and strategic themes filter on states, which carry no category. */
const STATE_OPTIONS: StatusOptionModel[] = [
  { value: 1, label: 'Proposed' },
  { value: 2, label: 'Active' },
]

const onStatusChange = jest.fn()

const renderBar = (
  selectedStatuses: number[],
  statusOptions: StatusOptionModel[] = STATUS_OPTIONS,
) =>
  render(
    <PpmFilterBar
      statusOptions={statusOptions}
      selectedStatuses={selectedStatuses}
      onStatusChange={onStatusChange}
    />,
  )

const getButton = (label: string) => screen.getByRole('button', { name: label })

/**
 * antd marks the active state with a class rather than aria. A status carrying a
 * lifecycle category is lit by taking the status column's tag colors inline; a
 * category-less state keeps antd's primary.
 */
const isLit = (label: string) => {
  const button = getButton(label)
  return (
    button.style.backgroundColor !== '' ||
    button.className.includes('color-primary')
  )
}

/** Color appears only on lit buttons; every unlit one is the same plain outline. */
const isColored = (label: string) => getButton(label).style.backgroundColor !== ''

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

  it('colors a lit status with its own color, not the default primary', () => {
    // Arrange / Act — only Completed is lit.
    renderBar([3])

    // Assert — it takes the status column's colors rather than antd's primary.
    const completed = getButton('Completed')
    expect(completed.className).not.toContain('color-primary')
    expect(completed.style.backgroundColor).not.toBe('')
    expect(completed.style.color).not.toBe('')
  })

  it('gives every lit button the same bordered box, so none reads smaller', () => {
    // Arrange / Act — every button lit, the case where a missing border shows.
    renderBar([])

    // Assert — every button keeps the outlined variant, lit or not, so they all
    // draw the same border box. A variant that drops the border left that button
    // reading a ring smaller than the rest.
    for (const label of ['Proposed', 'Active', 'Completed']) {
      expect(getButton(label).className).toContain('variant-outlined')
    }
  })

  it('dashes the unlit buttons, the one selection cue that does not rely on color', () => {
    // Arrange / Act — Proposed lit, the rest not. A not-started status is grey, so
    // its lit chip differs from an unlit button by a background step alone; the
    // dash is what makes the difference legible, colorblind readers included.
    renderBar([1])

    // Assert
    expect(getButton('Proposed').style.borderStyle).toBe('')
    expect(getButton('Active').style.borderStyle).toBe('dashed')
    expect(getButton('Completed').style.borderStyle).toBe('dashed')
  })

  it('colors only the lit buttons, leaving every unlit one a plain outline', () => {
    // Arrange / Act — Proposed is lit. Carrying the status color through both
    // states told them apart only by how vivid each was, which a grey status has
    // no way to show, so the row reads as "the colored ones are on" instead.
    renderBar([1])

    // Assert
    expect(isColored('Proposed')).toBe(true)
    expect(isColored('Active')).toBe(false)
    expect(isColored('Completed')).toBe(false)
    expect(getButton('Active').style.borderColor).toBe('')
  })

  it('leaves an unlit status uncolored', () => {
    // Arrange / Act — coloring the unlit buttons too would leave every button
    // colored, and selection is what the bar has to show first.
    renderBar([3])

    // Assert
    const proposed = getButton('Proposed')
    expect(proposed.style.backgroundColor).toBe('')
  })

  it('does not dash a lit state, which has no color to be found either way', () => {
    // Arrange / Act — roadmaps and strategic themes filter on states. Keying the
    // dash on "no color was found" rather than on selection marked their lit
    // buttons as off.
    renderBar([2], STATE_OPTIONS)

    // Assert
    expect(getButton('Active').style.borderStyle).toBe('')
    expect(getButton('Proposed').style.borderStyle).toBe('dashed')
  })

  it('falls back to the primary for states with no lifecycle category', () => {
    // Arrange / Act — roadmaps and strategic themes filter on states, which
    // carry no category to take a color from.
    renderBar([2], STATE_OPTIONS)

    // Assert
    const active = getButton('Active')
    expect(active.className).toContain('color-primary')
    expect(active.style.backgroundColor).toBe('')
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
