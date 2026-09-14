'use client'

import { Suspense, use, useState } from 'react'
import { Button } from 'antd'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import useAuth from '@/src/components/contexts/auth'
import EditRiskForm from '@/src/components/common/planning/edit-risk-form'
import { authorizePage } from '@/src/components/hoc'
import { notFound, useSearchParams } from 'next/navigation'
import {
  useGetRiskActivitiesQuery,
  useGetRiskQuery,
  useLazyGetRiskActivitiesQuery,
} from '@/src/store/features/planning/risks-api'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import RiskExposureTag from '@/src/components/common/planning/risk-exposure-tag'
import RiskFacts from './_components/risk-facts'
import RiskNarrative from './_components/risk-narrative'
import RiskDetailsLoading from './loading'

enum RiskSections {
  Narrative = 'narrative',
  Activities = 'activities',
}

const sections: RecordSection[] = [
  { id: RiskSections.Narrative, label: 'Risk' },
  { id: RiskSections.Activities, label: 'Activity' },
]

const RiskDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const riskKey = Number(key)

  useDocumentTitle('Risk Details')

  const [openUpdateRiskForm, setOpenUpdateRiskForm] = useState<boolean>(false)

  // The active section lives in the URL, owned by RecordLayout. Read here only
  // to hold back the activity query until its section is open.
  const searchParams = useSearchParams()
  const activeSection = (searchParams.get('section') ??
    RiskSections.Narrative) as RiskSections

  const { data: risk, isLoading, refetch } = useGetRiskQuery(riskKey)

  const activitiesQuery = useGetRiskActivitiesQuery(
    { idOrKey: risk?.id ?? '', page: 1, pageSize: ACTIVITY_LOG_PAGE_SIZE },
    { skip: !risk?.id || activeSection !== RiskSections.Activities },
  )
  const [fetchActivityLogPage] = useLazyGetRiskActivitiesQuery()

  const activityLog = useActivityLog({
    idOrKey: risk?.id,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: `risk-${risk?.key ?? riskKey}-activity`,
  })

  const { hasPermissionClaim } = useAuth()
  const canUpdateRisks = hasPermissionClaim('Permissions.Risks.Update')

  const onUpdateRiskFormClosed = (wasSaved: boolean) => {
    setOpenUpdateRiskForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  if (!isLoading && !risk) {
    return notFound()
  }

  if (!risk) {
    return <RiskDetailsLoading />
  }

  // A risk has no list of its own — it is reached from a team, a planning
  // interval, or the viewer's assigned risks. Its team is the nearest thing to
  // a parent, which is what the breadcrumb pointed at before.
  const teamHref =
    risk.team?.type === 'Team'
      ? `/organizations/teams/${risk.team.key}`
      : `/organizations/team-of-teams/${risk.team?.key}`

  const renderSection = (section: RiskSections) => {
    switch (section) {
      case RiskSections.Activities:
        return <ActivityLogTimeline {...activityLog.timelineProps} />
      default:
        return <RiskNarrative risk={risk} />
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={RiskSections.Narrative}
        record={{
          name: risk.summary,
          recordKey: String(risk.key),
          subtitle: 'Risk Details',
          parent: risk.team
            ? { label: risk.team.name, href: teamHref }
            : undefined,
          // Exposure is the one grading worth reading before anything else,
          // so it sits in the identity bar rather than only in the panel.
          tags: <RiskExposureTag exposure={risk.exposure?.name} />,
          descriptor: risk.status?.name,
          actions: canUpdateRisks && (
            <Button onClick={() => setOpenUpdateRiskForm(true)}>Edit</Button>
          ),
        }}
        facts={<RiskFacts risk={risk} />}
        sectionActions={
          activeSection === RiskSections.Activities ? (
            <ActivityLogExportButton activityLog={activityLog} />
          ) : undefined
        }
      >
        {(section) => renderSection(section as RiskSections)}
      </RecordLayout>
      {openUpdateRiskForm && (
        <EditRiskForm
          riskKey={riskKey}
          onFormSave={() => onUpdateRiskFormClosed(true)}
          onFormCancel={() => onUpdateRiskFormClosed(false)}
        />
      )}
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const RiskDetailsPageWithSuspense = (props: {
  params: Promise<{ key: string }>
}) => (
  <Suspense fallback={<RiskDetailsLoading />}>
    <RiskDetailsPage {...props} />
  </Suspense>
)

const RiskDetailsPageWithAuthorization = authorizePage(
  RiskDetailsPageWithSuspense,
  'Permission',
  'Permissions.Risks.View',
)

export default RiskDetailsPageWithAuthorization
