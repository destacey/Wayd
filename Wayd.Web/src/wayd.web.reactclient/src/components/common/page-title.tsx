import { Col, Flex, Grid, Row, Typography } from 'antd'
import { ReactNode } from 'react'
import RecordAvatar, { RecordAvatarProps } from './record-avatar'
import RecordKey from './record-key'

const { Title, Text } = Typography
const { useBreakpoint } = Grid

export interface PageTitleProps {
  title: string | ReactNode
  subtitle?: string
  tags?: ReactNode | null
  actions?: ReactNode | null
  extra?: ReactNode | null
  /**
   * The record's identifier, rendered as its own chip before the title.
   * Pass this rather than building `${key} - ${name}` — a concatenated key
   * cannot be styled, copied cleanly, or aligned across records whose keys
   * differ in length.
   *
   * For teams and team-of-teams this is the `code`, which is what people say
   * out loud; the numeric key belongs in the record's facts instead.
   */
  recordKey?: string
  /** Leading glyph. A circle for people, a rounded square for everything else. */
  avatar?: RecordAvatarProps
}

// TODO: align actions to the right/end when not the xs or sm breakpoint
const PageTitle = ({
  title,
  subtitle,
  tags,
  actions,
  extra,
  recordKey,
  avatar,
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
              <Flex gap={10} align="center" style={{ minWidth: 0 }}>
                {avatar && <RecordAvatar {...avatar} />}
                {recordKey && <RecordKey value={recordKey} />}
                <div style={{ minWidth: 0 }}>
                  <Title level={2} style={{ margin: '0px', fontWeight: '400' }}>
                    {title}
                  </Title>
                  {subtitle && <Text>{subtitle}</Text>}
                </div>
              </Flex>
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
