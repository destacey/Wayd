'use client'

import { useTourCompleted } from '@/src/hooks'
import { StoryMapDetailsDto } from '@/src/services/wayd-api'
import { Button, Space, TourProps, Typography } from 'antd'
import { useMemo, useState } from 'react'

const TOUR_KEY = 'storyMapBoard'

/**
 * Which shape the tour takes, decided by the board's state at the moment the tour starts and held
 * for the whole run (a build-along must not flip modes the instant its first goal appears):
 *
 * - `build` — the board is empty, so the tour walks the user through creating a goal, a step, and
 *   a task, waiting at each step until they do it.
 * - `walkthrough` — the board already has content, so "create your first goal" would read as
 *   nonsense; the tour instead points at the existing board and describes it, my-projects style.
 */
type TourMode = 'build' | 'walkthrough'

/**
 * Board node counts. In build mode the create-along steps advance when their count increases
 * rather than when a button is clicked — the count only rises once the new node is actually in the
 * board data, so by the time the tour moves on, the next step's target element has rendered.
 */
interface CreatableCounts {
  goals: number
  steps: number
  tasks: number
  personas: number
  /** Persona↔step/task links across the whole board — rises when the user tags a persona. */
  personaLinks: number
  swimLanes: number
}

const countCreatables = (
  map: StoryMapDetailsDto | undefined,
): CreatableCounts | null =>
  map
    ? {
        goals: map.goals.length,
        steps: map.goals.reduce((n, g) => n + g.steps.length, 0),
        tasks: map.goals.reduce(
          (n, g) => n + g.steps.reduce((m, s) => m + s.tasks.length, 0),
          0,
        ),
        personas: map.personas.length,
        personaLinks: map.goals.reduce(
          (n, g) =>
            n +
            g.steps.reduce(
              (m, s) =>
                m +
                s.personaIds.length +
                s.tasks.reduce((k, t) => k + t.personaIds.length, 0),
              0,
            ),
          0,
        ),
        swimLanes: map.swimLanes.length,
      }
    : null

/** Build mode: step index → the count whose increase advances the tour past that step. */
const AUTO_ADVANCE: Record<number, keyof CreatableCounts> = {
  1: 'goals',
  2: 'steps',
  3: 'tasks',
  6: 'personas',
  7: 'personaLinks',
  8: 'swimLanes',
}

/**
 * Build-mode steps that only complete by doing: the Next button is replaced with a hint (and an
 * escape-hatch Skip) so the user builds the map rather than reading about it. The persona and
 * swim-lane steps stay browsable — they auto-advance too, but creating one there is optional.
 */
const DO_IT_STEPS = new Set([1, 2, 3])

const TOTAL_STEPS = 10

/**
 * Whether a step makes sense given what exists on the board. Skipping ahead (or paging in either
 * direction) must not land on a card that points at nothing. Both modes have the same shape —
 * indices 1–3 cover goals/steps/tasks, 4 opens a task's details, 5 and 7 sit inside the board — but
 * walkthrough mode points at the nodes themselves, so it additionally needs a task to exist for its
 * task stop.
 *
 * The cases below are positional: inserting a step shifts every index after it.
 */
const isStepViable = (
  mode: TourMode,
  step: number,
  counts: CreatableCounts,
): boolean => {
  switch (step) {
    case 1:
      // Build mode's "create a goal" is the one stop that must work on an empty board.
      return mode === 'build' || counts.goals > 0
    case 2:
      // Build points at the goal header's + (needs a goal); walkthrough points at a step cell.
      return mode === 'build' ? counts.goals > 0 : counts.steps > 0
    case 3:
      return mode === 'build' ? counts.steps > 0 : counts.tasks > 0
    case 4:
      // Opening a task's details needs a task card to click, in both modes.
      return counts.tasks > 0
    case 5: // the board itself
    case 8: // the board's Add swim lane footer
      return counts.goals > 0
    case 7:
      // Tagging needs a persona to tag and a step/task footer carrying the dots.
      return counts.personas > 0 && (counts.steps > 0 || counts.tasks > 0)
    default:
      return true
  }
}

/** The welcome and closing steps are always viable, so these never run off either end. */
const nextViableStep = (
  mode: TourMode,
  from: number,
  counts: CreatableCounts,
): number => {
  for (let i = from + 1; i < TOTAL_STEPS - 1; i++) {
    if (isStepViable(mode, i, counts)) return i
  }
  return TOTAL_STEPS - 1
}

const prevViableStep = (
  mode: TourMode,
  from: number,
  counts: CreatableCounts,
): number => {
  for (let i = from - 1; i > 0; i--) {
    if (isStepViable(mode, i, counts)) return i
  }
  return 0
}

/**
 * Board cells render dynamically, so targets are resolved by data-tour anchors at the moment the
 * step shows rather than by refs threaded through the board. A missing anchor degrades to a
 * centered card instead of breaking.
 */
const anchor = (selector: string) =>
  (() =>
    document.querySelector<HTMLElement>(
      `[data-tour="${selector}"]`,
    )) as () => HTMLElement

export interface StoryMapTourResult {
  tourOpen: boolean
  tourCurrent: number
  tourSteps: TourProps['steps']
  tourActionsRender: TourProps['actionsRender']
  onTourChange: (current: number) => void
  onTourClose: () => void
  onTourStart: () => void
}

/**
 * The story map board tour. Adaptive: on an empty board it is an interactive build-along (create a
 * goal, a step beneath it, a task beneath that — waiting at each step until the user does it); on
 * a board that already has content it is a passive walkthrough of the existing pieces. Both modes
 * then introduce drag and drop, personas, and swim lanes.
 */
export const useStoryMapTour = (
  map: StoryMapDetailsDto | undefined,
  canEdit: boolean,
): StoryMapTourResult => {
  const { isCompleted, isLoading, markCompleted, resetTour } =
    useTourCompleted(TOUR_KEY)

  const [current, setCurrent] = useState(0)

  // The tour points at (and in build mode asks the user to click) editing controls, so read-only
  // viewers never see it.
  const tourOpen = !isLoading && !isCompleted && canEdit && !!map

  const counts = useMemo(() => countCreatables(map), [map])

  // The mode locks in when the tour opens; onTourStart re-picks it for a replay. Set during render
  // (the "adjust state when props change" pattern) because the first open happens when the
  // preference query resolves, not in any event handler.
  const [mode, setMode] = useState<TourMode | null>(null)
  if (tourOpen && mode === null && counts) {
    setMode(counts.goals === 0 ? 'build' : 'walkthrough')
  }

  // Build mode: advance a create-along step when its count rises. Comparing snapshots during
  // render rather than hooking the page's mutation callbacks means the new node is already in the
  // render that advances the tour, so the next step's anchor resolves on first try.
  const [prevCounts, setPrevCounts] = useState(counts)
  if (counts !== prevCounts) {
    setPrevCounts(counts)
    const kind = AUTO_ADVANCE[current]
    // No advance on the map's initial load — only between two loaded snapshots. Advance to the
    // next *viable* step — e.g. adding a persona on a still-empty board must not move on to the
    // swim-lane step, whose anchor only renders inside the board.
    if (
      mode === 'build' &&
      prevCounts &&
      counts &&
      tourOpen &&
      kind &&
      counts[kind] > prevCounts[kind]
    ) {
      setCurrent(nextViableStep(mode, current, counts))
    }
  }

  // Next/Previous and Skip route through viability, so paging never lands on a card whose subject
  // does not exist yet.
  const goForward = (from: number) =>
    setCurrent(mode && counts ? nextViableStep(mode, from, counts) : from + 1)

  const onTourChange = (next: number) => {
    if (!mode || !counts || next === current) {
      setCurrent(next)
    } else if (next > current) {
      goForward(current)
    } else {
      setCurrent(prevViableStep(mode, current, counts))
    }
  }

  const stepStyle: React.CSSProperties = { maxWidth: 360 }

  // ── Steps shared by both modes ──────────────────────────────────────────────

  const welcomeStep = (closingLine: string) => ({
    title: 'Welcome to Story Mapping',
    description: `A story map lays out the user journey: goals across the top, the steps to reach them beneath, and detailed tasks below. ${closingLine}`,
    target: null,
    style: stepStyle,
  })

  const taskDetailsStep = {
    title: 'Open a task',
    description:
      'Click a task card to open its details. That is where a description, a checklist, personas, and a linked work item live. The board stays live beside it, so you can keep working and click another card to switch. Click the open task again to close it.',
    target: anchor('task-card'),
    style: stepStyle,
  }

  const moveStep = {
    title: 'Move things around',
    description:
      'Everything drags. Reorder goals and steps by dragging them along their row — a step can even move to another goal — and drag tasks between cells and swim lanes. A blue line shows where it will land, and moving something to a new parent outlines the destination.',
    target: anchor('board'),
    style: stepStyle,
  }

  const personaStep = {
    title: 'Create Personas',
    description:
      'Personas are who the journey serves. Add one with + Persona — selecting it here later filters the board to just their journey.',
    // The + Persona button swaps to a name input while quick-adding, so fall back to the bar
    // itself for the moment the button is unmounted.
    target: (() =>
      document.querySelector<HTMLElement>('[data-tour="add-persona"]') ??
      document.querySelector<HTMLElement>(
        '[data-tour="persona-bar"]',
      )) as () => HTMLElement,
    mask: false,
    style: stepStyle,
  }

  // Only reachable when a persona exists and a step/task footer is there to carry the dots — the
  // viability rules skip it otherwise.
  const tagPersonaStep = {
    title: 'Tag a persona',
    description:
      'Every step and task footer shows one dot per persona. Click a dot to tag that persona — filled means tagged — mapping who each part of the journey serves.',
    target: anchor('persona-dots'),
    mask: false,
    style: stepStyle,
  }

  const swimLaneStep = {
    title: 'Slice releases with swim lanes',
    description:
      'Swim lanes split tasks into releases or stages — each can carry a date range. Click Add swim lane to create one, then drag tasks into it. A lane’s caret collapses it, which keeps a finished release out of the way.',
    target: anchor('add-swim-lane'),
    mask: false,
    placement: 'top' as const,
    style: stepStyle,
  }

  const closingStep = {
    title: 'You’re all set',
    description:
      'Goals, steps, tasks, personas, swim lanes — that’s the whole map. Replay this tour anytime from the ? button in the header.',
    target: null,
    style: stepStyle,
  }

  // ── Build mode: create along with the tour ──────────────────────────────────

  const buildSteps: TourProps['steps'] = [
    welcomeStep(
      'Let’s build yours together — the tour waits for you at each step.',
    ),
    {
      title: 'Create your first goal',
      description:
        'Goals are what your users are trying to accomplish. Click the highlighted button to add one, then give it a name.',
      // Prefer the empty-board prompt; fall back to the page header's + Goal button once the
      // board has content.
      target: (() =>
        document.querySelector<HTMLElement>('[data-tour="add-goal-empty"]') ??
        document.querySelector<HTMLElement>(
          '[data-tour="add-goal-header"]',
        )) as () => HTMLElement,
      // No mask on the create-along steps — the name editor opens right after creating, and
      // typing into it must not be blocked by an overlay.
      mask: false,
      style: stepStyle,
    },
    {
      title: 'Break it into steps',
      description:
        'Steps are the actions a user takes toward the goal, read left to right. Click the + in your goal’s header — or the Add step placeholder below it — to add the first one.',
      target: anchor('add-step'),
      mask: false,
      style: stepStyle,
    },
    {
      title: 'Add a task',
      description:
        'Tasks are the concrete work under each step. Click the + in your step’s footer to add one, or the + Task placeholder in any cell to add it straight to that swim lane.',
      target: anchor('add-task'),
      mask: false,
      style: stepStyle,
    },
    taskDetailsStep,
    moveStep,
    personaStep,
    tagPersonaStep,
    swimLaneStep,
    closingStep,
  ]

  // ── Walkthrough mode: describe the board that is already there ──────────────

  const walkthroughSteps: TourProps['steps'] = [
    welcomeStep('Here’s a quick walk around the board.'),
    {
      title: 'Goals',
      description:
        'Goals are what your users are trying to accomplish; each spans the steps beneath it. Click a name to rename it, use the + in its header to add steps, and add more goals with + Goal in the page header. The caret beside the name folds the goal away when you want it out of the way.',
      target: anchor('goal-cell'),
      style: stepStyle,
    },
    {
      title: 'Steps',
      description:
        'Steps are the actions a user takes toward the goal, read left to right. The + in a step’s footer adds a task beneath it.',
      target: anchor('step-cell'),
      style: stepStyle,
    },
    {
      title: 'Tasks',
      description:
        'Tasks are the concrete work under each step, sorted into the swim lanes below. Click a title to rename it in place, or use the + Task placeholder at the bottom of any cell to add one there.',
      target: anchor('task-card'),
      style: stepStyle,
    },
    taskDetailsStep,
    moveStep,
    personaStep,
    tagPersonaStep,
    swimLaneStep,
    closingStep,
  ]

  const tourSteps = mode === 'walkthrough' ? walkthroughSteps : buildSteps

  // On build mode's create-along steps the default Next button would let the user page past the
  // doing; swap it for a hint plus a small Skip escape hatch (mutation failed, or they already
  // know the ropes). Walkthrough mode keeps the default buttons throughout.
  const tourActionsRender: TourProps['actionsRender'] = (
    originNode,
    { current: renderedStep },
  ) =>
    mode === 'build' && DO_IT_STEPS.has(renderedStep) ? (
      <Space size="small">
        <Typography.Text type="secondary">Try it to continue</Typography.Text>
        <Button size="small" onClick={() => goForward(renderedStep)}>
          Skip
        </Button>
      </Space>
    ) : (
      originNode
    )

  return {
    tourOpen,
    tourCurrent: current,
    tourSteps,
    tourActionsRender,
    onTourChange,
    onTourClose: markCompleted,
    onTourStart: () => {
      // Re-pick the mode: a map built since the first run deserves the walkthrough, not another
      // build-along.
      setMode(counts && counts.goals === 0 ? 'build' : 'walkthrough')
      setCurrent(0)
      resetTour()
    },
  }
}
