import { Col, Flex, Grid, Row, Typography } from 'antd'
import { ReactNode } from 'react'

const { Title, Text } = Typography
const { useBreakpoint } = Grid

export interface PageTitleProps {
  title: string | ReactNode
  subtitle?: string
  tags?: ReactNode | null
  actions?: ReactNode | null
  extra?: ReactNode | null
}

// TODO: align actions to the right/end when not the xs or sm breakpoint
const PageTitle = ({
  title,
  subtitle,
  tags,
  actions,
  extra,
}: PageTitleProps) => {
  const screens = useBreakpoint()
  const isSuperSmall = !screens.sm // xs screens (< 576px)
  const titleMdSize = actions ? 16 : 24

  return (
    <>
      <Flex vertical gap={8} style={{ marginBottom: 12 }}>
        <Row align={'middle'} gutter={[0, 8]}>
          <Col xs={24} sm={24} md={titleMdSize}>
            <Flex vertical={isSuperSmall} gap={isSuperSmall ? 8 : 12} align={isSuperSmall ? 'flex-start' : 'center'}>
              <div>
                <Title level={2} style={{ margin: '0px', fontWeight: '400' }}>
                  {title}
                </Title>
                {subtitle && <Text>{subtitle}</Text>}
              </div>
              {tags && <div>{tags}</div>}
            </Flex>
          </Col>
          {actions && (
            <Col xs={24} sm={24} md={8}>
              <Flex
                wrap
                gap={8}
                justify={isSuperSmall ? 'flex-start' : 'flex-end'}
                align="center"
              >
                {actions}
              </Flex>
            </Col>
          )}
        </Row>
        {extra && <Row>{extra}</Row>}
      </Flex>
    </>
  )
}

export default PageTitle
