'use client'
'use no memo'
/* eslint-disable react-hooks/exhaustive-deps, react-hooks/refs, react-compiler/react-compiler */

import { useEffect, useRef, useState } from 'react'
import styles from './diagnostic-canvas.module.css'

const CELL = 16
const COLS = 20
const ROWS = 20
const WIDTH = COLS * CELL
const HEIGHT = ROWS * CELL

const COLORS = {
  bg: '#0d0d0d',
  grid: '#111',
  head: '#4ade80',
  body: '#22c55e',
  tail: '#166534',
  food: '#f87171',
  foodGlow: 'rgba(248,113,113,0.3)',
  dead: '#ef4444',
}

interface Point {
  x: number
  y: number
}

interface Dir {
  x: number
  y: number
}

const ARROW_DIRS: Record<string, Dir> = {
  ArrowUp: { x: 0, y: -1 },
  ArrowDown: { x: 0, y: 1 },
  ArrowLeft: { x: -1, y: 0 },
  ArrowRight: { x: 1, y: 0 },
}

const DPAD_DIRS: Record<string, Dir> = {
  UP: { x: 0, y: -1 },
  DOWN: { x: 0, y: 1 },
  LEFT: { x: -1, y: 0 },
  RIGHT: { x: 1, y: 0 },
}

function lerp(a: string, b: string, t: number): string {
  const ah = parseInt(a.slice(1), 16)
  const bh = parseInt(b.slice(1), 16)
  const ar = (ah >> 16) & 0xff, ag = (ah >> 8) & 0xff, ab = ah & 0xff
  const br = (bh >> 16) & 0xff, bg = (bh >> 8) & 0xff, bb = bh & 0xff
  return `rgb(${Math.round(ar + (br - ar) * t)},${Math.round(ag + (bg - ag) * t)},${Math.round(ab + (bb - ab) * t)})`
}

function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  ctx.beginPath()
  ctx.moveTo(x + r, y)
  ctx.lineTo(x + w - r, y)
  ctx.arcTo(x + w, y, x + w, y + r, r)
  ctx.lineTo(x + w, y + h - r)
  ctx.arcTo(x + w, y + h, x + w - r, y + h, r)
  ctx.lineTo(x + r, y + h)
  ctx.arcTo(x, y + h, x, y + h - r, r)
  ctx.lineTo(x, y + r)
  ctx.arcTo(x, y, x + r, y, r)
  ctx.closePath()
  ctx.fill()
}

function getEyes(dir: Dir): [number, number][] {
  if (dir.x === 1) return [[11, 4], [11, 12]]
  if (dir.x === -1) return [[5, 4], [5, 12]]
  if (dir.y === -1) return [[4, 5], [12, 5]]
  return [[4, 11], [12, 11]]
}

function spawnFood(snake: Point[]): Point {
  let p: Point
  do {
    p = { x: Math.floor(Math.random() * COLS), y: Math.floor(Math.random() * ROWS) }
  } while (snake.some((s) => s.x === p.x && s.y === p.y))
  return p
}

let audioCtx: AudioContext | null = null
function getAudio(): AudioContext {
  if (!audioCtx) audioCtx = new AudioContext()
  return audioCtx
}

function beep(opts: { freq: number; type?: OscillatorType; gain?: number; duration?: number; freqEnd?: number; slide?: boolean }) {
  try {
    const ac = getAudio()
    const osc = ac.createOscillator()
    const vol = ac.createGain()
    osc.connect(vol)
    vol.connect(ac.destination)
    osc.type = opts.type ?? 'square'
    osc.frequency.setValueAtTime(opts.freq, ac.currentTime)
    const duration = opts.duration ?? 0.08
    if (opts.slide && opts.freqEnd) osc.frequency.linearRampToValueAtTime(opts.freqEnd, ac.currentTime + duration)
    vol.gain.setValueAtTime(opts.gain ?? 0.15, ac.currentTime)
    vol.gain.exponentialRampToValueAtTime(0.001, ac.currentTime + duration)
    osc.start(ac.currentTime)
    osc.stop(ac.currentTime + duration)
  } catch { /* ignore audio errors */ }
}

interface DiagnosticCanvasProps {
  onClose: () => void
}

export default function DiagnosticCanvas({ onClose }: DiagnosticCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const diagnosticRef = useRef({
    snake: [{ x: 10, y: 10 }, { x: 9, y: 10 }, { x: 8, y: 10 }] as Point[],
    dir: { x: 1, y: 0 } as Dir,
    nextDir: { x: 1, y: 0 } as Dir,
    food: { x: 15, y: 10 } as Point,
    score: 0,
    best: 0,
    speed: 140,
    running: false,
    finished: false,
    loop: undefined as ReturnType<typeof setTimeout> | undefined,
  })

  const [, forceRender] = useState(0)
  const rerender = () => forceRender((n) => n + 1)

  const draw = (dead = false) => {
    const ctx = canvasRef.current?.getContext('2d')
    if (!ctx) return
    const g = diagnosticRef.current

    ctx.fillStyle = COLORS.bg
    ctx.fillRect(0, 0, WIDTH, HEIGHT)

    ctx.strokeStyle = COLORS.grid
    ctx.lineWidth = 0.5
    for (let x = 0; x <= COLS; x++) { ctx.beginPath(); ctx.moveTo(x * CELL, 0); ctx.lineTo(x * CELL, ROWS * CELL); ctx.stroke() }
    for (let y = 0; y <= ROWS; y++) { ctx.beginPath(); ctx.moveTo(0, y * CELL); ctx.lineTo(COLS * CELL, y * CELL); ctx.stroke() }

    ctx.fillStyle = COLORS.foodGlow
    ctx.beginPath()
    ctx.arc(g.food.x * CELL + CELL / 2, g.food.y * CELL + CELL / 2, CELL, 0, Math.PI * 2)
    ctx.fill()
    ctx.fillStyle = COLORS.food
    roundRect(ctx, g.food.x * CELL + 2, g.food.y * CELL + 2, CELL - 4, CELL - 4, 3)

    if (dead) {
      ctx.fillStyle = 'rgba(239,68,68,0.1)'
      ctx.fillRect(0, 0, WIDTH, HEIGHT)
      g.snake.forEach((s, i) => {
        ctx.fillStyle = i === 0 ? COLORS.dead : `rgba(239,68,68,${Math.max(0.1, 0.5 - i * 0.015)})`
        roundRect(ctx, s.x * CELL + 1, s.y * CELL + 1, CELL - 2, CELL - 2, i === 0 ? 3 : 2)
      })
    } else {
      for (let i = g.snake.length - 1; i > 0; i--) {
        const t = i / g.snake.length
        ctx.fillStyle = lerp(COLORS.tail, COLORS.body, 1 - t)
        roundRect(ctx, g.snake[i].x * CELL + 1, g.snake[i].y * CELL + 1, CELL - 2, CELL - 2, 2)
      }
      ctx.fillStyle = COLORS.head
      roundRect(ctx, g.snake[0].x * CELL + 1, g.snake[0].y * CELL + 1, CELL - 2, CELL - 2, 3)
      ctx.fillStyle = COLORS.bg
      getEyes(g.dir).forEach(([ox, oy]) => {
        ctx.beginPath()
        ctx.arc(g.snake[0].x * CELL + ox, g.snake[0].y * CELL + oy, 2, 0, Math.PI * 2)
        ctx.fill()
      })
    }
  }

  const die = () => {
    const g = diagnosticRef.current
    clearTimeout(g.loop)
    g.running = false
    g.finished = true
    beep({ freq: 220, freqEnd: 60, type: 'sawtooth', gain: 0.18, duration: 0.35, slide: true })
    draw(true)
    rerender()
  }

  const step = (): boolean => {
    const g = diagnosticRef.current
    g.dir = g.nextDir
    const head = { x: g.snake[0].x + g.dir.x, y: g.snake[0].y + g.dir.y }

    if (head.x < 0 || head.x >= COLS || head.y < 0 || head.y >= ROWS) { die(); return false }
    if (g.snake.some((s) => s.x === head.x && s.y === head.y)) { die(); return false }

    g.snake.unshift(head)
    if (head.x === g.food.x && head.y === g.food.y) {
      g.score++
      g.best = Math.max(g.best, g.score)
      g.speed = Math.max(60, 140 - g.score * 5)
      beep({ freq: 300, freqEnd: 600, type: 'square', gain: 0.12, duration: 0.1, slide: true })
      g.food = spawnFood(g.snake)
      rerender()
    } else {
      g.snake.pop()
    }
    return true
  }

  const startDiagnostic = () => {
    const g = diagnosticRef.current
    g.running = true
    g.finished = false
    rerender()
    const tick = () => {
      g.loop = setTimeout(() => {
        if (step()) {
          draw()
          tick()
        }
      }, g.speed)
    }
    tick()
  }

  const reset = () => {
    const g = diagnosticRef.current
    clearTimeout(g.loop)
    g.snake = [{ x: 10, y: 10 }, { x: 9, y: 10 }, { x: 8, y: 10 }]
    g.dir = { x: 1, y: 0 }
    g.nextDir = { x: 1, y: 0 }
    g.food = spawnFood(g.snake)
    g.speed = 140
    g.score = 0
    g.running = false
    g.finished = false
    rerender()
    draw()
  }

  const changeDirection = (d: Dir) => {
    const g = diagnosticRef.current
    if (d.x !== -g.dir.x || d.y !== -g.dir.y) {
      g.nextDir = d
      beep({ freq: 120, type: 'square', gain: 0.04, duration: 0.03 })
    }
    if (!g.running && !g.finished) startDiagnostic()
  }

  useEffect(() => {
    diagnosticRef.current.food = spawnFood(diagnosticRef.current.snake)
    draw()

    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { onClose(); return }
      const d = ARROW_DIRS[e.key]
      if (!d) return
      e.preventDefault()
      changeDirection(d)
    }
    const currentDiagnostic = diagnosticRef.current
    window.addEventListener('keydown', handleKey)
    return () => {
      window.removeEventListener('keydown', handleKey)
      clearTimeout(currentDiagnostic.loop)
    }
  }, [onClose])

  const g = diagnosticRef.current

  return (
    <div className={styles.overlay} onClick={onClose}>
      <div className={styles.diagnostic} onClick={(e) => e.stopPropagation()}>
        <button className={styles.closeButton} onClick={onClose}>
          &times;
        </button>
        <div className={styles.header}>
          <div>score <span className={styles.val}>{g.score}</span></div>
          <div>best <span className={styles.val}>{g.best}</span></div>
        </div>
        <canvas
          ref={canvasRef}
          width={WIDTH}
          height={HEIGHT}
          className={styles.canvas}
        />
        <div className={styles.message}>
          {g.finished ? (
            <>
              game over &middot; score: {g.score} &middot;{' '}
              <span className={styles.link} onClick={reset}>restart</span>
            </>
          ) : !g.running ? (
            <>
              <span
                className={styles.link}
                onClick={startDiagnostic}
              >
                press any arrow key
              </span>{' '}
              to start
            </>
          ) : null}
        </div>
        <div className={styles.dpad}>
          <div className={styles.dpadBlank} />
          <button className={styles.dpadBtn} onClick={() => changeDirection(DPAD_DIRS.UP)}>&#9650;</button>
          <div className={styles.dpadBlank} />
          <button className={styles.dpadBtn} onClick={() => changeDirection(DPAD_DIRS.LEFT)}>&#9664;</button>
          <div className={styles.dpadBlank} />
          <button className={styles.dpadBtn} onClick={() => changeDirection(DPAD_DIRS.RIGHT)}>&#9654;</button>
          <div className={styles.dpadBlank} />
          <button className={styles.dpadBtn} onClick={() => changeDirection(DPAD_DIRS.DOWN)}>&#9660;</button>
          <div className={styles.dpadBlank} />
        </div>
      </div>
    </div>
  )
}
