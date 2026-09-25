import { fireEvent, render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'
import dayjs from 'dayjs'
import {
  AllocationDimension,
  AllocationGroupKind,
  AllocationMeasure,
  TeamAllocationDto,
  ThemeCounting,
  UnestimatedHandling,
} from '@/src/services/wayd-api'
import { AllocationReportView, AllocationSettings } from './allocation-report'

jest.unmock('dayjs')

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamAllocationQuery: jest.fn(),
}))

const settings: AllocationSettings = {
  range: [dayjs('2026-07-01'), dayjs('2026-09-28')],
  dimension: AllocationDimension.Portfolio,
  measure: AllocationMeasure.Count,
  unestimated: UnestimatedHandling.Exclude,
  themeCounting: ThemeCounting.SplitEvenly,
}

const createAllocation = (): TeamAllocationDto =>
  ({
    team: {
      id: 't1',
      key: 1,
      name: 'Platform ART',
      code: 'ART',
      type: 'Team of Teams',
    },
    from: new Date('2026-07-01'),
    to: new Date('2026-09-28'),
    summary: {
      itemsCompleted: 4,
      itemsInPointSizedTeams: 3,
      estimatedItems: 2,
      storyPoints: 8,
      filledItems: 0,
      filledStoryPoints: 0,
      teamsIncluded: 2,
      excludedTeams: [],
      noProjectItems: 1,
      noProjectShare: 25,
    },
    groups: [
      {
        id: 'portfolio:1',
        kind: AllocationGroupKind.Record,
        recordId: 'p1',
        recordKey: '1',
        name: 'Customer Experience',
        projectKeys: ['ATLAS'],
        programCount: 0,
        items: 3,
        storyPoints: 8,
        filledStoryPoints: 0,
        value: 3,
        share: 75,
        largestContributor: {
          teamId: 'a',
          code: 'PAY',
          name: 'Payments Team',
          value: 2,
          items: 2,
        },
      },
      {
        id: 'no-project',
        kind: AllocationGroupKind.NoProject,
        name: 'No project',
        projectKeys: [],
        programCount: 0,
        items: 1,
        storyPoints: 0,
        filledStoryPoints: 0,
        value: 1,
        share: 25,
      },
    ],
    teams: [],
    periods: [],
  }) as unknown as TeamAllocationDto

describe('AllocationReportView', () => {
  it('lists each group with its share and names no project work', () => {
    render(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={settings}
        onSettingsChange={jest.fn()}
        isTeamOfTeams
      />,
    )

    expect(
      screen.getByRole('link', { name: 'Customer Experience' }),
    ).toHaveAttribute('href', '/ppm/portfolios/1')
    expect(screen.getAllByText('No project').length).toBeGreaterThan(0)
    expect(screen.getByText('Payments Team · 2 of 3 items')).toBeInTheDocument()
  })

  it('offers the By Team view only for a team of teams', () => {
    const { rerender } = render(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={settings}
        onSettingsChange={jest.fn()}
        isTeamOfTeams
      />,
    )
    expect(screen.getByRole('tab', { name: 'By Team' })).toBeInTheDocument()

    rerender(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={settings}
        onSettingsChange={jest.fn()}
        isTeamOfTeams={false}
      />,
    )
    expect(
      screen.queryByRole('tab', { name: 'By Team' }),
    ).not.toBeInTheDocument()
  })

  it('offers share of team effort only for a team of teams', () => {
    const { rerender } = render(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={settings}
        onSettingsChange={jest.fn()}
        isTeamOfTeams
      />,
    )
    expect(screen.getByText('Share of Team Effort')).toBeInTheDocument()

    rerender(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={settings}
        onSettingsChange={jest.fn()}
        isTeamOfTeams={false}
      />,
    )
    expect(screen.queryByText('Share of Team Effort')).not.toBeInTheDocument()
  })

  it('asks about unestimated items only when measuring story points', () => {
    const { rerender } = render(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={settings}
        onSettingsChange={jest.fn()}
        isTeamOfTeams
      />,
    )
    expect(screen.queryByText('Unestimated items')).not.toBeInTheDocument()

    rerender(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={{ ...settings, measure: AllocationMeasure.StoryPoints }}
        onSettingsChange={jest.fn()}
        isTeamOfTeams
      />,
    )
    expect(screen.getByText('Unestimated items')).toBeInTheDocument()
    expect(screen.getByText('Estimated')).toBeInTheDocument()
  })

  it('warns that shares overlap when themes count fully', () => {
    render(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={{
          ...settings,
          dimension: AllocationDimension.StrategicTheme,
          themeCounting: ThemeCounting.CountFully,
        }}
        onSettingsChange={jest.fn()}
        isTeamOfTeams
      />,
    )

    expect(screen.getByText(/add up to more than 100%/)).toBeInTheDocument()
  })

  it('reports a new dimension through onSettingsChange', () => {
    const onSettingsChange = jest.fn()
    render(
      <AllocationReportView
        allocation={createAllocation()}
        isLoading={false}
        settings={settings}
        onSettingsChange={onSettingsChange}
        isTeamOfTeams
      />,
    )

    fireEvent.click(screen.getByText('Work type'))

    expect(onSettingsChange).toHaveBeenCalledWith({
      ...settings,
      dimension: AllocationDimension.WorkType,
    })
  })

  it('shows an empty state when nothing was completed', () => {
    const allocation = createAllocation()
    allocation.summary.itemsCompleted = 0
    render(
      <AllocationReportView
        allocation={allocation}
        isLoading={false}
        settings={settings}
        onSettingsChange={jest.fn()}
        isTeamOfTeams
      />,
    )

    expect(
      screen.getByText('No completed work in this date range'),
    ).toBeInTheDocument()
  })
})
