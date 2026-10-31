import type { ReactNode } from 'react'

/*
 * A small markdown renderer for knowledge base articles. It builds React elements and never HTML, so raw tags in
 * the source show as plain text. Links are kept only for web, mail and same-site addresses.
 * Supported: headings, paragraphs, bullet and numbered lists, blockquotes, fenced code, bold, italic, inline code, links.
 */

const SAFE_PROTOCOLS = ['http:', 'https:', 'mailto:']

/** The link target when it is safe to render as a link, or null when the link should show as plain text. */
function safeHref(url: string): string | null {
  const trimmed = url.trim()
  if (trimmed.startsWith('/') && !trimmed.startsWith('//')) return trimmed
  try {
    return SAFE_PROTOCOLS.includes(new URL(trimmed).protocol) ? trimmed : null
  } catch {
    return null
  }
}

const INLINE = /(`[^`]+`)|(\*\*[^*]+\*\*)|(\*[^*\s][^*]*\*)|(\[[^\]]+\]\([^)\s]+\))/
const LINK = /^\[([^\]]+)\]\(([^)\s]+)\)$/
const HEADING = /^(#{1,3})\s+(.*)$/
const BULLET = /^[-*]\s+(.*)$/
const NUMBERED = /^\d+\.\s+(.*)$/
const QUOTE = /^>\s?(.*)$/

function renderInline(text: string, keyPrefix: string): ReactNode[] {
  const nodes: ReactNode[] = []
  let rest = text
  let index = 0

  while (rest.length > 0) {
    const match = INLINE.exec(rest)
    if (!match) {
      nodes.push(rest)
      break
    }
    if (match.index > 0) nodes.push(rest.slice(0, match.index))

    const token = match[0]
    const key = `${keyPrefix}-${index++}`
    if (token.startsWith('`')) {
      nodes.push(<code key={key}>{token.slice(1, -1)}</code>)
    } else if (token.startsWith('**')) {
      nodes.push(<strong key={key}>{renderInline(token.slice(2, -2), key)}</strong>)
    } else if (token.startsWith('*')) {
      nodes.push(<em key={key}>{renderInline(token.slice(1, -1), key)}</em>)
    } else {
      const [, label, url] = LINK.exec(token)!
      const href = safeHref(url)
      nodes.push(
        href ? (
          <a key={key} href={href} rel="noopener noreferrer">
            {renderInline(label, key)}
          </a>
        ) : (
          label
        ),
      )
    }

    rest = rest.slice(match.index + token.length)
  }

  return nodes
}

function startsBlock(line: string): boolean {
  return line.startsWith('```') || HEADING.test(line) || BULLET.test(line) || NUMBERED.test(line) || QUOTE.test(line)
}

function renderBlocks(source: string): ReactNode[] {
  const lines = source.replace(/\r\n?/g, '\n').split('\n')
  const blocks: ReactNode[] = []
  let i = 0

  while (i < lines.length) {
    const line = lines[i]
    const key = `block-${i}`

    if (line.trim() === '') {
      i++
      continue
    }

    if (line.startsWith('```')) {
      const code: string[] = []
      i++
      while (i < lines.length && !lines[i].startsWith('```')) code.push(lines[i++])
      i++
      blocks.push(
        <pre key={key}>
          <code>{code.join('\n')}</code>
        </pre>,
      )
      continue
    }

    const heading = HEADING.exec(line)
    if (heading) {
      // Article headings sit under the page title, so # becomes h2 and the range is h2 to h4
      const level = heading[1].length + 1
      const content = renderInline(heading[2], key)
      blocks.push(
        level === 2 ? <h2 key={key}>{content}</h2> : level === 3 ? <h3 key={key}>{content}</h3> : <h4 key={key}>{content}</h4>,
      )
      i++
      continue
    }

    if (BULLET.test(line) || NUMBERED.test(line)) {
      const ordered = NUMBERED.test(line)
      const pattern = ordered ? NUMBERED : BULLET
      const items: ReactNode[] = []
      while (i < lines.length && pattern.test(lines[i])) {
        const text = pattern.exec(lines[i])![1]
        items.push(<li key={`${key}-${items.length}`}>{renderInline(text, `${key}-${items.length}`)}</li>)
        i++
      }
      blocks.push(ordered ? <ol key={key}>{items}</ol> : <ul key={key}>{items}</ul>)
      continue
    }

    if (QUOTE.test(line)) {
      const quoted: string[] = []
      while (i < lines.length && QUOTE.test(lines[i])) quoted.push(QUOTE.exec(lines[i++])![1])
      blocks.push(<blockquote key={key}>{renderBlocks(quoted.join('\n'))}</blockquote>)
      continue
    }

    const paragraph: string[] = []
    while (i < lines.length && lines[i].trim() !== '' && !startsBlock(lines[i])) paragraph.push(lines[i++])
    blocks.push(<p key={key}>{renderInline(paragraph.join(' '), key)}</p>)
  }

  return blocks
}

export function Markdown({ source }: { source: string }) {
  return <div className="markdown">{renderBlocks(source)}</div>
}
