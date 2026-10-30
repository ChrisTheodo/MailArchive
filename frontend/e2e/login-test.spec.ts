// @ts-check
import { test, expect } from '@playwright/test'
import { vInput, fillField } from './helpers/vuetify.js'

test.describe('login', () => {
  test('has reached login page', async ({ page }) => {
    await page.goto('http://localhost:5173/')
    await expect(page, { message: 'Η σελίδα δεν περιέχει στον τίτλο "Σύνδεση"' }).toHaveTitle(
      /Σύνδεση/,
    )
  })

  test('can login', async ({ page }) => {
    await page.goto('http://localhost:5173/')
    const emailInputLocator = vInput(page, 'email-input')
    await expect(emailInputLocator, { message: 'Το email πεδίο δεν είναι ορατό' }).toBeVisible()
    await expect(emailInputLocator, {
      message: 'Το email πεδίο είναι απενεργοποιημένο',
    }).toBeEnabled()

    const passwordInputLocator = vInput(page, 'email-input')
    await expect(passwordInputLocator, {
      message: 'Το password πεδίο δεν είναι ορατό',
    }).toBeVisible()
    await expect(passwordInputLocator, {
      message: 'Το password πεδίο είναι απενεργοποιημένο',
    }).toBeEnabled()

    await fillField(page, 'email-input', 'admin@example.com')
    await fillField(page, 'password-input', 'Admin123!')

    const loginBtn = page.getByTestId('login-button')
    await expect(loginBtn, { message: 'Το κουμπί "Σύνδεση" δεν είναι ορατό' }).toBeVisible()
    await expect(loginBtn, { message: 'Το κουμπί "Σύνδεση" είναι απενεργοποιημένο' }).toBeEnabled()
    await loginBtn.click()
    await expect(page).toHaveURL(/mail/)
    await expect(page, { message: 'Η σελίδα δεν περιέχει στον τίτλο "Αρχική"' }).toHaveTitle(
      /Αρχική/,
    )
    const searchbarInput = vInput(page, 'searchbar-input')
    await expect(searchbarInput, { message: 'Η μπάρα αναζήτησης δεν είναι ορατή' }).toBeVisible()
  })

  test('has alert message on login fail', async ({ page }) => {
    await page.goto('http://localhost:5173')
    await fillField(page, 'email-input', 'admin@example.com')
    await fillField(page, 'password-input', 'User123!')
    const loginBtn = page.getByTestId('login-button')
    await loginBtn.click()

    const alertMessageLocator = page.getByTestId('login-alert-message')
    await expect(alertMessageLocator).toBeVisible()
  })
})
