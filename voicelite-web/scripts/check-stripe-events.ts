import Stripe from 'stripe';

const stripe = new Stripe(process.env.STRIPE_SECRET_KEY!, {
  apiVersion: '2025-09-30.clover',
});

async function main() {
  console.log('Fetching recent Stripe events...\n');

  // Get recent checkout sessions
  const sessions = await stripe.checkout.sessions.list({
    limit: 5,
  });

  console.log('Recent Checkout Sessions:');
  for (const session of sessions.data) {
    console.log(`\nSession ID: ${session.id}`);
    console.log(`Status: ${session.status}`);
    console.log(`Payment Status: ${session.payment_status}`);
    console.log(`Customer Email: ${session.customer_details?.email || 'N/A'}`);
    console.log(`Amount: $${(session.amount_total || 0) / 100}`);
    console.log(`Created: ${new Date(session.created * 1000).toISOString()}`);
    console.log(`Payment Intent: ${session.payment_intent}`);
  }

  console.log('\n\nRecent Webhook Events:');
  const events = await stripe.events.list({
    limit: 10,
    types: ['checkout.session.completed'],
  });

  for (const event of events.data) {
    console.log(`\nEvent ID: ${event.id}`);
    console.log(`Type: ${event.type}`);
    console.log(`Created: ${new Date(event.created * 1000).toISOString()}`);
    const session = event.data.object as Stripe.Checkout.Session;
    console.log(`Session ID: ${session.id}`);
    console.log(`Customer Email: ${session.customer_details?.email || 'N/A'}`);
  }
}

main().catch(console.error);