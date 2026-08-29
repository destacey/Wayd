'use client'

import { use, useState } from 'react'
import { Button } from 'antd'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import useAuth from '@/src/components/contexts/auth'
import EditRiskForm from '@/src/components/common/planning/edit-risk-form'
import { authorizePage } from '@/src/components/hoc'
import { notFound } from 'next/navigation'
import { useGetRiskQuery } from '@/src/store/features/planning/risks-api'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import RiskExposureTag from '@/src/components/common/planning/risk-exposure-tag'
import RiskFacts from './_components/risk-facts'
import RiskNarrative from './_components/risk-narrative'
import RiskDetailsLoading from './loading'

enum RiskSections {
  Narrative = 'narrative',
}

const RiskDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const riskKey = Number(key)

  useDocumentTitle('Risk Details')

  const [openUpdateRiskForm, setOpenUpdateRiskForm] = useState<boolean>(false)

  const { data: risk, isLoading, refetch } = useGetRiskQuery(riskKey)

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

  // One section, so RecordLayout renders no rail and the narrative gets the
  // full content column.
  const sections: RecordSection[] = [
    { id: RiskSections.Narrative, label: 'Risk' },
  ]

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
      >
        {() => <RiskNarrative risk={risk} />}
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

const RiskDetailsPageWithAuthorization = authorizePage(
  RiskDetailsPage,
  'Permission',
  'Permissions.Risks.View',
)

export default RiskDetailsPageWithAuthorization
