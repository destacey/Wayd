import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { App } from 'antd'
import { ActivityLogDto, EventActorKind } from '@/src/services/wayd-api'
import ActivityLogTimeline from './activity-log-timeline'

jest.mock('./export-activities-modal', () => {
  const MockExportModal = ({ open }: any) =>
    open ? <div>Export Activity History</div> : null
  MockExportModal.displayName = 'MockExportModal'
  return MockExportModal
})

jest.mock('./compare-payload-modal', () => {
  const MockCompareModal = ({ open }: any) =>
    open ? <div>Compare Event Payloads</div> : null
  MockCompareModal.displayName = 'MockCompareModal'
  return MockCompareModal
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
    type: 'Team',
  }),
  summary: 'Team Created',
  ...overrides,
})

describe('ActivityLogTimeline', () => {
  it('renders a skeleton while loading', () => {
    const { container } = render(
      <ActivityLogTimeline activities={undefined} isLoading={true} />,
    )

    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders the empty description when there is no activity history', () => {
    render(
      <ActivityLogTimeline
        activities={[]}
        isLoading={false}
        emptyDescription="No activities recorded."
      />,
    )

    expect(screen.getByText('No activities recorded.')).toBeInTheDocument()
  })

  it('renders activity items in the ledger list with summary and actor kind', () => {
    const activities = [
      createActivity({
        id: 'act-1',
        summary: 'Team Created',
        actorKind: EventActorKind.User,
      }),
      createActivity({
        id: 'act-2',
        eventType: 'TeamDeactivatedEvent',
        summary: 'Team Deactivated',
        actorKind: EventActorKind.System,
      }),
    ]

    render(<ActivityLogTimeline activities={activities} isLoading={false} />)

    expect(screen.getAllByText('Team Created').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('Team Deactivated')).toBeInTheDocument()
    expect(screen.getAllByText('User').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('System')).toBeInTheDocument()
  })

  it.each([
    // 'deactivated' contains 'activated', so a badge rule ordered the other way round labels a
    // deactivation as its opposite.
    ['TeamDeactivatedEvent', 'Deactivated'],
    ['TeamActivatedEvent', 'Activated'],
    ['TeamCreatedEvent', 'Created'],
    ['TeamUpdatedEvent', 'Updated'],
  ])('badges %s as %s', (eventType, expectedBadge) => {
    render(
      <ActivityLogTimeline
        activities={[createActivity({ id: 'act-1', eventType })]}
        isLoading={false}
      />,
    )

    expect(screen.getAllByText(expectedBadge).length).toBeGreaterThanOrEqual(1)
  })

  it('selects the first activity by default and displays its properties in the inspector pane', () => {
    const activities = [
      createActivity({
        id: 'act-1',
        payload: JSON.stringify({
          name: 'Phoenix Team',
          code: 'PHX',
        }),
      }),
    ]

    render(<ActivityLogTimeline activities={activities} isLoading={false} />)

    expect(screen.getByText('Phoenix Team')).toBeInTheDocument()
    expect(screen.getByText('PHX')).toBeInTheDocument()
    expect(screen.getByText('Event Properties')).toBeInTheDocument()
    expect(screen.queryByText(/UTC/)).not.toBeInTheDocument()
  })

  it('renders null or empty event properties as None', () => {
    const activities = [
      createActivity({
        id: 'act-1',
        payload: JSON.stringify({
          name: 'Phoenix Team',
          description: null,
        }),
      }),
    ]

    render(<ActivityLogTimeline activities={activities} isLoading={false} />)

    expect(screen.getByText('Description')).toBeInTheDocument()
    expect(screen.getByText('None')).toBeInTheDocument()
  })

  it('switches selected activity when clicking an item in the ledger list', async () => {
    const user = userEvent.setup()
    const activities = [
      createActivity({
        id: 'act-1',
        summary: 'Team Created',
        payload: JSON.stringify({ name: 'Phoenix Team', code: 'PHX' }),
      }),
      createActivity({
        id: 'act-2',
        eventType: 'TeamUpdatedEvent',
        summary: 'Team Updated',
        payload: JSON.stringify({ name: 'Orion Team', code: 'ORN' }),
      }),
    ]

    render(<ActivityLogTimeline activities={activities} isLoading={false} />)

    // Initially act-1 is selected
    expect(screen.getByText('Phoenix Team')).toBeInTheDocument()

    // Click act-2 in the master list
    await user.click(screen.getByText('Team Updated'))

    // Now act-2 details are displayed
    expect(screen.getByText('Orion Team')).toBeInTheDocument()
    expect(screen.getByText('ORN')).toBeInTheDocument()
  })

  it('renders employee actor card with link when employee details are provided', () => {
    const activities = [
      createActivity({
        id: 'act-1',
        employee: {
          id: 'emp-uuid-1',
          key: 42,
          name: 'Sarah Connor',
        },
      }),
    ]

    render(<ActivityLogTimeline activities={activities} isLoading={false} />)

    expect(screen.getAllByText('Sarah Connor').length).toBeGreaterThanOrEqual(1)
    const link = screen.getByRole('link', { name: 'Sarah Connor' })
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/organizations/employees/42')
    expect(screen.getByText('Initiated by employee #42')).toBeInTheDocument()
  })

  it('renders direct action attribution when actor is user without linked employee', () => {
    const activities = [
      createActivity({
        id: 'act-1',
        actorKind: EventActorKind.User,
        employee: undefined,
      }),
    ]

    render(<ActivityLogTimeline activities={activities} isLoading={false} />)

    expect(
      screen.getByText('Direct action performed by user'),
    ).toBeInTheDocument()
  })

  it('renders system process attribution when actor is system', () => {
    const activities = [
      createActivity({
        id: 'act-1',
        actorKind: EventActorKind.System,
        employee: undefined,
      }),
    ]

    render(<ActivityLogTimeline activities={activities} isLoading={false} />)

    expect(
      screen.getByText('Automated action performed by platform'),
    ).toBeInTheDocument()
  })

  it('filters activity items when typing in search input', async () => {
    const user = userEvent.setup()
    const activities = [
      createActivity({
        id: 'act-1',
        summary: 'Deploy Production',
      }),
      createActivity({
        id: 'act-2',
        eventType: 'RollbackEvent',
        summary: 'Rollback Release',
      }),
    ]

    render(<ActivityLogTimeline activities={activities} isLoading={false} />)

    expect(
      screen.getAllByText('Deploy Production').length,
    ).toBeGreaterThanOrEqual(1)
    expect(screen.getByText('Rollback Release')).toBeInTheDocument()

    const searchInput = screen.getByPlaceholderText(
      'Search events, actors, or types...',
    )
    await user.type(searchInput, 'Deploy')

    expect(
      screen.getAllByText('Deploy Production').length,
    ).toBeGreaterThanOrEqual(1)
    expect(screen.queryByText('Rollback Release')).not.toBeInTheDocument()
  })

  it('renders load more toolbar with remaining count and triggers callback on click', async () => {
    const user = userEvent.setup()
    const handleLoadMore = jest.fn()
    const activities = [createActivity({ id: 'act-1' })]

    render(
      <ActivityLogTimeline
        activities={activities}
        isLoading={false}
        totalCount={60}
        hasMore={true}
        onLoadMore={handleLoadMore}
      />,
    )

    expect(screen.getByText('Showing 1 of 60 events')).toBeInTheDocument()
    const loadMoreBtn = screen.getByRole('button', {
      name: /Load more activities \(59 remaining\)/i,
    })
    expect(loadMoreBtn).toBeInTheDocument()
    await user.click(loadMoreBtn)

    expect(handleLoadMore).toHaveBeenCalledTimes(1)
  })

  it('displays all loaded message when total count is reached', () => {
    const activities = Array.from({ length: 25 }, (_, i) =>
      createActivity({ id: `act-${i}` }),
    )

    render(
      <ActivityLogTimeline
        activities={activities}
        isLoading={false}
        totalCount={25}
        hasMore={false}
      />,
    )

    expect(screen.getByText('25 events')).toBeInTheDocument()
    expect(screen.getByText('All 25 activities loaded')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /Load more activities/i }),
    ).not.toBeInTheDocument()
  })

  it('copies raw payload to clipboard and triggers context-aware message on click', async () => {
    const user = userEvent.setup()
    const writeTextMock = jest
      .spyOn(navigator.clipboard, 'writeText')
      .mockResolvedValue(undefined)

    const activities = [
      createActivity({
        id: 'act-1',
        payload: JSON.stringify({ name: 'Alpha Team' }),
      }),
    ]

    render(
      <App>
        <ActivityLogTimeline activities={activities} isLoading={false} />
      </App>,
    )

    const collapseHeader = screen.getByText('Raw Event Payload & Traceability')
    await user.click(collapseHeader)

    const copyBtn = screen.getByRole('button', { name: /copy json/i })
    expect(copyBtn).toBeInTheDocument()
    fireEvent.click(copyBtn)

    expect(writeTextMock).toHaveBeenCalledWith(
      JSON.stringify({ name: 'Alpha Team' }, null, 2),
    )
  })

  it('does not render panel export button by default', () => {
    const activities = [createActivity({ id: 'act-1' })]

    render(
      <App>
        <ActivityLogTimeline
          activities={activities}
          isLoading={false}
          totalCount={1}
        />
      </App>,
    )

    expect(
      screen.queryByRole('button', { name: /Export activity history/i }),
    ).not.toBeInTheDocument()
  })

  it('renders export modal when isExportOpen is true', () => {
    const activities = [createActivity({ id: 'act-1' })]

    render(
      <App>
        <ActivityLogTimeline
          activities={activities}
          isLoading={false}
          totalCount={1}
          isExportOpen={true}
        />
      </App>,
    )

    expect(screen.getByText('Export Activity History')).toBeInTheDocument()
  })

  it('opens export modal when showExportButton is enabled and clicked', async () => {
    const user = userEvent.setup()
    const activities = [createActivity({ id: 'act-1' })]

    render(
      <App>
        <ActivityLogTimeline
          activities={activities}
          isLoading={false}
          totalCount={1}
          showExportButton={true}
        />
      </App>,
    )

    const exportBtn = screen.getByRole('button', {
      name: /Export activity history/i,
    })
    expect(exportBtn).toBeInTheDocument()
    await user.click(exportBtn)

    expect(screen.getByText('Export Activity History')).toBeInTheDocument()
  })

  it('shows the compare action when a preceding event exists and opens the compare modal on click', async () => {
    const user = userEvent.setup()
    const activities = [
      createActivity({
        id: 'act-2',
        eventType: 'TeamUpdatedEvent',
        summary: 'Team Updated',
        timestamp: new Date('2026-04-01T10:00:00Z'),
      }),
      createActivity({
        id: 'act-1',
        eventType: 'TeamCreatedEvent',
        summary: 'Team Created',
        timestamp: new Date('2026-04-01T09:00:00Z'),
      }),
    ]

    render(
      <App>
        <ActivityLogTimeline
          activities={activities}
          isLoading={false}
          totalCount={2}
        />
      </App>,
    )

    // The first item (act-2) is selected by default and has a preceding item (act-1)
    const compareButtons = screen.getAllByRole('button', {
      name: /Compare changes/i,
    })
    expect(compareButtons).toHaveLength(1)

    await user.click(compareButtons[0])
    expect(screen.getByText('Compare Event Payloads')).toBeInTheDocument()
  })

  it('does not show the compare action when viewing the oldest/initial event', () => {
    const activities = [
      createActivity({
        id: 'act-1',
        eventType: 'TeamCreatedEvent',
        summary: 'Team Created',
      }),
    ]

    render(
      <App>
        <ActivityLogTimeline
          activities={activities}
          isLoading={false}
          totalCount={1}
        />
      </App>,
    )

    expect(
      screen.queryByRole('button', { name: /Compare changes/i }),
    ).not.toBeInTheDocument()
  })
})

