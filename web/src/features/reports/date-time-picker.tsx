import { CalendarIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { FormSelect } from './form-select'
import { cn } from '@/lib/utils'

/**
 * Date and time picker built from our own components.
 *
 * Replaces <input type="datetime-local">, whose picker is drawn by the browser: it cannot be
 * styled at all and looks different in Chrome, Safari and Firefox.
 *
 * The value stays in the same "YYYY-MM-DDTHH:mm" local format the native input used, so
 * callers and toUtcIso() are unaffected.
 */

const HOURS = Array.from({ length: 24 }, (_, hour) => ({
  value: String(hour).padStart(2, '0'),
  label: String(hour).padStart(2, '0'),
}))

const MINUTES = Array.from({ length: 12 }, (_, index) => ({
  value: String(index * 5).padStart(2, '0'),
  label: String(index * 5).padStart(2, '0'),
}))

function toLocalInput(date: Date) {
  const offset = date.getTimezoneOffset() * 60000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

export function DateTimePicker({
  id,
  value,
  onChange,
  invalid,
}: {
  id: string
  value: string
  onChange: (value: string) => void
  invalid?: boolean
}) {
  const parsed = value ? new Date(value) : null
  const isValid = parsed !== null && !Number.isNaN(parsed.getTime())

  const [datePart = '', timePart = '00:00'] = value.split('T')
  const [hour = '00', minute = '00'] = timePart.split(':')

  function setDate(date: Date | undefined) {
    if (!date) return
    // Keep whatever time is already chosen; only the day changes.
    const next = new Date(date)
    next.setHours(Number(hour), Number(minute), 0, 0)
    onChange(toLocalInput(next))
  }

  function setTime(nextHour: string, nextMinute: string) {
    // Falls back to today if no day has been picked yet.
    const base = isValid ? new Date(parsed) : new Date()
    base.setHours(Number(nextHour), Number(nextMinute), 0, 0)
    onChange(toLocalInput(base))
  }

  return (
    <div className="flex flex-col gap-2 sm:flex-row">
      <Popover>
        <PopoverTrigger
          render={
            <Button
              id={id}
              variant="outline"
              aria-invalid={invalid}
              className={cn(
                'flex-1 justify-start gap-2 font-normal',
                !isValid && 'text-muted-foreground',
              )}
            />
          }
        >
          <CalendarIcon className="size-4 text-muted-foreground" aria-hidden="true" />
          {isValid
            ? parsed.toLocaleDateString('en', { weekday: 'short', day: 'numeric', month: 'short' })
            : 'Pick a date'}
        </PopoverTrigger>

        <PopoverContent className="w-auto p-0" align="start">
          <Calendar
            mode="single"
            selected={isValid ? parsed : undefined}
            onSelect={setDate}
            defaultMonth={isValid ? parsed : undefined}
            // Nothing was lost in the future.
            disabled={{ after: new Date() }}
            autoFocus
          />
        </PopoverContent>
      </Popover>

      <div className="flex items-center gap-1.5">
        <FormSelect
          id={`${id}-hour`}
          value={hour}
          onValueChange={(next) => setTime(next, minute)}
          options={HOURS}
          placeholder="00"
        />
        <span className="text-muted-foreground" aria-hidden="true">
          :
        </span>
        <FormSelect
          id={`${id}-minute`}
          value={minute}
          onValueChange={(next) => setTime(hour, next)}
          options={MINUTES}
          placeholder="00"
        />
      </div>

      {/* The date part is carried by the trigger label; this keeps the value in the DOM for
          anything reading the form, without a second visible control. */}
      <input type="hidden" name={id} value={datePart ? value : ''} readOnly />
    </div>
  )
}
