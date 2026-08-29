'use client'

import { LabeledContent } from '@/src/components/common/content'
import { ScoringModelDetailsDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface ScoringModelFactsProps {
  scoringModel: ScoringModelDetailsDto
}

/**
 * A scoring model's stable facts, for the details panel.
 *
 * These were the "Details" tab, which held two lines of text and opened by
 * default — so the record's richest content sat one click behind its thinnest.
 * As facts they are visible alongside whichever section is open instead.
 */
const ScoringModelFacts = ({ scoringModel }: ScoringModelFactsProps) => (
  <Flex vertical gap={10}>
    <LabeledContent label="Key">{scoringModel.key}</LabeledContent>
    <LabeledContent label="State">{scoringModel.state?.name}</LabeledContent>
    {scoringModel.description && (
      <LabeledContent label="Description">
        {scoringModel.description}
      </LabeledContent>
    )}
  </Flex>
)

export default ScoringModelFacts
