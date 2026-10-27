'use client'

import { PageTitle } from '@/src/components/common'
import {
  DragHandleCell,
  WaydGrid,
  type GridColumnContext,
  type RowReorderEvent,
} from '@/src/components/common/wayd-grid'
import {
  createActionsColumn,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useMessage } from '@/src/components/contexts/messaging'
import { useDocumentTitle } from '@/src/hooks'
import { IntakeChannelDto } from '@/src/services/wayd-api'
import {
  useGetIntakeChannelsQuery,
  useReorderIntakeChannelsMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { isApiError } from '@/src/utils'
import { Button, Typography } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { IntakeChannelForm, useIntakeChannelActions } from './_components'

const { Paragraph } = Typography

/**
 * The company's list of where requests come from. Each desk offers the
 * channels that apply to it from this one list, which is what keeps reporting
 * by channel comparable across desks.
 */
const IntakeChannelsPage = () => {
  useDocumentTitle('Demand Management - Channels')
  const [openCreateForm, setOpenCreateForm] = useState(false)
  const messageApi = useMessage()

  // Archived as well: a settings screen manages what a picker hides.
  const {
    data: channels,
    isLoading,
    error,
    refetch,
  } = useGetIntakeChannelsQuery({ includeArchived: true })
  const [reorderChannels] = useReorderIntakeChannelsMutation()
  const { getActionItems, dialogs } = useIntakeChannelActions()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the channels',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = useMemo(
    () =>
      (context: GridColumnContext): ColumnDef<IntakeChannelDto, any>[] => [
        createActionsColumn<IntakeChannelDto>({
          getItems: getActionItems,
          ariaLabel: 'Channel actions',
          size: 70,
          leading: (
            <DragHandleCell
              isDragEnabled={context.isDragEnabled}
              disabledTooltip="Clear sorting, filters, and search to reorder channels."
            />
          ),
        }),
        {
          id: 'order',
          accessorKey: 'order',
          header: 'Order',
          size: 90,
          enableColumnFilter: false,
        },
        { id: 'name', accessorKey: 'name', header: 'Name', size: 240 },
        {
          id: 'description',
          accessorKey: 'description',
          header: 'Description',
          size: 420,
        },
        {
          id: 'isActive',
          accessorKey: 'isActive',
          header: 'Active',
          size: 100,
          meta: { columnType: 'yesNo' },
        },
      ],
    [getActionItems],
  )

  // The whole list travels, as the API requires; the grid allows dragging only
  // when nothing is sorted, filtered or searched.
  const onRowReorder = async (event: RowReorderEvent<IntakeChannelDto>) => {
    try {
      await reorderChannels({
        orderedChannelIds: event.orderedData.map((c) => c.id),
      }).unwrap()
    } catch (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while reordering the channels',
      )
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle
        title="Channels"
        actions={
          <Button onClick={() => setOpenCreateForm(true)}>Add Channel</Button>
        }
      />
      <Paragraph type="secondary">
        Where requests come from. Each desk offers the channels that apply to
        it, chosen in its settings. Archive a channel to stop offering it;
        requests already from it keep it.
      </Paragraph>

      <WaydGrid
        columns={columns}
        data={channels ?? []}
        onRefresh={async () => {
          refetch()
        }}
        isLoading={isLoading}
        persistStateKey="settings-intake-channels"
        csvFileName="intake-channels"
        getRowId={(channel: IntakeChannelDto) => channel.id}
        onRowReorder={onRowReorder}
      />

      {dialogs}

      {openCreateForm && (
        <IntakeChannelForm
          onFormComplete={() => setOpenCreateForm(false)}
          onFormCancel={() => setOpenCreateForm(false)}
        />
      )}
    </div>
  )
}

const IntakeChannelsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    IntakeChannelsPage,
    'Permission',
    'Permissions.IntakeRequests.Administer',
  ),
  'demand-management',
)

export default IntakeChannelsPageWithAuthorization
