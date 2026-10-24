import {
  StatusCategory,
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
  TransitionConditionKind,
  TransitionMode,
  WorkflowMoveDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import {
  anyMoveNote,
  buildRecordPath,
  buildWorkflowDiagram,
  edgePath,
  initialStatusId,
} from './workflow-diagram-layout'

const categoryIds: Record<StatusCategory, number> = {
  [StatusCategory.Proposed]: 0,
  [StatusCategory.Active]: 1,
  [StatusCategory.Done]: 2,
  [StatusCategory.Removed]: 3,
}

const status = (
  id: string,
  category: StatusCategory,
  order: number,
  aliasName?: string,
): WorkflowStatusDto => ({
  id,
  name: id[0].toUpperCase() + id.slice(1),
  category: { id: categoryIds[category], name: category },
  alias: aliasName ? 1 : 0,
  aliasName,
  order,
})

// Listed out of order on purpose: the layout, not the list, decides where each status goes.
const statuses: WorkflowStatusDto[] = [
  status('retired', StatusCategory.Done, 5, 'Retired'),
  status('active', StatusCategory.Active, 2, 'Active'),
  status('withdrawn', StatusCategory.Removed, 6),
  status('concept', StatusCategory.Proposed, 1),
  status('sunset', StatusCategory.Active, 3),
  status('idea', StatusCategory.Proposed, 0),
]

const transition = (
  from: string,
  to: string,
  conditions: TransitionConditionKind[] = [],
) => ({ id: `${from}-${to}`, fromStatusId: from, toStatusId: to, conditions })

const workflow = (
  overrides: Partial<StatusWorkflowDetailsDto> = {},
): StatusWorkflowDetailsDto => ({
  id: 'wf-1',
  key: 9,
  name: 'Lifecycle',
  owner: { key: 'product.product', name: 'Product' },
  state: 'Published',
  isSystem: false,
  isAssigned: true,
  statuses,
  transitionMode: TransitionMode.Restricted,
  transitions: [
    transition('idea', 'concept'),
    transition('concept', 'active'),
    transition('active', 'sunset'),
    transition('sunset', 'active'),
    transition('sunset', 'retired', [TransitionConditionKind.RequiresReason]),
    transition('active', 'withdrawn'),
  ],
  availableConditions: [],
  declaredMoves: [],
  supportsRestrictedTransitions: true,
  transitionIssues: [],
  missingRequiredAliases: [],
  canEdit: false,
  canPublish: false,
  canArchive: false,
  ...overrides,
})

const move = (
  from: string,
  to: string,
  actions: string[],
  isRequired: boolean,
): WorkflowMoveDto => ({
  fromStatusId: from,
  toStatusId: to,
  actions,
  isRequired,
  acceptedConditions: [],
})

const history = (
  sequence: number,
  from: string | undefined,
  to: string,
  workflowId = 'wf-1',
): StatusTransitionDto => ({
  id: `h-${sequence}`,
  sequence,
  fromStatus: from
    ? { id: from, name: from, category: StatusCategory.Active }
    : undefined,
  toStatus: { id: to, name: to, category: StatusCategory.Active, alias: 0 },
  workflowId,
  actorKind: { id: 0, name: 'User' },
  changedBySystem: false,
  durationSeconds: 0,
  statusVisitCount: 1,
  statusTotalSeconds: 0,
  changedOn: new Date('2026-05-01T10:00:00Z'),
})

describe('buildWorkflowDiagram', () => {
  it('places the categories in flow order, left to right, skipping an empty one', () => {
    // Arrange
    const withoutActive = statuses.filter(
      (s) => s.category.id !== categoryIds[StatusCategory.Active],
    )

    // Act
    const diagram = buildWorkflowDiagram(
      workflow({ statuses: withoutActive, transitions: [] }),
    )

    // Assert
    expect(diagram.columns.map((c) => c.category)).toEqual([
      StatusCategory.Proposed,
      StatusCategory.Done,
      StatusCategory.Removed,
    ])
    const xs = diagram.columns.map((c) => c.x)
    expect(xs).toEqual([...xs].sort((a, b) => a - b))
    expect(new Set(xs).size).toBe(3)
  })

  it('stacks a column by status order, and draws every status once', () => {
    // Act
    const diagram = buildWorkflowDiagram(workflow())

    // Assert
    expect(diagram.nodes.map((n) => n.id)).toEqual([
      'idea',
      'concept',
      'active',
      'sunset',
      'retired',
      'withdrawn',
    ])
    const idea = diagram.nodes.find((n) => n.id === 'idea')!
    const concept = diagram.nodes.find((n) => n.id === 'concept')!
    expect(concept.x).toBe(idea.x)
    expect(concept.y).toBeGreaterThan(idea.y)
  })

  it('draws the same workflow the same way every time', () => {
    // Act
    const first = buildWorkflowDiagram(workflow())
    const second = buildWorkflowDiagram(
      workflow({ statuses: [...statuses].reverse() }),
    )

    // Assert
    expect(second.nodes).toEqual(first.nodes)
  })

  it('draws every allowed transition once, routed by the columns it joins', () => {
    // Arrange
    const duplicated = workflow({
      transitions: [...workflow().transitions, transition('idea', 'concept')],
    })

    // Act
    const diagram = buildWorkflowDiagram(duplicated)

    // Assert
    expect(
      diagram.edges.map((e) => [e.source, e.target, e.kind, e.route, e.span]),
    ).toEqual([
      ['idea', 'concept', 'allowed', 'down', 0],
      ['concept', 'active', 'allowed', 'forward', 1],
      ['active', 'sunset', 'allowed', 'down', 0],
      ['sunset', 'active', 'allowed', 'up', 0],
      ['sunset', 'retired', 'allowed', 'forward', 1],
      ['active', 'withdrawn', 'allowed', 'over', 2],
    ])
  })

  it('routes a move back to an earlier column beneath the statuses', () => {
    // Act
    const diagram = buildWorkflowDiagram(
      workflow({ transitions: [transition('retired', 'concept')] }),
    )

    // Assert
    expect(diagram.edges[0]).toMatchObject({ route: 'backward', span: 2 })
  })

  it('marks the initial status and names each alias', () => {
    // Act
    const diagram = buildWorkflowDiagram(workflow())

    // Assert
    expect(diagram.nodes.filter((n) => n.isInitial).map((n) => n.id)).toEqual([
      'idea',
    ])
    expect(diagram.nodes.find((n) => n.id === 'retired')!.aliasName).toBe(
      'Retired',
    )
    expect(
      diagram.nodes.find((n) => n.id === 'sunset')!.aliasName,
    ).toBeUndefined()
  })

  it('marks a move with conditions and names them on hover', () => {
    // Act
    const diagram = buildWorkflowDiagram(workflow())

    // Assert
    const retire = diagram.edges.find((e) => e.target === 'retired')!
    expect(retire.conditions).toEqual([TransitionConditionKind.RequiresReason])
    expect(retire.summary).toEqual(['Sunset → Retired', 'Requires a reason'])
    expect(
      diagram.edges.filter((e) => e.conditions.length > 0).map((e) => e.id),
    ).toEqual([retire.id])
  })

  it('draws statuses with no edges when any move is allowed', () => {
    // Act
    const diagram = buildWorkflowDiagram(
      workflow({ transitionMode: TransitionMode.AnyToAny }),
      { showOmittedMoves: true },
    )

    // Assert
    expect(diagram.allowsAnyMove).toBe(true)
    expect(diagram.nodes).toHaveLength(statuses.length)
    expect(diagram.edges).toEqual([])
  })

  describe('declared moves', () => {
    const declared = workflow({
      owner: { key: 'delivery.version', name: 'Version' },
      transitions: [
        transition('concept', 'active'),
        transition('active', 'retired'),
        transition('idea', 'concept'),
      ],
      declaredMoves: [
        move('concept', 'active', ['Cut'], true),
        move('active', 'retired', ['Mark Released'], false),
        move('retired', 'withdrawn', ['Withdraw'], false),
      ],
    })

    it('labels each move with its actions, and marks the required ones', () => {
      // Act
      const diagram = buildWorkflowDiagram(declared)

      // Assert
      const byTarget = (id: string) =>
        diagram.edges.find((e) => e.target === id)!
      expect(byTarget('active')).toMatchObject({
        actions: ['Cut'],
        isRequired: true,
        summary: ['Concept → Active', 'Required: Cut makes this move'],
      })
      expect(byTarget('retired')).toMatchObject({
        actions: ['Mark Released'],
        isRequired: false,
        summary: [
          'Active → Retired',
          'Optional: Mark Released makes this move',
        ],
      })
      expect(byTarget('concept')).toMatchObject({
        actions: [],
        summary: ['Idea → Concept', 'No action makes this move'],
      })
    })

    it('draws a declared move the workflow leaves out only when asked, as refused', () => {
      // Act
      const without = buildWorkflowDiagram(declared)
      const withOmitted = buildWorkflowDiagram(declared, {
        showOmittedMoves: true,
      })

      // Assert
      expect(without.edges.some((e) => e.target === 'withdrawn')).toBe(false)
      const withdraw = withOmitted.edges.find((e) => e.target === 'withdrawn')!
      expect(withdraw).toMatchObject({
        kind: 'omitted',
        isRequired: false,
        actions: ['Withdraw'],
        summary: [
          'Retired → Withdrawn',
          'Not allowed: Withdraw is refused from Retired',
        ],
      })
      expect(withOmitted.edges).toHaveLength(without.edges.length + 1)
    })
  })
})

describe('lanes', () => {
  it('gives moves whose stretches overlap their own lanes, above the columns going on and below going back', () => {
    // Arrange
    const routed = workflow({
      transitions: [
        transition('concept', 'withdrawn'),
        transition('idea', 'retired'),
        transition('active', 'withdrawn'),
        transition('retired', 'idea'),
        transition('idea', 'concept'),
        transition('active', 'sunset'),
      ],
    })

    // Act
    const diagram = buildWorkflowDiagram(routed)

    // Assert
    const lane = (from: string, to: string) =>
      diagram.edges.find((e) => e.source === from && e.target === to)!
    expect(lane('idea', 'retired')).toMatchObject({ lane: 0, laneY: -20 })
    expect(lane('active', 'withdrawn')).toMatchObject({ lane: 1, laneY: -38 })
    expect(lane('concept', 'withdrawn')).toMatchObject({ lane: 2, laneY: -56 })
    expect(lane('retired', 'idea')).toMatchObject({
      lane: 0,
      laneY: diagram.height + 20,
    })
    // Moves within different columns never meet, so each takes the first lane.
    expect(lane('idea', 'concept').lane).toBe(0)
    expect(lane('active', 'sunset').lane).toBe(0)
    expect(diagram.top).toBe(-56)
    expect(diagram.bottom).toBe(diagram.height + 20)
  })

  it('keeps the bounds at the columns when nothing is routed around them', () => {
    // Act
    const diagram = buildWorkflowDiagram(
      workflow({ transitions: [transition('concept', 'active')] }),
    )

    // Assert
    expect(diagram.top).toBe(0)
    expect(diagram.bottom).toBe(diagram.height)
  })
})

describe('edgePath', () => {
  const source = { x: 100, y: 60 }
  const target = { x: 500, y: 160 }

  it('takes a move over the columns out sideways, up the gap, across its lane and down into the target', () => {
    // Act
    const [path, labelX, labelY] = edgePath(
      { route: 'over', lane: 1, laneY: -38, gutter: 40 },
      source,
      target,
    )!

    // Assert
    expect(path.startsWith('M 100,60')).toBe(true)
    expect(path.endsWith('L 500,160')).toBe(true)
    for (const corner of ['146,60', '146,-38', '454,-38', '454,160']) {
      expect(path).toContain(`Q ${corner}`)
    }
    expect([labelX, labelY]).toEqual([300, -38])
  })

  it('takes a move back out of the source on its left and into the target on its right', () => {
    // Act
    const [path, , labelY] = edgePath(
      { route: 'backward', lane: 0, laneY: 300, gutter: 40 },
      { x: 500, y: 60 },
      { x: 100, y: 160 },
    )!

    // Assert
    for (const corner of ['460,60', '460,300', '140,300', '140,160']) {
      expect(path).toContain(`Q ${corner}`)
    }
    expect(labelY).toBe(300)
  })

  it('bows a move down a column out to the right, and up it out to the left', () => {
    // Act
    const [down, downX, downY] = edgePath(
      { route: 'down', lane: 1, gutter: 40 },
      { x: 300, y: 60 },
      { x: 300, y: 200 },
    )!
    const [up, upX] = edgePath(
      { route: 'up', lane: 0, gutter: 40 },
      { x: 140, y: 200 },
      { x: 140, y: 60 },
    )!

    // Assert
    expect(down).toContain('Q 310,60')
    expect(down).toContain('Q 310,200')
    expect([downX, downY]).toEqual([310, 130])
    expect(up).toContain('Q 134,200')
    expect(upX).toBe(134)
  })

  it('leaves a move to the next column to a plain curve', () => {
    // Act & Assert
    expect(
      edgePath({ route: 'forward', lane: 0, gutter: 40 }, source, target),
    ).toBeNull()
  })
})

describe('initialStatusId', () => {
  it('starts at the lowest-ordered Proposed status, else the lowest-ordered status', () => {
    // Arrange
    const noProposed = statuses.filter(
      (s) => s.category.id !== categoryIds[StatusCategory.Proposed],
    )

    // Act & Assert
    expect(initialStatusId(statuses)).toBe('idea')
    expect(initialStatusId(noProposed)).toBe('active')
  })
})

describe('anyMoveNote', () => {
  it('names the owner type when its actions still decide the moves', () => {
    // Act & Assert
    expect(anyMoveNote(workflow())).toBe(
      'Any move is allowed: a record can move from any status to any other.',
    )
    expect(
      anyMoveNote(
        workflow({
          owner: { key: 'delivery.version', name: 'Version' },
          declaredMoves: [move('concept', 'active', ['Cut'], true)],
        }),
      ),
    ).toBe(
      'Any move is allowed. Version records still move only through their actions.',
    )
  })
})

describe('record path', () => {
  it('highlights the current status and the moves made under this workflow', () => {
    // Arrange
    const entries = [
      history(1, undefined, 'idea'),
      history(2, 'idea', 'concept'),
      history(3, 'concept', 'active'),
    ]

    // Act
    const path = buildRecordPath(workflow(), entries)
    const diagram = buildWorkflowDiagram(workflow(), { path })

    // Assert
    expect(diagram.nodes.filter((n) => n.isCurrent).map((n) => n.id)).toEqual([
      'active',
    ])
    expect(diagram.nodes.filter((n) => n.isVisited).map((n) => n.id)).toEqual([
      'idea',
      'concept',
      'active',
    ])
    expect(
      diagram.edges.filter((e) => e.isTaken).map((e) => [e.source, e.target]),
    ).toEqual([
      ['idea', 'concept'],
      ['concept', 'active'],
    ])
    expect(path.notDrawn).toEqual([])
  })

  it('lists changes under an earlier workflow, and the switch onto this one, rather than drawing them', () => {
    // Arrange
    const entries = [
      history(1, undefined, 'old-draft', 'wf-0'),
      history(2, 'old-draft', 'old-live', 'wf-0'),
      history(3, 'old-live', 'active'),
      history(4, 'active', 'sunset'),
    ]

    // Act
    const path = buildRecordPath(workflow(), entries)
    const diagram = buildWorkflowDiagram(workflow(), { path })

    // Assert
    expect(path.notDrawn.map((e) => e.id)).toEqual(['h-3', 'h-2', 'h-1'])
    expect(path.currentStatusId).toBe('sunset')
    expect(
      diagram.edges.filter((e) => e.isTaken).map((e) => [e.source, e.target]),
    ).toEqual([['active', 'sunset']])
    expect(diagram.edges).toHaveLength(workflow().transitions.length)
  })

  it('draws the moves a record made under an any-move workflow, and no others', () => {
    // Arrange
    const anyMove = workflow({ transitionMode: TransitionMode.AnyToAny })
    const entries = [
      history(1, undefined, 'idea'),
      history(2, 'idea', 'retired'),
    ]

    // Act
    const diagram = buildWorkflowDiagram(anyMove, {
      path: buildRecordPath(anyMove, entries),
    })

    // Assert
    expect(diagram.edges.map((e) => [e.source, e.target, e.kind])).toEqual([
      ['idea', 'retired', 'taken'],
    ])
  })

  it('names no current status when the record has moved onto another workflow since', () => {
    // Act
    const path = buildRecordPath(workflow(), [
      history(1, undefined, 'idea'),
      history(2, 'idea', 'elsewhere', 'wf-2'),
    ])

    // Assert
    expect(path.currentStatusId).toBeUndefined()
  })

  it('draws both stays on a workflow the record left and came back to, and lists the stay between', () => {
    // Arrange — on wf-1, moved to wf-2, then moved back.
    const entries = [
      history(1, undefined, 'idea'),
      history(2, 'idea', 'concept'),
      history(3, 'concept', 'other-draft', 'wf-2'),
      history(4, 'other-draft', 'active'),
      history(5, 'active', 'sunset'),
    ]

    // Act
    const path = buildRecordPath(workflow(), entries)

    // Assert
    expect([...path.takenMoves]).toEqual(['active:sunset', 'idea:concept'])
    expect(path.notDrawn.map((e) => e.id)).toEqual(['h-4', 'h-3'])
    expect(path.currentStatusId).toBe('sunset')
  })
})
