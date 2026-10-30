'use client'

import { Checkbox } from 'antd'
import { useState } from 'react'
import {
  DiscussionComposerValue,
  MentionCandidate,
  RecordDiscussion,
} from '@/src/components/common/discussions'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  DiscussionMessageKind,
  EmployeeNavigationDto,
  IntakeDeskVisibility,
  IntakeRequestDetailsDto,
  IntakeRequestSubstanceDto,
} from '@/src/services/wayd-api'
import {
  DiscussionChannelTypes,
  usePostDiscussionMessageMutation,
} from '@/src/store/features/common/discussions-api'
import {
  useChangeIntakeRequestStatusMutation,
  useGetIntakeDeskQuery,
  useGetIntakeEmployeeOptionsQuery,
  useGetIntakeRequestStatusOptionsQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'

/**
 * The status alias meaning Waiting on Requester. A value of the server's `IntakeRequestStatusAlias`, which the
 * generated client carries only as a number; matched by meaning so a desk that renames the status still has it.
 */
export const WAITING_ON_REQUESTER = 2

const audience = {
  [DiscussionMessageKind.Public]:
    "Seen by the requester, everyone taking part and the desk's agents",
  [DiscussionMessageKind.InternalNote]: "Only the desk's agents will see this",
}

const candidate = (person: EmployeeNavigationDto): MentionCandidate => ({
  id: person.id,
  name: person.name,
})

const distinct = (people: MentionCandidate[]) => [
  ...new Map(people.map((p) => [p.id, p])).values(),
]

export interface IntakeRequestDiscussionProps {
  request: IntakeRequestDetailsDto
  substance: IntakeRequestSubstanceDto
}

/**
 * The request's conversation. An agent replying can move the request to Waiting on Requester in the same action,
 * so asking the requester a question and setting the status to wait for them is one step, saved together.
 */
const IntakeRequestDiscussion = ({
  request,
  substance,
}: IntakeRequestDiscussionProps) => {
  const { summary } = request
  const messageApi = useMessage()
  // Off until asked for: a reply that also moves the request should be the agent's choice each time.
  const [moveToWaiting, setMoveToWaiting] = useState(false)

  const { data: desk } = useGetIntakeDeskQuery(summary.desk.id)
  const mayMentionAnyone =
    request.canAddParticipants &&
    desk !== undefined &&
    desk.visibility !== IntakeDeskVisibility.Private
  const { data: everyone } = useGetIntakeEmployeeOptionsQuery(undefined, {
    skip: !mayMentionAnyone,
  })
  const { data: statusOptions } = useGetIntakeRequestStatusOptionsQuery(
    summary.id,
    { skip: !request.canChangeStatus },
  )
  const [postMessage] = usePostDiscussionMessageMutation()
  const [changeStatus] = useChangeIntakeRequestStatusMutation()

  const deskMembers = (desk?.members ?? []).map((m) => candidate(m.employee))
  const requestPeople = distinct([
    candidate(summary.requester),
    candidate(substance.submittedBy),
    ...substance.participants.map((p) => candidate(p.employee)),
    ...deskMembers,
  ])
  // Who the server would let the author mention; it narrows again whatever is sent.
  const replyCandidates = mayMentionAnyone
    ? (everyone ?? []).map(candidate)
    : requestPeople

  const waiting = statusOptions?.find(
    (s) => s.alias === WAITING_ON_REQUESTER && !s.needsReason,
  )

  const post = async (value: DiscussionComposerValue) => {
    const askAndMove =
      waiting && moveToWaiting && value.kind === DiscussionMessageKind.Public
    const response = askAndMove
      ? await changeStatus({
          requestId: summary.id,
          statusId: waiting.id,
          reply: { subject: value.subject, body: value.body },
        })
      : await postMessage({
          channelType: DiscussionChannelTypes.IntakeRequest,
          ownerId: summary.id,
          ...value,
        })
    if ('error' in response) {
      messageApi.error(
        toApiError(response.error).detail ?? 'Could not post the message.',
      )
      return false
    }
    return true
  }

  return (
    <RecordDiscussion
      channelType={DiscussionChannelTypes.IntakeRequest}
      ownerId={summary.id}
      mentionCandidates={replyCandidates}
      noteMentionCandidates={deskMembers}
      audience={audience}
      replyExtra={
        waiting && (
          <Checkbox
            checked={moveToWaiting}
            onChange={(e) => setMoveToWaiting(e.target.checked)}
          >
            Move the request to {waiting.name}
          </Checkbox>
        )
      }
      onPost={post}
    />
  )
}

export default IntakeRequestDiscussion
