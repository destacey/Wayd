'use client'

import { InactiveTag, PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetWorkProcessQuery } from '@/src/store/features/work-management/work-process-api'
import { ItemType } from 'antd/es/menu/interface'
import { notFound } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import ChangeWorkProcessIsActiveForm from '../_components/change-work-process-isactive-form'
import WorkProcessDetailsLoading from './loading'
import { WorkProcessFacts, WorkProcessSchemes } from './_components'

enum WorkProcessSections {
  Schemes = 'schemes',
}

const WorkProcessDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)

  const [openChangeIsActiveForm, setOpenChangeIsActiveForm] = useState(false)

  const { hasPermissionClaim } = useAuth()
  const canUpdateWorkProcess = hasPermissionClaim(
    'Permissions.WorkProcesses.Update',
  )

  const {
    data: workProcess,
    isLoading,
    error,
    refetch,
  } = useGetWorkProcessQuery(key)

  useDocumentTitle(`${workProcess?.key ?? key} - Work Process Details`)

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const actionsMenuItems: ItemType[] = (() => {
    if (!canUpdateWorkProcess || workProcess?.isActive === undefined) return []
    return [
      {
        key: 'toggle-active',
        label: workProcess.isActive ? 'Deactivate' : 'Activate',
        onClick: () => setOpenChangeIsActiveForm(true),
      },
    ]
  })()

  // One section, so `RecordLayout` renders no rail — a rail holding a single
  // item spends its width saying there is nowhere to go.
  const sections: RecordSection[] = [
    { id: WorkProcessSections.Schemes, label: 'Work Types and Workflows' },
  ]

  const onChangeIsActiveFormClosed = (wasSaved: boolean) => {
    setOpenChangeIsActiveForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  if (isLoading) {
    return <WorkProcessDetailsLoading />
  }

  if (!workProcess) {
    return notFound()
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={WorkProcessSections.Schemes}
        record={{
          name: workProcess.name,
          recordKey: String(workProcess.key),
          parent: {
            label: 'Work Processes',
            href: '/settings/work-management/work-processes',
          },
          subtitle: 'Work Process Details',
          tags: <InactiveTag isActive={workProcess.isActive ?? false} />,
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={<WorkProcessFacts workProcess={workProcess} />}
      >
        {() => <WorkProcessSchemes workProcessId={workProcess.id} />}
      </RecordLayout>

      {openChangeIsActiveForm && (
        <ChangeWorkProcessIsActiveForm
          workProcessId={workProcess.id}
          workProcessName={workProcess.name}
          isActive={!!workProcess.isActive}
          onFormSave={() => onChangeIsActiveFormClosed(true)}
          onFormCancel={() => onChangeIsActiveFormClosed(false)}
        />
      )}
    </>
  )
}

const WorkProcessDetailsPageWithAuthorization = authorizePage(
  WorkProcessDetailsPage,
  'Permission',
  'Permissions.WorkProcesses.View',
)

export default WorkProcessDetailsPageWithAuthorization
