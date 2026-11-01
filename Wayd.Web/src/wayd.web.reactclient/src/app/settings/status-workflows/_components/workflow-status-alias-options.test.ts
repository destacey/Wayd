import {
  StatusCategory,
  WorkflowAliasDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import {
  aliasFitsCategory,
  buildAliasOptions,
} from './workflow-status-alias-options'

const ALIASES: WorkflowAliasDto[] = [
  { value: 1, name: 'Ready', isRequired: true, category: StatusCategory.Done },
  {
    value: 2,
    name: 'Released',
    isRequired: true,
    category: StatusCategory.Done,
  },
  {
    value: 3,
    name: 'Withdrawn',
    isRequired: false,
    category: StatusCategory.Done,
  },
  {
    value: 4,
    name: 'Started',
    isRequired: false,
    category: StatusCategory.Active,
  },
]

const DONE = StatusCategory.Done

const status = (id: string, alias: number): WorkflowStatusDto => ({
  id,
  name: `Status ${id}`,
  category: { id: 2, name: 'Active' },
  alias,
  order: 1,
})

describe('buildAliasOptions', () => {
  it('always offers None first', () => {
    // Arrange / Act
    const options = buildAliasOptions(ALIASES, [], DONE)

    // Assert
    expect(options[0]).toEqual({ value: 0, label: 'None' })
  })

  it('drops an alias another status already holds', () => {
    // Arrange — only one status may carry a given meaning, so offering a taken
    // one would build a workflow the server refuses.
    const statuses = [status('a', 2)]

    // Act
    const options = buildAliasOptions(ALIASES, statuses, DONE)

    // Assert
    expect(options.map((o) => o.value)).toEqual([0, 1, 3])
  })

  it('keeps the alias of the status being edited', () => {
    // Arrange
    const statuses = [status('a', 2), status('b', 1)]

    // Act
    const options = buildAliasOptions(ALIASES, statuses, DONE, 'a')

    // Assert — 'a' keeps Released; 'b' still blocks Ready
    expect(options.map((o) => o.value)).toEqual([0, 2, 3])
  })

  it('marks required meanings so the publish rule is visible while choosing', () => {
    // Arrange / Act
    const options = buildAliasOptions(ALIASES, [], DONE)

    // Assert
    expect(options.map((o) => o.label)).toEqual([
      'None',
      'Ready (required)',
      'Released (required)',
      'Withdrawn',
    ])
  })

  it('tolerates a workflow with no statuses or an owner type with no aliases', () => {
    // Arrange / Act / Assert
    expect(buildAliasOptions(undefined, undefined, undefined)).toEqual([
      { value: 0, label: 'None' },
    ])
  })
})

describe('buildAliasOptions by category', () => {
  it("offers only the chosen category's aliases", () => {
    // Arrange / Act — each alias belongs to one category, and the server
    // refuses a status that pairs them otherwise
    const options = buildAliasOptions(ALIASES, [], StatusCategory.Active)

    // Assert
    expect(options.map((o) => o.value)).toEqual([0, 4])
  })

  it("keeps the edited status's own alias when it is filed under the wrong category, saying where it belongs", () => {
    // Arrange — a draft cloned from a workflow saved before aliases were tied
    // to categories can hold Started (Active) under Done
    const statuses = [status('a', 4)]

    // Act
    const options = buildAliasOptions(ALIASES, statuses, DONE, 'a')

    // Assert
    expect(options).toContainEqual({
      value: 4,
      label: 'Started (belongs under Active)',
    })
  })

  it('says where a misfiled alias belongs by the owner type label', () => {
    // Arrange
    const statuses = [status('a', 4)]
    const labels = [{ category: StatusCategory.Active, label: 'Underway' }]

    // Act
    const options = buildAliasOptions(ALIASES, statuses, DONE, 'a', labels)

    // Assert
    expect(options).toContainEqual({
      value: 4,
      label: 'Started (belongs under Underway)',
    })
  })

  it("keeps the edited status's own alias as an unknown meaning when the owner type no longer declares it", () => {
    // Arrange
    const statuses = [status('a', 6)]

    // Act
    const options = buildAliasOptions(ALIASES, statuses, DONE, 'a')

    // Assert
    expect(options).toContainEqual({ value: 6, label: 'Unknown meaning (6)' })
  })

  it('offers only None until a category is chosen', () => {
    // Arrange / Act
    const options = buildAliasOptions(ALIASES, [], undefined)

    // Assert
    expect(options).toEqual([{ value: 0, label: 'None' }])
  })
})

describe('aliasFitsCategory', () => {
  it('fits no alias to any category', () => {
    // Arrange / Act / Assert
    expect(aliasFitsCategory(ALIASES, 0, StatusCategory.Proposed)).toBe(true)
    expect(aliasFitsCategory(ALIASES, undefined, StatusCategory.Removed)).toBe(
      true,
    )
  })

  it('fits an alias only to its own category', () => {
    // Arrange / Act / Assert
    expect(aliasFitsCategory(ALIASES, 4, StatusCategory.Active)).toBe(true)
    expect(aliasFitsCategory(ALIASES, 4, StatusCategory.Done)).toBe(false)
  })
})
