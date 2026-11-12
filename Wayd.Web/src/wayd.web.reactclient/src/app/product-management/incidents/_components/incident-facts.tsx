'use client'

import { LabeledContent } from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import IncidentTeamsChat from '@/src/app/product-management/incidents/_components/incident-teams-chat'
import {
  RecordFactsGroup,
  RecordLinkList,
  RecordPersonLink,
} from '@/src/components/common/record'
import { IncidentDto } from '@/src/services/wayd-api'
import { formatInstantInZone } from '@/src/utils'
import { Divider, Flex, Typography } from 'antd'
import type { BaseOptionType } from 'antd/es/select'

const { Text } = Typography

export interface IncidentFactsProps {
  incident: IncidentDto
  /** The viewer's zone, so the timeline reads as local time. */
  viewerZone: string
  canUpdate: boolean
  employees: BaseOptionType[]
}

/**
 * An incident's stable facts, for the details panel.
 *
 * When it was noticed and who is running it, then the products and releases it
 * points at, then the same links card a PI objective uses. Symptoms and the
 * discussion are sections of the record, not labels beside it.
 */
const IncidentFacts = ({
  incident,
  viewerZone,
  canUpdate,
  employees,
}: IncidentFactsProps) => {
  const products = incident.products.map((product) => ({
    id: product.id,
    name: product.name,
    href: `/product-management/products/${product.key}`,
  }))
  const releases = incident.releases.map((release) => ({
    id: release.id,
    name: release.name,
    href: `/product-management/releases/${release.key}`,
  }))

  return (
    <>
      <Flex vertical gap={10}>
        <LabeledContent label="Noticed">
          {formatInstantInZone(incident.noticedAt, viewerZone)}
        </LabeledContent>
        <LabeledContent label="Response started">
          {incident.startedAt
            ? formatInstantInZone(incident.startedAt, viewerZone)
            : '—'}
        </LabeledContent>
        <LabeledContent label="Recovered">
          {incident.recoveredAt
            ? formatInstantInZone(incident.recoveredAt, viewerZone)
            : '—'}
        </LabeledContent>
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      <RecordFactsGroup label="Relationships">
        <LabeledContent label="Submitted by">
          <RecordPersonLink
            name={incident.submittedBy.name}
            href={`/organizations/employees/${incident.submittedBy.key}`}
          />
        </LabeledContent>
        <LabeledContent label="Reported by">
          <RecordPersonLink
            name={incident.reportedBy.name}
            href={`/organizations/employees/${incident.reportedBy.key}`}
          />
        </LabeledContent>
        <LabeledContent label="Commander">
          {incident.commander ? (
            <RecordPersonLink
              name={incident.commander.name}
              href={`/organizations/employees/${incident.commander.key}`}
            />
          ) : (
            <Text type="secondary">None assigned</Text>
          )}
        </LabeledContent>
        <LabeledContent label="Products">
          {products.length > 0 ? (
            <RecordLinkList items={products} />
          ) : (
            <Text type="secondary">None linked</Text>
          )}
        </LabeledContent>
        <LabeledContent label="Releases">
          {releases.length > 0 ? (
            <RecordLinkList items={releases} />
          ) : (
            <Text type="secondary">None linked</Text>
          )}
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <IncidentTeamsChat
        incident={incident}
        canUpdate={canUpdate}
        employees={employees}
      />

      <LinksCard objectId={incident.id} width="100%" />
    </>
  )
}

export default IncidentFacts
