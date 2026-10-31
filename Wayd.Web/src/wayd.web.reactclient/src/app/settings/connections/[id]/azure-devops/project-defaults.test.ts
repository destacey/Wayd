import { suggestWorkspaceKey, workItemUrlTemplate } from './project-defaults'

describe('project-defaults', () => {
  describe('suggestWorkspaceKey', () => {
    it.each([
      ['Wayd', 'WAYD'],
      ['Mobile App', 'MOBILEAPP'],
      ['my-project_2', 'MYPROJECT2'],
      ['2024 Roadmap', 'ROADMAP'],
      ['Customer Onboarding Portal', 'CUSTOMERON'],
    ])('suggests a valid key for "%s"', (projectName, expected) => {
      // Arrange
      const name = projectName

      // Act
      const key = suggestWorkspaceKey(name)

      // Assert
      expect(key).toBe(expected)
      expect(key).toMatch(/^[A-Z][A-Z0-9]{1,19}$/)
    })

    it.each(['X', '123', '---', ''])(
      'suggests nothing for "%s", which has too few usable characters',
      (projectName) => {
        // Arrange
        const name = projectName

        // Act
        const key = suggestWorkspaceKey(name)

        // Assert
        expect(key).toBe('')
      },
    )
  })

  describe('workItemUrlTemplate', () => {
    it('builds the work item link, encoding the project name', () => {
      // Arrange
      const organizationUrl = 'https://dev.azure.com/acme/'

      // Act
      const template = workItemUrlTemplate(organizationUrl, 'Mobile App')

      // Assert
      expect(template).toBe(
        'https://dev.azure.com/acme/Mobile%20App/_workitems/edit/',
      )
    })
  })
})
