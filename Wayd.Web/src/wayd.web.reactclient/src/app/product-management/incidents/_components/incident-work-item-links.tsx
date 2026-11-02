'use client'

import ManageIncidentWorkItemsForm from '@/src/app/product-management/incidents/_components/manage-incident-work-items-form'
import { renderWorkItemLink } from '@/src/components/common/wayd-grid'
import { useGetIncidentWorkItemsQuery } from '@/src/store/features/product-management/incidents-api'
import { FormOutlined } from '@ant-design/icons'
import { Button } from 'antd'
import { useState } from 'react'
import styles from '@/src/app/product-management/incidents/_components/incidents.module.css'

export interface IncidentWorkItemLinksProps {
  incidentId: string
  idOrKey: string
  canManage: boolean
}

const IncidentWorkItemLinks = ({
  incidentId,
  idOrKey,
  canManage,
}: IncidentWorkItemLinksProps) => {
  const [manageOpen, setManageOpen] = useState(false)
  const { data, refetch } = useGetIncidentWorkItemsQuery({
    idOrKey,
    incidentId,
  })

  const workItems = data?.workItems ?? []

  const onManageClosed = (saved: boolean) => {
    setManageOpen(false)
    if (saved) {
      void refetch()
    }
  }

  return (
    <>
      <div className={styles.linksHeadingRow}>
        <h2 className={styles.asideHeading}>Links</h2>
        {canManage ? (
          <Button
            type="text"
            size="small"
            icon={<FormOutlined />}
            title="Manage work items"
            onClick={() => setManageOpen(true)}
          />
        ) : null}
      </div>
      <div className={styles.linksets}>
        <div className={styles.linkset}>
          <svg className={styles.linkMark} viewBox="0 0 16 16" aria-hidden>
            <rect
              x="4"
              y="1"
              width="11"
              height="2.4"
              rx="1.2"
              fill="var(--ant-color-primary)"
            />
            <rect
              x="1"
              y="4.8"
              width="6.2"
              height="2.4"
              rx="1.2"
              fill="var(--ant-color-success)"
            />
            <rect
              x="1"
              y="8.6"
              width="6.2"
              height="2.4"
              rx="1.2"
              fill="var(--ant-color-warning)"
            />
            <rect
              x="4"
              y="12.4"
              width="11"
              height="2.4"
              rx="1.2"
              fill="var(--ant-color-error)"
            />
          </svg>
          <div className={styles.linksetBody}>
            <span className={styles.srOnly}>Work items</span>
            {workItems.length === 0 ? (
              <span className={styles.linkMuted}>None linked</span>
            ) : (
              workItems.map((item) => (
                <div key={item.id} className={styles.linkRow}>
                  {renderWorkItemLink({
                    key: item.key,
                    workspaceKey: item.workspace.key,
                    externalViewWorkItemUrl: item.externalViewWorkItemUrl,
                    label: item.title,
                  })}
                </div>
              ))
            )}
          </div>
        </div>
      </div>
      {manageOpen ? (
        <ManageIncidentWorkItemsForm
          incidentId={incidentId}
          idOrKey={idOrKey}
          onFormComplete={() => onManageClosed(true)}
          onFormCancel={() => onManageClosed(false)}
        />
      ) : null}
    </>
  )
}

export default IncidentWorkItemLinks
