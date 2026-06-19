'use client'

import React, { FC } from 'react'
import { Flex, Grid } from 'antd'

const { useBreakpoint } = Grid

interface ResponsiveFlexProps {
  children: React.ReactNode
  gap?: string
  align?: string
}

const ResponsiveFlex: FC<ResponsiveFlexProps> = ({
  children,
  gap = 'middle',
  align = 'start',
}: ResponsiveFlexProps) => {
  const screens = useBreakpoint()
  const isSmallScreen = !screens.md

  return (
    <Flex gap={gap} align={align} vertical={isSmallScreen}>
      {isSmallScreen
        ? children
        : React.Children.map(children, (child) =>
            // On horizontal layout, give each child an equal-width, shrinkable
            // basis. Without `minWidth: 0`, flex items (e.g. antd Descriptions'
            // table) refuse to shrink below their content and collapse to a
            // single character per line.
            child == null ? (
              child
            ) : (
              <div style={{ flex: 1, minWidth: 0 }}>{child}</div>
            ),
          )}
    </Flex>
  )
}

export default ResponsiveFlex
