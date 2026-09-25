'use client'

import { ClearOutlined, ReloadOutlined } from '@ant-design/icons'
import { useGetProjectStatusOptionsQuery } from '@/src/store/features/ppm/projects-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { Button, Flex, Segmented, Skeleton, theme } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { FC, RefObject } from 'react'
import { LifecycleCategory } from '@/src/components/types'
import { getLifecycleCategoryStatusSurface } from '@/src/utils'
import { DashboardScope, ROLE_OPTIONS } from './dashboard-model'
import styles from '../projects-dashboard.module.css'

export interface ScopeBarProps {
  scope: DashboardScope
  onScopeChange: (scope: DashboardScope) => void
  /** Hides the Me option — an unlinked account has no projects of its own. */
  hasLinkedEmployee: boolean
  selectedRoles: number[]
  onRoleChange: (roles: number[]) => void
  selectedStatuses: number[]
  onStatusChange: (statuses: number[]) => void
  onReset: () => void
  onRefresh: () => void
  containerRef?: RefObject<HTMLDivElement | null>
}

const toggle = (values: number[], value: number) =>
  values.includes(value)
    ? values.filter((v) => v !== value)
    : [...values, value]

const ScopeBar: FC<ScopeBarProps> = ({
  scope,
  onScopeChange,
  hasLinkedEmployee,
  selectedRoles,
  onRoleChange,
  selectedStatuses,
  onStatusChange,
  onReset,
  onRefresh,
  containerRef,
}) => {
  const { data: statusOptions, isLoading } = useGetProjectStatusOptionsQuery()
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(false, {
    skip: scope.kind !== 'person',
  })
  const { token } = theme.useToken()

  if (isLoading) {
    return (
      <div ref={containerRef} className={styles.scopeBar}>
        <Skeleton.Input active size="small" style={{ width: 300 }} />
      </div>
    )
  }

  const scopeOptions = [
    ...(hasLinkedEmployee ? [{ label: 'Me', value: 'me' }] : []),
    { label: 'Person', value: 'person' },
  ]

  return (
    <div ref={containerRef} className={styles.scopeBar}>
      <Flex align="center" gap={16} wrap>
        <Flex gap={8} align="center" wrap>
          <span className={styles.scopeLabel}>Scope</span>
          <Segmented
            size="small"
            options={scopeOptions}
            value={scope.kind}
            onChange={(value) =>
              onScopeChange(
                value === 'me'
                  ? { kind: 'me' }
                  : { kind: 'person', employeeId: null },
              )
            }
          />
          {scope.kind === 'person' && (
            <div className={styles.employeeSelect}>
              <EmployeeSelect
                employees={employeeOptions ?? []}
                placeholder="Choose an employee"
                value={scope.employeeId ?? undefined}
                onChange={(value) =>
                  onScopeChange({
                    kind: 'person',
                    employeeId: typeof value === 'string' ? value : null,
                  })
                }
              />
            </div>
          )}
        </Flex>

        <div className={styles.scopeDivider} />

        <Flex gap={2} wrap align="center">
          <span className={styles.scopeLabel}>Role</span>
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
                onClick={() => onRoleChange(toggle(selectedRoles, role.value))}
              >
                {role.label}
              </Button>
            )
          })}
        </Flex>

        <div className={styles.scopeDivider} />

        <Flex gap={2} wrap align="center">
          <span className={styles.scopeLabel}>Status</span>
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
                onClick={() =>
                  onStatusChange(toggle(selectedStatuses, status.value))
                }
              >
                {status.label}
              </Button>
            )
          })}
        </Flex>

        <Flex gap={2} style={{ marginLeft: 'auto' }}>
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

export default ScopeBar
