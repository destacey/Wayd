'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { formatDateTime } from '@/src/components/common/wayd-grid'
import { DeploymentDto } from '@/src/services/wayd-api'
import { Flex, Tag, Tooltip } from 'antd'
import Link from 'next/link'

export interface DeploymentFactsProps {
  deployment: DeploymentDto
}

/**
 * A deployment's stable facts, for the details panel.
 *
 * The category shown is the one frozen on the deployment, which can legitimately differ from the
 * environment's category today — reclassifying an environment must not rewrite what past deployments
 * counted as. The tooltip says so, since the two disagreeing otherwise looks like a bug.
 */
const DeploymentFacts = ({ deployment }: DeploymentFactsProps) => (
  <>
    <Flex vertical gap={10}>
      <LabeledContent label="Deployed">
        {deployment.version ? (
          <Link href={`/delivery/releases/${deployment.version.key}`}>
            {deployment.version.name}
          </Link>
        ) : deployment.package ? (
          <Link href={`/delivery/release-packages/${deployment.package.key}`}>
            {deployment.package.name}
          </Link>
        ) : null}
      </LabeledContent>
      <LabeledContent label="Kind">
        {deployment.version ? 'Release' : 'Package'}
      </LabeledContent>
      <LabeledContent label="Environment">
        {deployment.environment.name}
      </LabeledContent>
      <LabeledContent label="Category">
        <Tooltip title="The environment's category when this deployment ran. Reclassifying an environment does not change what past deployments counted as.">
          <span>{deployment.environmentCategory}</span>
        </Tooltip>
      </LabeledContent>
      {deployment.artifactId && (
        <LabeledContent label="Artifact">{deployment.artifactId}</LabeledContent>
      )}
      {deployment.isChangeFailure && (
        <LabeledContent label="Change Failure">
          <Tooltip title="Counts against change failure rate: it failed or was rolled back in production.">
            <Tag color="red">Yes</Tag>
          </Tooltip>
        </LabeledContent>
      )}
    </Flex>

    <RecordFactsGroup label="Timing">
      <Flex vertical gap={10}>
        <LabeledContent label="Started">
          {formatDateTime(deployment.startedAt)}
        </LabeledContent>
        <LabeledContent label="Completed">
          {formatDateTime(deployment.completedAt) || 'In flight'}
        </LabeledContent>
      </Flex>
    </RecordFactsGroup>
  </>
)

export default DeploymentFacts
