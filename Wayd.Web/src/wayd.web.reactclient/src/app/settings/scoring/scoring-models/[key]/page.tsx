'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { useGetScoringModelQuery } from '@/src/store/features/scoring/scoring-models-api'
import { isApiError } from '@/src/utils'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import ChangeScoringModelStateForm, {
  ScoringModelStateAction,
} from '../_components/change-scoring-model-state-form'
import DeleteScoringModelForm from '../_components/delete-scoring-model-form'
import EditScoringModelForm from '../_components/edit-scoring-model-form'
import ScoringModelCriteriaList from '../_components/scoring-model-criteria-list'
import ScoringModelOutputsList from '../_components/scoring-model-outputs-list'
import ScoringModelTestPanel from '../_components/scoring-model-test-panel'
import ScoringScalesList from '../_components/scoring-scales-list'
import ScoringModelDetailsLoading from './loading'
import { ScoringModelFacts } from './_components'

enum ScoringModelSections {
  Criteria = 'criteria',
  RatingScale = 'rating-scale',
  Outputs = 'outputs',
  Test = 'test',
}

/** The dialogs this record can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'archive' | 'delete'

const ScoringModelDetailsPage = (props: {
  params: Promise<{ key: number }>
}) => {
  const { key } = use(props.params)

  const [dialog, setDialog] = useState<DialogId | null>(null)

  const messageApi = useMessage()
  const router = useRouter()

  const {
    data: scoringModel,
    isLoading,
    error,
    refetch,
  } = useGetScoringModelQuery(key.toString())

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.ScoringModels.Update')
  const canDelete = hasPermissionClaim('Permissions.ScoringModels.Delete')

  // A model's shape is only editable while it is Proposed — once it is in use
  // for scoring, changing its criteria would silently rewrite past scores.
  const isProposed = scoringModel?.state?.name === 'Proposed'
  const canManage = canUpdate && isProposed

  useDocumentTitle(
    scoringModel
      ? `${scoringModel.name} - Scoring Model Details`
      : 'Scoring Model Details',
  )

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading scoring model details',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const actionsMenuItems: ItemType[] = (() => {
    const state = scoringModel?.state?.name
    const canBeDeleted = state === 'Proposed'
    const canBeActivated = state === 'Proposed'
    const canBeArchived = state === 'Active'

    const items: ItemType[] = []

    if (canUpdate && isProposed) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setDialog('edit'),
      })
    }
    if (canDelete && canBeDeleted) {
      items.push({
        key: 'delete',
        label: 'Delete',
        onClick: () => setDialog('delete'),
      })
    }
    if (canUpdate && (canBeActivated || canBeArchived)) {
      if (items.length > 0) {
        items.push({ key: 'manage-divider', type: 'divider' })
      }
      items.push({
        key: canBeActivated ? 'activate' : 'archive',
        label: canBeActivated ? 'Activate' : 'Archive',
        onClick: () => setDialog(canBeActivated ? 'activate' : 'archive'),
      })
    }

    return items
  })()

  const closeDialog = (changed: boolean) => {
    setDialog(null)
    if (changed) refetch()
  }

  // Counts come from the record's own query rather than one per section, so
  // the rail renders complete on first paint and cannot disagree with the
  // section beside it.
  const sections: RecordSection[] = [
    {
      id: ScoringModelSections.Criteria,
      label: 'Criteria',
      count: scoringModel?.criteria?.length,
    },
    {
      id: ScoringModelSections.RatingScale,
      label: 'Rating Scales',
      count: scoringModel?.scales?.length,
    },
    {
      id: ScoringModelSections.Outputs,
      label: 'Outputs',
      count: scoringModel?.outputs?.length,
    },
  ]

  // Test is a tool rather than record content — it previews what the model
  // would produce without saving anything — so it sits in the rail's Reports
  // group below the divider rather than among the sections.
  const reports: RecordSection[] = [
    { id: ScoringModelSections.Test, label: 'Test' },
  ]

  const renderSection = (section: string) => {
    switch (section as ScoringModelSections) {
      case ScoringModelSections.Criteria:
        return (
          <ScoringModelCriteriaList
            scoringModel={scoringModel!}
            canManage={canManage}
            loadData={refetch}
          />
        )
      case ScoringModelSections.RatingScale:
        return (
          <ScoringScalesList
            scoringModel={scoringModel!}
            canManage={canManage}
            loadData={refetch}
          />
        )
      case ScoringModelSections.Outputs:
        return (
          <ScoringModelOutputsList
            scoringModel={scoringModel!}
            canManage={canManage}
            loadData={refetch}
          />
        )
      case ScoringModelSections.Test:
        return <ScoringModelTestPanel scoringModel={scoringModel!} />
      default:
        return null
    }
  }

  if (isLoading) {
    return <ScoringModelDetailsLoading />
  }

  if (!scoringModel) {
    return notFound()
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        reports={reports}
        defaultSection={ScoringModelSections.Criteria}
        record={{
          name: scoringModel.name,
          recordKey: String(scoringModel.key),
          parent: {
            label: 'Scoring Models',
            href: '/settings/scoring/scoring-models',
          },
          subtitle: 'Scoring Model Details',
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={<ScoringModelFacts scoringModel={scoringModel} />}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {dialog === 'edit' && (
        <EditScoringModelForm
          scoringModelId={scoringModel.id}
          onFormComplete={() => closeDialog(true)}
          onFormCancel={() => closeDialog(false)}
        />
      )}
      {(dialog === 'activate' || dialog === 'archive') && (
        <ChangeScoringModelStateForm
          scoringModel={scoringModel}
          stateAction={
            dialog === 'activate'
              ? ScoringModelStateAction.Activate
              : ScoringModelStateAction.Archive
          }
          onFormComplete={() => closeDialog(true)}
          onFormCancel={() => closeDialog(false)}
        />
      )}
      {dialog === 'delete' && (
        <DeleteScoringModelForm
          scoringModel={scoringModel}
          onFormComplete={() => {
            setDialog(null)
            router.push('/settings/scoring/scoring-models')
          }}
          onFormCancel={() => closeDialog(false)}
        />
      )}
    </>
  )
}

const ScoringModelDetailsPageWithAuthorization = authorizePage(
  ScoringModelDetailsPage,
  'Permission',
  'Permissions.ScoringModels.View',
)

export default ScoringModelDetailsPageWithAuthorization
