'use client'

import { IconMenu } from '@/src/components/common'
import { useGetPlanningIntervalsQuery } from '@/src/store/features/planning/planning-interval-api'
import { SwapOutlined } from '@ant-design/icons'
import { usePathname, useRouter } from 'next/navigation'
import { useState } from 'react'

// Top-level tabs that are safe to carry across PIs. Deeper routes (e.g. a
// specific objective or risk) belong to one PI's data and would 404 on the
// next, so those fall back to the PI root.
const PRESERVABLE_SUBPATHS = [
  'overview',
  'details',
  'plan-review',
  'objectives/health-report',
  'objectives',
  'risks',
]

const resolveSubPath = (pathname: string, piKey: number) => {
  const prefix = `/planning/planning-intervals/${piKey}`
  if (!pathname.startsWith(prefix)) return ''
  const rest = pathname.slice(prefix.length).replace(/^\/+/, '')
  return PRESERVABLE_SUBPATHS.find(
    (sub) => rest === sub || rest.startsWith(`${sub}/`),
  )
}

const PlanningIntervalSwitcher = ({ piKey }: { piKey: number }) => {
  const router = useRouter()
  const pathname = usePathname()
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
    const subPath = resolveSubPath(pathname ?? '', piKey)
    const target = subPath
      ? `/planning/planning-intervals/${value}/${subPath}`
      : `/planning/planning-intervals/${value}`
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
