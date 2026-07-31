import { Button, Card, Space } from 'antd'
import useTheme from '../../components/contexts/theme'

const ThemeTest = () => {
  const { currentTheme, currentMode, token } = useTheme()

  return (
    <Card title="Theme Test">
      <Space vertical>
        <p>Current Theme: {currentTheme}</p>
        <p>Current Mode: {currentMode}</p>
        <p>Primary Color: {token.colorPrimary}</p>
        <p>Border Radius: {token.borderRadius}px</p>
        <p>Wireframe: {String(token.wireframe)}</p>
        <Button type="primary">Test Button</Button>
      </Space>
    </Card>
  )
}

export default ThemeTest
