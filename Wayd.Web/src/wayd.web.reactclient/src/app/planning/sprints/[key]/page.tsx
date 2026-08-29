'use client'

import { IconMenu } from '@/src/components/common'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetSprintBacklogQuery,
  useGetSprintQuery,
} from '@/src/store/features/planning/sprints-api'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { ReactNode, use, useState } from 'react'
import SprintDetailsLoading from './loading'
import {
  SprintBacklogGrid,
  SprintDetails,
} from '@/src/app/planning/sprints/_components'
import { IterationStateTag } from '@/src/components/common/planning'
import { IterationState } from '@/src/components/types'
import {
  useGetTeamOperatingModelAsOfQuery,
  useGetTeamSprintsQuery,
} from '@/src/store/features/organizations/team-api'
import { SwapOutlined } from '@ant-design/icons'
import { Space } from 'antd'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import SprintFacts from './_components/sprint-facts'

enum SprintSections {
  Overview = 'overview',
  Backlog = 'backlog',
}

const SprintDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const sprintKey = Number(key)

  // Rendered by SprintMetrics once its own query resolves, so the sprint's
  // health can sit in the identity bar beside the record's name.
  const [healthIndicator, setHealthIndicator] = useState<ReactNode>(null)

  const router = useRouter()

  // The active section lives in the URL, owned by RecordLayout. Read here only
  // to gate the backlog, which is the expensive query.
  const searchParams = useSearchParams()
  const activeSection = (searchParams.get('section') ??
    SprintSections.Overview) as SprintSections

  const { data: sprint, isLoading } = useGetSprintQuery(sprintKey, {
    skip: !sprintKey,
  })

  const {
    data: workItems,
    isLoading: workItemsLoading,
    refetch: refetchWorkItems,
  } = useGetSprintBacklogQuery(sprintKey, {
    skip: !sprintKey || activeSection !== SprintSections.Backlog,
  })

  const { data: teamOperatingModel } = useGetTeamOperatingModelAsOfQuery(
    {
      teamId: sprint?.team.id ?? '',
      asOfDate: sprint?.start ?? '',
    },
    { skip: !sprint || !sprint?.team.id },
  )

  useDocumentTitle(`${sprint?.name ?? sprintKey} - Sprint Details`)

  const { data: teamSprints } = useGetTeamSprintsQuery(sprint?.team.id ?? '', {
    skip: !sprint?.team.id,
  })

  const handleSprintChange = (value: string | number) => {
    router.push(`/planning/sprints/${value}`)
  }

  const sprintsItems = !teamSprints
    ? []
    : [...teamSprints]
        .sort(
          (a, b) => new Date(b.start).getTime() - new Date(a.start).getTime(),
        )
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

  const sections: RecordSection[] = [
    { id: SprintSections.Overview, label: 'Overview' },
    { id: SprintSections.Backlog, label: 'Backlog' },
  ]

  const renderSection = (section: SprintSections) => {
    switch (section) {
      case SprintSections.Backlog:
        return (
          <SprintBacklogGrid
            workItems={workItems ?? []}
            isLoading={workItemsLoading}
            refetch={refetchWorkItems}
            hideTeamColumn
            persistStateKey="sprint-backlog"
          />
        )
      default:
        return (
          <SprintDetails
            sprint={sprint}
            sizingMethod={teamOperatingModel?.sizingMethod}
            onHealthIndicatorReady={setHealthIndicator}
          />
        )
    }
  }

  return (
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
          </Space>
        ),
        actions: healthIndicator,
      }}
      facts={<SprintFacts sprint={sprint} />}
    >
      {(section) => renderSection(section as SprintSections)}
    </RecordLayout>
  )
}

const SprintDetailsPageWithAuthorization = authorizePage(
  SprintDetailsPage,
  'Permission',
  'Permissions.Iterations.View',
)

export default SprintDetailsPageWithAuthorization
