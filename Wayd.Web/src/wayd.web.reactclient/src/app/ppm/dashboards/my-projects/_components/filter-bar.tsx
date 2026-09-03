'use client'

import { ClearOutlined, ReloadOutlined } from '@ant-design/icons'
import { useGetProjectStatusOptionsQuery } from '@/src/store/features/ppm/projects-api'
import { Button, Flex, Skeleton, theme } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { FC, RefObject } from 'react'
import { LifecycleCategory } from '@/src/components/types'
import { getLifecycleCategoryStatusSurface } from '@/src/utils'
import styles from '../my-projects-dashboard.module.css'

const ROLE_OPTIONS = [
  { label: 'Sponsor', value: 1 },
  { label: 'Owner', value: 2 },
  { label: 'PM', value: 3 },
  { label: 'Member', value: 4 },
  { label: 'Task Assignee', value: 5 },
]

export interface MyProjectsDashboardFilterBarProps {
  selectedRoles: number[]
  onRoleChange: (roles: number[]) => void
  selectedStatuses: number[]
  onStatusChange: (statuses: number[]) => void
  onReset: () => void
  onRefresh: () => void
  containerRef?: RefObject<HTMLDivElement | null>
}

const MyProjectsDashboardFilterBar: FC<MyProjectsDashboardFilterBarProps> = ({
  selectedRoles,
  onRoleChange,
  selectedStatuses,
  onStatusChange,
  onReset,
  onRefresh,
  containerRef,
}) => {
  const { data: statusOptions, isLoading } = useGetProjectStatusOptionsQuery()
  const { token } = theme.useToken()

  if (isLoading) {
    return (
      <div ref={containerRef} className={styles.filterBar}>
        <Skeleton.Input active size="small" style={{ width: 300 }} />
      </div>
    )
  }

  const toggleRole = (value: number) => {
    const next = selectedRoles.includes(value)
      ? selectedRoles.filter((r) => r !== value)
      : [...selectedRoles, value]
    onRoleChange(next)
  }

  const toggleStatus = (value: number) => {
    const next = selectedStatuses.includes(value)
      ? selectedStatuses.filter((s) => s !== value)
      : [...selectedStatuses, value]
    onStatusChange(next)
  }

  return (
    <div ref={containerRef} className={styles.filterBar}>
      <Flex align="center" gap={16} wrap>
        <Flex gap={2} wrap align="center">
          <span className={styles.filterLabel}>My Role:</span>
          <Button
            size="small"
            className={styles.chipButton}
            color={selectedRoles.length === 0 ? 'primary' : 'default'}
            variant="outlined"
            style={
              selectedRoles.length === 0 ? undefined : { borderStyle: 'dashed' }
            }
            onClick={() => onRoleChange([])}
          >
            All
          </Button>
          {ROLE_OPTIONS.map((role) => {
            const isSelected = selectedRoles.includes(role.value)
            return (
              <Button
                key={role.value}
                size="small"
                className={styles.chipButton}
                color={isSelected ? 'primary' : 'default'}
                variant="outlined"
                // Roles carry no status color, so the dash alone separates the two
                // states — the same cue the status buttons beside them use.
                style={isSelected ? undefined : { borderStyle: 'dashed' }}
                onClick={() => toggleRole(role.value)}
              >
                {role.label}
              </Button>
            )
          })}
        </Flex>

        <Flex gap={2} wrap align="center">
          <span className={styles.filterLabel}>Status:</span>
          {statusOptions?.map((status) => {
            const isSelected = selectedStatuses.includes(status.value)
            // Matches the PPM filter bar: a lit button wears the colors the status
            // column shows for that status, an unlit one drops the color and goes
            // dashed. The dash, not the color, carries selection — a not-started
            // status is grey, so its lit chip differs from an unlit button by a
            // background step alone, and the dash stays legible without relying on
            // hue at all.
            const category =
              LifecycleCategory[
                status.lifecycleCategory as keyof typeof LifecycleCategory
              ]
            const surface =
              isSelected && category !== undefined
                ? getLifecycleCategoryStatusSurface(category, token)
                : undefined
            return (
              <Button
                key={status.value}
                size="small"
                className={styles.chipButton}
                color={isSelected && !surface ? 'primary' : 'default'}
                variant="outlined"
                style={
                  isSelected
                    ? surface
                      ? {
                          backgroundColor: surface.background,
                          borderColor: surface.border,
                          color: surface.text,
                        }
                      : undefined
                    : { borderStyle: 'dashed' }
                }
                onClick={() => toggleStatus(status.value)}
              >
                {status.label}
              </Button>
            )
          })}
        </Flex>

        <Flex gap={2}>
          <WaydTooltip title="Refresh Data">
            <Button
              type="text"
              shape="circle"
              icon={<ReloadOutlined />}
              aria-label="Refresh data"
              onClick={onRefresh}
            />
          </WaydTooltip>
          <WaydTooltip title="Reset Filters">
            <Button
              type="text"
              shape="circle"
              icon={<ClearOutlined />}
              aria-label="Reset filters"
              onClick={onReset}
            />
          </WaydTooltip>
        </Flex>
      </Flex>
    </div>
  )
}

export default MyProjectsDashboardFilterBar
