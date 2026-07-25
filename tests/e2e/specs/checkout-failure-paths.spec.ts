import {
  expect,
  test,
  type Page,
  type Response,
} from '@playwright/test';

const customerUsername =
  process.env.E2E_CUSTOMER_USERNAME
  ?? 'alice.customer';

const customerPassword =
  process.env.E2E_CUSTOMER_PASSWORD
  ?? 'Alice123!';

const customerEmail =
  'alice.customer@eshop.local';

const outOfStockProductName =
  'E2E Out of Stock Keyboard';

const paymentFailureProductName =
  'E2E Payment Failure Keyboard';

type CheckoutOptions = {
  productName: string;
  paymentMethod: 'test-success' | 'test-fail';
};

test.describe(
  'checkout failure paths',
  () => {
    test(
      'order fails when inventory has insufficient stock',
      async ({
        page,
      }) => {
        await createOrderThroughCheckout(
          page,
          {
            productName:
              outOfStockProductName,

            paymentMethod:
              'test-success',
          },
        );

        await expectOrderStatus(
          page,
          'StockReservationFailed',
        );

        await expect(
          page.getByText(
            outOfStockProductName,
            {
              exact:
                true,
            },
          ),
        ).toBeVisible();

        await expect(
          page.getByText(
            'test-success',
            {
              exact:
                true,
            },
          ),
        ).toBeVisible();

        await expect(
          getCurrentStatusCard(page)
            .getByText(
              'Confirmed',
              {
                exact:
                  true,
              },
            ),
        ).toHaveCount(0);
      },
    );

    test(
      'failed payment releases stock and cancels the order',
      async ({
        page,
      }) => {
        await createOrderThroughCheckout(
          page,
          {
            productName:
              paymentFailureProductName,

            paymentMethod:
              'test-fail',
          },
        );

        await expectOrderStatus(
          page,
          'Cancelled',
          60_000,
        );

        await expect(
          page.getByText(
            paymentFailureProductName,
            {
              exact:
                true,
            },
          ),
        ).toBeVisible();

        await expect(
          page.getByText(
            'test-fail',
            {
              exact:
                true,
            },
          ),
        ).toBeVisible();

        await expect(
          getCurrentStatusCard(page)
            .getByText(
              'Confirmed',
              {
                exact:
                  true,
              },
            ),
        ).toHaveCount(0);
      },
    );
  },
);

async function createOrderThroughCheckout(
  page: Page,
  options: CheckoutOptions,
): Promise<void> {
  await page.goto('/');

  await expect(
    page.getByRole(
      'heading',
      {
        name:
          'Product Catalog',
      },
    ),
  ).toBeVisible();

  await signInAsCustomer(page);

  await refreshProductsAndVerifyProduct(
    page,
    options.productName,
  );

  const productHeading =
    page.getByRole(
      'heading',
      {
        name:
          options.productName,
      },
    );

  const productCard =
    page.locator(
      'article',
      {
        has:
          productHeading,
      },
    );

  await expect(
    productCard,
  ).toBeVisible();

  const addToBasketResponsePromise =
    page.waitForResponse(
      response =>
        response
          .url()
          .includes(
            '/api/v1/basket/items',
          )
        && response
          .request()
          .method() === 'POST',
    );

  await productCard
    .getByRole(
      'button',
      {
        name:
          'Add to basket',
      },
    )
    .click();

  const addToBasketResponse =
    await addToBasketResponsePromise;

  await assertSuccessfulResponse(
    addToBasketResponse,
    'Add-to-basket request failed.',
  );

  await expect(
    page.getByText(
      `${options.productName} was added to the basket.`,
    ),
  ).toBeVisible();

  await page
    .getByRole(
      'link',
      {
        name:
          'Open basket',
      },
    )
    .click();

  await expect(
    page.getByRole(
      'heading',
      {
        name:
          'Basket',
      },
    ),
  ).toBeVisible();

  await expect(
    page.getByRole(
      'heading',
      {
        name:
          options.productName,
      },
    ),
  ).toBeVisible();

  await page
    .getByRole(
      'link',
      {
        name:
          'Continue to checkout',
      },
    )
    .click();

  await expect(
    page.getByRole(
      'heading',
      {
        name:
          'Checkout',
      },
    ),
  ).toBeVisible();

  await page
    .getByLabel('Email')
    .fill(customerEmail);

  await page
    .getByLabel(
      'Fake payment method',
    )
    .selectOption(
      options.paymentMethod,
    );

  const createOrderResponsePromise =
    page.waitForResponse(
      response =>
        response
          .url()
          .includes('/api/v1/orders')
        && response
          .request()
          .method() === 'POST',
    );

  await page
    .getByRole(
      'button',
      {
        name:
          'Create order',
      },
    )
    .click();

  const createOrderResponse =
    await createOrderResponsePromise;

  await assertSuccessfulResponse(
    createOrderResponse,
    'Create-order request failed.',
  );

  await expect(page).toHaveURL(
    /\/orders\/[0-9a-f-]{36}$/i,
  );

  await expect(
    page.getByRole(
      'heading',
      {
        name:
          'Current status',
      },
    ),
  ).toBeVisible();
}

async function signInAsCustomer(
  page: Page,
): Promise<void> {
  await page
    .getByRole(
      'button',
      {
        name:
          'Sign in',
      },
    )
    .click();

  await expect(
    page.locator('#username'),
  ).toBeVisible();

  await page
    .locator('#username')
    .fill(customerUsername);

  await page
    .locator('#password')
    .fill(customerPassword);

  await page
    .locator('#kc-login')
    .click();

  await expect(
    page.getByRole(
      'heading',
      {
        name:
          'Product Catalog',
      },
    ),
  ).toBeVisible();

  await expect(
    page.getByText(
      customerUsername,
      {
        exact:
          true,
      },
    ),
  ).toBeVisible();
}

async function refreshProductsAndVerifyProduct(
  page: Page,
  productName: string,
): Promise<void> {
  const productsResponsePromise =
    page.waitForResponse(
      response =>
        response
          .url()
          .includes('/api/v1/products')
        && response
          .request()
          .method() === 'GET',
    );

  await page
    .getByRole(
      'button',
      {
        name:
          'Refresh products',
      },
    )
    .click();

  const productsResponse =
    await productsResponsePromise;

  const responseBody =
    await productsResponse.text();

  expect(
    productsResponse.ok(),
    [
      'Catalog request failed.',
      `Status: ${productsResponse.status()}`,
      `URL: ${productsResponse.url()}`,
      `Response: ${responseBody}`,
    ].join('\n'),
  ).toBeTruthy();

  expect(
    responseBody,
  ).toContain(productName);

  await expect(
    page.getByRole(
      'heading',
      {
        name:
          productName,
      },
    ),
  ).toBeVisible();
}

async function expectOrderStatus(
  page: Page,
  expectedStatus: string,
  timeout = 45_000,
): Promise<void> {
  await expect(
    getCurrentStatusCard(page)
      .getByText(
        expectedStatus,
        {
          exact:
            true,
        },
      ),
  ).toBeVisible({
    timeout,
  });
}

function getCurrentStatusCard(
  page: Page,
) {
  return page
    .getByRole(
      'heading',
      {
        name:
          'Current status',
      },
    )
    .locator('..');
}

async function assertSuccessfulResponse(
  response: Response,
  errorTitle: string,
): Promise<void> {
  const responseBody =
    await response.text();

  expect(
    response.ok(),
    [
      errorTitle,
      `Status: ${response.status()}`,
      `URL: ${response.url()}`,
      `Response: ${responseBody}`,
    ].join('\n'),
  ).toBeTruthy();
}