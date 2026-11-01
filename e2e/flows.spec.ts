import { test, expect, type Page } from '@playwright/test'

async function signIn(page: Page, testId: string) {
  await page.goto('/')
  // The API hashes passwords with bcrypt, which is slow by design, so the first sign-in can outlast the default 5s expect timeout
  const login = page.waitForResponse((response) => response.url().endsWith('/auth/login') && response.ok())
  await page.getByTestId(testId).click()
  await login
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible({ timeout: 15_000 })
}

test('a customer opens a ticket, an agent works it, and the customer sees the reply', async ({ browser, page }) => {
  test.setTimeout(120_000)
  const title = `Invoice shows the wrong VAT rate ${Date.now()}`

  // Customer: create the ticket
  await signIn(page, 'login-demo-customer')
  await page.goto('/tickets/new')
  await page.getByLabel('Title').fill(title)
  await page.getByLabel('Category').selectOption('Billing')
  await page.getByLabel('Priority').selectOption('high')
  await page.getByLabel('Description').fill('The March invoice charges 20% VAT. Our rate is 15%.')
  await page.getByRole('button', { name: 'Submit ticket' }).click()

  await page.waitForURL(/\/tickets\/\d+$/)
  const ticketUrl = new URL(page.url()).pathname
  await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible()

  // Customer: a follow-up reply
  await page.getByTestId('comment-input').fill('Here is the invoice number: INV-2041.')
  await page.getByRole('button', { name: 'Send reply' }).click()
  await expect(page.getByText('Here is the invoice number: INV-2041.')).toBeVisible()

  // Agent, in its own browser session: assign, change status, reply
  const agentContext = await browser.newContext()
  const agent = await agentContext.newPage()
  await signIn(agent, 'login-demo-agent')
  await agent.goto(ticketUrl)
  await expect(agent.getByRole('heading', { level: 1, name: title })).toBeVisible()

  await agent.getByRole('combobox', { name: 'Assignee' }).selectOption({ label: 'Priya Shah' })
  await expect(agent.getByTestId('timeline-item').filter({ hasText: 'Priya Shah' }).first()).toBeVisible()

  await agent.getByTestId('status-select').selectOption('in_progress')
  await expect(agent.getByTestId('ticket-status')).toHaveText('In progress')

  await agent.getByTestId('comment-input').fill('Thanks, we are checking the tax table now.')
  await agent.getByRole('button', { name: 'Send reply' }).click()
  await expect(agent.getByText('Thanks, we are checking the tax table now.')).toBeVisible()
  await agentContext.close()

  // Customer: the agent's reply and the new status are visible after a reload
  await page.reload()
  await expect(page.getByText('Thanks, we are checking the tax table now.')).toBeVisible()
  await expect(page.getByTestId('ticket-status')).toHaveText('In progress')
})

test('an admin changes a user role and the change is visible in the list', async ({ page }) => {
  test.setTimeout(120_000)
  await signIn(page, 'login-demo-admin')
  await page.goto('/users')

  const row = page.locator('tr', { hasText: 'elena.novak@teamdesk.demo' })
  const role = row.getByRole('combobox')
  await expect(role).toHaveValue('agent')

  // Promote, check, then put the role back so the seeded data stays as it was
  await role.selectOption('admin')
  await expect(role).toHaveValue('admin')
  await role.selectOption('agent')
  await expect(role).toHaveValue('agent')
})
