'use client'

import { ClearOutlined } from '@ant-design/icons'
import { Button, Card, Flex, Select, Skeleton, Space } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { BaseOptionType } from 'antd/es/select'
import { FC } from 'react'
import styles from './ppm-filter-bar.module.css'

export interface PpmFilterBarProps {
  statusOptions: { value: number; label: string }[] | undefined
  selectedStatuses: number[]
  onStatusChange: (statuses: number[]) => void
  portfolioOptions?: BaseOptionType[] | undefined
  selectedPortfolioId?: string | null
  onPortfolioChange?: (portfolioId: string | null) => void
  selectedRole?: string | null
  onRoleChange?: (role: string | null) => void
  showRoleFilter?: boolean
  onReset?: () => void
  loading?: boolean
}

const ROLE_OPTIONS: BaseOptionType[] = [
  { label: 'All', value: 'all' },
  { label: 'Sponsor', value: '1' },
  { label: 'Owner', value: '2' },
  { label: 'PM', value: '3' },
  { label: 'Member', value: '4' },
  { label: 'Task Assignee', value: '5' },
]

const hasPortfolioFilter = (
  props: PpmFilterBarProps,
): props is PpmFilterBarProps &
  Required<
    Pick<PpmFilterBarProps, 'portfolioOptions' | 'onPortfolioChange'>
  > => {
  return (
    props.onPortfolioChange !== undefined &&
    props.portfolioOptions !== undefined
  )
}

const hasRoleFilter = (
  props: PpmFilterBarProps,
): props is PpmFilterBarProps &
  Required<Pick<PpmFilterBarProps, 'onRoleChange'>> => {
  return props.showRoleFilter === true && props.onRoleChange !== undefined
}

const PpmFilterBar: FC<PpmFilterBarProps> = (props) => {
  const { statusOptions, selectedStatuses, onStatusChange, loading } = props

  if (loading) {
    return (
      <Card size="small" className={styles.filterCard}>
        <Skeleton.Input active size="small" className={styles.skeleton} />
      </Card>
    )
  }

  return (
    <div className={styles.filterCard}>
      <Flex align="center" gap={16} wrap>
        {hasPortfolioFilter(props) && (
          <Space size="small" align="center">
            <span className={styles.filterLabel}>Portfolio:</span>
            <Select
              placeholder="All Portfolios"
              size="small"
              allowClear
              value={props.selectedPortfolioId}
              onChange={(value) => props.onPortfolioChange(value ?? null)}
              options={props.portfolioOptions}
              className={styles.portfolioSelect}
              popupMatchSelectWidth={false}
            />
          </Space>
        )}

        <Space size="small" align="center">
          <span className={styles.filterLabel}>Status:</span>
          <Flex gap={2}>
            {statusOptions?.map((status) => {
              const isSelected = selectedStatuses.includes(status.value)
              return (
                <Button
                  key={status.value}
                  size="small"
                  className={styles.statusButton}
                  color={isSelected ? 'primary' : 'default'}
                  variant="outlined"
                  onClick={() => {
                    const next = isSelected
                      ? selectedStatuses.filter((s) => s !== status.value)
                      : [...selectedStatuses, status.value]
                    onStatusChange(next)
                  }}
                >
                  {status.label}
                </Button>
              )
            })}
          </Flex>
        </Space>

        {hasRoleFilter(props) && (
          <Space size="small" align="center">
            <span className={styles.filterLabel}>My Role:</span>
            <Select
              placeholder="No Filter"
              size="small"
              allowClear
              value={props.selectedRole}
              onChange={(value) => props.onRoleChange(value ?? null)}
              options={ROLE_OPTIONS}
              className={styles.roleSelect}
              popupMatchSelectWidth={false}
            />
          </Space>
        )}

        {props.onReset && (
          <WaydTooltip title="Reset Filters">
            <Button
              type="text"
              shape="circle"
              icon={<ClearOutlined />}
              aria-label="Reset filters"
              onClick={props.onReset}
            />
          </WaydTooltip>
        )}
      </Flex>
    </div>
  )
}

export default PpmFilterBar
