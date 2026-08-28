'use client'

import { LabeledContent } from '@/src/components/common/content'
import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'

export interface ExpenditureCategoryPanelProps {
  expenditureCategory: ExpenditureCategoryDetailsDto | undefined
}

/**
 * An expenditure category's fields, for the config list's detail panel.
 *
 * `LabeledContent` rather than `Descriptions`: the panel is a narrow column,
 * where a label above its value reads better than a label beside it, and it
 * matches the record facts rail so the two panels feel like one pattern.
 */
const ExpenditureCategoryPanel = ({
  expenditureCategory,
}: ExpenditureCategoryPanelProps) => {
  if (!expenditureCategory) return null

  return (
    <Flex vertical gap={10}>
      <LabeledContent label="State">
        {expenditureCategory.state.name}
      </LabeledContent>
      <LabeledContent label="Capitalizable">
        {expenditureCategory.isCapitalizable ? 'Yes' : 'No'}
      </LabeledContent>
      <LabeledContent label="Requires Depreciation">
        {expenditureCategory.requiresDepreciation ? 'Yes' : 'No'}
      </LabeledContent>
      {expenditureCategory.accountingCode && (
        <LabeledContent label="Accounting Code">
          {expenditureCategory.accountingCode}
        </LabeledContent>
      )}
      {expenditureCategory.description && (
        <LabeledContent label="Description">
          {expenditureCategory.description}
        </LabeledContent>
      )}
    </Flex>
  )
}

export default ExpenditureCategoryPanel
