'use client'

import {
  MentionCandidate,
  RecordDiscussion,
} from '@/src/components/common/discussions'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid-core/grid-sorting'
import useAuth from '@/src/components/contexts/auth'
import {
  DiscussionMessageKind,
  EmployeeNavigationDto,
  IncidentDto,
} from '@/src/services/wayd-api'
import { DiscussionChannelTypes } from '@/src/store/features/common/discussions-api'
import { useGetEmployeesQuery } from '@/src/store/features/organizations/employee-api'

const audience = {
  [DiscussionMessageKind.Public]:
    'Seen by everyone who can view this incident',
}

const candidate = (person: EmployeeNavigationDto): MentionCandidate => ({
  id: person.id,
  name: person.name,
  key: person.key,
})

const byName = (people: MentionCandidate[]) =>
  [...new Map(people.map((p) => [p.id, p])).values()].sort((a, b) =>
    caseInsensitiveCompare(a.name, b.name),
  )

export interface IncidentDiscussionProps {
  incident: IncidentDto
}

const IncidentDiscussion = ({ incident }: IncidentDiscussionProps) => {
  const { hasPermissionClaim } = useAuth()
  const canViewEmployees = hasPermissionClaim('Permissions.Employees.View')
  const { data: employees } = useGetEmployeesQuery(false, {
    skip: !canViewEmployees,
  })

  const incidentPeople = byName(
    incident.commander ? [candidate(incident.commander)] : [],
  )
  const mentionCandidates =
    canViewEmployees && employees
      ? employees.map((e) => ({ id: e.id, name: e.displayName, key: e.key }))
      : incidentPeople

  return (
    <RecordDiscussion
      channelType={DiscussionChannelTypes.Incident}
      ownerId={incident.id}
      mentionCandidates={mentionCandidates}
      audience={audience}
    />
  )
}

export default IncidentDiscussion
