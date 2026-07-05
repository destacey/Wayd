'use client'

import { IconMenu, PageTitle } from '@/src/components/common'
import { authorizePage } from '@/src/components/hoc'
import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import {
  useGetPlanningIntervalIterationBacklogQuery,
  useGetPlanningIntervalIterationQuery,
  useGetPlanningIntervalIterationsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import { notFound, usePathname, useRouter } from 'next/navigation'
import { ReactNode, use, useEffect, useState } from 'react'
import PlanningIntervalIterationDetailsLoading from './loading'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import { SwapOutlined } from '@ant-design/icons'
import { Card, Flex, Space, Tag } from 'antd'
import {
  IterationStateTag,
  SprintBacklogGrid,
} from '@/src/components/common/planning'
import { PlanningIntervalIterationOverview } from './_components'
import { IterationState } from '@/src/components/types'

enum IterationTabs {
  Overview = 'overview',
  Backlog = 'backlog',
}

const tabs = [
  {
    key: IterationTabs.Overview,
    tab: 'Overview',
  },
  {
    key: IterationTabs.Backlog,
    tab: 'Backlog',
  },
]

const PlanningIntervalIterationDetailsPage = (props: {
  params: Promise<{ key: string; iterationKey: string }>
}) => {
  const { key, iterationKey } = use(props.params)
  const piKey = Number(key)
  const piIterationKey = Number(iterationKey)

  const [activeTab, setActiveTab] = useState(IterationTabs.Overview)
  const [backlogQueryEnabled, setBacklogQueryEnabled] = useState(false)
  const [healthIndicator, setHealthIndicator] = useState<ReactNode>(null)

  const pathname = usePathname()
  const router = useRouter()
  const dispatch = useAppDispatch()

  const {
    data: iterationData,
    isLoading,
    error,
  } = useGetPlanningIntervalIterationQuery({
    planningIntervalKey: piKey,
    iterationKey: piIterationKey,
  })

  useDocumentTitle(
    `${iterationData?.name ?? piIterationKey} - PI Iteration Details`,
  )

  const { data: piIterationsData } =
    useGetPlanningIntervalIterationsQuery(piKey)

  const {
    data: backlogData,
    isLoading: backlogIsLoading,
    refetch: refetchBacklog,
  } = useGetPlanningIntervalIterationBacklogQuery(
    {
      planningIntervalKey: piKey,
      iterationKey: piIterationKey,
    },
    {
      skip: !backlogQueryEnabled,
    },
  )

  const handleIterationChange = (value: string | number) => {
    router.push(`/planning/planning-intervals/${piKey}/iterations/${value}`)
  }

  const iterationItems = !piIterationsData
    ? []
    : [...piIterationsData]
        .sort(
          (a, b) => new Date(b.start).getTime() - new Date(a.start).getTime(),
        )
        .map((option) => ({
          label: option.name,
          value: option.key,
        }))

  const switchIterations = !iterationItems.length ? null : (
    <IconMenu
      icon={<SwapOutlined />}
      tooltip="Switch to another PI iteration"
      items={iterationItems}
      selectedKeys={[piIterationKey.toString()]}
      onChange={handleIterationChange}
    />
  )

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as IterationTabs)

    if (tabKey === IterationTabs.Backlog && !backlogQueryEnabled) {
      setBacklogQueryEnabled(true)
    }
  }

  const renderTabContent = () => {
    switch (activeTab) {
      case IterationTabs.Overview:
        return (
          <PlanningIntervalIterationOverview
            iteration={iterationData!}
            onHealthIndicatorReady={setHealthIndicator}
          />
        )
      case IterationTabs.Backlog:
        return (
          <SprintBacklogGrid
            workItems={backlogData ?? []}
            isLoading={backlogIsLoading}
            refetch={refetchBacklog}
            gridHeight={550}
            persistStateKey="iteration-backlog"
          />
        )
      default:
        return null
    }
  }

  useEffect(() => {
    if (!iterationData) return

    dispatch(setBreadcrumbTitle({ title: piIterationKey.toString(), pathname }))
  }, [dispatch, iterationData, pathname, piIterationKey])

  if (isLoading) {
    return <PlanningIntervalIterationDetailsLoading />
  }

  if (!iterationData) return notFound()

  const state =
    IterationState[iterationData.state as keyof typeof IterationState] ??
    IterationState.Future

  return (
    <>
      <PageTitle
        title={`${iterationKey} - ${iterationData.name}`}
        subtitle="PI Iteration Details"
        tags={
          <Space>
            {switchIterations}
            <IterationStateTag state={state} />
            <Tag>{iterationData.category.name}</Tag>
          </Space>
        }
        actions={healthIndicator}
      />
      <Flex vertical gap="middle">
        <Card
          style={{ width: '100%' }}
          tabList={tabs}
          activeTabKey={activeTab}
          onTabChange={onTabChange}
        >
          {renderTabContent()}
        </Card>
      </Flex>
    </>
  )
}

const PlanningIntervalIterationDetailsPageWithAuthorization = authorizePage(
  PlanningIntervalIterationDetailsPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalIterationDetailsPageWithAuthorization
