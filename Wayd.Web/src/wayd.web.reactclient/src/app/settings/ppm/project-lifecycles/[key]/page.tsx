'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { useGetProjectLifecycleQuery } from '@/src/store/features/ppm/project-lifecycles-api'
import { isApiError } from '@/src/utils'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import ChangeProjectLifecycleStateForm, {
  ProjectLifecycleStateAction,
} from '../_components/change-project-lifecycle-state-form'
import DeleteProjectLifecycleForm from '../_components/delete-project-lifecycle-form'
import EditProjectLifecycleForm from '../_components/edit-project-lifecycle-form'
import ProjectLifecycleStagesList from '../_components/project-lifecycle-stages-list'
import ProjectLifecycleDetailsLoading from './loading'
import { ProjectLifecycleFacts } from './_components'

enum ProjectLifecycleSections {
  Stages = 'stages',
}

/** The dialogs this record can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'archive' | 'delete'

const ProjectLifecycleDetailsPage = (props: {
  params: Promise<{ key: number }>
}) => {
  const { key } = use(props.params)

  const [dialog, setDialog] = useState<DialogId | null>(null)

  const messageApi = useMessage()
  const router = useRouter()

  const {
    data: lifecycle,
    isLoading,
    error,
    refetch,
  } = useGetProjectLifecycleQuery(key.toString())

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.ProjectLifecycles.Update')
  const canDelete = hasPermissionClaim('Permissions.ProjectLifecycles.Delete')

  // Stages are only editable while the lifecycle is Proposed — once projects
  // are running against it, changing its stages would rewrite their history.
  const canManageStages = canUpdate && lifecycle?.state?.name === 'Proposed'

  useDocumentTitle(
    lifecycle
      ? `${lifecycle.name} - Project Lifecycle Details`
      : 'Project Lifecycle Details',
  )

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading project lifecycle details',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const actionsMenuItems: ItemType[] = (() => {
    const state = lifecycle?.state?.name
    const canBeDeleted = state === 'Proposed'
    const canBeActivated = state === 'Proposed'
    const canBeArchived = state === 'Active'

    const items: ItemType[] = []

    if (canUpdate) {
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

  // One section, so `RecordLayout` renders no rail — the stages are the whole
  // of the record's content, and the two-line Details tab is now the facts.
  const sections: RecordSection[] = [
    {
      id: ProjectLifecycleSections.Stages,
      label: 'Stages',
      count: lifecycle?.stages?.length,
    },
  ]

  if (isLoading) {
    return <ProjectLifecycleDetailsLoading />
  }

  if (!lifecycle) {
    return notFound()
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={ProjectLifecycleSections.Stages}
        record={{
          name: lifecycle.name,
          recordKey: String(lifecycle.key),
          parent: {
            label: 'Project Lifecycles',
            href: '/settings/ppm/project-lifecycles',
          },
          subtitle: 'Project Lifecycle Details',
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={<ProjectLifecycleFacts lifecycle={lifecycle} />}
      >
        {() => (
          <ProjectLifecycleStagesList
            lifecycle={lifecycle}
            canManageStages={canManageStages}
            loadData={refetch}
          />
        )}
      </RecordLayout>

      {dialog === 'edit' && (
        <EditProjectLifecycleForm
          lifecycleId={lifecycle.id}
          onFormComplete={() => closeDialog(true)}
          onFormCancel={() => closeDialog(false)}
        />
      )}
      {(dialog === 'activate' || dialog === 'archive') && (
        <ChangeProjectLifecycleStateForm
          lifecycle={lifecycle}
          stateAction={
            dialog === 'activate'
              ? ProjectLifecycleStateAction.Activate
              : ProjectLifecycleStateAction.Archive
          }
          onFormComplete={() => closeDialog(true)}
          onFormCancel={() => closeDialog(false)}
        />
      )}
      {dialog === 'delete' && (
        <DeleteProjectLifecycleForm
          lifecycle={lifecycle}
          onFormComplete={() => {
            setDialog(null)
            router.push('/settings/ppm/project-lifecycles')
          }}
          onFormCancel={() => closeDialog(false)}
        />
      )}
    </>
  )
}

const ProjectLifecycleDetailsPageWithAuthorization = authorizePage(
  ProjectLifecycleDetailsPage,
  'Permission',
  'Permissions.ProjectLifecycles.View',
)

export default ProjectLifecycleDetailsPageWithAuthorization
