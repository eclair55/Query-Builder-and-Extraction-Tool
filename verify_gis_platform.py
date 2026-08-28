import asyncio
from playwright.async_api import async_playwright
import os

async def main():
    os.makedirs('/tmp/screenshots', exist_ok=True)
    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=True)
        page = await browser.new_page(viewport={'width': 1280, 'height': 800})

        # Load app
        await page.goto('http://localhost:3000')
        await page.wait_for_timeout(2000)

        # Screenshot 1: Map View
        await page.screenshot(path='/tmp/screenshots/1_map_view.png')

        # Click Query Builder
        await page.click('text="Query Builder"')
        await page.wait_for_timeout(1000)
        await page.screenshot(path='/tmp/screenshots/2_query_builder.png')

        # Click Data Extraction
        await page.click('text="Data Extraction"')
        await page.wait_for_timeout(1000)
        await page.screenshot(path='/tmp/screenshots/3_extraction.png')

        # Click Admin Portal
        await page.click('text="Admin Portal"')
        await page.wait_for_timeout(1000)
        await page.screenshot(path='/tmp/screenshots/4_admin_portal.png')

        await browser.close()
        print("Playwright screenshots generated in /tmp/screenshots")

if __name__ == "__main__":
    asyncio.run(main())
