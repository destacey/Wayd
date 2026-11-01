import { act, renderHook } from '@testing-library/react'
import { useRoundFieldAutosave } from './use-round-field-autosave'

interface Props {
  roundId: string
  serverValue: string | undefined
}

const setup = (initial: Props) => {
  const save = jest.fn()
  const hook = renderHook(
    (props: Props) => useRoundFieldAutosave({ ...props, save }),
    { initialProps: initial },
  )
  return { save, ...hook }
}

describe('useRoundFieldAutosave', () => {
  beforeEach(() => {
    jest.useFakeTimers()
  })

  afterEach(() => {
    jest.useRealTimers()
  })

  it('saves the trimmed value once typing pauses', () => {
    // Arrange
    const { result, save } = setup({ roundId: 'r1', serverValue: '' })

    // Act
    act(() => result.current.onChange(' WI-1 '))
    act(() => jest.advanceTimersByTime(600))

    // Assert
    expect(save).toHaveBeenCalledTimes(1)
    expect(save).toHaveBeenCalledWith('r1', 'WI-1')
  })

  it('saves on blur without waiting, and only once', () => {
    // Arrange
    const { result, save } = setup({ roundId: 'r1', serverValue: '' })
    act(() => result.current.onChange('WI-1'))

    // Act
    act(() => result.current.onBlur())
    act(() => jest.advanceTimersByTime(600))

    // Assert
    expect(save).toHaveBeenCalledTimes(1)
  })

  it('does not save on blur when nothing was typed', () => {
    // Arrange
    const { result, save } = setup({ roundId: 'r1', serverValue: 'WI-1' })

    // Act
    act(() => result.current.onBlur())

    // Assert
    expect(save).not.toHaveBeenCalled()
  })

  it('keeps unsent typing when a refresh arrives', () => {
    // Arrange
    const { result, rerender } = setup({ roundId: 'r1', serverValue: 'old' })
    act(() => result.current.onChange('new text'))

    // Act
    rerender({ roundId: 'r1', serverValue: 'changed elsewhere' })

    // Assert
    expect(result.current.value).toBe('new text')
  })

  it('keeps a trailing space when the trimmed echo of its own save comes back', () => {
    // Arrange
    const { result, rerender } = setup({ roundId: 'r1', serverValue: '' })
    act(() => result.current.onChange('foo '))
    act(() => jest.advanceTimersByTime(600))

    // Act
    rerender({ roundId: 'r1', serverValue: 'foo' })

    // Assert
    expect(result.current.value).toBe('foo ')
  })

  it("takes someone else's change when there are no unsent edits", () => {
    // Arrange
    const { result, rerender } = setup({ roundId: 'r1', serverValue: 'old' })

    // Act
    rerender({ roundId: 'r1', serverValue: 'changed elsewhere' })

    // Assert
    expect(result.current.value).toBe('changed elsewhere')
  })

  it('sends a pending edit to its own round when the round changes', () => {
    // Arrange
    const { result, rerender, save } = setup({ roundId: 'r1', serverValue: '' })
    act(() => result.current.onChange('for r1'))

    // Act
    rerender({ roundId: 'r2', serverValue: 'r2 value' })

    // Assert
    expect(save).toHaveBeenCalledTimes(1)
    expect(save).toHaveBeenCalledWith('r1', 'for r1')
    expect(result.current.value).toBe('r2 value')
  })

  it('sends a pending edit when the field unmounts', () => {
    // Arrange
    const { result, unmount, save } = setup({ roundId: 'r1', serverValue: '' })
    act(() => result.current.onChange('unsaved'))

    // Act
    unmount()

    // Assert
    expect(save).toHaveBeenCalledWith('r1', 'unsaved')
  })
})
