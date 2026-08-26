'use client'

import {
  CreatePlanningIntervalObjectiveHealthCheckForm,
  DeletePlanningIntervalObjectiveForm,
  EditPlanningIntervalObjectiveForm,
} from '@/src/app/(legacy)/planning/planning-intervals/_components'
import PiObjectiveHealthCheckTag from '@/src/app/(legacy)/planning/planning-intervals/_components/pi-objective-health-check-tag'
import PiObjectiveHealthReportGrid from '@/src/app/(legacy)/planning/planning-intervals/_components/pi-objective-health-report-grid'
import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle, useLinkedEmployee } from '@/src/hooks'
import { useGetPlanningIntervalObjectiveQuery } from '@/src/store/features/planning/planning-interval-api'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter } from 'next/navigation'
import { Suspense, use, useState } from 'react'
import PlanningIntervalObjectiveLoading from './loading'
import PlanningIntervalObjectiveFacts from './_components/planning-interval-objective-facts'
import PlanningIntervalObjectiveOverview from './_components/planning-interval-objective-overview'
import PlanningIntervalObjectiveWorkItemsSection from './_components/planning-interval-objective-work-items-section'

enum ObjectiveSections {
  Overview = 'overview',
  WorkItems = 'work-items',
  HealthReport = 'health-report',
}

const sections: RecordSection[] = [
  { id: ObjectiveSections.Overview, label: 'Overview' },
  { id: ObjectiveSections.WorkItems, label: 'Work Items' },
]

const reports: RecordSection[] = [
  { id: ObjectiveSections.HealthReport, label: 'Health Report' },
]

const PlanningIntervalObjectivePage = (props: {
  params: Promise<{ key: string; objectiveKey: string }>
}) => {
  const { key, objectiveKey } = use(props.params)

  const [openUpdateForm, setOpenUpdateForm] = useState<boolean>(false)
  const [openDeleteForm, setOpenDeleteForm] = useState<boolean>(false)
  const [openCreateHealthCheckForm, setOpenCreateHealthCheckForm] =
    useState<boolean>(false)

  const {
    data: objective,
    isLoading,
    refetch: refetchObjective,
  } = useGetPlanningIntervalObjectiveQuery({
    planningIntervalKey: key,
    objectiveKey,
  })

  useDocumentTitle(`${objective?.name ?? objectiveKey} - PI Objective`)

  const router = useRouter()
  const { hasPermissionClaim } = useAuth()
  const { hasLinkedEmployee } = useLinkedEmployee()
  const canManageObjectives = !!hasPermissionClaim(
    'Permissions.PlanningIntervalObjectives.Manage',
  )
  // A health check records who reported it, so it needs an employee link as
  // well as the permission — the API rejects an unlinked account with 403.
  const canCreateHealthChecks = canManageObjectives && hasLinkedEmployee

  const closeUpdateForm = (wasSaved: boolean) => {
    setOpenUpdateForm(false)
    if (wasSaved) refetchObjective()
  }

  const closeDeleteForm = (wasSaved: boolean) => {
    setOpenDeleteForm(false)
    // The record no longer exists, so fall back to the PI that held it.
    if (wasSaved) router.push(`/planning/planning-intervals/${key}`)
  }

  const closeCreateHealthCheckForm = (wasSaved: boolean) => {
    setOpenCreateHealthCheckForm(false)
    if (wasSaved) refetchObjective()
  }

  if (isLoading) return <PlanningIntervalObjectiveLoading />
  if (!objective) return notFound()

  const piHref = `/planning/planning-intervals/${objective.planningInterval.key}`

  // Plan review has a tab per team, so a team-of-teams objective has nowhere to
  // land there and stops at the PI instead.
  const planReviewHop =
    objective.team?.type === 'Team'
      ? [
          {
            label: 'Plan Review',
            href: `${piHref}?section=plan-review&team=${objective.team.code.toLowerCase()}`,
          },
        ]
      : []

  const actionsMenuItems: ItemType[] = !canManageObjectives
    ? []
    : [
        {
          key: 'edit',
          label: 'Edit',
          onClick: () => setOpenUpdateForm(true),
        },
        {
          key: 'delete',
          label: 'Delete',
          onClick: () => setOpenDeleteForm(true),
        },
        { key: 'divider', type: 'divider' },
        {
          key: 'createHealthCheck',
          label: 'Create Health Check',
          disabled: !canCreateHealthChecks,
          onClick: () => setOpenCreateHealthCheckForm(true),
        },
      ]

  const renderSection = (section: ObjectiveSections) => {
    switch (section) {
      case ObjectiveSections.WorkItems:
        return (
          <PlanningIntervalObjectiveWorkItemsSection
            planningIntervalKey={objective.planningInterval.key}
            objectiveKey={objective.key}
            canLinkWorkItems={canManageObjectives}
          />
        )
      case ObjectiveSections.HealthReport:
        return (
          <PiObjectiveHealthReportGrid
            planningIntervalId={objective.planningInterval?.id}
            objectiveId={objective.id}
          />
        )
      default:
        return (
          <PlanningIntervalObjectiveOverview
            objective={objective}
            canManageObjectives={canManageObjectives}
          />
        )
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        reports={reports}
        defaultSection={ObjectiveSections.Overview}
        record={{
          name: objective.name,
          recordKey: String(objective.key),
          subtitle: 'PI Objective',
          parent: [
            { label: objective.planningInterval.name, href: piHref },
            ...planReviewHop,
          ],
          tags: (
            <PiObjectiveHealthCheckTag
              healthCheck={objective.healthCheck}
              planningIntervalId={objective.planningInterval?.id}
              objectiveId={objective.id}
            />
          ),
          actions: <PageActions actionItems={actionsMenuItems} />,
        }}
        facts={<PlanningIntervalObjectiveFacts objective={objective} />}
      >
        {(section) => renderSection(section as ObjectiveSections)}
      </RecordLayout>
      {openUpdateForm && (
        <EditPlanningIntervalObjectiveForm
          objectiveKey={objective.key}
          planningIntervalKey={objective.planningInterval?.key}
          onFormSave={() => closeUpdateForm(true)}
          onFormCancel={() => closeUpdateForm(false)}
        />
      )}
      {openDeleteForm && (
        <DeletePlanningIntervalObjectiveForm
          objective={objective}
          onFormSave={() => closeDeleteForm(true)}
          onFormCancel={() => closeDeleteForm(false)}
        />
      )}
      {openCreateHealthCheckForm && (
        <CreatePlanningIntervalObjectiveHealthCheckForm
          planningIntervalId={objective.planningInterval.id}
          objectiveId={objective.id}
          onFormCreate={() => closeCreateHealthCheckForm(true)}
          onFormCancel={() => closeCreateHealthCheckForm(false)}
        />
      )}
    </>
  )
}

// RecordLayout reads useSearchParams, which suspends a prerendered route up to
// the nearest boundary. In development routes render on demand, so a missing
// one only fails the production build.
const PlanningIntervalObjectivePageWithSuspense = (props: {
  params: Promise<{ key: string; objectiveKey: string }>
}) => (
  <Suspense fallback={<PlanningIntervalObjectiveLoading />}>
    <PlanningIntervalObjectivePage {...props} />
  </Suspense>
)

const PlanningIntervalObjectivePageWithAuthorization = authorizePage(
  PlanningIntervalObjectivePageWithSuspense,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalObjectivePageWithAuthorization
