'use client'

import {
  MentionCandidate,
  RecordDiscussion,
} from '@/src/components/common/discussions'
import {
  DiscussionMessageKind,
  EmployeeNavigationDto,
  IntakeDeskVisibility,
  IntakeRequestDetailsDto,
  IntakeRequestSubstanceDto,
} from '@/src/services/wayd-api'
import { DiscussionChannelTypes } from '@/src/store/features/common/discussions-api'
import {
  useGetIntakeDeskQuery,
  useGetIntakeEmployeeOptionsQuery,
} from '@/src/store/features/demand-management/demand-management-api'

const audience = {
  [DiscussionMessageKind.Public]:
    "Seen by the requester, everyone taking part and the desk's agents",
  [DiscussionMessageKind.InternalNote]: "Only the desk's agents will see this",
}

const candidate = (person: EmployeeNavigationDto): MentionCandidate => ({
  id: person.id,
  name: person.name,
  key: person.key,
})

const distinct = (people: MentionCandidate[]) => [
  ...new Map(people.map((p) => [p.id, p])).values(),
]

export interface IntakeRequestDiscussionProps {
  request: IntakeRequestDetailsDto
  substance: IntakeRequestSubstanceDto
}

/**
 * The request's conversation, with who can be mentioned in it. Asking the requester a question and moving the
 * request to Waiting on Requester is done from the status form, which posts the question here.
 */
const IntakeRequestDiscussion = ({
  request,
  substance,
}: IntakeRequestDiscussionProps) => {
  const { summary } = request

  const { data: desk } = useGetIntakeDeskQuery(summary.desk.id)
  const mayMentionAnyone =
    request.canAddParticipants &&
    desk !== undefined &&
    desk.visibility !== IntakeDeskVisibility.Private
  const { data: everyone } = useGetIntakeEmployeeOptionsQuery(undefined, {
    skip: !mayMentionAnyone,
  })

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

  return (
    <RecordDiscussion
      channelType={DiscussionChannelTypes.IntakeRequest}
      ownerId={summary.id}
      mentionCandidates={replyCandidates}
      noteMentionCandidates={deskMembers}
      audience={audience}
    />
  )
}

export default IntakeRequestDiscussion
