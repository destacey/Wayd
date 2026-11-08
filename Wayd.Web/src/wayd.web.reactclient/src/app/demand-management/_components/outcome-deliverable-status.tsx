import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import {
  ProjectDeliverableStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'
import { FC } from 'react'

const aliasLabel: Partial<Record<ProjectDeliverableStatusAlias, string>> = {
  [ProjectDeliverableStatusAlias.NotStarted]: 'Not Started',
  [ProjectDeliverableStatusAlias.InProgress]: 'In Progress',
  [ProjectDeliverableStatusAlias.Accepted]: 'Accepted',
  [ProjectDeliverableStatusAlias.Descoped]: 'Descoped',
}

const categoryLabel: Record<StatusCategory, string> = {
  [StatusCategory.Proposed]: 'Not started',
  [StatusCategory.Active]: 'In progress',
  [StatusCategory.Done]: 'Done',
  [StatusCategory.Removed]: 'Removed',
}

/**
 * How a mapped deliverable's status reads on a request: its meaning, or for a company's own status with none,
 * its category. Demand Management keeps no status names, which a workflow can rename without telling it.
 */
export const outcomeDeliverableStatusLabel = (
  alias: ProjectDeliverableStatusAlias,
  category: StatusCategory,
) => aliasLabel[alias] ?? categoryLabel[category]

export interface OutcomeDeliverableStatusProps {
  alias: ProjectDeliverableStatusAlias
  category: StatusCategory
}

/** A mapped deliverable's status, coloured by its category. */
const OutcomeDeliverableStatus: FC<OutcomeDeliverableStatusProps> = ({
  alias,
  category,
}) => (
  <WorkflowStatusTag
    name={outcomeDeliverableStatusLabel(alias, category)}
    category={category}
  />
)

export default OutcomeDeliverableStatus
