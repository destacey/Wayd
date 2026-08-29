'use client'

import { IconMenu } from '@/src/components/common'
import { useGetPlanningIntervalsQuery } from '@/src/store/features/planning/planning-interval-api'
import { SwapOutlined } from '@ant-design/icons'
import { useRouter, useSearchParams } from 'next/navigation'
import { useState } from 'react'

/**
 * Jumps to the same section of another PI.
 *
 * `section` is the only param worth carrying: the others name this PI's own
 * data — a team on Plan Review, a filter — and would not resolve on the next.
 */
const PlanningIntervalSwitcher = ({ piKey }: { piKey: number }) => {
  const router = useRouter()
  const searchParams = useSearchParams()
  const [queryEnabled, setQueryEnabled] = useState(false)
  const { data: piListData } = useGetPlanningIntervalsQuery(undefined, {
    skip: !queryEnabled,
  })

  const piItems = !piListData
    ? []
    : [...piListData]
        .sort(
          (a, b) => new Date(b.start).getTime() - new Date(a.start).getTime(),
        )
        .map((option) => ({
          label: option.name,
          extra: option.state.name,
          value: option.key,
        }))

  const handlePIChange = (value: string | number) => {
    const section = searchParams.get('section')
    const target = `/planning/planning-intervals/${value}${
      section ? `?section=${section}` : ''
    }`
    router.push(target)
  }

  const handleOpenChange = (open: boolean) => {
    if (open && !queryEnabled) {
      setQueryEnabled(true)
    }
  }

  return (
    <IconMenu
      icon={<SwapOutlined />}
      tooltip="Switch to another PI"
      items={piItems}
      selectedKeys={[piKey.toString()]}
      alwaysRender
      onChange={handlePIChange}
      onOpenChange={handleOpenChange}
    />
  )
}

export default PlanningIntervalSwitcher
