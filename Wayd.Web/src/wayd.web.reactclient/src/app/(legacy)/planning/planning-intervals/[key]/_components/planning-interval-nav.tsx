'use client'

import { IterationStateTag } from '@/src/components/common/planning'
import { IterationState } from '@/src/components/types'
import { useGetPlanningIntervalQuery } from '@/src/store/features/planning/planning-interval-api'
import { Divider, Flex, Menu, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import PlanningIntervalSwitcher from './planning-interval-switcher'

const { Text } = Typography

const PlanningIntervalNav = ({ piKey }: { piKey: number }) => {
  const { data: planningIntervalData } = useGetPlanningIntervalQuery(piKey)
  const pathname = usePathname()

  const basePath = `/planning/planning-intervals/${piKey}`
  const selectedKey = (() => {
    if (pathname.startsWith(`${basePath}/overview`)) return 'pi-overview'
    if (pathname.startsWith(`${basePath}/details`)) return 'pi-details'
    if (pathname.startsWith(`${basePath}/plan-review`)) return 'pi-plan-review'
    if (pathname.startsWith(`${basePath}/objectives/health-report`))
      return 'pi-reports-health-report'
    if (pathname.startsWith(`${basePath}/objectives`)) return 'pi-objectives'
    if (pathname.startsWith(`${basePath}/risks`)) return 'pi-risks'
    return ''
  })()

  const items: ItemType[] = [
    {
      label: (
        <Link href={`/planning/planning-intervals/${piKey}/overview`}>
          Overview
        </Link>
      ),
      key: 'pi-overview',
    },
    {
      label: (
        <Link href={`/planning/planning-intervals/${piKey}/details`}>
          Details
        </Link>
      ),
      key: 'pi-details',
    },
    {
      label: (
        <Link href={`/planning/planning-intervals/${piKey}/plan-review`}>
          Plan Review
        </Link>
      ),
      key: 'pi-plan-review',
    },
    {
      label: (
        <Link href={`/planning/planning-intervals/${piKey}/objectives`}>
          Objectives
        </Link>
      ),
      key: 'pi-objectives',
    },
    {
      label: (
        <Link href={`/planning/planning-intervals/${piKey}/risks`}>Risks</Link>
      ),
      key: 'pi-risks',
    },
    {
      label: 'Reports',
      key: 'pi-reports',
      children: [
        {
          label: (
            <Link
              href={`/planning/planning-intervals/${piKey}/objectives/health-report`}
            >
              Health Report
            </Link>
          ),
          key: 'pi-reports-health-report',
        },
      ],
    },
  ]

  return (
    <Flex
      align="center"
      gap={16}
      style={{
        margin: '0 -24px 12px',
        padding: '0 24px',
        background: 'var(--ant-color-bg-container)',
        borderBottom: '1px solid var(--ant-color-split)',
      }}
    >
      <Text type="secondary" style={{ whiteSpace: 'nowrap' }}>
        Planning Interval: {planningIntervalData?.name}
      </Text>
      {planningIntervalData?.state && (
        <IterationStateTag
          state={planningIntervalData.state.id as IterationState}
        />
      )}
      <PlanningIntervalSwitcher piKey={piKey} />
      <Divider orientation="vertical" />
      <Menu
        selectedKeys={selectedKey ? [selectedKey] : []}
        style={{
          flex: 1,
          minWidth: 0,
          borderBottom: 'none',
        }}
        mode="horizontal"
        items={items}
      />
    </Flex>
  )
}

export default PlanningIntervalNav

