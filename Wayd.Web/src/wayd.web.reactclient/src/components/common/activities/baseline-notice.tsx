'use client'

import { Alert, Flex, Typography } from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'
import EntityLink from '@/src/components/common/entity-link'
import { useGetEmployeeByIdQuery } from '@/src/store/features/organizations/employee-api'

const { Text } = Typography

/** Payload fields every baseline carries alongside its creation event's, and this notice presents. */
export const BASELINE_RECORD_KEYS = ['recordCreatedOn', 'recordCreatedById']

interface BaselineRecordCreation {
  createdOn?: string
  createdById?: string
}

const readRecordCreation = (payload: string): BaselineRecordCreation => {
  try {
    const raw = JSON.parse(payload)
    return {
      createdOn:
        typeof raw?.recordCreatedOn === 'string'
          ? raw.recordCreatedOn
          : undefined,
      createdById:
        typeof raw?.recordCreatedById === 'string'
          ? raw.recordCreatedById
          : undefined,
    }
  } catch {
    return {}
  }
}

const CreatedBy: FC<{ employeeId: string }> = ({ employeeId }) => {
  const { data: employee } = useGetEmployeeByIdQuery(employeeId)
  if (!employee) return null

  return (
    <>
      {' by '}
      <EntityLink href={`/organizations/employees/${employee.key}`}>
        {employee.displayName}
      </EntityLink>
    </>
  )
}

export interface BaselineNoticeProps {
  payload: string
}

const BaselineNotice: FC<BaselineNoticeProps> = ({ payload }) => {
  const { createdOn, createdById } = readRecordCreation(payload)
  const created = createdOn ? dayjs(createdOn) : null

  return (
    <Alert
      type="info"
      showIcon
      title="Tracking started"
      description={
        <Flex vertical gap={4}>
          <span>
            This record existed before its activity was recorded. This entry
            shows how it looked when tracking began, not a change made to it.
          </span>
          {created?.isValid() && (
            <Text>
              Record created {created.format('MMM D, YYYY')}
              {createdById && <CreatedBy employeeId={createdById} />}
            </Text>
          )}
        </Flex>
      }
    />
  )
}

export default BaselineNotice
