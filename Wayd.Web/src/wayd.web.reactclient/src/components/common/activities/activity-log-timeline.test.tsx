import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ActivityLogDto, EventActorKind } from '@/src/services/wayd-api'
import ActivityLogTimeline from './activity-log-timeline'

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

  it('renders pagination toolbar and triggers callback on page change', async () => {
    const user = userEvent.setup()
    const handlePageChange = jest.fn()
    const activities = [createActivity({ id: 'act-1' })]

    render(
      <ActivityLogTimeline
        activities={activities}
        isLoading={false}
        totalCount={60}
        page={1}
        pageSize={20}
        onPageChange={handlePageChange}
      />,
    )

    expect(screen.getByText('60 events')).toBeInTheDocument()
    const nextBtn = screen.getByTitle('Next Page')
    expect(nextBtn).toBeInTheDocument()
    await user.click(nextBtn)

    expect(handlePageChange).toHaveBeenCalledWith(2, 20)
  })
})

