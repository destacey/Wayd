import { ProjectListDto } from '@/src/services/wayd-api'
import { getProjectsNeedingAttention } from './projects-needing-attention'

interface ProjectShape {
  id: string
  name: string
  health?: string | null
  lifecycleCategory?: string
  program?: string | null
  position?: number
}

const buildProject = ({
  id,
  name,
  health = 'Unhealthy',
  lifecycleCategory = 'Active',
  program = null,
  position,
}: ProjectShape): ProjectListDto =>
  ({
    id,
    key: id.toUpperCase(),
    name,
    status: { id: 2, name: 'Active', lifecycleCategory },
    healthCheck: health ? { id: `hc-${id}`, status: { id: 1, name: health } } : undefined,
    program: program ? { id: `pg-${program}`, key: 1, name: program } : undefined,
    position,
    strategicThemes: [],
  }) as unknown as ProjectListDto

const names = (projects: ProjectListDto[]) => projects.map((p) => p.name)

describe('getProjectsNeedingAttention', () => {
  describe('which projects are flagged', () => {
    it('keeps only At Risk and Unhealthy projects', () => {
      // Arrange
      const projects = [
        buildProject({ id: 'a', name: 'Alpha', health: 'Healthy' }),
        buildProject({ id: 'b', name: 'Bravo', health: 'At Risk' }),
        buildProject({ id: 'c', name: 'Charlie', health: 'Unhealthy' }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'name')

      // Assert
      expect(names(result)).toEqual(['Bravo', 'Charlie'])
    })

    it('drops projects with no health check', () => {
      // Arrange — nobody has reported on it, so there is nothing to act on.
      const projects = [
        buildProject({ id: 'a', name: 'Alpha', health: null }),
        buildProject({ id: 'b', name: 'Bravo', health: 'Unhealthy' }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'name')

      // Assert
      expect(names(result)).toEqual(['Bravo'])
    })

    it('drops completed and canceled projects however unhealthy', () => {
      // Arrange — their health describes work that is already over.
      const projects = [
        buildProject({
          id: 'a',
          name: 'Alpha',
          lifecycleCategory: 'Completed',
        }),
        buildProject({
          id: 'b',
          name: 'Bravo',
          lifecycleCategory: 'Canceled',
        }),
        buildProject({ id: 'c', name: 'Charlie' }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'name')

      // Assert
      expect(names(result)).toEqual(['Charlie'])
    })
  })

  describe('ordering', () => {
    it('puts Unhealthy before At Risk by health', () => {
      // Arrange
      const projects = [
        buildProject({ id: 'a', name: 'Alpha', health: 'At Risk' }),
        buildProject({ id: 'b', name: 'Bravo', health: 'Unhealthy' }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'health')

      // Assert
      expect(names(result)).toEqual(['Bravo', 'Alpha'])
    })

    it('sorts by name case-insensitively', () => {
      // Arrange
      const projects = [
        buildProject({ id: 'a', name: 'zeta' }),
        buildProject({ id: 'b', name: 'Alpha' }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'name')

      // Assert
      expect(names(result)).toEqual(['Alpha', 'zeta'])
    })

    it('breaks ties on health whatever the sort mode', () => {
      // Arrange — same program, so health decides which is more urgent.
      const projects = [
        buildProject({
          id: 'a',
          name: 'Alpha',
          health: 'At Risk',
          program: 'Payments',
        }),
        buildProject({
          id: 'b',
          name: 'Bravo',
          health: 'Unhealthy',
          program: 'Payments',
        }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'program')

      // Assert
      expect(names(result)).toEqual(['Bravo', 'Alpha'])
    })

    it('orders by portfolio rank', () => {
      // Arrange
      const projects = [
        buildProject({ id: 'a', name: 'Alpha', position: 3 }),
        buildProject({ id: 'b', name: 'Bravo', position: 1 }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'rank')

      // Assert
      expect(names(result)).toEqual(['Bravo', 'Alpha'])
    })

    it('sorts unranked projects last', () => {
      // Arrange — a missing position must not read as rank zero.
      const projects = [
        buildProject({ id: 'a', name: 'Alpha', position: undefined }),
        buildProject({ id: 'b', name: 'Bravo', position: 9 }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'rank')

      // Assert
      expect(names(result)).toEqual(['Bravo', 'Alpha'])
    })

    it('groups by program name', () => {
      // Arrange
      const projects = [
        buildProject({ id: 'a', name: 'Alpha', program: 'Zebra' }),
        buildProject({ id: 'b', name: 'Bravo', program: 'Apollo' }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'program')

      // Assert
      expect(names(result)).toEqual(['Bravo', 'Alpha'])
    })

    it('sorts projects with no program last', () => {
      // Arrange — a project held directly by the portfolio has no program to
      // group under, so it follows the grouped ones.
      const projects = [
        buildProject({ id: 'a', name: 'Alpha', program: null }),
        buildProject({ id: 'b', name: 'Bravo', program: 'Zebra' }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'program')

      // Assert
      expect(names(result)).toEqual(['Bravo', 'Alpha'])
    })

    it('orders equally urgent projects by name, so the list is stable', () => {
      // Arrange
      const projects = [
        buildProject({ id: 'a', name: 'Charlie' }),
        buildProject({ id: 'b', name: 'Alpha' }),
        buildProject({ id: 'c', name: 'Bravo' }),
      ]

      // Act
      const result = getProjectsNeedingAttention(projects, 'health')

      // Assert
      expect(names(result)).toEqual(['Alpha', 'Bravo', 'Charlie'])
    })
  })

  it('returns nothing when no project needs attention', () => {
    // Arrange
    const projects = [
      buildProject({ id: 'a', name: 'Alpha', health: 'Healthy' }),
    ]

    // Act
    const result = getProjectsNeedingAttention(projects, 'health')

    // Assert
    expect(result).toEqual([])
  })
})
