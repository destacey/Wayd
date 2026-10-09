'use client'

import { IconMenu, PageActions, WaydTooltip } from '@/src/components/common'
import { scopeIsReadable } from '@/src/components/common/metrics'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { compareCalendarDates } from '@/src/utils'
import {
  useGetSprintActivitiesQuery,
  useGetSprintBacklogQuery,
  useGetSprintMetricsQuery,
  useGetSprintQuery,
  useGetSprintScopeQuery,
  useLazyGetSprintActivitiesQuery,
} from '@/src/store/features/work-management/sprints-api'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { ReactNode, use, useState } from 'react'
import SprintDetailsLoading from './loading'
import {
  ChangeSprintLifecycleForm,
  CorrectSprintActualDatesForm,
  SprintBacklogGrid,
  SprintDetails,
  SprintLifecycleAction,
  SprintScopeGrid,
  SprintTeamDaysOffForm,
  SprintTypeForm,
} from '@/src/app/work/sprints/_components'
import {
  IterationStateTag,
  sprintTypeLabels,
} from '@/src/components/common/planning'
import { IterationState } from '@/src/components/types'
import { useGetTeamSprintsQuery } from '@/src/store/features/organizations/team-api'
import { SwapOutlined } from '@ant-design/icons'
import { SprintType } from '@/src/services/wayd-api'
import { Alert, Segmented, Space, Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import SprintFacts from './_components/sprint-facts'

enum SprintSections {
  Overview = 'overview',
  Backlog = 'backlog',
  Activities = 'activities',
}

enum BacklogView {
  Scope = 'scope',
  Current = 'current',
}

const SprintDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const sprintKey = Number(key)

  // Rendered by SprintMetrics once its own query resolves, so the sprint's
  // health can sit in the identity bar beside the record's name.
  const [healthIndicator, setHealthIndicator] = useState<ReactNode>(null)

  const [lifecycleAction, setLifecycleAction] =
    useState<SprintLifecycleAction | null>(null)
  const [correctingDates, setCorrectingDates] = useState(false)
  const [editingDaysOff, setEditingDaysOff] = useState(false)
  const [editingType, setEditingType] = useState(false)

  const router = useRouter()
  const { hasPermissionClaim } = useAuth()

  // The active section lives in the URL, owned by RecordLayout. Read here only
  // to gate the backlog, which is the expensive query.
  const searchParams = useSearchParams()
  const activeSection = (searchParams.get('section') ??
    SprintSections.Overview) as SprintSections

  const { data: sprint, isLoading } = useGetSprintQuery(sprintKey, {
    skip: !sprintKey,
  })

  // Scope exists from the commitment point on; before it, the sprint holds
  // only what is planned, which the current items show.
  const scopeAvailable =
    sprint?.state.id === IterationState.Active ||
    sprint?.state.id === IterationState.Completed
  const [backlogView, setBacklogView] = useState(BacklogView.Scope)
  const onBacklog = !!sprintKey && activeSection === SprintSections.Backlog

  // Read whenever scope could show, so incomplete history is known before
  // choosing the view; the Overview's summary shares the cached result.
  const {
    data: scope,
    isLoading: scopeLoading,
    refetch: refetchScope,
  } = useGetSprintScopeQuery(sprintKey, {
    skip: !onBacklog || !scopeAvailable,
  })

  // Incomplete history leaves nothing to show in the scope view, and so does a
  // sprint still in its commitment grace period: it is Active from its planned
  // start, but has no scope until its commitment point. The current items stand
  // in for it until then.
  const historyIncomplete = !!scope?.historyIncomplete
  const offersScope = scopeAvailable && (!scope || scopeIsReadable(scope))
  const showsScope = offersScope && backlogView === BacklogView.Scope

  const {
    data: workItems,
    isLoading: workItemsLoading,
    refetch: refetchWorkItems,
  } = useGetSprintBacklogQuery(sprintKey, {
    skip: !onBacklog || showsScope,
  })

  // The sprint's sizing method on its planned start, which its metrics report.
  const { data: sprintMetrics } = useGetSprintMetricsQuery(sprintKey, {
    skip: !onBacklog || showsScope,
  })

  const activitiesQuery = useGetSprintActivitiesQuery(
    { idOrKey: sprint?.id ?? '', page: 1, pageSize: ACTIVITY_LOG_PAGE_SIZE },
    { skip: !sprint?.id || activeSection !== SprintSections.Activities },
  )
  const [fetchActivityLogPage] = useLazyGetSprintActivitiesQuery()

  const activityLog = useActivityLog({
    idOrKey: sprint?.id,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: `sprint-${sprint?.key ?? sprintKey}-activity`,
  })

  useDocumentTitle(`${sprint?.name ?? sprintKey} - Sprint Details`)

  const { data: teamSprints } = useGetTeamSprintsQuery(sprint?.team.id ?? '', {
    skip: !sprint?.team.id,
  })

  const handleSprintChange = (value: string | number) => {
    router.push(`/work/sprints/${value}`)
  }

  const sprintsItems = !teamSprints
    ? []
    : [...teamSprints]
        .sort((a, b) => compareCalendarDates(b.start, a.start))
        .map((option) => ({
          label: option.name,
          extra: option.state.name,
          value: option.key,
        }))

  const switchSprints = !sprintsItems.length ? null : (
    <IconMenu
      icon={<SwapOutlined />}
      tooltip="Switch to another team sprint"
      items={sprintsItems}
      selectedKeys={[sprintKey.toString()]}
      onChange={handleSprintChange}
    />
  )

  if (isLoading) {
    return <SprintDetailsLoading />
  }

  if (!sprint) {
    return notFound()
  }

  // The claim alone is not enough: the caller must also belong to the sprint's
  // team, and the lifecycle rules must allow the move right now.
  const canManage =
    hasPermissionClaim('Permissions.Iterations.Update') &&
    sprint.canManageSprint
  const lifecycleItems: ItemType[] = [
    { action: SprintLifecycleAction.Start, allowed: sprint.canStart },
    { action: SprintLifecycleAction.Complete, allowed: sprint.canComplete },
    { action: SprintLifecycleAction.Reopen, allowed: sprint.canReopen },
  ]
    .filter(({ allowed }) => canManage && allowed)
    .map<ItemType>(({ action }) => ({
      key: action,
      label: action,
      onClick: () => setLifecycleAction(action),
    }))
    .concat(
      canManage
        ? [
            {
              key: 'correct-actual-dates',
              label: 'Correct Actual Dates',
              onClick: () => setCorrectingDates(true),
            },
            {
              key: 'team-days-off',
              label: 'Team Days Off',
              onClick: () => setEditingDaysOff(true),
            },
            {
              key: 'sprint-type',
              label: 'Sprint Type',
              onClick: () => setEditingType(true),
            },
          ]
        : [],
    )

  // In the order the server's timeline uses, so the neighbours offered are the
  // ones a correction is checked against.
  const timelineSprints = [...(teamSprints ?? [])].sort(
    (a, b) => compareCalendarDates(a.start, b.start) || a.key - b.key,
  )
  const sprintIndex = timelineSprints.findIndex((s) => s.key === sprint.key)
  const previousSprint =
    sprintIndex > 0 ? timelineSprints[sprintIndex - 1] : undefined
  const nextSprint =
    sprintIndex >= 0 ? timelineSprints[sprintIndex + 1] : undefined

  const sections: RecordSection[] = [
    { id: SprintSections.Overview, label: 'Overview' },
    { id: SprintSections.Backlog, label: 'Backlog' },
    { id: SprintSections.Activities, label: 'Activity' },
  ]

  const renderSection = (section: SprintSections) => {
    switch (section) {
      case SprintSections.Activities:
        return <ActivityLogTimeline {...activityLog.timelineProps} />
      case SprintSections.Backlog:
        // Block-level children: the grid collapses as a flex item.
        return (
          <>
            {historyIncomplete && (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 8 }}
                title="History incomplete — run a full sync"
                description="This sprint's scope comes from work item history, which has not yet been read in full for every workspace holding its work. A full sync of the connection completes it; until then, these are the items in the sprint now."
              />
            )}
            {offersScope && (
              <div style={{ marginBottom: 8 }}>
                <Segmented<BacklogView>
                  value={backlogView}
                  onChange={setBacklogView}
                  options={[
                    {
                      value: BacklogView.Scope,
                      label: (
                        <WaydTooltip title="Every item that was in the sprint between its commitment point and its end, from work item history">
                          Sprint Scope
                        </WaydTooltip>
                      ),
                    },
                    {
                      value: BacklogView.Current,
                      label: (
                        <WaydTooltip title="The items in the sprint right now">
                          Current Items
                        </WaydTooltip>
                      ),
                    },
                  ]}
                />
              </div>
            )}
            {showsScope ? (
              <SprintScopeGrid
                scope={scope}
                isLoading={scopeLoading}
                refetch={refetchScope}
                persistStateKey="sprint-scope"
              />
            ) : (
              <SprintBacklogGrid
                workItems={workItems ?? []}
                isLoading={workItemsLoading}
                refetch={refetchWorkItems}
                hideTeamColumn
                sizingMethod={sprintMetrics?.sizingMethod}
                persistStateKey="sprint-backlog"
              />
            )}
          </>
        )
      default:
        return (
          <SprintDetails
            sprint={sprint}
            onHealthIndicatorReady={setHealthIndicator}
          />
        )
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={SprintSections.Overview}
        record={{
          name: sprint.name,
          recordKey: String(sprint.key),
          subtitle: 'Sprint Details',
          parent: {
            label: sprint.team.name,
            href: `/organizations/teams/${sprint.team.key}`,
          },
          tags: (
            <Space>
              {switchSprints}
              <IterationStateTag state={sprint.state.id as IterationState} />
              {sprint.sprintType === SprintType.NonStandard && (
                <WaydTooltip title="Not comparable with the team's other sprints. Its own metrics still show, but it is marked to be left out when comparing them.">
                  <Tag>{sprintTypeLabels[sprint.sprintType]}</Tag>
                </WaydTooltip>
              )}
            </Space>
          ),
          actions: (
            <>
              {healthIndicator}
              <PageActions actionItems={lifecycleItems} />
            </>
          ),
        }}
        facts={<SprintFacts sprint={sprint} />}
        sectionActions={
          activeSection === SprintSections.Activities ? (
            <ActivityLogExportButton activityLog={activityLog} />
          ) : undefined
        }
      >
        {(section) => renderSection(section as SprintSections)}
      </RecordLayout>
      {lifecycleAction && (
        <ChangeSprintLifecycleForm
          sprint={sprint}
          action={lifecycleAction}
          onFormComplete={() => setLifecycleAction(null)}
          onFormCancel={() => setLifecycleAction(null)}
        />
      )}
      {correctingDates && (
        <CorrectSprintActualDatesForm
          sprint={sprint}
          previousSprint={previousSprint}
          nextSprint={nextSprint}
          onFormComplete={() => setCorrectingDates(false)}
          onFormCancel={() => setCorrectingDates(false)}
        />
      )}
      {editingDaysOff && (
        <SprintTeamDaysOffForm
          sprint={sprint}
          onFormComplete={() => setEditingDaysOff(false)}
          onFormCancel={() => setEditingDaysOff(false)}
        />
      )}
      {editingType && (
        <SprintTypeForm
          sprint={sprint}
          onFormComplete={() => setEditingType(false)}
          onFormCancel={() => setEditingType(false)}
        />
      )}
    </>
  )
}

const SprintDetailsPageWithAuthorization = authorizePage(
  SprintDetailsPage,
  'Permission',
  'Permissions.Iterations.View',
)

export default SprintDetailsPageWithAuthorization
