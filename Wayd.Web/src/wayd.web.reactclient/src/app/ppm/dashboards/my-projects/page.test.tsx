import { render } from '@testing-library/react'
import MyProjectsRedirectPage from './page'

const mockReplace = jest.fn()

jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockReplace }),
}))

describe('MyProjectsRedirectPage', () => {
  it('forwards the old My Projects address to the Projects Dashboard', () => {
    // Arrange / Act
    const { container } = render(<MyProjectsRedirectPage />)

    // Assert
    expect(mockReplace).toHaveBeenCalledWith('/ppm/dashboards/projects')
    expect(container).toBeEmptyDOMElement()
  })
})
