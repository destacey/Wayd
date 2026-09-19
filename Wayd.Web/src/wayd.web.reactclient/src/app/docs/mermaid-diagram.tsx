'use client'

import { useEffect, useId, useState } from 'react'
import { Skeleton } from 'antd'
import mermaid, { type MermaidConfig } from 'mermaid'
import useTheme from '@/src/components/contexts/theme'

// mermaid.render(id) starts by removing any element with that id, and the SVG
// it returns carries the same id — so reusing an id deletes the diagram already
// on screen, and React never restores it because the new svg string is equal.
let renderSequence = 0

type RenderState =
  | { status: 'loading' }
  | { status: 'failed' }
  | { status: 'ready'; svg: string }

interface MermaidDiagramProps {
  chart: string
}

export default function MermaidDiagram({ chart }: MermaidDiagramProps) {
  const id = useId().replace(/:/g, '-')
  const { currentMode, token } = useTheme()
  const [state, setState] = useState<RenderState>({ status: 'loading' })

  // `base` is the only mermaid theme that honours themeVariables. Serialized so
  // the effect re-runs when a color actually changes, not on every new token object.
  const config = JSON.stringify({
    startOnLoad: false,
    securityLevel: 'strict',
    theme: 'base',
    themeVariables: {
      darkMode: currentMode !== 'light',
      background: token.colorBgContainer,
      fontFamily: token.fontFamily,
      fontSize: `${token.fontSize}px`,
      textColor: token.colorText,
      primaryColor: token.colorPrimaryBg,
      primaryBorderColor: token.colorPrimaryBorder,
      primaryTextColor: token.colorText,
      secondaryColor: token.colorBgContainer,
      secondaryBorderColor: token.colorBorder,
      secondaryTextColor: token.colorText,
      tertiaryColor: token.colorBgLayout,
      tertiaryBorderColor: token.colorBorder,
      tertiaryTextColor: token.colorText,
      clusterBkg: token.colorFillQuaternary,
      clusterBorder: token.colorBorder,
      titleColor: token.colorTextHeading,
      lineColor: token.colorTextTertiary,
      edgeLabelBackground: token.colorBgContainer,
      noteBkgColor: token.colorWarningBg,
      noteBorderColor: token.colorWarningBorder,
      noteTextColor: token.colorText,
    },
  } satisfies MermaidConfig)

  useEffect(() => {
    let cancelled = false
    const render = async () => {
      try {
        mermaid.initialize(JSON.parse(config))
        const { svg } = await mermaid.render(
          `mermaid-${id}-${++renderSequence}`,
          chart,
        )
        if (!cancelled) setState({ status: 'ready', svg })
      } catch {
        if (!cancelled) setState({ status: 'failed' })
      }
    }
    render()
    return () => {
      cancelled = true
    }
  }, [chart, id, config])

  if (state.status === 'loading') {
    return (
      <div className="docs-mermaid">
        <Skeleton.Node active className="docs-mermaid-placeholder" />
      </div>
    )
  }

  if (state.status === 'failed') {
    return (
      <pre>
        <code>{chart}</code>
      </pre>
    )
  }

  return (
    <div
      className="docs-mermaid"
      dangerouslySetInnerHTML={{ __html: state.svg }}
    />
  )
}
