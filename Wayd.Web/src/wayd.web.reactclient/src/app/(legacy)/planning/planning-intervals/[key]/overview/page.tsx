'use client'

import PageTitle from '@/src/components/common/page-title'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { authorizePage } from '@/src/components/hoc'
import { notFound, usePathname } from 'next/navigation'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import PlanningIntervalDetailsLoading from '../loading'
import { use, useEffect } from 'react'
import { useGetPlanningIntervalQuery } from '@/src/store/features/planning/planning-interval-api'
import { Flex } from 'antd'
import { IterationState } from '@/src/components/types'
import {
  IterationCards,
  PlanningIntervalAtAGlance,
  PlanningIntervalNeedsAttentionCard,
  PlanningIntervalTeamCards,
} from '../_components'

const PlanningIntervalOverviewPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const piKey = Number(key)

  const pathname = usePathname()
  const dispatch = useAppDispatch()

  const { data: planningIntervalData, isLoading } =
    useGetPlanningIntervalQuery(+piKey)

  useDocumentTitle(`${planningIntervalData?.name ?? piKey} - PI Overview`)

  useEffect(() => {
    planningIntervalData &&
      dispatch(
        setBreadcrumbTitle({ title: planningIntervalData.name, pathname }),
      )
  }, [dispatch, pathname, planningIntervalData])

  if (isLoading) {
    return <PlanningIntervalDetailsLoading />
  }

  if (!isLoading && !planningIntervalData) {
    return notFound()
  }

  const isFuturePlanningInterval =
    planningIntervalData!.state.id === IterationState.Future
  const isActivePlanningInterval =
    planningIntervalData!.state.id === IterationState.Active

  return (
    <Flex vertical gap="middle">
      <PageTitle title="PI Overview" />
      <PlanningIntervalAtAGlance planningInterval={planningIntervalData!} />
      <IterationCards piKey={piKey} />
      {!isFuturePlanningInterval && <PlanningIntervalTeamCards piKey={piKey} />}
      {isActivePlanningInterval && <PlanningIntervalNeedsAttentionCard piKey={piKey} />}
    </Flex>
  )
}

const PlanningIntervalOverviewPageWithAuthorization = authorizePage(
  PlanningIntervalOverviewPage,
  'Permission',
  'Permissions.PlanningIntervals.View',
)

export default PlanningIntervalOverviewPageWithAuthorization
