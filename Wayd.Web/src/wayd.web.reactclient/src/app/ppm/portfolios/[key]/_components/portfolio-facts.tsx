'use client'

import {
  ExpandableContent,
  LabeledContent,
} from '@/src/components/common/content'
import LinksCard from '@/src/components/common/links/links-card'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RecordFactsGroup } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { ProjectPortfolioDetailsDto } from '@/src/services/wayd-api'
import { Divider, Flex } from 'antd'
import Link from 'next/link'
import RecordRoleList from '../../../_components/record-role-list'

export interface PortfolioFactsProps {
  portfolio: ProjectPortfolioDetailsDto
}

/**
 * A portfolio's stable facts, for the details panel.
 *
 * Its description and scoring model, then the people accountable for it. A
 * portfolio is the top of the hierarchy, so it carries no Relationships group.
 */
const PortfolioFacts = ({ portfolio }: PortfolioFactsProps) => {
  const { hasPermissionClaim } = useAuth()
  const canViewScoringModel = hasPermissionClaim(
    'Permissions.ScoringModels.View',
  )

  return (
    <>
      <Flex vertical gap={10}>
        {portfolio.scoringModel && (
          <LabeledContent label="Scoring Model">
            {canViewScoringModel ? (
              <Link
                href={`/settings/scoring/scoring-models/${portfolio.scoringModel.key}`}
              >
                {portfolio.scoringModel.name}
              </Link>
            ) : (
              portfolio.scoringModel.name
            )}
          </LabeledContent>
        )}

        {portfolio.description && (
          <LabeledContent label="Description">
            <ExpandableContent>
              <MarkdownRenderer markdown={portfolio.description} />
            </ExpandableContent>
          </LabeledContent>
        )}
      </Flex>

      <Divider size="small" style={{ margin: 0 }} />

      {/* No Relationships group: a portfolio is the top of the hierarchy, so
          its roles are the only thing this half of the panel carries. */}
      <RecordFactsGroup label="Roles">
        <LabeledContent label="Sponsors">
          <RecordRoleList
            people={portfolio.portfolioSponsors}
            emptyText="No sponsor assigned"
          />
        </LabeledContent>

        <LabeledContent label="Owners">
          <RecordRoleList
            people={portfolio.portfolioOwners}
            emptyText="No owner assigned"
          />
        </LabeledContent>

        <LabeledContent label="PMs" tooltip="Portfolio Managers">
          <RecordRoleList
            people={portfolio.portfolioManagers}
            emptyText="No PM assigned"
          />
        </LabeledContent>
      </RecordFactsGroup>

      <Divider size="small" style={{ margin: 0 }} />

      <LinksCard objectId={portfolio.id} width="100%" />
    </>
  )
}

export default PortfolioFacts
