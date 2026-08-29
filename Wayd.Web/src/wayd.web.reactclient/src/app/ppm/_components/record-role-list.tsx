'use client'

import { PersonAvatar, PersonPopover } from '@/src/components/common'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { EmployeeNavigationDto } from '@/src/services/wayd-api'
import { Flex, Typography } from 'antd'
import Link from 'next/link'

const { Text } = Typography

/** Matches `RecordPersonLink`, so people read the same across facts panels. */
const AVATAR_SIZE = 22

export interface RecordRoleListProps {
  people: EmployeeNavigationDto[]
  /** Shown when nobody holds the role — "No owner assigned" and the like. */
  emptyText: string
}

/**
 * The people holding one PPM role.
 *
 * Two targets, answering two different questions: the avatar opens their card
 * — who is this? — and the name goes to their record. Splitting them means the
 * quick question costs no navigation, which is the common one when scanning a
 * list of names beside work you are already reading.
 *
 * Sorted here rather than by the caller: every role list on every PPM record
 * wants the same order, and doing it at the point of render means a caller
 * cannot forget.
 */
const RecordRoleList = ({ people, emptyText }: RecordRoleListProps) => {
  if (people.length === 0) {
    return <Text type="secondary">{emptyText}</Text>
  }

  const sorted = [...people].sort((a, b) =>
    caseInsensitiveCompare(a.name, b.name),
  )

  return (
    <Flex vertical gap={6}>
      {sorted.map((person) => (
        <Flex key={person.id} align="center" gap={8}>
          <PersonPopover name={person.name} employeeId={person.id}>
            <PersonAvatar
              name={person.name}
              colorKey={person.id}
              size={AVATAR_SIZE}
              // The name is right there; a tooltip repeating it would only
              // cover the card the avatar opens.
              showTooltip={false}
              style={{ flexShrink: 0, fontSize: 10 }}
            />
          </PersonPopover>
          <Link href={`/organizations/employees/${person.key}`}>
            {person.name}
          </Link>
        </Flex>
      ))}
    </Flex>
  )
}

export default RecordRoleList
