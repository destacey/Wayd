'use client'

import { MarkdownRenderer } from '@/src/components/common/markdown'
import RiskMatrix from '@/src/components/common/planning/risk-matrix'
import RiskRoam from '@/src/components/common/planning/risk-roam'
import { RiskDetailsDto } from '@/src/services/wayd-api'
import { Col, Flex, Row, Typography } from 'antd'

const { Text } = Typography

export interface RiskNarrativeProps {
  risk: RiskDetailsDto
}

const Section = ({
  label,
  markdown,
  empty,
}: {
  label: string
  markdown?: string
  empty: string
}) => (
  <Flex vertical gap={6}>
    <Text strong style={{ fontSize: 14 }}>
      {label}
    </Text>
    {markdown ? (
      <MarkdownRenderer markdown={markdown} />
    ) : (
      <Text type="secondary">{empty}</Text>
    )}
  </Flex>
)

/**
 * What the team decided, what the risk is, what is being done about it, and
 * how it is graded.
 *
 * The matrix sits here rather than in the details panel: it is the one part of
 * a risk worth reading before the prose, and at panel width it shrinks to a
 * thumbnail no one can read the axes of.
 */
const RiskNarrative = ({ risk }: RiskNarrativeProps) => (
  <Flex vertical gap="large">
    {/* Centred above everything: ROAM is the decision the team reached about
        this risk, and it frames how the description and response read. */}
    <Flex justify="center">
      <RiskRoam category={risk.category?.name} />
    </Flex>

    <Row gutter={[24, 24]}>
      <Col xs={24} lg={15} xxl={16}>
        <Flex vertical gap="large">
          <Section
            label="Description"
            markdown={risk.description}
            empty="No description provided."
          />
          <Section
            label="Response"
            markdown={risk.response}
            empty="No response recorded."
          />
        </Flex>
      </Col>

      <Col xs={24} lg={9} xxl={8}>
        <Flex vertical gap={6}>
          <Text strong style={{ fontSize: 14 }}>
            Evaluation
          </Text>
          <RiskMatrix
            impact={risk.impact?.name}
            likelihood={risk.likelihood?.name}
            exposure={risk.exposure?.name}
          />
        </Flex>
      </Col>
    </Row>
  </Flex>
)

export default RiskNarrative
