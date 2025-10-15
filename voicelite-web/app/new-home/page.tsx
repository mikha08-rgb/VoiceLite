import { Button, Card, CardIcon, CardTitle, CardDescription, Table, Badge, Accordion } from '@/components/ui';
import { Lock, Zap, DollarSign, Mic, Check, Download } from 'lucide-react';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'VoiceLite - Stop Typing. Start Speaking.',
  description: 'VoiceLite turns your voice into text instantly—anywhere on Windows. Private, fast, and $20 one-time. No subscription. 100% offline voice typing.',
  openGraph: {
    title: 'VoiceLite - Stop Typing. Start Speaking.',
    description: 'Private, fast voice typing for Windows. $20 one-time. No subscription.',
    type: 'website',
  },
};

/**
 * VoiceLite Homepage
 * Built with the new component library (components/ui/)
 * Based on front-end-spec.md
 */
export default function NewHomePage() {
  return (
    <main className="min-h-screen bg-gray-50">
      {/* Hero Section */}
      <section className="bg-white border-b border-gray-200">
        <div className="container mx-auto max-w-7xl px-6 py-24">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-16 items-center">
            {/* Hero Text */}
            <div className="space-y-8">
              <div className="inline-flex items-center gap-2 px-4 py-2 bg-blue-50 border border-blue-200 rounded-full text-sm font-medium text-blue-700">
                <Mic size={16} />
                100% Private · No Cloud · 99 Languages
              </div>

              <h1 className="text-5xl font-bold leading-tight text-gray-900">
                Stop Typing. <br />Start Speaking.
              </h1>

              <p className="text-lg leading-relaxed text-gray-700">
                VoiceLite turns your voice into text instantly—anywhere on Windows.
                Private, fast, and $20 one-time. <strong className="font-semibold text-gray-900">No subscription.</strong>
              </p>

              <div className="flex flex-col sm:flex-row gap-4">
                <a href="https://github.com/mikha08-rgb/VoiceLite/releases/latest">
                  <Button variant="primary" size="lg">
                    <Download size={20} className="mr-2" />
                    Download for Windows
                  </Button>
                </a>

                <a href="#pricing">
                  <Button variant="secondary" size="lg">
                    See Pricing
                  </Button>
                </a>
              </div>

              <p className="text-sm text-gray-500 flex items-center gap-4">
                <span>Windows 10/11</span>
                <span className="h-1 w-1 rounded-full bg-gray-300" />
                <span>540MB installer</span>
                <span className="h-1 w-1 rounded-full bg-gray-300" />
                <span>2 minutes setup</span>
              </p>
            </div>

            {/* Hero Video Placeholder */}
            <div className="aspect-video rounded-lg bg-gradient-to-br from-blue-100 to-emerald-100 border border-gray-300 flex items-center justify-center">
              <div className="text-center space-y-4 px-8">
                <Mic size={48} className="text-blue-600 mx-auto" />
                <p className="text-sm text-gray-700 font-medium">
                  Demo Video Coming Soon
                </p>
                <p className="text-xs text-gray-500">
                  (Record your 45-60 second demo as per spec Section 10.3)
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Social Proof Strip */}
      <section className="bg-gray-100 border-b border-gray-200 py-4">
        <div className="container mx-auto max-w-7xl px-6">
          <div className="flex flex-wrap items-center justify-center gap-8 text-sm text-gray-700">
            <div className="flex items-center gap-2">
              <Check size={16} className="text-green-500" />
              <span>1000+ users</span>
            </div>
            <div className="flex items-center gap-2">
              <Check size={16} className="text-green-500" />
              <span>100% private—data never leaves your PC</span>
            </div>
            <div className="flex items-center gap-2">
              <Check size={16} className="text-green-500" />
              <span>Open source (MIT License)</span>
            </div>
          </div>
        </div>
      </section>

      {/* Feature Highlights (3 columns) */}
      <section className="py-24">
        <div className="container mx-auto max-w-7xl px-6">
          <div className="text-center space-y-4 mb-16">
            <h2 className="text-4xl font-bold text-gray-900">
              Designed for Privacy, Built for Speed
            </h2>
            <p className="text-lg text-gray-700 max-w-2xl mx-auto">
              Three reasons VoiceLite is different from cloud-based competitors
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <Card variant="feature">
              <CardIcon>
                <Lock size={24} />
              </CardIcon>
              <CardTitle>Privacy First</CardTitle>
              <CardDescription>
                No cloud, no tracking, fully offline. Your voice is processed locally using OpenAI Whisper AI.
                Audio files are deleted immediately after transcription.
              </CardDescription>
            </Card>

            <Card variant="feature">
              <CardIcon>
                <Zap size={24} />
              </CardIcon>
              <CardTitle>Works Everywhere</CardTitle>
              <CardDescription>
                Email, code editors, Slack, Discord, Terminal—any Windows app with a text field.
                Just press your hotkey and speak.
              </CardDescription>
            </Card>

            <Card variant="feature">
              <CardIcon>
                <DollarSign size={24} />
              </CardIcon>
              <CardTitle>One-Time $20</CardTitle>
              <CardDescription>
                No subscription. Pay once, use forever. Includes all 5 AI models, lifetime updates,
                and 30-day money-back guarantee.
              </CardDescription>
            </Card>
          </div>
        </div>
      </section>

      {/* How It Works (3 steps with icons) */}
      <section className="bg-white border-y border-gray-200 py-24">
        <div className="container mx-auto max-w-5xl px-6">
          <div className="text-center space-y-4 mb-16">
            <h2 className="text-4xl font-bold text-gray-900">How It Works</h2>
            <p className="text-lg text-gray-700">Three simple steps to frictionless voice typing</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-12">
            {[
              { step: '1', title: 'Press Hotkey', description: 'Hold Left Alt (or any custom key)', icon: '⌨️' },
              { step: '2', title: 'Speak Naturally', description: 'Say what you want to type', icon: '🎤' },
              { step: '3', title: 'Text Appears', description: 'Release key, text is instantly typed', icon: '✅' },
            ].map((item) => (
              <div key={item.step} className="text-center space-y-4">
                <div className="w-16 h-16 mx-auto bg-blue-100 rounded-full flex items-center justify-center text-3xl">
                  {item.icon}
                </div>
                <div className="space-y-2">
                  <h3 className="text-xl font-semibold text-gray-900">{item.title}</h3>
                  <p className="text-base text-gray-700">{item.description}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Model Comparison Table */}
      <section id="models" className="py-24">
        <div className="container mx-auto max-w-6xl px-6">
          <div className="text-center space-y-4 mb-16">
            <h2 className="text-4xl font-bold text-gray-900">Choose Your Accuracy Level</h2>
            <p className="text-lg text-gray-700">All models included with $20 purchase</p>
          </div>

          <Table
            columns={[
              { key: 'model', label: 'Model' },
              { key: 'size', label: 'Size' },
              { key: 'accuracy', label: 'Accuracy' },
              { key: 'speed', label: 'Speed' },
              { key: 'use_case', label: 'Best For' },
            ]}
            data={[
              {
                model: <><Badge variant="neutral" size="sm">Tiny</Badge> <span className="ml-2">ggml-tiny.bin</span></>,
                size: '75MB',
                accuracy: '80-85%',
                speed: 'Fastest',
                use_case: 'Basic dictation, quick notes',
              },
              {
                model: <><Badge variant="success" size="sm">Small</Badge> <span className="ml-2 font-semibold">ggml-small.bin</span></>,
                size: '466MB',
                accuracy: <span className="font-semibold text-blue-600">90-93%</span>,
                speed: 'Very Fast',
                use_case: <span className="font-semibold">Recommended for most users</span>,
              },
              {
                model: <><Badge variant="info" size="sm">Base</Badge> <span className="ml-2">ggml-base.bin</span></>,
                size: '142MB',
                accuracy: '85-88%',
                speed: 'Fast',
                use_case: 'Good balance, mobile-friendly',
              },
              {
                model: <><Badge variant="warning" size="sm">Medium</Badge> <span className="ml-2">ggml-medium.bin</span></>,
                size: '1.5GB',
                accuracy: '93-96%',
                speed: 'Moderate',
                use_case: 'Professional writing, technical docs',
              },
              {
                model: <><Badge variant="danger" size="sm">Large</Badge> <span className="ml-2">ggml-large-v3.bin</span></>,
                size: '2.9GB',
                accuracy: '96-98%',
                speed: 'Slower',
                use_case: 'Maximum accuracy, powerful PCs',
              },
            ]}
            highlightColumn="accuracy"
            mobileView="cards"
          />

          <p className="text-sm text-gray-500 text-center mt-8">
            💡 <strong>Tip:</strong> Start with Small model (90-93% accuracy, fast). Switch models anytime in settings.
          </p>
        </div>
      </section>

      {/* Pricing Section */}
      <section id="pricing" className="bg-white border-y border-gray-200 py-24">
        <div className="container mx-auto max-w-4xl px-6">
          <div className="text-center space-y-4 mb-16">
            <h2 className="text-4xl font-bold text-gray-900">$20. One-Time. Yours Forever.</h2>
            <p className="text-lg text-gray-700">No subscription. No recurring charges. Lifetime updates.</p>
          </div>

          <Card variant="pricing" className="max-w-xl mx-auto">
            <div className="text-center space-y-6">
              <div className="space-y-2">
                <h3 className="text-2xl font-bold text-gray-900">VoiceLite Pro</h3>
                <div className="text-5xl font-bold text-blue-600">$20</div>
                <p className="text-sm text-gray-500">one-time payment</p>
              </div>

              <div className="space-y-3 text-left">
                <p className="font-semibold text-gray-900">What's Included:</p>
                <ul className="space-y-2">
                  {[
                    'All 5 AI models (Tiny → Large)',
                    'Lifetime updates',
                    'Works in unlimited apps',
                    '100% offline privacy',
                    '30-day money-back guarantee',
                    'Email support',
                  ].map((item) => (
                    <li key={item} className="flex items-start gap-2 text-sm text-gray-700">
                      <Check size={16} className="text-green-500 flex-shrink-0 mt-0.5" />
                      <span>{item}</span>
                    </li>
                  ))}
                </ul>
              </div>

              <Button variant="primary" size="lg" className="w-full">
                Buy VoiceLite Pro - $20
              </Button>

              <p className="text-xs text-gray-500">
                Secure payment via Stripe · Instant license delivery · No subscription
              </p>
            </div>
          </Card>

          {/* Comparison to Competitors */}
          <div className="mt-16">
            <h3 className="text-xl font-semibold text-gray-900 text-center mb-8">How We Compare</h3>
            <Table
              columns={[
                { key: 'feature', label: 'Feature' },
                { key: 'voicelite', label: 'VoiceLite' },
                { key: 'dragon', label: 'Dragon' },
                { key: 'otter', label: 'Otter.ai' },
              ]}
              data={[
                {
                  feature: 'Price',
                  voicelite: <Badge variant="success">$20 once</Badge>,
                  dragon: '$500',
                  otter: '$17/mo',
                },
                {
                  feature: 'Privacy',
                  voicelite: <Badge variant="success">100% local</Badge>,
                  dragon: 'Local',
                  otter: 'Cloud',
                },
                {
                  feature: 'Platform',
                  voicelite: 'Windows',
                  dragon: 'Win/Mac',
                  otter: 'Web',
                },
                {
                  feature: 'Subscription',
                  voicelite: <Badge variant="success">None</Badge>,
                  dragon: 'None',
                  otter: 'Required',
                },
              ]}
              highlightColumn="voicelite"
            />
          </div>
        </div>
      </section>

      {/* FAQ */}
      <section id="faq" className="py-24">
        <div className="container mx-auto max-w-3xl px-6">
          <div className="text-center space-y-4 mb-12">
            <h2 className="text-4xl font-bold text-gray-900">Frequently Asked Questions</h2>
            <p className="text-lg text-gray-700">Everything you need to know about VoiceLite</p>
          </div>

          <Accordion
            type="single"
            items={[
              {
                id: 'refund',
                title: 'Can I get a refund?',
                content: 'Yes, we offer a 30-day money-back guarantee. If VoiceLite doesn\'t work for you, email support for a full refund—no questions asked.',
              },
              {
                id: 'internet',
                title: 'Do I need internet?',
                content: 'Only for the initial purchase and download. VoiceLite works 100% offline after installation. Your voice is processed locally using OpenAI Whisper AI.',
              },
              {
                id: 'mac',
                title: 'Will it work on Mac?',
                content: 'No, VoiceLite is Windows-only (Windows 10/11). We use Windows-specific APIs for global hotkeys and text injection.',
              },
              {
                id: 'accuracy',
                title: 'How accurate is the transcription?',
                content: 'Tiny model: 80-85% accuracy. Small model (recommended): 90-93% accuracy. Large model: 96-98% accuracy. All models recognize technical terms like useState, npm, and git.',
              },
              {
                id: 'apps',
                title: 'Which apps does it work with?',
                content: 'VoiceLite works in ANY Windows application with a text field: VS Code, Notepad, Discord, Slack, Terminal, Word, browsers, games (windowed mode), and more.',
              },
            ]}
          />
        </div>
      </section>

      {/* Final CTA */}
      <section className="bg-gradient-to-br from-blue-600 to-emerald-600 text-white py-24">
        <div className="container mx-auto max-w-4xl px-6 text-center space-y-8">
          <h2 className="text-4xl font-bold">Ready to 10x your productivity?</h2>
          <p className="text-lg opacity-90">
            Join 1000+ users who've ditched typing for voice. Start with the free version today.
          </p>

          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <a href="https://github.com/mikha08-rgb/VoiceLite/releases/latest">
              <Button
                variant="primary"
                size="lg"
                className="bg-white text-blue-600 hover:bg-gray-100"
              >
                <Download size={20} className="mr-2" />
                Download VoiceLite Free
              </Button>
            </a>

            <a href="#pricing">
              <Button
                variant="secondary"
                size="lg"
                className="bg-transparent border-2 border-white text-white hover:bg-white hover:text-blue-600"
              >
                Buy Pro for $20
              </Button>
            </a>
          </div>

          <p className="text-sm opacity-75">
            Windows 10/11 · 30-day money-back guarantee · No subscription
          </p>
        </div>
      </section>

      {/* Footer */}
      <footer className="bg-gray-900 text-gray-400 py-12">
        <div className="container mx-auto max-w-7xl px-6">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-8">
            <div className="md:col-span-2 space-y-4">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-gradient-to-br from-blue-600 to-emerald-600 rounded-lg flex items-center justify-center">
                  <Mic size={20} className="text-white" />
                </div>
                <span className="text-xl font-bold text-white">VoiceLite</span>
              </div>
              <p className="text-sm leading-relaxed">
                Privacy-focused offline voice typing for Windows. Your voice never leaves your computer.
              </p>
            </div>

            <div>
              <h4 className="text-sm font-semibold text-white mb-4">Product</h4>
              <ul className="space-y-2 text-sm">
                <li><a href="#" className="hover:text-white transition-colors">Download</a></li>
                <li><a href="#models" className="hover:text-white transition-colors">Features</a></li>
                <li><a href="#pricing" className="hover:text-white transition-colors">Pricing</a></li>
              </ul>
            </div>

            <div>
              <h4 className="text-sm font-semibold text-white mb-4">Support</h4>
              <ul className="space-y-2 text-sm">
                <li><a href="#faq" className="hover:text-white transition-colors">FAQ</a></li>
                <li><a href="https://github.com/mikha08-rgb/VoiceLite/issues" target="_blank" rel="noopener" className="hover:text-white transition-colors">Report Issue</a></li>
                <li><a href="https://github.com/mikha08-rgb/VoiceLite" target="_blank" rel="noopener" className="hover:text-white transition-colors">Documentation</a></li>
              </ul>
            </div>
          </div>

          <div className="border-t border-gray-800 mt-8 pt-8 flex flex-col md:flex-row justify-between items-center gap-4 text-sm">
            <p>© {new Date().getFullYear()} VoiceLite. Open source under MIT License.</p>
            <div className="flex gap-6">
              <a href="https://github.com/mikha08-rgb/VoiceLite" target="_blank" rel="noopener" className="hover:text-white transition-colors">GitHub</a>
              <a href="https://github.com/mikha08-rgb/VoiceLite/blob/master/LICENSE" target="_blank" rel="noopener" className="hover:text-white transition-colors">License</a>
            </div>
          </div>
        </div>
      </footer>
    </main>
  );
}
