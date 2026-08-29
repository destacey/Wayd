'use client'

import {
  EditPlanningIntervalForm,
  ManagePlanningIntervalDatesForm,
  ManagePlanningIntervalTeamsForm,
} from '@/src/app/planning/planning-intervals/_components'
import {
  IterationCards,
  PlanningIntervalAtAGlance,
  PlanningIntervalNeedsAttentionCard,
  PlanningIntervalTeamCards,
} from '@/src/app/planning/planning-intervals/[key]/_components'
import PlanningIntervalSwitcher from './_components/planning-interval-switcher'
import { PageActions } from '@/src/components/common'
import {
  IterationStateTag,
  SprintBacklogGrid,
} from '@/src/components/common/planning'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { IterationState } from '@/src/components/types'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetPlanningIntervalBacklogQuery,
  useGetPlanningIntervalQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import { Button, Flex } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useSearchParams } from 'next/navigation'
import { Suspense, use, useState } from 'react'
import PlanningIntervalLoading from './loading'
import ObjectivesHealthReportSection from './_components/objectives-health-report-section'
import PlanningIntervalFacts from './_components/planning-interval-facts'
import PlanningIntervalObjectivesSection from './_components/planning-interval-objectives-section'
import PlanningIntervalPlanReviewSection from './_components/planning-interval-plan-review-section'
import PlanningIntervalRisksSection from './_components/planning-interval-risks-section'
import TeamSprintMappingsModal from './_components/team-sprint-mappings-modal'

enum PlanningIntervalSections {
  Overview = 'overview',
  Objectives = 'objectives',
  PlanReview = 'plan-review',
  Risks = 'risks',
  Backlog = 'backlog',
  HealthReport = 'health-report',
}

const sections: RecordSection[] = [
  { id: PlanningIntervalSections.Overview, label: 'Overview' },
  { id: PlanningIntervalSections.PlanReview, label: 'Plan Review' },
  { id: PlanningIntervalSections.Objectives, label: 'Objectives' },
  { id: PlanningIntervalSections.Risks, label: 'Risks' },
  { id: PlanningIntervalSections.Backlog, label: 'Backlog' },
]

const reports: RecordSection[] = [
  { id: PlanningIntervalSections.HealthReport, label: 'Objectives Health' },
]

const PlanningIntervalPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const piKey = Number(key)

  const [openEditForm, setOpenEditForm] = useState<boolean>(false)
  const [openManageDatesForm, setOpenManageDatesForm] = useState<boolean>(false)
  const [openManageTeamsForm, setOpenManageTeamsForm] = useState<boolean>(false)
  const [openTeamSprintsModal, setOpenTeamSprintsModal] =
    useState<boolean>(false)
  const [openCreateObjectiveForm, setOpenCreateObjectiveForm] =
    useState<boolean>(false)

  const { hasPermissionClaim } = useAuth()
  const canUpdatePlanningInterval = hasPermissionClaim(
    'Permissions.PlanningIntervals.Update',
  )
  const canManageObjectives = hasPermissionClaim(
    'Permissions.PlanningIntervalObjectives.Manage',
  )

  // The active section lives in the URL, owned by RecordLayout. Read here only
  // to hold back the teams query until a section actually needs it.
  const searchParams = useSearchParams()
  const activeSection = (searchParams.get('section') ??
    PlanningIntervalSections.Overview) as PlanningIntervalSections

  const {
    data: planningInterval,
    isLoading,
    refetch: refetchPlanningInterval,
  } = useGetPlanningIntervalQuery(piKey)

  useDocumentTitle(`${planningInterval?.name ?? piKey} - Planning Interval`)

  // A new objective must have a team to own it, so the create action can only
  // be offered once we know the PI has one.
  const { data: teams, refetch: refetchTeams } =
    useGetPlanningIntervalTeamsQuery(piKey, {
      skip: activeSection !== PlanningIntervalSections.Objectives,
    })

  // Every work item in every sprint of the PI, so it waits for its section.
  const {
    data: backlog,
    isLoading: backlogIsLoading,
    refetch: refetchBacklog,
  } = useGetPlanningIntervalBacklogQuery(piKey, {
    skip: activeSection !== PlanningIntervalSections.Backlog,
  })

  const closeEditForm = (wasSaved: boolean) => {
    setOpenEditForm(false)
    if (wasSaved) refetchPlanningInterval()
  }

  const closeManageDatesForm = (wasSaved: boolean) => {
    setOpenManageDatesForm(false)
    if (wasSaved) refetchPlanningInterval()
  }

  const closeManageTeamsForm = (wasSaved: boolean) => {
    setOpenManageTeamsForm(false)
    if (wasSaved) {
      refetchPlanningInterval()
      refetchTeams()
    }
  }

  if (isLoading) return <PlanningIntervalLoading />
  if (!planningInterval) return notFound()

  // The PI's own attributes first, then the team setup it holds: choose the
  // teams, then map their sprints.
  const actionsMenuItems: ItemType[] = !canUpdatePlanningInterval
    ? []
    : [
        {
          key: 'edit-pi-menu-item',
          label: 'Edit',
          onClick: () => setOpenEditForm(true),
        },
        {
          key: 'manage-dates-menu-item',
          label: 'Manage Dates',
          onClick: () => setOpenManageDatesForm(true),
        },
        { key: 'teams-divider', type: 'divider' },
        {
          key: 'manage-teams-menu-item',
          label: 'Manage Teams',
          onClick: () => setOpenManageTeamsForm(true),
        },
        {
          key: 'team-sprints-menu-item',
          label: 'Team Sprints',
          onClick: () => setOpenTeamSprintsModal(true),
        },
      ]

  const stateId = planningInterval.state.id as IterationState

  // An objective needs a team to own it, and a locked PI takes no new ones.
  const canCreateObjectives =
    canManageObjectives &&
    !planningInterval.objectivesLocked &&
    (teams?.filter((t) => t.type === 'Team').length ?? 0) > 0

  const sectionActions =
    activeSection === PlanningIntervalSections.Objectives &&
    canCreateObjectives ? (
      <Button onClick={() => setOpenCreateObjectiveForm(true)}>
        Create Objective
      </Button>
    ) : null

  const renderSection = (section: PlanningIntervalSections) => {
    switch (section) {
      case PlanningIntervalSections.Objectives:
        return (
          <PlanningIntervalObjectivesSection
            planningIntervalKey={piKey}
            createFormOpen={openCreateObjectiveForm}
            onCreateFormClose={() => setOpenCreateObjectiveForm(false)}
          />
        )
      case PlanningIntervalSections.PlanReview:
        return (
          <PlanningIntervalPlanReviewSection
            planningInterval={planningInterval}
            refreshPlanningInterval={refetchPlanningInterval}
          />
        )
      case PlanningIntervalSections.Risks:
        return <PlanningIntervalRisksSection planningIntervalKey={piKey} />
      case PlanningIntervalSections.Backlog:
        return (
          <SprintBacklogGrid
            workItems={backlog ?? []}
            isLoading={backlogIsLoading}
            refetch={refetchBacklog}
            persistStateKey="planning-interval-backlog"
          />
        )
      case PlanningIntervalSections.HealthReport:
        return <ObjectivesHealthReportSection planningIntervalKey={piKey} />
      default:
        return (
          <Flex vertical gap="middle">
            <PlanningIntervalAtAGlance planningInterval={planningInterval} />
            <IterationCards piKey={piKey} />
            {/* A future PI has no delivery to report on yet. */}
            {stateId !== IterationState.Future && (
              <PlanningIntervalTeamCards piKey={piKey} />
            )}
            {stateId === IterationState.Active && (
              <PlanningIntervalNeedsAttentionCard piKey={piKey} />
            )}
          </Flex>
        )
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        reports={reports}
        defaultSection={PlanningIntervalSections.Overview}
        record={{
          name: planningInterval.name,
          recordKey: String(planningInterval.key),
          parent: {
            label: 'Planning Intervals',
            href: '/planning/planning-intervals',
          },
          subtitle: 'Planning Interval',
          tags: (
            <Flex gap="small" align="center">
              <PlanningIntervalSwitcher piKey={piKey} />
              <IterationStateTag state={stateId} />
            </Flex>
          ),
          actions: <PageActions actionItems={actionsMenuItems} />,
        }}
        facts={<PlanningIntervalFacts planningInterval={planningInterval} />}
        sectionActions={sectionActions}
      >
        {(section) => renderSection(section as PlanningIntervalSections)}
      </RecordLayout>
      {openEditForm && (
        <EditPlanningIntervalForm
          planningIntervalKey={piKey}
          onFormUpdate={() => closeEditForm(true)}
          onFormCancel={() => closeEditForm(false)}
        />
      )}
      {openManageDatesForm && (
        <ManagePlanningIntervalDatesForm
          id={planningInterval.id}
          planningIntervalKey={piKey}
          onFormSave={() => closeManageDatesForm(true)}
          onFormCancel={() => closeManageDatesForm(false)}
        />
      )}
      {openManageTeamsForm && (
        <ManagePlanningIntervalTeamsForm
          id={planningInterval.id}
          onFormSave={() => closeManageTeamsForm(true)}
          onFormCancel={() => closeManageTeamsForm(false)}
        />
      )}
      {openTeamSprintsModal && (
        <TeamSprintMappingsModal
          planningInterval={planningInterval}
          onClose={() => setOpenTeamSprintsModal(false)}
        />
      )}
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const PlanningIntervalPageWithSuspense = (props: {
  params: Promise<{ key: string }>
}) => (
  <Suspense fallback={<PlanningIntervalLoading />}>
    <PlanningIntervalPage {...props} />
  </Suspense>
)

const PlanningIntervalPageWithAuthorization = authorizePage(
  PlanningIntervalPageWithSuspense,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalPageWithAuthorization
