import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ActivityLogDto, EventActorKind } from '@/src/services/wayd-api'
import ExportActivitiesModal from './export-activities-modal'

const mockDownloadJsonWithTimestamp = jest.fn()
jest.mock('@/src/utils/json-utils', () => ({
  downloadJsonWithTimestamp: (...args: any[]) =>
    mockDownloadJsonWithTimestamp(...args),
}))

const mockSuccess = jest.fn()
const mockError = jest.fn()
jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  return {
    ...actual,
    App: {
      ...actual.App,
      useApp: () => ({
        message: {
          success: mockSuccess,
          error: mockError,
        },
      }),
    },
  }
})

const createActivity = (
  overrides: Partial<ActivityLogDto> = {},
): ActivityLogDto => ({
  id: '11111111-1111-1111-1111-111111111111',
  eventType: 'TeamCreatedEvent',
  domainArea: 'Organization',
  aggregateType: 'Team',
  aggregateId: '22222222-2222-2222-2222-222222222222',
  actorKind: EventActorKind.User,
  timestamp: new Date('2026-04-01T09:00:00Z'),
  eventVersion: '1.0',
  payload: JSON.stringify({
    name: 'Alpha Team',
    code: 'ALPHA',
  }),
  summary: 'Team Created',
  ...overrides,
})

describe('ExportActivitiesModal', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders modal with All events and Date range options', () => {
    render(
      <ExportActivitiesModal
        open={true}
        onClose={jest.fn()}
        activities={[createActivity()]}
        totalCount={1}
      />,
    )

    expect(screen.getByText('Export Activity History')).toBeInTheDocument()
    expect(screen.getByText('All events')).toBeInTheDocument()
    expect(screen.getByText('Date range')).toBeInTheDocument()
    expect(
      screen.getByText(/Export the complete recorded history \(1 event\)/),
    ).toBeInTheDocument()
  })

  it('shows Current search results option when searchQuery is passed', () => {
    render(
      <ExportActivitiesModal
        open={true}
        onClose={jest.fn()}
        activities={[createActivity()]}
        searchQuery="Alpha"
      />,
    )

    expect(screen.getByText('Current search results')).toBeInTheDocument()
    expect(
      screen.getByText(/Export only events matching "Alpha"/),
    ).toBeInTheDocument()
  })

  it('exports all loaded activities to JSON on Download JSON click', async () => {
    const user = userEvent.setup()
    const onClose = jest.fn()
    const activities = [
      createActivity({
        id: 'act-1',
        summary: 'Team Created',
        payload: JSON.stringify({ name: 'Alpha' }),
      }),
      createActivity({
        id: 'act-2',
        summary: 'Team Updated',
        payload: JSON.stringify({ name: 'Alpha Prime' }),
      }),
    ]

    render(
      <ExportActivitiesModal
        open={true}
        onClose={onClose}
        activities={activities}
        totalCount={2}
        exportFilename="team-alpha-activity"
      />,
    )

    await user.click(screen.getByRole('button', { name: /Download JSON/i }))

    expect(mockDownloadJsonWithTimestamp).toHaveBeenCalledTimes(1)
    const [jsonString, filename] = mockDownloadJsonWithTimestamp.mock.calls[0]
    expect(filename).toBe('team-alpha-activity')

    const envelope = JSON.parse(jsonString)
    expect(envelope.entity).toBe('team-alpha-activity')
    expect(envelope.scope).toBe('all')
    expect(envelope.totalEvents).toBe(2)
    expect(envelope.events[0].eventVersion).toBe('1.0')
    expect(envelope.events[0].payload).toEqual({ name: 'Alpha' })
    expect(envelope.events[1].payload).toEqual({ name: 'Alpha Prime' })

    expect(mockSuccess).toHaveBeenCalledWith('Exported 2 events to JSON')
    expect(onClose).toHaveBeenCalled()
  })

  it('progressively fetches remaining batches when onFetchBatch is provided and totalCount exceeds loaded activities', async () => {
    const user = userEvent.setup()
    const onClose = jest.fn()

    const initialActivities = [createActivity({ id: 'act-1' })]
    const batch2 = [
      createActivity({ id: 'act-2' }),
      createActivity({ id: 'act-3' }),
    ]

    const onFetchBatch = jest.fn().mockImplementation(async (page: number) => {
      if (page === 1) {
        return { items: initialActivities, totalCount: 150 }
      }
      return { items: batch2, totalCount: 150 }
    })

    render(
      <ExportActivitiesModal
        open={true}
        onClose={onClose}
        activities={initialActivities}
        totalCount={150}
        onFetchBatch={onFetchBatch}
        exportFilename="team-paged-activity"
      />,
    )

    await user.click(screen.getByRole('button', { name: /Download JSON/i }))

    await waitFor(() => {
      expect(onFetchBatch).toHaveBeenCalledWith(1, 100)
      expect(onFetchBatch).toHaveBeenCalledWith(2, 100)
      expect(mockDownloadJsonWithTimestamp).toHaveBeenCalledTimes(1)
    })

    const [jsonString] = mockDownloadJsonWithTimestamp.mock.calls[0]
    const envelope = JSON.parse(jsonString)
    expect(envelope.totalEvents).toBe(3)
    expect(mockSuccess).toHaveBeenCalledWith('Exported 3 events to JSON')
  })

  it('filters by search query when scope is filtered', async () => {
    const user = userEvent.setup()
    const onClose = jest.fn()

    const activities = [
      createActivity({ id: 'act-1', summary: 'Sprint 1 Created' }),
      createActivity({ id: 'act-2', summary: 'Member Added' }),
    ]

    const isMatchingSearch = (act: ActivityLogDto, query: string) =>
      Boolean(act.summary?.toLowerCase().includes(query.toLowerCase()))

    render(
      <ExportActivitiesModal
        open={true}
        onClose={onClose}
        activities={activities}
        totalCount={2}
        searchQuery="Sprint"
        isMatchingSearch={isMatchingSearch}
      />,
    )

    // Click "Current search results" radio
    await user.click(screen.getByLabelText(/Current search results/i))
    await user.click(screen.getByRole('button', { name: /Download JSON/i }))

    expect(mockDownloadJsonWithTimestamp).toHaveBeenCalledTimes(1)
    const [jsonString] = mockDownloadJsonWithTimestamp.mock.calls[0]
    const envelope = JSON.parse(jsonString)
    expect(envelope.totalEvents).toBe(1)
    expect(envelope.events[0].id).toBe('act-1')
  })
})

