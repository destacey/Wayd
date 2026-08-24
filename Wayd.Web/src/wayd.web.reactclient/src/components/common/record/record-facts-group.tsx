'use client'

import { Avatar, Flex, Typography } from 'antd'
import Link from 'next/link'
import { ReactNode } from 'react'
import { getAvatarColor, getInitials } from '@/src/utils'
import styles from './record-layout.module.css'

const { Text } = Typography

export interface RecordFactsGroupProps {
  /** Section label, e.g. "Record" or "Relationships". */
  label: string
  children: ReactNode
}

/**
 * A labelled group of facts inside the details panel.
 *
 * The panel carries two kinds of thing — what the record *is* and what it is
 * *connected to* — and they read very differently. Grouping keeps a list of
 * people from looking like another field.
 */
export const RecordFactsGroup = ({ label, children }: RecordFactsGroupProps) => (
  <Flex vertical gap={10}>
    <Text type="secondary" className={styles.factsGroupLabel}>
      {label}
    </Text>
    {children}
  </Flex>
)

export interface RecordLinkListProps {
  items: { id: string; name: string; href: string }[]
}

/**
 * A list of related records — teams, programs, anything that is not a person.
 *
 * No avatar: initials belong to people, and on a record they both read as a
 * person and collide ("Identity Platform" and "Infrastructure Program" are
 * both IP).
 */
export const RecordLinkList = ({ items }: RecordLinkListProps) => (
  <Flex vertical gap={4}>
    {items.map((item) => (
      <Link key={item.id} href={item.href}>
        {item.name}
      </Link>
    ))}
  </Flex>
)

export interface RecordPersonLinkProps {
  name: string
  href: string
}

/**
 * A person in the relationships group: small avatar plus a link.
 *
 * Deliberately smaller than the identity bar's avatar — these are pointers to
 * other records, not the subject of this one.
 */
export const RecordPersonLink = ({ name, href }: RecordPersonLinkProps) => (
  <Flex align="center" gap={8}>
    <Avatar
      size={22}
      style={{
        flexShrink: 0,
        fontSize: 10,
        backgroundColor: getAvatarColor(name),
      }}
    >
      {getInitials(name)}
    </Avatar>
    <Link href={href}>{name}</Link>
  </Flex>
)
