import { render, screen, fireEvent } from '@testing-library/react'
import { ActivityLogDto, EventActorKind } from '@/src/services/wayd-api'
import ComparePayloadModal, {
  computePayloadDiff,
} from './compare-payload-modal'

const mockSuccess = jest.fn()
jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  const MockModal = ({ title, open, children }: any) =>
    open ? (
      <div role="dialog">
        <div>{title}</div>
        {children}
      </div>
    ) : null
  MockModal.displayName = 'MockModal'

  return {
    ...actual,
    Modal: MockModal,
    App: {
      ...actual.App,
      useApp: () => ({
        message: {
          success: mockSuccess,
        },
      }),
    },
  }
})

const createActivity = (
  overrides: Partial<ActivityLogDto> = {},
): ActivityLogDto => ({
  id: '11111111-1111-1111-1111-111111111111',
  eventType: 'TeamUpdatedEvent',
  domainArea: 'Organization',
  aggregateType: 'Team',
  aggregateId: '22222222-2222-2222-2222-222222222222',
  actorKind: EventActorKind.User,
  timestamp: new Date('2026-04-01T10:00:00Z'),
  eventVersion: '1.0',
  payload: JSON.stringify({
    name: 'New Core Team',
    code: 'CORE',
    description: 'Updated description',
  }),
  summary: 'Team Updated',
  ...overrides,
})

describe('computePayloadDiff helper', () => {
  it('correctly identifies modified, added, removed, and unchanged properties', () => {
    const prev = {
      name: 'Old Team',
      code: 'CORE',
      extra: 'will be removed',
    }
    const curr = {
      name: 'New Team',
      code: 'CORE',
      newProp: 'freshly added',
    }

    const diff = computePayloadDiff(prev, curr)

    const nameDiff = diff.find((d) => d.key === 'name')
    expect(nameDiff).toBeDefined()
    expect(nameDiff?.status).toBe('changed')
    expect(nameDiff?.previousValue).toBe('Old Team')
    expect(nameDiff?.currentValue).toBe('New Team')

    const codeDiff = diff.find((d) => d.key === 'code')
    expect(codeDiff?.status).toBe('unchanged')

    const extraDiff = diff.find((d) => d.key === 'extra')
    expect(extraDiff?.status).toBe('removed')

    const newDiff = diff.find((d) => d.key === 'newProp')
    expect(newDiff?.status).toBe('added')
  })

  it('excludes metadata properties by default', () => {
    const prev = {
      name: 'Team A',
      id: 'uuid-1',
      eventId: 'evt-1',
      actor: { kind: 'user' },
      timestamp: '2026-01-01',
    }
    const curr = {
      name: 'Team B',
      id: 'uuid-1',
      eventId: 'evt-2',
      actor: { kind: 'user' },
      timestamp: '2026-01-02',
    }

    const diffNoMeta = computePayloadDiff(prev, curr, false)
    expect(diffNoMeta.map((d) => d.key)).toEqual(['name'])

    const diffWithMeta = computePayloadDiff(prev, curr, true)
    expect(diffWithMeta.map((d) => d.key)).toContain('eventId')
  })
})

describe('ComparePayloadModal component', () => {
  const prevActivity = createActivity({
    id: 'prev-1',
    eventType: 'TeamCreatedEvent',
    summary: 'Team Created',
    timestamp: new Date('2026-04-01T09:00:00Z'),
    payload: JSON.stringify({
      name: 'Old Core Team',
      code: 'CORE',
      description: null,
    }),
  })

  const currActivity = createActivity({
    id: 'curr-1',
    eventType: 'TeamUpdatedEvent',
    summary: 'Team Updated',
    timestamp: new Date('2026-04-01T10:00:00Z'),
    payload: JSON.stringify({
      name: 'New Core Team',
      code: 'CORE',
      description: 'Now has description',
    }),
  })

  it('renders modal with base and target event cards', () => {
    render(
      <ComparePayloadModal
        open={true}
        onClose={jest.fn()}
        currentActivity={currActivity}
        previousActivity={prevActivity}
        allActivities={[currActivity, prevActivity]}
      />,
    )

    expect(screen.getByText('Compare Event Payloads')).toBeInTheDocument()
    expect(screen.getByText('BASE (EARLIER EVENT):')).toBeInTheDocument()
    expect(screen.getByText('TARGET (CURRENT EVENT):')).toBeInTheDocument()
    expect(screen.getByText('Team Created')).toBeInTheDocument()
    expect(screen.getByText('Team Updated')).toBeInTheDocument()
  })

  it('renders event summary once in the header when both events share the same type', () => {
    const prevSameActivity = createActivity({
      id: 'prev-same-1',
      eventType: 'TeamUpdatedEvent',
      summary: 'Team Updated',
      timestamp: new Date('2026-04-01T09:00:00Z'),
      payload: JSON.stringify({ name: 'Alpha' }),
    })

    render(
      <ComparePayloadModal
        open={true}
        onClose={jest.fn()}
        currentActivity={currActivity}
        previousActivity={prevSameActivity}
        allActivities={[currActivity, prevSameActivity]}
      />,
    )

    // "Team Updated" should be displayed once in the header, not duplicated in both cards
    const matches = screen.getAllByText('Team Updated')
    expect(matches).toHaveLength(1)
  })

  it('displays modified fields in table diff', () => {
    render(
      <ComparePayloadModal
        open={true}
        onClose={jest.fn()}
        currentActivity={currActivity}
        previousActivity={prevActivity}
      />,
    )

    // Name was changed
    expect(screen.getByText('Old Core Team')).toBeInTheDocument()
    expect(screen.getByText('New Core Team')).toBeInTheDocument()

    // Description was changed
    expect(screen.getByText('Now has description')).toBeInTheDocument()

    // Status badges
    const modifiedTags = screen.getAllByText('Modified')
    expect(modifiedTags.length).toBeGreaterThanOrEqual(1)
  })

  it('toggles between visual diff and raw JSON diff', () => {
    render(
      <ComparePayloadModal
        open={true}
        onClose={jest.fn()}
        currentActivity={currActivity}
        previousActivity={prevActivity}
      />,
    )

    // Click Raw JSON Diff segment
    const rawJsonTab = screen.getByText('Raw JSON Diff')
    fireEvent.click(rawJsonTab)

    expect(screen.getByText('Previous Event Payload')).toBeInTheDocument()
    expect(screen.getByText('Current Event Payload')).toBeInTheDocument()
  })

  it('handles empty or malformed payload gracefully', () => {
    const emptyPrev = createActivity({
      id: 'prev-empty',
      payload: '',
    })
    const emptyCurr = createActivity({
      id: 'curr-empty',
      payload: 'invalid-json',
    })

    render(
      <ComparePayloadModal
        open={true}
        onClose={jest.fn()}
        currentActivity={emptyCurr}
        previousActivity={emptyPrev}
      />,
    )

    expect(screen.getByText('Compare Event Payloads')).toBeInTheDocument()
  })
})

