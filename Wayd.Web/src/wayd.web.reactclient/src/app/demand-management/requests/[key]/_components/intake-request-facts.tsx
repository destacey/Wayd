'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import { Typography } from 'antd'
import NextLink from 'next/link'
import { FC } from 'react'
import { deskHref } from '../../../_components/desk-status'
import EmployeeName from '../../../_components/employee-name'

const { Text, Link } = Typography

export interface IntakeRequestFactsProps {
  request: IntakeRequestDetailsDto
}

/**
 * The request's stable facts. People link to their pages only for a reader who can open them. Who
 * submitted it and where it came from are substance, so they appear only with it.
 */
const IntakeRequestFacts: FC<IntakeRequestFactsProps> = ({ request }) => {
  const { summary, substance } = request

  return (
    <>
      <RecordFactsGroup label="Request">
        <LabeledContent label="Desk">
          <NextLink href={deskHref(summary.desk.key)}>
            {summary.desk.name}
          </NextLink>
        </LabeledContent>
        <LabeledContent label="Channel">{summary.channel.name}</LabeledContent>
      </RecordFactsGroup>
      <RecordFactsGroup label="People">
        <LabeledContent label="Requester">
          <EmployeeName employee={summary.requester} />
        </LabeledContent>
        {substance && (
          <LabeledContent label="Submitted By">
            <EmployeeName employee={substance.submittedBy} />
          </LabeledContent>
        )}
        <LabeledContent label="Assignee">
          {summary.assignee ? (
            <EmployeeName employee={summary.assignee} />
          ) : (
            <Text type="secondary">Not assigned</Text>
          )}
        </LabeledContent>
      </RecordFactsGroup>
      {substance?.origin && (
        <RecordFactsGroup label="Origin">
          <LabeledContent label="Source System">
            {substance.origin.sourceSystem}
          </LabeledContent>
          <LabeledContent label="External Id">
            {substance.origin.url ? (
              <Link
                href={substance.origin.url}
                target="_blank"
                rel="noopener noreferrer"
              >
                {substance.origin.externalId}
              </Link>
            ) : (
              substance.origin.externalId
            )}
          </LabeledContent>
        </RecordFactsGroup>
      )}
    </>
  )
}

export default IntakeRequestFacts
