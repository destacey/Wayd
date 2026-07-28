import { renderHook, act } from '@testing-library/react'
import { useStoryMapTour } from './use-story-map-tour'
import { useTourCompleted } from '@/src/hooks'
import { StoryMapDetailsDto } from '@/src/services/wayd-api'

jest.mock('@/src/hooks', () => ({
  useTourCompleted: jest.fn(),
}))

const mockUseTourCompleted = useTourCompleted as jest.Mock
const mockMarkCompleted = jest.fn()
const mockResetTour = jest.fn()

/** The tour only reads goal/step/task/persona/lane collections, so a skeletal map suffices. */
const buildMap = (shape?: {
  goals?: {
    steps: { tasks: { personaIds?: string[] }[]; personaIds?: string[] }[]
  }[]
  personas?: unknown[]
  swimLanes?: unknown[]
}): StoryMapDetailsDto =>
  ({
    goals: (shape?.goals ?? []).map((goal) => ({
      steps: goal.steps.map((step) => ({
        personaIds: step.personaIds ?? [],
        tasks: step.tasks.map((task) => ({
          personaIds: task.personaIds ?? [],
        })),
      })),
    })),
    personas: shape?.personas ?? [],
    swimLanes: shape?.swimLanes ?? [{}],
  }) as unknown as StoryMapDetailsDto

const emptyMap = buildMap()
const mapWithGoal = buildMap({ goals: [{ steps: [] }] })
const mapWithStep = buildMap({ goals: [{ steps: [{ tasks: [] }] }] })
const mapWithTask = buildMap({ goals: [{ steps: [{ tasks: [{}] }] }] })

beforeEach(() => {
  jest.clearAllMocks()
  mockUseTourCompleted.mockReturnValue({
    isCompleted: false,
    isLoading: false,
    markCompleted: mockMarkCompleted,
    resetTour: mockResetTour,
  })
})

interface HookProps {
  map: StoryMapDetailsDto | undefined
  canEdit: boolean
}

const render = (initial: HookProps) =>
  renderHook((props: HookProps) => useStoryMapTour(props.map, props.canEdit), {
    initialProps: initial,
  })

describe('useStoryMapTour', () => {
  it('uses the storyMapBoard tour key', () => {
    render({ map: emptyMap, canEdit: true })

    expect(mockUseTourCompleted).toHaveBeenCalledWith('storyMapBoard')
  })

  it('returns tourOpen true when not completed, not loading, editable, and map loaded', () => {
    // Arrange / Act
    const { result } = render({ map: emptyMap, canEdit: true })

    // Assert
    expect(result.current.tourOpen).toBe(true)
  })

  it('returns tourOpen false when tour is completed', () => {
    // Arrange
    mockUseTourCompleted.mockReturnValue({
      isCompleted: true,
      isLoading: false,
      markCompleted: mockMarkCompleted,
      resetTour: mockResetTour,
    })

    // Act
    const { result } = render({ map: emptyMap, canEdit: true })

    // Assert
    expect(result.current.tourOpen).toBe(false)
  })

  it('returns tourOpen false while preferences are loading', () => {
    // Arrange
    mockUseTourCompleted.mockReturnValue({
      isCompleted: false,
      isLoading: true,
      markCompleted: mockMarkCompleted,
      resetTour: mockResetTour,
    })

    // Act
    const { result } = render({ map: emptyMap, canEdit: true })

    // Assert
    expect(result.current.tourOpen).toBe(false)
  })

  it('returns tourOpen false for read-only viewers', () => {
    // Arrange / Act
    const { result } = render({ map: emptyMap, canEdit: false })

    // Assert
    expect(result.current.tourOpen).toBe(false)
  })

  it('returns tourOpen false before the map has loaded', () => {
    // Arrange / Act
    const { result } = render({ map: undefined, canEdit: true })

    // Assert
    expect(result.current.tourOpen).toBe(false)
  })

  it('uses build-along steps when the board is empty', () => {
    // Arrange / Act
    const { result } = render({ map: emptyMap, canEdit: true })

    // Assert
    const titles = result.current.tourSteps!.map((s) => s.title)
    expect(titles).toEqual([
      'Welcome to Story Mapping',
      'Create your first goal',
      'Break it into steps',
      'Add a task',
      'Move things around',
      'Create Personas',
      'Tag a persona',
      'Slice releases with swim lanes',
      'You’re all set',
    ])
  })

  it('uses walkthrough steps when the board already has content', () => {
    // Arrange / Act
    const { result } = render({ map: mapWithTask, canEdit: true })

    // Assert
    const titles = result.current.tourSteps!.map((s) => s.title)
    expect(titles).toEqual([
      'Welcome to Story Mapping',
      'Goals',
      'Steps',
      'Tasks',
      'Move things around',
      'Create Personas',
      'Tag a persona',
      'Slice releases with swim lanes',
      'You’re all set',
    ])
  })

  it('keeps build mode for the whole run once started', () => {
    // Arrange — the build-along creates goals; that must not flip the copy to walkthrough mid-run.
    const { result, rerender } = render({ map: emptyMap, canEdit: true })

    // Act
    rerender({ map: mapWithGoal, canEdit: true })

    // Assert
    expect(result.current.tourSteps![1].title).toBe('Create your first goal')
  })

  it('re-picks the mode on restart', () => {
    // Arrange — first run started on an empty board (build mode), map has since been built out.
    const { result, rerender } = render({ map: emptyMap, canEdit: true })
    rerender({ map: mapWithTask, canEdit: true })

    // Act
    act(() => result.current.onTourStart())

    // Assert
    expect(result.current.tourSteps![1].title).toBe('Goals')
  })

  it('centers the welcome and closing steps (no target)', () => {
    // Arrange / Act
    const { result } = render({ map: emptyMap, canEdit: true })

    // Assert
    expect(result.current.tourSteps![0].target).toBeNull()
    expect(result.current.tourSteps![8].target).toBeNull()
  })

  it('advances past the goal step when a goal is created', () => {
    // Arrange
    const { result, rerender } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))

    // Act
    rerender({ map: mapWithGoal, canEdit: true })

    // Assert
    expect(result.current.tourCurrent).toBe(2)
  })

  it('advances past the step step when a step is created', () => {
    // Arrange — build mode only starts on an empty board, so build along like a real user.
    const { result, rerender } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))
    rerender({ map: mapWithGoal, canEdit: true })

    // Act
    rerender({ map: mapWithStep, canEdit: true })

    // Assert
    expect(result.current.tourCurrent).toBe(3)
  })

  it('advances past the task step when a task is created', () => {
    // Arrange
    const { result, rerender } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))
    rerender({ map: mapWithGoal, canEdit: true })
    rerender({ map: mapWithStep, canEdit: true })

    // Act
    rerender({ map: mapWithTask, canEdit: true })

    // Assert
    expect(result.current.tourCurrent).toBe(4)
  })

  it('skips forward past steps whose subject does not exist yet', () => {
    // Arrange — on an empty board, everything between "create a goal" and the persona step
    // presupposes the board, so skipping the goal step must land on personas.
    const { result } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))

    // Act
    act(() => result.current.onTourChange(2))

    // Assert
    expect(result.current.tourCurrent).toBe(5)
  })

  it('goes backward past steps whose subject does not exist yet', () => {
    // Arrange — at the persona step on an empty board.
    const { result } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))
    act(() => result.current.onTourChange(2))

    // Act
    act(() => result.current.onTourChange(4))

    // Assert — back to "create a goal", not to the board steps that do not exist.
    expect(result.current.tourCurrent).toBe(1)
  })

  it('auto-advance also skips non-viable steps', () => {
    // Arrange — at the persona step on an empty board; creating a persona must not move on to
    // the swim-lane step, whose anchor only renders inside the board.
    const { result, rerender } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))
    act(() => result.current.onTourChange(2))

    // Act
    rerender({ map: buildMap({ personas: [{}] }), canEdit: true })

    // Assert — straight to the closing step: tagging and swim lanes both presuppose the board.
    expect(result.current.tourCurrent).toBe(8)
  })

  it('walks the tag-persona stop when a persona and step exist', () => {
    // Arrange — build along to the persona stop with a step on the board.
    const { result, rerender } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))
    rerender({ map: mapWithGoal, canEdit: true })
    rerender({ map: mapWithStep, canEdit: true })
    act(() => result.current.onTourChange(4))
    act(() => result.current.onTourChange(5))

    // Act / Assert — creating a persona advances to the tag stop, not past it.
    rerender({
      map: buildMap({ goals: [{ steps: [{ tasks: [] }] }], personas: [{}] }),
      canEdit: true,
    })
    expect(result.current.tourCurrent).toBe(6)

    // Act / Assert — tagging the persona on the step advances to the swim-lane stop.
    rerender({
      map: buildMap({
        goals: [{ steps: [{ tasks: [], personaIds: ['p1'] }] }],
        personas: [{}],
      }),
      canEdit: true,
    })
    expect(result.current.tourCurrent).toBe(7)
  })

  it('does not advance when a different kind of node is created', () => {
    // Arrange — waiting on a goal, but a persona shows up instead.
    const { result, rerender } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))

    // Act
    rerender({ map: buildMap({ personas: [{}] }), canEdit: true })

    // Assert
    expect(result.current.tourCurrent).toBe(1)
  })

  it('does not advance on the map’s initial load', () => {
    // Arrange — the first loaded snapshot jumps counts from nothing; that is not user activity.
    const { result, rerender } = render({ map: undefined, canEdit: true })
    act(() => result.current.onTourChange(1))

    // Act
    rerender({ map: mapWithGoal, canEdit: true })

    // Assert
    expect(result.current.tourCurrent).toBe(1)
  })

  it('does not advance when the tour is not open', () => {
    // Arrange
    mockUseTourCompleted.mockReturnValue({
      isCompleted: true,
      isLoading: false,
      markCompleted: mockMarkCompleted,
      resetTour: mockResetTour,
    })
    const { result, rerender } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))

    // Act
    rerender({ map: mapWithGoal, canEdit: true })

    // Assert
    expect(result.current.tourCurrent).toBe(1)
  })

  it('replaces the default actions on build-along steps only', () => {
    // Arrange
    const { result } = render({ map: emptyMap, canEdit: true })
    const originNode = 'origin'

    // Act / Assert — info steps keep the default buttons; do-it steps get the hint + skip.
    expect(
      result.current.tourActionsRender!(originNode, { current: 0, total: 9 }),
    ).toBe(originNode)
    expect(
      result.current.tourActionsRender!(originNode, { current: 1, total: 9 }),
    ).not.toBe(originNode)
    expect(
      result.current.tourActionsRender!(originNode, { current: 4, total: 9 }),
    ).toBe(originNode)
  })

  it('keeps the default actions on every step in walkthrough mode', () => {
    // Arrange
    const { result } = render({ map: mapWithTask, canEdit: true })
    const originNode = 'origin'

    // Act / Assert
    expect(
      result.current.tourActionsRender!(originNode, { current: 1, total: 9 }),
    ).toBe(originNode)
  })

  it('does not auto-advance in walkthrough mode', () => {
    // Arrange — a passive tour must not jump because content happened to be created.
    const { result, rerender } = render({ map: mapWithGoal, canEdit: true })
    act(() => result.current.onTourChange(1))

    // Act
    rerender({ map: mapWithStep, canEdit: true })

    // Assert
    expect(result.current.tourCurrent).toBe(1)
  })

  it('walkthrough paging skips stops whose subject does not exist', () => {
    // Arrange — a goal but no steps or tasks: the Steps and Tasks stops have nothing to show.
    const { result } = render({ map: mapWithGoal, canEdit: true })
    act(() => result.current.onTourChange(1))

    // Act
    act(() => result.current.onTourChange(2))

    // Assert — straight to the drag-and-drop stop.
    expect(result.current.tourCurrent).toBe(4)
  })

  it('onTourClose calls markCompleted', () => {
    // Arrange
    const { result } = render({ map: emptyMap, canEdit: true })

    // Act
    act(() => result.current.onTourClose())

    // Assert
    expect(mockMarkCompleted).toHaveBeenCalled()
  })

  it('onTourStart resets the tour and returns to the first step', () => {
    // Arrange
    const { result } = render({ map: emptyMap, canEdit: true })
    act(() => result.current.onTourChange(1))

    // Act
    act(() => result.current.onTourStart())

    // Assert
    expect(mockResetTour).toHaveBeenCalled()
    expect(result.current.tourCurrent).toBe(0)
  })
})
