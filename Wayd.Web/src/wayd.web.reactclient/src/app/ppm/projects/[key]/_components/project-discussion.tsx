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
  ProjectDetailsDto,
} from '@/src/services/wayd-api'
import { DiscussionChannelTypes } from '@/src/store/features/common/discussions-api'
import { useGetEmployeesQuery } from '@/src/store/features/organizations/employee-api'

const audience = {
  [DiscussionMessageKind.Public]: 'Seen by everyone who can see this project',
}

const candidate = (person: EmployeeNavigationDto): MentionCandidate => ({
  id: person.id,
  name: person.name,
  key: person.key,
})

/** Each person once, by name, as the full employee list comes. */
const byName = (people: MentionCandidate[]) =>
  [...new Map(people.map((p) => [p.id, p])).values()].sort((a, b) =>
    caseInsensitiveCompare(a.name, b.name),
  )

export interface ProjectDiscussionProps {
  project: ProjectDetailsDto
}

/**
 * The project's conversation. Anyone may be mentioned, since a mention only notifies; the list is every active
 * employee for a reader who may see them, and the project's own people otherwise.
 */
const ProjectDiscussion = ({ project }: ProjectDiscussionProps) => {
  const { hasPermissionClaim } = useAuth()
  const canViewEmployees = hasPermissionClaim('Permissions.Employees.View')
  const { data: employees } = useGetEmployeesQuery(false, {
    skip: !canViewEmployees,
  })

  const projectPeople = byName(
    [
      ...project.projectSponsors,
      ...project.projectOwners,
      ...project.projectManagers,
      ...project.projectMembers,
    ].map(candidate),
  )
  const mentionCandidates =
    canViewEmployees && employees
      ? employees.map((e) => ({ id: e.id, name: e.displayName, key: e.key }))
      : projectPeople

  return (
    <RecordDiscussion
      channelType={DiscussionChannelTypes.Project}
      ownerId={project.id}
      mentionCandidates={mentionCandidates}
      audience={audience}
    />
  )
}

export default ProjectDiscussion
