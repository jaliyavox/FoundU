import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'

/**
 * The one light section on the landing page. The change of ground is what divides it from
 * the dark bands either side, so it deliberately re-states its own colours rather than
 * inheriting the page's white-on-forest.
 */

const FAQS = [
  {
    question: 'Who can report a lost item?',
    answer:
      'Any student with a campus account. Sign in, describe what you lost and roughly where and when you had it last. Staff and security log the items that are handed in to them.',
  },
  {
    question: 'How does FoundU know an item is really mine?',
    answer:
      'When staff log a found item they record a detail that is never shown to anyone claiming it - what is in the front pocket, a sticker on the lid, the colour of a keychain. To claim it you describe that detail from memory. Get it right and staff confirm the handover.',
  },
  {
    question: 'Can other students see what I have reported?',
    answer:
      'No. Your lost report is visible to you and to the staff working the lost-and-found desk. Found items are not published as a public list, precisely so that nobody can browse for something to claim.',
  },
  {
    question: 'What happens if two people claim the same item?',
    answer:
      'Both claims go to staff with the answers each person gave. If it is not clear-cut, the claim is escalated to an administrator for review rather than being decided automatically.',
  },
  {
    question: 'Where do I collect something once it is matched?',
    answer:
      'FoundU tells you which desk is holding it. Items stay where they were handed in unless staff move them, and every transfer is recorded, so the location you are given is the current one.',
  },
]

export function Faq() {
  return (
    <section
      id="faq"
      aria-labelledby="faq-heading"
      className="relative isolate overflow-hidden bg-brand-mist text-[oklch(0.205_0.020_150)] dark:bg-brand-mist"
    >
      {/* Soft brand wash so the light band is not a flat rectangle. */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 -z-10">
        <div
          className="absolute inset-0"
          style={{
            background:
              'radial-gradient(ellipse 70% 90% at 50% 0%, rgba(255,255,255,0.9), transparent 70%)',
          }}
        />
        <div className="fu-aurora-c absolute -bottom-32 right-[8%] size-80 rounded-full bg-brand-sage/40 blur-3xl" />
      </div>

      <div className="mx-auto grid w-full max-w-6xl gap-10 px-6 py-20 sm:py-28 lg:grid-cols-[0.8fr_1.2fr] lg:gap-16">
        <div className="flex flex-col items-start gap-3">
          <p className="text-sm font-medium text-brand-green">FAQ</p>

          <h2
            id="faq-heading"
            className="text-3xl font-semibold tracking-tight text-balance text-brand-forest sm:text-4xl"
          >
            Questions people actually ask
          </h2>

          <p className="text-sm text-pretty text-brand-forest/70">
            Still stuck? The desk staff can see everything you have reported and can help in
            person.
          </p>
        </div>

        <Accordion className="w-full">
          {FAQS.map(({ question, answer }) => (
            <AccordionItem
              key={question}
              value={question}
              className="border-brand-forest/12 not-last:border-b"
            >
              <AccordionTrigger className="py-4 text-base text-brand-forest hover:no-underline **:data-[slot=accordion-trigger-icon]:text-brand-green">
                {question}
              </AccordionTrigger>
              <AccordionContent className="pb-4 text-sm text-pretty text-brand-forest/75">
                {answer}
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </div>
    </section>
  )
}
