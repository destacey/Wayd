'use client'

import { PageActions } from '@/src/components/common'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetIntakeRequestActivitiesQuery,
  useGetIntakeRequestQuery,
  useGetIntakeRequestScoringQuery,
  useLazyGetIntakeRequestActivitiesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { FollowOwnerTypes } from '@/src/store/features/common/follows-api'
import { toApiError } from '@/src/utils'
import { Alert, Button, Flex } from 'antd'
import dayjs from 'dayjs'
import type { ItemType } from 'antd/es/menu/interface'
import NextLink from 'next/link'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import { StatusHistoryTag } from '@/src/components/common/status-workflows'
import { TagList } from '@/src/components/common/tags'
import { deskHref } from '../../_components/desk-status'
import { requestHref } from '../../_components/intake-requests-grid'
import AddIntakeRequestParticipantForm from './_components/add-intake-request-participant-form'
import ChangeIntakeRequestAssigneeForm from './_components/change-intake-request-assignee-form'
import ChangeIntakeRequestStatusForm from './_components/change-intake-request-status-form'
import EditIntakeRequestForm from './_components/edit-intake-request-form'
import IntakeRequestDetails from './_components/intake-request-details'
import IntakeRequestFacts from './_components/intake-request-facts'
import IntakeRequestParticipants from './_components/intake-request-participants'
import IntakeRequestScore from './_components/intake-request-score'
import ScoreIntakeRequestForm from './_components/score-intake-request-form'
import IntakeRequestStatusHistory from './_components/intake-request-status-history'
import ManageIntakeRequestTagsForm from './_components/manage-intake-request-tags-form'
import RerouteIntakeRequestForm from './_components/reroute-intake-request-form'
import WithdrawIntakeRequestForm from './_components/withdraw-intake-request-form'
import MoveIntakeRequestButton from './_components/move-intake-request-button'
import IntakeRequestDetailsLoading from './loading'

enum RequestSection {
  Details = 'details',
  Participants = 'participants',
  Score = 'score',
  StatusHistory = 'status-history',
  Activity = 'activity',
}

type RequestDialog =
  | 'edit'
  | 'add-participant'
  | 'reroute'
  | 'change-status'
  | 'change-assignee'
  | 'withdraw'
  | 'manage-tags'
  | 'score'

/** The query parameter a redirected duplicate's number arrives in, so its surviving request can say why. */
const DUPLICATE_PARAM = 'duplicate'

const IntakeRequestDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  useDocumentTitle('Request Details')

  // The active section lives in the URL, owned by RecordLayout; read here only to load activity lazily.
  const searchParams = useSearchParams()
  const activeSection = searchParams.get('section') ?? RequestSection.Details

  const [dialog, setDialog] = useState<RequestDialog | null>(null)
  // The status the form opens on, when a move picked from the header needs a reason or the original.
  const [statusToOpen, setStatusToOpen] = useState<string>()
  const router = useRouter()

  const { data: request, error, refetch } = useGetIntakeRequestQuery(key)

  // A duplicate sends its readers to the request it gives way to. The agents who decide it stay, so they
  // can see why and reopen it; a reader who cannot see the surviving request gets no link and stays too.
  const redirectTo =
    request?.duplicateOf && !request.isDeskAgent
      ? `${requestHref(request.duplicateOf.key)}?${DUPLICATE_PARAM}=${request.summary.key}`
      : null
  useEffect(() => {
    if (redirectTo) router.replace(redirectTo)
  }, [redirectTo, router])

  // The number of a duplicate that redirected here. Taken from the address, so it says nothing the reader
  // was not already shown.
  const arrivedFrom = Number(searchParams.get(DUPLICATE_PARAM)) || null

  const requestId = request?.summary.id
  const activitiesQuery = useGetIntakeRequestActivitiesQuery(
    { idOrKey: requestId ?? '', page: 1, pageSize: ACTIVITY_LOG_PAGE_SIZE },
    { skip: !requestId || activeSection !== RequestSection.Activity },
  )
  // Substance tier, so fetched only for the Score section, which only a reader with the substance is offered.
  const { data: scoring } = useGetIntakeRequestScoringQuery(requestId ?? '', {
    skip:
      !requestId ||
      !request?.substance ||
      activeSection !== RequestSection.Score,
  })
  const [fetchActivityLogPage] = useLazyGetIntakeRequestActivitiesQuery()
  const activityLog = useActivityLog({
    idOrKey: requestId,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: `request-${key}-activity`,
  })

  // A request on a private desk the reader is not part of answers 404, exactly like one that does not
  // exist, so both read as not found.
  if (error && toApiError(error).status === 404) {
    return notFound()
  }

  if (!request || redirectTo) {
    if (error) {
      return (
        <div className="page-gutters">
          <Alert
            type="error"
            showIcon
            title="The request could not be loaded."
            action={<Button onClick={() => refetch()}>Try again</Button>}
          />
        </div>
      )
    }
    return <IntakeRequestDetailsLoading />
  }

  const { summary, substance } = request

  const sections: RecordSection[] = [
    { id: RequestSection.Details, label: 'Details' },
    // The request's people are substance; a reader without it is not told who they are.
    ...(substance
      ? [
          {
            id: RequestSection.Participants,
            label: 'People',
            count:
              substance.participants.length +
              (substance.submittedBy.id !== summary.requester.id ? 2 : 1),
          },
        ]
      : []),
    // A score ranks one ask above another, which is what a restricted desk restricts, so it is substance.
    ...(substance ? [{ id: RequestSection.Score, label: 'Score' }] : []),
    { id: RequestSection.StatusHistory, label: 'Status History' },
    { id: RequestSection.Activity, label: 'Activity' },
  ]

  const openStatusForm = (statusId?: string) => {
    setStatusToOpen(statusId)
    setDialog('change-status')
  }

  const actionItems: ItemType[] = [
    ...(request.canEdit && substance
      ? [{ key: 'edit', label: 'Edit', onClick: () => setDialog('edit') }]
      : []),
    ...(request.canChangeAssignee
      ? [
          {
            key: 'change-assignee',
            label: 'Change Assignee',
            onClick: () => setDialog('change-assignee'),
          },
        ]
      : []),
    ...(request.canChangeTags
      ? [
          {
            key: 'manage-tags',
            label: 'Manage Tags',
            onClick: () => setDialog('manage-tags'),
          },
        ]
      : []),
    ...(request.canWithdraw
      ? [
          {
            key: 'withdraw',
            label: 'Withdraw Request',
            onClick: () => setDialog('withdraw'),
          },
        ]
      : []),
    ...(request.canReroute
      ? [
          {
            key: 'reroute',
            label: 'Move to Another Desk',
            onClick: () => setDialog('reroute'),
          },
        ]
      : []),
  ]

  const sectionActions =
    activeSection === RequestSection.Participants &&
    request.canAddParticipants ? (
      <Button size="small" onClick={() => setDialog('add-participant')}>
        Add Participant
      </Button>
    ) : activeSection === RequestSection.Score && scoring?.canScore ? (
      <Button size="small" onClick={() => setDialog('score')}>
        {scoring.scores.length > 0 ? 'Re-score' : 'Score'}
      </Button>
    ) : activeSection === RequestSection.Activity ? (
      <ActivityLogExportButton activityLog={activityLog} />
    ) : undefined

  const renderSection = (section: string) => {
    switch (section) {
      case RequestSection.Details:
        return (
          <IntakeRequestDetails
            request={request}
            notices={
              <>
                {request.duplicateOf && (
                  <Alert
                    type="info"
                    showIcon
                    title={
                      <>
                        A duplicate of{' '}
                        <NextLink href={requestHref(request.duplicateOf.key)}>
                          {request.duplicateOf.key} ·{' '}
                          {request.duplicateOf.title}
                        </NextLink>
                      </>
                    }
                    description="Readers who can see that request are taken to it when they open this one. The desk's agents stay here."
                  />
                )}
                {arrivedFrom && (
                  <Alert
                    type="info"
                    showIcon
                    title={`Request ${arrivedFrom} was marked a duplicate of this one, so you were brought here.`}
                  />
                )}
              </>
            }
          />
        )
      case RequestSection.StatusHistory:
        return <IntakeRequestStatusHistory requestId={summary.id} />
      case RequestSection.Participants:
        return substance ? (
          <IntakeRequestParticipants request={request} substance={substance} />
        ) : null
      case RequestSection.Score:
        return substance ? <IntakeRequestScore requestId={summary.id} /> : null
      case RequestSection.Activity:
        return <ActivityLogTimeline {...activityLog.timelineProps} />
      default:
        return null
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={RequestSection.Details}
        record={{
          name: summary.title,
          recordKey: String(summary.key),
          // Outermost first: the list, then the desk the request is on.
          parent: [
            { label: 'Requests', href: '/demand-management/requests' },
            { label: summary.desk.name, href: deskHref(summary.desk.key) },
          ],
          subtitle: 'Request Details',
          caption: `Raised by ${summary.requester.name} on ${dayjs(summary.submittedOn).format('MMM D, YYYY')}${summary.channel ? ` · from ${summary.channel.name}` : ''}`,
          // Status leads; the request's tags follow, visible to everyone who sees the request.
          tags: (
            <Flex gap="small" align="center" wrap>
              <StatusHistoryTag
                status={summary.status}
                onOpenHistory={() =>
                  router.replace(`?section=${RequestSection.StatusHistory}`, {
                    scroll: false,
                  })
                }
              />
              <TagList
                // The server orders them by axis, then name.
                tags={summary.tags.map((tag) => ({
                  id: tag.id,
                  label: tag.name,
                  qualifier: tag.categoryName,
                }))}
                maxVisible={3}
                onManage={
                  request.canChangeTags
                    ? () => setDialog('manage-tags')
                    : undefined
                }
              />
            </Flex>
          ),
          follow: {
            ownerType: FollowOwnerTypes.IntakeRequest,
            ownerId: summary.id,
          },
          actions: (
            <Flex gap="small" align="center">
              {request.canChangeStatus && (
                <MoveIntakeRequestButton
                  request={request}
                  onNeedsDetail={openStatusForm}
                />
              )}
              <PageActions actionItems={actionItems} />
            </Flex>
          ),
        }}
        facts={<IntakeRequestFacts request={request} />}
        sectionActions={sectionActions}
      >
        {renderSection}
      </RecordLayout>
      {dialog === 'edit' && substance && request.canEdit && (
        <EditIntakeRequestForm
          request={request}
          substance={substance}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'reroute' && request.canReroute && (
        <RerouteIntakeRequestForm
          request={request}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'change-status' && request.canChangeStatus && (
        <ChangeIntakeRequestStatusForm
          request={request}
          initialStatusId={statusToOpen}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'change-assignee' && request.canChangeAssignee && (
        <ChangeIntakeRequestAssigneeForm
          request={request}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'manage-tags' && request.canChangeTags && (
        <ManageIntakeRequestTagsForm
          request={request}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'withdraw' && request.canWithdraw && (
        <WithdrawIntakeRequestForm
          request={request}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'score' && scoring?.canScore && scoring.scoringModel && (
        <ScoreIntakeRequestForm
          requestId={summary.id}
          scoringModel={scoring.scoringModel}
          modelArchived={scoring.scoringModelArchived}
          needBy={summary.needBy}
          currentScore={scoring.scores[0]}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'add-participant' && request.canAddParticipants && (
        <AddIntakeRequestParticipantForm
          request={request}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
    </>
  )
}

// No permission gate: a request's own people and its desk read it without one, and the server answers 404
// for anyone else.
const IntakeRequestDetailsPageWithAuthorization = requireFeatureFlag(
  IntakeRequestDetailsPage,
  'demand-management',
)

export default IntakeRequestDetailsPageWithAuthorization
