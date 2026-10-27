'use client'

import {
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import {
  IntakeRequestDetailsDto,
  NeedByBasisDto,
  NeedByBasisFamilyDto,
} from '@/src/services/wayd-api'
import { Alert, Card, Flex, Typography } from 'antd'
import { FC, ReactNode } from 'react'
import { RequestNeedBy } from '../../../_components/need-by'

const { Text } = Typography

export interface IntakeRequestDetailsProps {
  request: IntakeRequestDetailsDto
  needByBases: NeedByBasisDto[] | undefined
  needByFamilies: NeedByBasisFamilyDto[] | undefined
  /** What the page has to say about the request before its detail, such as where a duplicate leads. */
  notices?: ReactNode
}

/**
 * The ask itself, on a card like a project's definition, with what the page has to say about the request
 * above it. When the reader may see only the summary, the section says so plainly instead of showing empty
 * fields: the detail exists, and is kept to the desk.
 */
const IntakeRequestDetails: FC<IntakeRequestDetailsProps> = ({
  request,
  needByBases,
  needByFamilies,
  notices,
}) => {
  const { summary, substance } = request

  return (
    <Flex vertical gap={16}>
      {notices}
      {substance?.statusReason && (
        <Alert
          type="info"
          showIcon
          title={summary.status.name}
          description={substance.statusReason}
        />
      )}
      {!substance && (
        <Alert
          type="info"
          showIcon
          title={`The detail of this request is limited to the ${summary.desk.name} desk.`}
          description="You can see what is being asked for and where it stands. Its description, expected benefits and the people involved are visible to the desk's reviewers and the request's own people."
        />
      )}
      <Card size="small">
        <Flex vertical gap={10}>
          {substance && (
            <>
              <LabeledContent label="Description">
                <ExpandableContent lines={8}>
                  <MarkdownRenderer markdown={substance.description} />
                </ExpandableContent>
              </LabeledContent>
              <LabeledContent label="Expected Benefits">
                {substance.expectedBenefits ? (
                  <ExpandableContent lines={8}>
                    <MarkdownRenderer markdown={substance.expectedBenefits} />
                  </ExpandableContent>
                ) : (
                  <Text type="secondary">None given</Text>
                )}
              </LabeledContent>
            </>
          )}
          <LabeledContent label="Need By">
            {summary.needBy ? (
              <RequestNeedBy
                needBy={summary.needBy}
                bases={needByBases}
                families={needByFamilies}
              />
            ) : (
              <Text type="secondary">No date</Text>
            )}
          </LabeledContent>
        </Flex>
      </Card>
    </Flex>
  )
}

export default IntakeRequestDetails
