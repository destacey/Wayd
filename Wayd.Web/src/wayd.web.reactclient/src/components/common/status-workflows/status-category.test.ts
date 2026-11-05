import { StatusCategory } from '@/src/services/wayd-api'
import {
  statusCategoryDescription,
  statusCategoryFromId,
  statusCategoryLabel,
  statusCategoryOptions,
} from './status-category'

describe('statusCategoryDescription', () => {
  it('describes every category', () => {
    // A category with no description leaves a tooltip empty, which reads as a broken control rather
    // than as nothing to say.
    // Arrange / Act / Assert
    for (const category of Object.values(StatusCategory)) {
      expect(statusCategoryDescription({ category })).not.toBe('')
    }
  })

  it('distinguishes done from removed', () => {
    // The two terminal categories are the pair a reader most needs told apart: both mean the record
    // has stopped moving, and only one means it succeeded.
    // Arrange / Act
    const done = statusCategoryDescription({ category: StatusCategory.Done })
    const removed = statusCategoryDescription({
      category: StatusCategory.Removed,
    })

    // Assert
    expect(done).not.toBe(removed)
    expect(removed).toMatch(/not completed/i)
  })

  it("names the category by the owner type's label, keeping the category's meaning", () => {
    // Arrange / Act
    const description = statusCategoryDescription({
      category: StatusCategory.Done,
      categoryLabel: 'Shipped',
    })

    // Assert
    expect(description).toBe('Shipped — completed successfully.')
  })

  it('falls back to the category name when there is no label', () => {
    // Arrange / Act
    const description = statusCategoryDescription({
      category: StatusCategory.Proposed,
      categoryLabel: '',
    })

    // Assert
    expect(description).toBe('Proposed — not started yet.')
  })
})

describe('statusCategoryFromId', () => {
  it('maps each numeric id to its category', () => {
    // Arrange / Act / Assert
    expect([0, 1, 2, 3].map(statusCategoryFromId)).toEqual([
      StatusCategory.Proposed,
      StatusCategory.Active,
      StatusCategory.Done,
      StatusCategory.Removed,
    ])
    expect(statusCategoryFromId(undefined)).toBeUndefined()
  })
})

describe('statusCategoryLabel', () => {
  it('uses the declared label, else the category name', () => {
    // Arrange
    const labels = [{ category: StatusCategory.Removed, label: 'Shelved' }]

    // Act / Assert
    expect(statusCategoryLabel(labels, StatusCategory.Removed)).toBe('Shelved')
    expect(statusCategoryLabel(labels, StatusCategory.Done)).toBe('Done')
    expect(statusCategoryLabel(undefined, StatusCategory.Active)).toBe('Active')
  })
})

describe('statusCategoryOptions', () => {
  it('offers every category in order, labelled, with the category as the value', () => {
    // Arrange
    const labels = [{ category: StatusCategory.Proposed, label: 'Planned' }]

    // Act
    const options = statusCategoryOptions(labels)

    // Assert
    expect(options).toEqual([
      { value: StatusCategory.Proposed, label: 'Planned' },
      { value: StatusCategory.Active, label: 'Active' },
      { value: StatusCategory.Done, label: 'Done' },
      { value: StatusCategory.Removed, label: 'Removed' },
    ])
  })
})
