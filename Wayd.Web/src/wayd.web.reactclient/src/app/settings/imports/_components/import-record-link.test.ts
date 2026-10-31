import { ImportRecordType } from '@/src/services/wayd-api'
import {
  importRecordHref,
  importRecordLabel,
  importRecordText,
} from './import-record-link'

describe('importRecordHref', () => {
  it('links a record to its own page by key', () => {
    // Arrange
    const link = {
      type: ImportRecordType.Project,
      key: 'APOLLO',
      name: 'Apollo',
    }

    // Act
    const href = importRecordHref(link)

    // Assert
    expect(href).toBe('/ppm/projects/APOLLO')
  })

  it('links a team of teams to the team-of-teams page, not the team page', () => {
    // Arrange
    const link = { type: ImportRecordType.TeamOfTeams, key: '7' }

    // Act
    const href = importRecordHref(link)

    // Assert
    expect(href).toBe('/organizations/team-of-teams/7')
  })

  it('places an objective under its planning interval', () => {
    // Arrange
    const link = {
      type: ImportRecordType.PlanningIntervalObjective,
      key: '12',
      parentKey: '3',
    }

    // Act
    const href = importRecordHref(link)

    // Assert
    expect(href).toBe('/planning/planning-intervals/3/objectives/12')
  })

  it('gives no link for an objective whose planning interval is unknown', () => {
    // Arrange
    const link = { type: ImportRecordType.PlanningIntervalObjective, key: '12' }

    // Act
    const href = importRecordHref(link)

    // Assert
    expect(href).toBeUndefined()
  })

  it('sends an environment to the environments list', () => {
    // Arrange
    const link = { type: ImportRecordType.DeploymentEnvironment, key: '4' }

    // Act
    const href = importRecordHref(link)

    // Assert
    expect(href).toBe('/product-management/environments')
  })

  it('has a route for every record type', () => {
    // Arrange
    const types = Object.values(ImportRecordType)

    // Act
    const hrefs = types.map((type) =>
      importRecordHref({ type, key: '1', parentKey: '1' }),
    )

    // Assert
    expect(hrefs.every((href) => href?.startsWith('/'))).toBe(true)
  })
})

describe('importRecordLabel', () => {
  it('shows the key and the name', () => {
    // Arrange
    const link = {
      type: ImportRecordType.Risk,
      key: '42',
      name: 'Vendor delay',
    }

    // Act
    const label = importRecordLabel(link)

    // Assert
    expect(label).toBe('42 - Vendor delay')
  })

  it('shows the key alone when there is no name', () => {
    // Arrange
    const link = { type: ImportRecordType.Deployment, key: '9' }

    // Act
    const label = importRecordLabel(link)

    // Assert
    expect(label).toBe('9')
  })
})

describe('importRecordText', () => {
  it('labels a linked record', () => {
    // Arrange
    const row = {
      createdEntityId: 'a1',
      createdRecord: {
        type: ImportRecordType.Team,
        key: '7',
        name: 'Platform',
      },
    }

    // Act
    const text = importRecordText(row)

    // Assert
    expect(text).toBe('7 - Platform')
  })

  it('says a created record with no link no longer exists', () => {
    // Arrange
    const row = { createdEntityId: 'a1' }

    // Act
    const text = importRecordText(row)

    // Assert
    expect(text).toBe('No longer exists')
  })

  it('is empty for a row that created nothing', () => {
    // Arrange
    const row = {}

    // Act
    const text = importRecordText(row)

    // Assert
    expect(text).toBe('')
  })
})
