# VoiceLite UI Component Library

A comprehensive, accessible component library built for the VoiceLite marketing website.

## 📦 Components

Based on the [UI/UX Specification](../../docs/front-end-spec.md), this library includes 9 core components:

### 1. **Button** ([button.tsx](./button.tsx))
Primary, Secondary, Ghost, and Danger variants with loading states.

```tsx
import { Button } from '@/components/ui';

<Button variant="primary" size="lg">Download Now</Button>
<Button variant="secondary" isLoading>Processing...</Button>
```

### 2. **Card** ([card.tsx](./card.tsx))
Feature, Pricing, Testimonial, and Stat variants with composable subcomponents.

```tsx
import { Card, CardIcon, CardTitle, CardDescription } from '@/components/ui';

<Card variant="feature" hoverable>
  <CardIcon><Mic /></CardIcon>
  <CardTitle>Privacy First</CardTitle>
  <CardDescription>No cloud, no tracking, fully offline</CardDescription>
</Card>
```

### 3. **Navigation** ([navigation.tsx](./navigation.tsx))
Responsive nav with desktop dropdowns and mobile hamburger menu.

```tsx
import { Navigation } from '@/components/ui';

<Navigation logo={<Logo />} />
```

### 4. **Input** ([input.tsx](./input.tsx))
Text, Email, Search, and Textarea components with validation states.

```tsx
import { Input, Textarea, SearchInput } from '@/components/ui';

<Input
  label="Email Address"
  type="email"
  error="Please enter a valid email"
  required
/>

<SearchInput placeholder="Search for help..." />
```

### 5. **VideoPlayer** ([video-player.tsx](./video-player.tsx))
Hero, Feature, and Thumbnail variants with autoplay and controls.

```tsx
import { VideoPlayer } from '@/components/ui';

<VideoPlayer
  variant="hero"
  src="/videos/demo.mp4"
  poster="/images/demo-poster.jpg"
/>
```

### 6. **Accordion** ([accordion.tsx](./accordion.tsx))
Expandable FAQ items with single/multiple open modes and deep linking.

```tsx
import { Accordion } from '@/components/ui';

<Accordion
  type="single"
  items={[
    { id: 'refund', title: 'Can I get a refund?', content: 'Yes, 30 days...' },
  ]}
/>
```

### 7. **Modal** ([modal.tsx](./modal.tsx))
Dialog component with focus trap, backdrop, and keyboard support.

```tsx
import { Modal } from '@/components/ui';

<Modal
  isOpen={isOpen}
  onClose={() => setIsOpen(false)}
  title="Confirm Purchase"
  footer={<Button onClick={handleConfirm}>Confirm</Button>}
>
  <p>Are you sure?</p>
</Modal>
```

### 8. **Table** ([table.tsx](./table.tsx))
Responsive table that converts to cards on mobile, with sorting support.

```tsx
import { Table } from '@/components/ui';

<Table
  columns={[
    { key: 'feature', label: 'Feature' },
    { key: 'voicelite', label: 'VoiceLite' },
  ]}
  data={[
    { feature: 'Price', voicelite: '$20' },
  ]}
  highlightColumn="voicelite"
/>
```

### 9. **Badge** ([badge.tsx](./badge.tsx))
Status indicators with Info, Success, Warning, Danger, and Neutral variants.

```tsx
import { Badge } from '@/components/ui';

<Badge variant="success">Active</Badge>
<Badge variant="info" size="sm">v1.0.66</Badge>
```

---

## 🎨 Design System

All components follow the design system defined in [front-end-spec.md](../../docs/front-end-spec.md):

### Colors
- **Primary:** `#2563EB` (blue-600) - Main CTAs, links
- **Accent:** `#10B981` (emerald-500) - Purchase buttons
- **Neutral:** Gray scale (900 → 50)

### Typography
- **Font:** Inter (primary), JetBrains Mono (monospace)
- **Scale:** H1 (48px) → Body (16px) → Small (14px)

### Spacing
- **Base unit:** 8px (Tailwind default)
- **Breakpoints:** Mobile (<640px), Tablet (640-1024px), Desktop (1024px+)

---

## ♿ Accessibility

All components meet **WCAG 2.1 Level AA** standards:

- ✅ Color contrast ratios ≥ 4.5:1
- ✅ Keyboard navigation (Tab, Enter, Escape, Arrow keys)
- ✅ Screen reader support (ARIA labels, semantic HTML)
- ✅ Focus indicators (2px blue ring)
- ✅ Touch targets ≥ 44x44px

---

## 📦 Installation

### Required Dependencies

```bash
cd voicelite-web
npm install clsx tailwind-merge
```

### Optional (for icons)
Icons use Lucide React (already installed in your project):
```bash
npm install lucide-react  # Already installed ✅
```

---

## 🚀 Usage

### Import Components

```tsx
// Individual imports
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';

// Or use the index
import { Button, Card, Input } from '@/components/ui';
```

### TypeScript

All components are fully typed with TypeScript:

```tsx
import { ButtonProps, CardProps } from '@/components/ui';

const MyButton: React.FC<ButtonProps> = (props) => {
  return <Button {...props} />;
};
```

---

## 📝 Examples

### Homepage Hero Section

```tsx
import { Button, VideoPlayer } from '@/components/ui';

export function Hero() {
  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
      <div>
        <h1 className="text-5xl font-bold text-gray-900 mb-4">
          Stop Typing. Start Speaking.
        </h1>
        <p className="text-lg text-gray-700 mb-6">
          VoiceLite turns your voice into text instantly—anywhere on Windows.
        </p>
        <Button variant="primary" size="lg">
          Download for Windows
        </Button>
      </div>
      <VideoPlayer
        variant="hero"
        src="/videos/demo.mp4"
        poster="/images/demo-poster.jpg"
      />
    </div>
  );
}
```

### Feature Cards Grid

```tsx
import { Card, CardIcon, CardTitle, CardDescription } from '@/components/ui';
import { Lock, Zap, DollarSign } from 'lucide-react';

export function Features() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
      <Card variant="feature">
        <CardIcon><Lock size={24} /></CardIcon>
        <CardTitle>Privacy First</CardTitle>
        <CardDescription>No cloud, no tracking, fully offline</CardDescription>
      </Card>

      <Card variant="feature">
        <CardIcon><Zap size={24} /></CardIcon>
        <CardTitle>Works Everywhere</CardTitle>
        <CardDescription>Email, code, Slack, Discord—any Windows app</CardDescription>
      </Card>

      <Card variant="feature">
        <CardIcon><DollarSign size={24} /></CardIcon>
        <CardTitle>One-Time $20</CardTitle>
        <CardDescription>No subscription. Pay once, use forever.</CardDescription>
      </Card>
    </div>
  );
}
```

### Pricing Comparison Table

```tsx
import { Table, Badge } from '@/components/ui';

export function PricingComparison() {
  return (
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
          voicelite: <Badge variant="info">100% local</Badge>,
          dragon: 'Local',
          otter: 'Cloud',
        },
      ]}
      highlightColumn="voicelite"
    />
  );
}
```

### FAQ Accordion

```tsx
import { Accordion } from '@/components/ui';

export function FAQ() {
  return (
    <Accordion
      type="single"
      items={[
        {
          id: 'refund',
          title: 'Can I get a refund?',
          content: 'Yes, we offer a 30-day money-back guarantee.',
        },
        {
          id: 'internet',
          title: 'Do I need internet?',
          content: 'Only for the initial purchase. VoiceLite works 100% offline.',
        },
        {
          id: 'mac',
          title: 'Will it work on Mac?',
          content: 'No, VoiceLite is Windows-only (Windows 10/11).',
        },
      ]}
    />
  );
}
```

---

## 🧪 Testing

All components are tested for:
- ✅ Rendering without errors
- ✅ Keyboard navigation
- ✅ Screen reader compatibility
- ✅ Responsive behavior

---

## 📄 License

MIT License - See [LICENSE](../../LICENSE) for details

---

## 🙋 Questions?

Refer to the [UI/UX Specification](../../docs/front-end-spec.md) for detailed design guidelines and usage patterns.
