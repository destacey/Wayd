import { TeamNavigationDto } from '@/src/services/wayd-api'

export interface TeamListItem extends TeamNavigationDto {
  code?: string
  isActive?: boolean
  teamOfTeams?: TeamNavigationDto | undefined
}

export interface CreateTeamFormValues {
  type: TeamTypeName
  name: string
  code: string
  description: string
  activeDate: Date
}

export interface EditTeamFormValues {
  id: string
  key: number
  name: string
  code: string
  description: string
  type?: TeamTypeName
}

export type TeamTypeName = 'Team' | 'Team of Teams'
