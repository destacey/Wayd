'use client'

import { LabeledContent } from '@/src/components/common/content'
import { useGetEmployeeByIdQuery } from '@/src/store/features/organizations/employee-api'
import { Flex, Popover, Skeleton, Typography } from 'antd'
import Link from 'next/link'
import { ReactNode } from 'react'
import PersonAvatar from './person-avatar'
import styles from './person-popover.module.css'

const { Text } = Typography

export interface PersonPopoverProps {
  /** The person's display name. Shown while the record loads, and if it fails. */
  name: string
  /**
   * The employee record to open. Absent for an account with no employee link —
   * the trigger then renders on its own, with no popover.
   */
  employeeId?: string
  /** Colour seed for the default avatar. See `PersonAvatar`. */
  colorKey?: string
  /**
   * Hover text, when the name alone is not the whole story — a project's team
   * list adds each person's roles. Defaults to the name.
   */
  tooltip?: string
  /** What opens the card. Defaults to the person's avatar. */
  children?: ReactNode
}

const Body = ({ employeeId, name }: { employeeId: string; name: string }) => {
  // RTK Query caches by args, so hovering the same person twice — or several
  // people who share a manager — costs one request.
  const { data: employee, isLoading, isError } = useGetEmployeeByIdQuery(employeeId)

  if (isLoading) {
    return (
      <div className={styles.card}>
        <Skeleton active paragraph={{ rows: 3 }} title={false} />
      </div>
    )
  }

  if (isError || !employee) {
    return (
      <div className={styles.card}>
        <Text strong>{name}</Text>
        <div>
          <Text type="secondary">Details are unavailable.</Text>
        </div>
      </div>
    )
  }

  return (
    <Flex vertical gap={12} className={styles.card}>
      <Flex align="center" gap={10}>
        <PersonAvatar
          name={employee.displayName}
          colorKey={employee.id}
          size={40}
          showTooltip={false}
        />
        <Flex vertical>
          <Link href={`/organizations/employees/${employee.key}`}>
            {employee.displayName}
          </Link>
          {employee.jobTitle && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              {employee.jobTitle}
            </Text>
          )}
        </Flex>
      </Flex>

      <Flex vertical gap={8}>
        {employee.email && (
          <LabeledContent label="Email">
            <Link href={`mailto:${employee.email}`}>{employee.email}</Link>
          </LabeledContent>
        )}

        {employee.department && (
          <LabeledContent label="Department">
            {employee.department}
          </LabeledContent>
        )}

        {employee.officeLocation && (
          <LabeledContent label="Office">
            {employee.officeLocation}
          </LabeledContent>
        )}

        {employee.manager && (
          <LabeledContent label="Manager">
            <Link href={`/organizations/employees/${employee.manager.key}`}>
              {employee.manager.name}
            </Link>
          </LabeledContent>
        )}
      </Flex>
    </Flex>
  )
}

/**
 * A card of basic details about a person, opened from their avatar.
 *
 * Answers "who is this?" without leaving the page — the common question when a
 * name or a set of initials appears beside work you are looking at. The record
 * is fetched only when the card opens, so a board full of avatars costs
 * nothing until one is clicked.
 */
const PersonPopover = ({
  name,
  employeeId,
  colorKey,
  tooltip,
  children,
}: PersonPopoverProps) => {
  const trigger = children ?? (
    <PersonAvatar name={name} colorKey={colorKey} tooltip={tooltip} />
  )

  if (!employeeId) return <>{trigger}</>

  return (
    <Popover
      trigger="click"
      placement="bottomLeft"
      content={<Body employeeId={employeeId} name={name} />}
    >
      {/* The click stops here: these avatars often sit on a container that
          navigates, and opening the card must not also trigger it. */}
      <span className={styles.trigger} onClick={(e) => e.stopPropagation()}>
        {trigger}
      </span>
    </Popover>
  )
}

export default PersonPopover
