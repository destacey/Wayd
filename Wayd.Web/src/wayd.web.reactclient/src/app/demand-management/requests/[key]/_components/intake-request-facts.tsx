'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import { Typography } from 'antd'
import { FC } from 'react'

const { Text, Link } = Typography

export interface IntakeRequestFactsProps {
  request: IntakeRequestDetailsDto
}

/**
 * The request's stable facts. People are names rather than links, since most requesters cannot open
 * employee pages. Who submitted it and where it came from are substance, so they appear only with it.
 */
const IntakeRequestFacts: FC<IntakeRequestFactsProps> = ({ request }) => {
  const { summary, substance } = request

  return (
    <>
      <RecordFactsGroup label="Request">
        <LabeledContent label="Desk">{summary.desk.name}</LabeledContent>
        <LabeledContent label="Channel">{summary.channel.name}</LabeledContent>
      </RecordFactsGroup>
      <RecordFactsGroup label="People">
        <LabeledContent label="Requester">
          {summary.requester.name}
        </LabeledContent>
        {substance && (
          <LabeledContent label="Submitted By">
            {substance.submittedBy.name}
          </LabeledContent>
        )}
        <LabeledContent label="Assigned Reviewer">
          {summary.assignedReviewer?.name ?? (
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
