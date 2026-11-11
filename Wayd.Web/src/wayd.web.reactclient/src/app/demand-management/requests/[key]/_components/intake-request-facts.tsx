'use client'

import { NOT_RECORDED_CHANNEL_LABEL } from '../../../_components/channel'
import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import { Typography } from 'antd'
import NextLink from 'next/link'
import { FC } from 'react'
import DeliverySignalTag, {
  shownDeliveryState,
} from '../../../_components/delivery-signal-tag'
import { deskHref } from '../../../_components/desk-status'
import EmployeeName from '../../../_components/employee-name'
import { EstimateValue } from '../../../_components/estimate-value'

const { Text, Link } = Typography

export interface IntakeRequestFactsProps {
  request: IntakeRequestDetailsDto
}

/**
 * The request's stable facts. People link to their pages only for a reader who can open them. Who
 * submitted it, where it came from and its estimate are substance, so they appear only with it.
 */
const IntakeRequestFacts: FC<IntakeRequestFactsProps> = ({ request }) => {
  const { summary, substance } = request
  const deliveryState = shownDeliveryState(
    summary.status.category,
    request.deliveryState,
  )

  return (
    <>
      <RecordFactsGroup label="Request">
        <LabeledContent label="Desk">
          <NextLink href={deskHref(summary.desk.key)}>
            {summary.desk.name}
          </NextLink>
        </LabeledContent>
        <LabeledContent label="Channel">
          {summary.channel?.name ?? NOT_RECORDED_CHANNEL_LABEL}
        </LabeledContent>
        {deliveryState && (
          <LabeledContent label="Delivery">
            <DeliverySignalTag signal={deliveryState} />
          </LabeledContent>
        )}
        {/* Shown once there is an estimate, or to the team that can give one; a desk that does not
            estimate would otherwise read "Not estimated" on every request. */}
        {substance &&
          (substance.estimate || request.estimateValues.length > 0) && (
            <LabeledContent label="Estimate">
              {substance.estimate ? (
                <EstimateValue estimate={substance.estimate} />
              ) : (
                <Text type="secondary">Not estimated</Text>
              )}
            </LabeledContent>
          )}
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
