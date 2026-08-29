'use client'

import {
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { projectHelpText } from '@/src/app/ppm/projects/_components/project-help-text'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { Card, Flex, Typography } from 'antd'

const { Text } = Typography

const NOT_PROVIDED = 'Not provided'

export interface ProjectDefinitionProps {
  project: ProjectDetailsDto
}

/**
 * Why the project exists: what it is, the case for it, and what it should
 * deliver.
 *
 * Every field renders whether or not it is filled in — an unwritten business
 * case is a gap worth seeing on a project awaiting approval, not something to
 * hide.
 */
const ProjectDefinition = ({ project }: ProjectDefinitionProps) => (
  <Card size="small">
    <Flex vertical gap={10}>
      <LabeledContent label="Description" tooltip={projectHelpText.description}>
        {project.description ? (
          <ExpandableContent lines={8}>
            <MarkdownRenderer markdown={project.description} />
          </ExpandableContent>
        ) : (
          <Text type="secondary">{NOT_PROVIDED}</Text>
        )}
      </LabeledContent>

      <LabeledContent
        label="Business Case"
        tooltip={projectHelpText.businessCase}
      >
        {project.businessCase ? (
          <ExpandableContent lines={8}>
            <MarkdownRenderer markdown={project.businessCase} />
          </ExpandableContent>
        ) : (
          <Text type="secondary">{NOT_PROVIDED}</Text>
        )}
      </LabeledContent>

      <LabeledContent
        label="Expected Benefits"
        tooltip={projectHelpText.expectedBenefits}
      >
        {project.expectedBenefits ? (
          <ExpandableContent lines={8}>
            <MarkdownRenderer markdown={project.expectedBenefits} />
          </ExpandableContent>
        ) : (
          <Text type="secondary">{NOT_PROVIDED}</Text>
        )}
      </LabeledContent>
    </Flex>
  </Card>
)

export default ProjectDefinition
