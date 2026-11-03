'use client'

import {
  PlanningTeamNavigationDto,
  StoryMapDetailsDto,
  StoryMapEditAccess,
  Visibility,
} from '@/src/services/wayd-api'
import {
  EmployeeRow,
  PeopleSection,
  TeamMembersTag,
} from '@/src/components/common/planning/record-people'
import { useGetStoryMapEditorTeamMembersQuery } from '@/src/store/features/planning/story-maps-api'
import { Drawer, Flex, Typography } from 'antd'
import { FC, useState } from 'react'

const { Text } = Typography

export interface StoryMapPeopleDrawerProps {
  map: StoryMapDetailsDto
  open: boolean
  onClose: () => void
}

/** An editor team as one tag; opening it lists who the team brings in. */
const EditorTeam: FC<{
  storyMapId: string
  team: PlanningTeamNavigationDto
}> = ({ storyMapId, team }) => {
  const [expanded, setExpanded] = useState(false)
  const {
    data: members,
    isFetching,
    isError,
  } = useGetStoryMapEditorTeamMembersQuery(
    { storyMapId, teamId: team.id },
    { skip: !expanded },
  )

  return (
    <TeamMembersTag
      team={team}
      expanded={expanded}
      onToggle={() => setExpanded((e) => !e)}
      members={members}
      isFetching={isFetching}
      isError={isError}
      note="Members of its team of teams can edit too."
    />
  )
}

/** The sentence that says who can see and who can change the map. */
export const accessSummary = (map: StoryMapDetailsDto): string => {
  if (map.visibility === Visibility.Private)
    return 'Private: only owners and editors can see and edit this map.'
  return map.editAccess === StoryMapEditAccess.OwnersAndEditors
    ? 'Public: anyone who can view Story Maps can see this map; only owners and editors can edit it.'
    : 'Public: anyone who can view Story Maps can see and edit this map.'
}

/** Who owns the map, who can edit it, and who can see it. */
const StoryMapPeopleDrawer: FC<StoryMapPeopleDrawerProps> = ({
  map,
  open,
  onClose,
}) => (
  <Drawer title="People" open={open} onClose={onClose} size="default">
    <Flex vertical gap={24}>
      <PeopleSection title="Owners">
        {map.owners.length === 0 ? (
          <Text type="secondary">
            No owners. A Story Maps administrator can manage this map.
          </Text>
        ) : (
          map.owners.map((o) => <EmployeeRow key={o.id} employee={o} />)
        )}
      </PeopleSection>

      <PeopleSection title="Editors">
        {map.editors.length === 0 && map.editorTeams.length === 0 ? (
          <Text type="secondary">No one is invited to edit.</Text>
        ) : (
          <>
            {map.editors.map((e) => (
              <EmployeeRow key={e.id} employee={e} />
            ))}
            {map.editorTeams.map((t) => (
              <EditorTeam key={t.id} storyMapId={map.id} team={t} />
            ))}
          </>
        )}
      </PeopleSection>

      <PeopleSection title="Access">
        <Text type="secondary">{accessSummary(map)}</Text>
      </PeopleSection>
    </Flex>
  </Drawer>
)

export default StoryMapPeopleDrawer
