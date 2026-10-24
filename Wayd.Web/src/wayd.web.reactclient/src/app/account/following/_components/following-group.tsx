'use client'

import { Button, Card, Flex, Pagination, Typography } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import { useState } from 'react'
import { WaydList } from '@/src/components/common'
import { useMessage } from '@/src/components/contexts/messaging'
import { FollowedRecordDto, FollowGroupDto } from '@/src/services/wayd-api'
import {
  FollowOwnerType,
  useGetMyFollowsQuery,
  useUnfollowMutation,
} from '@/src/store/features/common/follows-api'
import { toApiError } from '@/src/utils/problem-details'
import { followedRecordHref } from './followed-record-href'

const { Text } = Typography

export interface FollowingGroupProps {
  /** The group as the overview returned it, which is its first page. */
  group: FollowGroupDto
  pageSize: number
}

/**
 * One kind of followed record. The first page comes with the overview; later
 * pages are fetched for this kind alone.
 */
const FollowingGroup = ({ group, pageSize }: FollowingGroupProps) => {
  const [page, setPage] = useState(1)
  const [pendingId, setPendingId] = useState<string | null>(null)
  const messageApi = useMessage()
  const [unfollow] = useUnfollowMutation()

  // currentData, not data: data would keep showing the previous page's rows
  // while the requested one loads.
  const { currentData: laterPage, isFetching } = useGetMyFollowsQuery(
    { ownerType: group.ownerType, page, pageSize },
    { skip: page === 1 },
  )

  // Until a later page arrives it shows no rows, never the first page's, so an
  // Unfollow can only act on a row that is really on the page shown.
  const follows =
    page === 1
      ? group.follows
      : (laterPage?.find((g) => g.ownerType === group.ownerType)?.follows ?? {
          ...group.follows,
          items: [],
          pageNumber: page,
        })

  const onUnfollow = async (record: FollowedRecordDto) => {
    setPendingId(record.id)
    const response = await unfollow({
      ownerType: record.ownerType as FollowOwnerType,
      ownerId: record.id,
    })
    setPendingId(null)
    if ('error' in response) {
      messageApi.error(
        toApiError(response.error).detail ?? 'Could not stop following.',
      )
      return
    }
    // The last item on a later page leaves that page empty.
    if (page > 1 && follows.items.length === 1) setPage(page - 1)
  }

  return (
    <Card
      size="small"
      title={group.displayName}
      extra={<Text type="secondary">{follows.totalCount}</Text>}
    >
      <WaydList
        loading={isFetching}
        dataSource={follows.items}
        rowKey={(record) => record.id}
        renderItem={(record) => {
          const href = followedRecordHref(record)
          const title = `${record.key} - ${record.name}`
          return (
            <WaydList.Item
              actions={[
                <Button
                  key="unfollow"
                  type="link"
                  size="small"
                  aria-label={`Unfollow ${title}`}
                  loading={pendingId === record.id}
                  disabled={pendingId !== null}
                  onClick={() => onUnfollow(record)}
                >
                  Unfollow
                </Button>,
              ]}
            >
              <WaydList.Item.Meta
                title={href ? <Link href={href}>{title}</Link> : title}
                description={
                  <Flex gap="small" wrap>
                    {record.parentName && <Text>{record.parentName}</Text>}
                    <Text type="secondary">
                      Followed {dayjs(record.followedOn).format('MMM D, YYYY')}
                    </Text>
                  </Flex>
                }
              />
            </WaydList.Item>
          )
        }}
      />
      {follows.totalCount > pageSize && (
        <Flex justify="end">
          <Pagination
            size="small"
            current={page}
            pageSize={pageSize}
            total={follows.totalCount}
            showSizeChanger={false}
            onChange={setPage}
          />
        </Flex>
      )}
    </Card>
  )
}

export default FollowingGroup
