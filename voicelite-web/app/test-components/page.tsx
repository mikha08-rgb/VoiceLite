'use client';

import { Button } from '@/components/ui/button';
import { Card, CardIcon, CardTitle, CardDescription } from '@/components/ui/card';
import { Lock } from 'lucide-react';

/**
 * Simple test page to verify components work
 * Visit: http://localhost:3000/test-components
 */
export default function TestComponentsPage() {
  return (
    <main className="min-h-screen bg-gray-50 p-12">
      <div className="container mx-auto max-w-4xl space-y-12">
        <h1 className="text-4xl font-bold text-gray-900">Component Library Test</h1>

        {/* Test Button */}
        <section className="space-y-4">
          <h2 className="text-2xl font-semibold text-gray-900">Buttons</h2>
          <div className="flex gap-4 flex-wrap">
            <Button variant="primary" size="lg">
              Primary Button
            </Button>
            <Button variant="secondary" size="md">
              Secondary Button
            </Button>
            <Button variant="ghost" size="sm">
              Ghost Button
            </Button>
            <Button variant="primary" isLoading>
              Loading...
            </Button>
          </div>
        </section>

        {/* Test Card */}
        <section className="space-y-4">
          <h2 className="text-2xl font-semibold text-gray-900">Cards</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <Card variant="feature">
              <CardIcon>
                <Lock size={24} />
              </CardIcon>
              <CardTitle>Privacy First</CardTitle>
              <CardDescription>
                No cloud, no tracking, fully offline. Your voice is processed locally.
              </CardDescription>
            </Card>

            <Card variant="feature">
              <CardIcon>
                <Lock size={24} />
              </CardIcon>
              <CardTitle>Feature 2</CardTitle>
              <CardDescription>
                This is a test card to verify the component library works correctly.
              </CardDescription>
            </Card>

            <Card variant="feature">
              <CardIcon>
                <Lock size={24} />
              </CardIcon>
              <CardTitle>Feature 3</CardTitle>
              <CardDescription>
                Another test card with some sample description text.
              </CardDescription>
            </Card>
          </div>
        </section>

        {/* Status */}
        <section className="space-y-4">
          <h2 className="text-2xl font-semibold text-gray-900">Status</h2>
          <div className="p-6 bg-green-50 border border-green-200 rounded-lg">
            <p className="text-green-900 font-semibold">✅ Components loaded successfully!</p>
            <p className="text-sm text-green-700 mt-2">
              If you can see this page with styled buttons and cards, the component library is working.
            </p>
          </div>
        </section>
      </div>
    </main>
  );
}
