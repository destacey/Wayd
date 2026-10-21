'use client'

import {
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
} from '@/src/services/wayd-api'
import { Alert, Flex, Segmented, Skeleton, Typography } from 'antd'
import dynamic from 'next/dynamic'
import StatusHistoryTimeline from './status-history-timeline'
import { buildRecordPath } from './workflow-diagram-layout'

const { Title } = Typography

// Loaded on demand: React Flow and its stylesheet are only needed once the reader asks for the diagram.
const WaydWorkflowDiagram = dynamic(() => import('./wayd-workflow-diagram'), {
  ssr: false,
  loading: () => <Skeleton active paragraph={{ rows: 6 }} />,
})

export type StatusHistoryView = 'timeline' | 'diagram'

export interface RecordStatusHistoryProps {
  transitions: StatusTransitionDto[] | undefined
  isLoading: boolean
  /** What to say when the record has no recorded changes. Names the record type. */
  emptyDescription?: string
  /** Which view is shown. Held by the caller, which fetches the workflow only for the diagram. */
  view: StatusHistoryView
  onViewChange: (view: StatusHistoryView) => void
  /** The workflow the record is on now; needed only for the diagram. */
  workflow: StatusWorkflowDetailsDto | undefined
  isWorkflowLoading: boolean
  isWorkflowError: boolean
}

/**
 * A record's status history, as a timeline or drawn on the workflow it moves through.
 *
 * The diagram can draw only what happened under the record's current workflow. Anything earlier — and
 * the switch onto this workflow — is listed beneath it instead, so no change goes unshown.
 */
const RecordStatusHistory = ({
  transitions,
  isLoading,
  emptyDescription,
  view,
  onViewChange,
  workflow,
  isWorkflowLoading,
  isWorkflowError,
}: RecordStatusHistoryProps) => {
  const renderDiagram = () => {
    if (isLoading || isWorkflowLoading) {
      return <Skeleton active paragraph={{ rows: 6 }} />
    }

    if (isWorkflowError || !workflow) {
      return (
        <Alert
          type="error"
          showIcon
          title="The workflow could not be loaded"
          description="The timeline still shows every status change."
        />
      )
    }

    const path = buildRecordPath(workflow, transitions ?? [])

    return (
      <Flex vertical gap={16}>
        <WaydWorkflowDiagram workflow={workflow} path={path} />
        {path.notDrawn.length > 0 && (
          <Flex vertical gap={8}>
            <Title level={5} style={{ margin: 0 }}>
              Under an earlier workflow
            </Title>
            <StatusHistoryTimeline
              transitions={path.notDrawn}
              isLoading={false}
            />
          </Flex>
        )}
      </Flex>
    )
  }

  return (
    <Flex vertical gap={16}>
      <Segmented<StatusHistoryView>
        aria-label="Status history view"
        value={view}
        onChange={onViewChange}
        options={[
          { label: 'Timeline', value: 'timeline' },
          { label: 'Diagram', value: 'diagram' },
        ]}
        style={{ alignSelf: 'flex-start' }}
      />
      {view === 'diagram' ? (
        renderDiagram()
      ) : (
        <StatusHistoryTimeline
          transitions={transitions}
          isLoading={isLoading}
          emptyDescription={emptyDescription}
        />
      )}
    </Flex>
  )
}

export default RecordStatusHistory
