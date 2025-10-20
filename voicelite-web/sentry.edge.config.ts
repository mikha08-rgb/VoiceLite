import * as Sentry from "@sentry/nextjs";

Sentry.init({
  dsn: "https://fc2c972d187ce7415033c7e8dcc1d31c@o4510219420368896.ingest.us.sentry.io/4510219422072832",

  // Adjust this value in production, or use tracesSampler for greater control
  tracesSampleRate: 0.1,

  // Setting this option to true will print useful information to the console while you're setting up Sentry.
  debug: false,
});
