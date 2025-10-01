'use client';

import { useState } from 'react';
import { ChevronDown } from 'lucide-react';

interface FAQItem {
  question: string;
  answer: string;
}

interface FAQProps {
  items: FAQItem[];
}

function FAQAccordionItem({ question, answer }: FAQItem) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="rounded-2xl border border-stone-200 bg-white transition-colors duration-200 hover:border-purple-200 dark:border-stone-800 dark:bg-stone-900/30 dark:hover:border-purple-800">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex w-full items-center justify-between gap-4 px-6 py-5 text-left transition-colors duration-200 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-purple-500"
        aria-expanded={isOpen}
      >
        <h3 className="text-base font-bold leading-6 text-stone-900 dark:text-stone-50">{question}</h3>
        <ChevronDown
          className={`h-5 w-5 flex-shrink-0 text-stone-400 transition-transform duration-300 dark:text-stone-500 ${
            isOpen ? 'rotate-180' : ''
          }`}
          aria-hidden="true"
        />
      </button>
      <div
        className={`overflow-hidden transition-all duration-300 ease-in-out ${
          isOpen ? 'max-h-96 opacity-100' : 'max-h-0 opacity-0'
        }`}
      >
        <p className="px-6 pb-5 text-sm leading-[1.7] text-stone-600 dark:text-stone-400">{answer}</p>
      </div>
    </div>
  );
}

export function FAQ({ items }: FAQProps) {
  return (
    <div className="space-y-4">
      {items.map((item, index) => (
        <FAQAccordionItem key={index} question={item.question} answer={item.answer} />
      ))}
    </div>
  );
}
