'use client'

import { use, useEffect, useState } from 'react'
import { MenuProps, Spin } from 'antd'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { authorizePage } from '@/src/components/hoc'
import { notFound, usePathname, useRouter } from 'next/navigation'
import { useAppDispatch } from '@/src/hooks'
import { setBreadcrumbTitle } from '@/src/store/breadcrumbs'
import { InactiveTag, PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import { personInitials } from '@/src/components/common/record-initials'
import { useGetEmployeeQuery } from '@/src/store/features/organizations/employee-api'
import EmployeeDetailsLoading from './loading'
import { useMessage } from '@/src/components/contexts/messaging'
import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import DeleteEmployeeForm from '@/src/app/(legacy)/organizations/employees/_components/delete-employee-form'
import EmployeeOverview from './_components/employee-overview'
import EmployeeFacts from './_components/employee-facts'
import EmployeeTeamsGrid from './_components/employee-teams-grid'
import EmployeeWorkItems from './_components/employee-work-items'
import dynamic from 'next/dynamic'

const EmployeeCycleTimeReport = dynamic(
  () =>
    import('@/src/components/common/work/cycle-time-report').then((mod) => ({
      default: mod.EmployeeCycleTimeReport,
    })),
  { ssr: false, loading: () => <Spin /> },
)

enum EmployeeTabs {
  Overview = 'overview',
  Teams = 'teams',
  WorkItems = 'work-items',
  CycleTimeReport = 'cycle-time-report',
}

const EmployeeDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const employeeKey = Number(key)

  const [openDeleteEmployeeForm, setOpenDeleteEmployeeForm] =
    useState<boolean>(false)

  const messageApi = useMessage()
  const pathname = usePathname()
  const dispatch = useAppDispatch()

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canDeleteEmployee = hasPermissionClaim('Permissions.Employees.Delete')

  const {
    data: employeeData,
    isLoading,
    error,
  } = useGetEmployeeQuery(employeeKey)

  useDocumentTitle(
    employeeData?.displayName
      ? `${employeeData.displayName} - Employee Details`
      : 'Employee Details',
  )

  // Overview's metric tiles link through to the section each one summarises.
  const goToSection = (sectionId: string) =>
    router.replace(`${pathname}?section=${sectionId}`, { scroll: false })

  const renderSectionContent = (activeTab: EmployeeTabs) => {
    switch (activeTab) {
      case EmployeeTabs.Overview:
        return (
          <EmployeeOverview
            employee={employeeData!}
            onNavigateToSection={goToSection}
          />
        )
      case EmployeeTabs.Teams:
        return <EmployeeTeamsGrid employeeId={employeeData!.id} />
      case EmployeeTabs.WorkItems:
        return <EmployeeWorkItems employeeId={employeeData!.id} />
      case EmployeeTabs.CycleTimeReport:
        return <EmployeeCycleTimeReport employeeId={employeeData!.id} />
      default:
        return null
    }
  }

  // No Details section: every fact it held now lives in the record facts rail,
  // so a section would repeat the rail beside it verbatim.
  const sections: RecordSection[] = [
    { id: EmployeeTabs.Overview, label: 'Overview' },
    { id: EmployeeTabs.Teams, label: 'Teams' },
    { id: EmployeeTabs.WorkItems, label: 'Assigned Work Items' },
  ]

  // Reports are permanent entries in the rail's Reports group rather than
  // closable tabs, so they are addressable by URL like any other section.
  const reports: RecordSection[] = [
    {
      id: EmployeeTabs.CycleTimeReport,
      label: 'Cycle Time',
      // The report renders its own title alongside its date and percentile
      // controls, so the layout heading would stack a duplicate above it.
      hideHeading: true,
    },
  ]

  useEffect(() => {
    dispatch(setBreadcrumbTitle({ title: 'Details', pathname }))
  }, [dispatch, pathname])

  useEffect(() => {
    if (error) {
      messageApi.error('Failed to load employee details.')
    }
  }, [error, messageApi])

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []
    if (canDeleteEmployee) {
      items.push({
        key: 'delete',
        label: 'Delete',
        onClick: () => setOpenDeleteEmployeeForm(true),
      })
    }

    // Reports are permanent entries in the rail's Reports group, so they are
    // not repeated here — the menu is for actions, not navigation.

    return items
  })()

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteEmployeeForm(false)
    if (wasDeleted) {
      router.push('/organizations/employees/')
    }
  }

  if (isLoading) {
    return <EmployeeDetailsLoading />
  }

  if (!employeeData) {
    return notFound()
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        reports={reports}
        defaultSection={EmployeeTabs.Overview}
        record={{
          name: employeeData.displayName,
          // `title` is the honorific (Mr, Dr), not the job title — it has no
          // place naming the page. The job title is a descriptor instead.
          subtitle: 'Employee Details',
          descriptor: [employeeData.jobTitle, employeeData.department]
            .filter(Boolean)
            .join(', '),
          parent: { label: 'Employees', href: '/organizations/employees' },
          recordKey: String(employeeData.key),
          avatar: {
            initials: personInitials(
              employeeData.firstName,
              employeeData.lastName,
              employeeData.displayName,
            ),
          },
          tags: <InactiveTag isActive={employeeData?.isActive} />,
          actions: <PageActions actionItems={actionsMenuItems} />,
        }}
        facts={<EmployeeFacts employee={employeeData} />}
      >
        {(section) => renderSectionContent(section as EmployeeTabs)}
      </RecordLayout>

      {/* Delete Employee Form */}
      {openDeleteEmployeeForm && (
        <DeleteEmployeeForm
          employeeKey={employeeData.key}
          onFormComplete={() => {
            onDeleteFormClosed(true)
          }}
          onFormCancel={() => {
            onDeleteFormClosed(false)
          }}
        />
      )}
    </>
  )
}

const EmployeeDetailsPageWithAuthorization = authorizePage(
  EmployeeDetailsPage,
  'Permission',
  'Permissions.Employees.View',
)

export default EmployeeDetailsPageWithAuthorization
