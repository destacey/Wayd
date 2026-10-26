'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import {
  IntakeRequestDetailsDto,
  NeedByBasisDto,
  NeedByBasisFamilyDto,
} from '@/src/services/wayd-api'
import { Alert, Flex, Typography } from 'antd'
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

const Block: FC<{ title: string; children: ReactNode }> = ({
  title,
  children,
}) => (
  <Flex vertical gap={4}>
    <Text strong style={{ fontSize: 14 }}>
      {title}
    </Text>
    {children}
  </Flex>
)

/**
 * The ask itself. When the reader may see only the summary, the section says so plainly instead of
 * showing empty fields: the detail exists, and is kept to the desk.
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
      {substance && (
        <>
          <Block title="Description">
            <MarkdownRenderer markdown={substance.description} />
          </Block>
          <Block title="Expected Benefits">
            {substance.expectedBenefits ? (
              <MarkdownRenderer markdown={substance.expectedBenefits} />
            ) : (
              <Text type="secondary">None given</Text>
            )}
          </Block>
        </>
      )}
      <Block title="Need By">
        {summary.needBy ? (
          <RequestNeedBy
            needBy={summary.needBy}
            bases={needByBases}
            families={needByFamilies}
          />
        ) : (
          <Text type="secondary">No date</Text>
        )}
      </Block>
    </Flex>
  )
}

export default IntakeRequestDetails
