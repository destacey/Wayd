import { teamUrl } from './team-url'

describe('teamUrl', () => {
  it('links a Team to the teams route', () => {
    // Arrange / Act / Assert
    expect(teamUrl({ key: 12, type: 'Team' })).toBe('/organizations/teams/12')
  })

  it('links a Team of Teams to the team-of-teams route', () => {
    // Arrange / Act / Assert
    expect(teamUrl({ key: 5, type: 'TeamOfTeams' })).toBe(
      '/organizations/team-of-teams/5',
    )
  })

  it('treats a missing type as a team of teams (only Team is a team)', () => {
    // Arrange / Act / Assert — matches the existing behavior: type !== 'Team'
    // falls through to the team-of-teams route.
    expect(teamUrl({ key: 7 })).toBe('/organizations/team-of-teams/7')
  })

  it('accepts a string key', () => {
    // Arrange / Act / Assert
    expect(teamUrl({ key: 'abc', type: 'Team' })).toBe(
      '/organizations/teams/abc',
    )
  })
})
